using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;
using PokemonMMO.Services;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using PokemonMMO.Options;

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
    private readonly BattleOptions _opts;

    // In-memory player tracking (sessionId → playerId)
    public static readonly ConcurrentDictionary<string, string> ConnectedPlayers = new();

    // Hàng chờ tìm trận (PlayerId -> ConnectionId)
    private static readonly ConcurrentDictionary<string, string> MatchmakingQueue = new();
    
    // Lưu trữ các Task timeout để hủy nếu tìm thấy trận sớm
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> MatchmakingTasks = new();

    public MatchmakingHub(MongoDbContext db, BattleService battleService, GameService gameService, IOptions<BattleOptions> opts)
    {
        _db = db;
        _battleService = battleService;
        _gameService = gameService;
        _opts = opts.Value;
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

        // Nạp Pokemon khởi đầu nếu người chơi chưa có con nào trong Party
        var partyCount = await _db.PokemonInstances
            .CountDocumentsAsync(p => p.OwnerId == player.Id && p.IsInParty);
        
        await Clients.Caller.SendAsync("Debug", $"[Server] Player: {player.Name} ({player.Id}), PartyCount: {partyCount}");

        if (partyCount == 0)
        {
            await Clients.Caller.SendAsync("Debug", "[Server] Seeding initial Pokemon...");
            await _gameService.SeedInitialPokemonAsync(player.Id);
        }

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

        await Clients.Caller.SendAsync("Debug", $"[Server] Syncing party for {playerId}, count: {party.Count}");

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
            await CreateAndNotifyBattle(myPlayerId, opponentPlayerId, Context.ConnectionId, opponentConnectionId);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to start match: {ex.Message}");
        }
    }

    /// <summary>
    /// Tạo bot battle ngay lập tức, không cần đợi 30s. Dùng cho demo / testing.
    /// </summary>
    public async Task<string> FightBot()
    {
        // Ưu tiên ConnectedPlayers (nếu đã JoinLobby), fallback sang DB qua JWT
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
        {
            var player = await GetAuthenticatedPlayer();
            if (player == null) { await Clients.Caller.SendAsync("Error", "Not authenticated."); return ""; }
            playerId = player.Id;
            ConnectedPlayers[Context.ConnectionId] = playerId;
        }

        return await CreateAndNotifyBattle(playerId, BattleService.BotPlayerId, Context.ConnectionId, null);
    }

    public async Task FindMatch()
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var myPlayerId))
        {
            await Clients.Caller.SendAsync("Error", "You must join lobby first.");
            return;
        }

        // 1. Kiểm tra xem có ai đang đợi không
        var opponent = MatchmakingQueue.FirstOrDefault(kv => kv.Key != myPlayerId);
        if (opponent.Key != null)
        {
            // Tìm thấy đối thủ người thật!
            if (MatchmakingQueue.TryRemove(opponent.Key, out var opponentConnId))
            {
                if (MatchmakingTasks.TryRemove(opponent.Key, out var cts)) cts.Cancel();
                
                await CreateAndNotifyBattle(myPlayerId, opponent.Key, Context.ConnectionId, opponentConnId);
                return;
            }
        }

        // 2. Không có ai, vào hàng chờ
        MatchmakingQueue[myPlayerId] = Context.ConnectionId;
        
        int countdown = _opts.BotFallbackSeconds;
        await Clients.Caller.SendAsync("SearchStarted", new { CountdownSeconds = countdown });

        var myCts = new CancellationTokenSource();
        MatchmakingTasks[myPlayerId] = myCts;

        try
        {
            for (int i = countdown; i > 0; i--)
            {
                await Clients.Caller.SendAsync("SearchTick", new { SecondsLeft = i });
                await Task.Delay(1000, myCts.Token);
            }
            await Clients.Caller.SendAsync("SearchTick", new { SecondsLeft = 0 });

            if (MatchmakingQueue.TryRemove(myPlayerId, out _))
            {
                MatchmakingTasks.TryRemove(myPlayerId, out _);
                await CreateAndNotifyBattle(myPlayerId, BattleService.BotPlayerId, Context.ConnectionId, null);
            }
        }
        catch (TaskCanceledException)
        {
            // Trận đấu đã được tìm thấy bởi người khác hoặc bị hủy
        }
    }

    public async Task CancelMatchmaking()
    {
        if (ConnectedPlayers.TryGetValue(Context.ConnectionId, out var myPlayerId))
        {
            MatchmakingQueue.TryRemove(myPlayerId, out _);
            if (MatchmakingTasks.TryRemove(myPlayerId, out var cts)) 
            {
                cts.Cancel();
                await Clients.Caller.SendAsync("SearchCancelled", "Đã hủy tìm trận.");
            }
        }
    }

    private async Task<string> CreateAndNotifyBattle(string p1, string p2, string conn1, string? conn2)
    {
        var battle = await _battleService.CreateBattle(p1, p2);
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

        if (!string.IsNullOrEmpty(conn1))
            await Clients.Client(conn1).SendAsync("MatchFound", startDto);

        if (!string.IsNullOrEmpty(conn2))
            await Clients.Client(conn2).SendAsync("MatchFound", startDto);

        return battle.BattleId;
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
        if (Context.User == null) {
            Console.WriteLine("[Matchmaking] FAIL: Context.User is NULL");
            return null;
        }

        // Log tất cả các claims để debug
        foreach (var claim in Context.User.Claims) {
            Console.WriteLine($"[Matchmaking] Claim: {claim.Type} = {claim.Value}");
        }

        var accountId = Context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? Context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
            ?? Context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? Context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(accountId)) {
            Console.WriteLine("[Matchmaking] FAIL: AccountId not found in claims");
            return null;
        }

        var player = await _db.Players
            .Find(Builders<Player>.Filter.Eq(p => p.AccountId, accountId))
            .FirstOrDefaultAsync();

        if (player == null) {
            Console.WriteLine($"[Matchmaking] FAIL: Player profile not found for AccountId: {accountId}");
            // Fallback: Tạo profile nếu thiếu
            var username = Context.User.Identity?.Name ?? "Guest_" + accountId[..5];
            player = new Player { AccountId = accountId, Name = username, MMR = 1000 };
            await _db.Players.InsertOneAsync(player);
            Console.WriteLine($"[Matchmaking] FIXED: Created missing player profile for {username}");
        }

        return player;
    }
}
