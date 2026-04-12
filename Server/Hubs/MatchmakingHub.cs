using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;
using PokemonMMO.Services;
using MongoDB.Driver;

namespace PokemonMMO.Hubs;

/// <summary>
/// Thư mục Hubs/ - MatchmakingHub (Tìm trận)
/// Xử lý việc người chơi tham gia Lobby và bắt đầu trận đấu 1v1.
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MatchmakingHub : Hub
{
    private readonly MongoDbContext _db;
    private readonly BattleService _battleService;
    private readonly GameService _gameService;

    // In-memory player tracking (sessionId → playerId)
    // Dùng chung một Dictionary tĩnh để theo dõi tất cả người chơi online
    public static readonly ConcurrentDictionary<string, string> ConnectedPlayers = new();

    public MatchmakingHub(MongoDbContext db, BattleService battleService, GameService gameService)
    {
        _db = db;
        _battleService = battleService;
        _gameService = gameService;
    }

    public async Task JoinLobby()
    {
        var player = await GetAuthenticatedPlayer();
        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", "Unable to resolve player from token.");
            Context.Abort();
            return;
        }

        if (!ConnectedPlayers.TryAdd(Context.ConnectionId, player.Id))
        {
            ConnectedPlayers[Context.ConnectionId] = player.Id;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");

        await Clients.Group("Lobby").SendAsync("PlayerJoined", new PlayerJoinedEventDto
        {
            SessionId = Context.ConnectionId,
            Id = player.Id,
            Name = player.Name
        });

        // Đồng bộ Party khi vừa vào Lobby
        await SyncPartyToClient(player.Id);
    }

    public async Task Heal()
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
            return;

        try
        {
            await _gameService.HealPlayerParty(playerId);
            await SyncPartyToClient(playerId);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to heal party: {ex.Message}");
        }
    }

    private async Task SyncPartyToClient(string playerId)
    {
        var party = await _db.PokemonInstances
            .Find(Builders<PokemonInstance>.Filter.And(
                Builders<PokemonInstance>.Filter.Eq(p => p.OwnerId, playerId),
                Builders<PokemonInstance>.Filter.Eq(p => p.IsInParty, true)))
            .ToListAsync();

        var partyData = party.Select(p => new PartyPokemonDto
        {
            Id = p.Id,
            SpeciesId = p.SpeciesId,
            Level = p.Level,
            Hp = p.CurrentHp,
            MaxHp = p.MaxHp
        }).ToList();

        await Clients.Caller.SendAsync("PartyUpdated", partyData);
    }

    public async Task StartMatch(string opponentPlayerId)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var myPlayerId))
        {
            await Clients.Caller.SendAsync("Error", "You must join lobby first.");
            return;
        }

        var opponentConnectionId = ConnectedPlayers
            .FirstOrDefault(kv => kv.Value == opponentPlayerId).Key;

        if (string.IsNullOrWhiteSpace(opponentConnectionId))
        {
            await Clients.Caller.SendAsync("Error", "Opponent is not online.");
            return;
        }

        try
        {
            var battle = await _battleService.CreateBattle(myPlayerId, opponentPlayerId);
            
            // Thông báo cho cả 2 người chơi rằng trận đấu đã bắt đầu
            // Client sẽ dựa vào event này để chuyển hướng sang BattleScene và kết nối tới BattleHub
            var startDto = new BattleStartedEventDto
            {
                BattleId = battle.BattleId,
                Player1Id = battle.Player1Id,
                Player2Id = battle.Player2Id,
                TurnNumber = battle.TurnNumber,
                TurnTimeoutSeconds = _battleService.TurnTimeoutSeconds,
                TurnDeadlineUtc = battle.TurnDeadlineUtc,
                State = battle.State.ToString()
            };

            await Clients.Client(Context.ConnectionId).SendAsync("MatchFound", startDto);
            await Clients.Client(opponentConnectionId).SendAsync("MatchFound", startDto);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to start match: {ex.Message}");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectedPlayers.TryRemove(Context.ConnectionId, out var playerId))
        {
            await Clients.Group("Lobby").SendAsync("PlayerLeft", new PlayerLeftEventDto
            {
                SessionId = Context.ConnectionId
            });
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task<Player?> GetAuthenticatedPlayer()
    {
        var accountId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst(JwtSecurityTokenHandler.DefaultInboundClaimTypeMap[JwtRegisteredClaimNames.Sub])?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(accountId)) return null;

        return await _db.Players
            .Find(Builders<Player>.Filter.Eq(p => p.AccountId, accountId))
            .FirstOrDefaultAsync();
    }
}
