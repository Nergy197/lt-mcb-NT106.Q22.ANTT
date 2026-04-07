using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
/// SignalR Hub — replaces the Colyseus GameRoom.
/// Unity clients connect via WebSocket to /game.
///
/// Client → Server messages:  JoinGame, Heal, StartMatch, ChooseMove, SwitchPokemon, SyncBattle
/// Server → Client messages:  PlayerJoined, PlayerLeft, PartyUpdated, BattleStarted, TurnResolved, Error
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GameHub : Hub
{
    private readonly MongoDbContext _db;
    private readonly GameService _gameService;
    private readonly BattleService _battleService;

    // In-memory player tracking (sessionId → playerId)
    private static readonly ConcurrentDictionary<string, string> ConnectedPlayers = new();

    public GameHub(MongoDbContext db, GameService gameService, BattleService battleService)
    {
        _db = db;
        _gameService = gameService;
        _battleService = battleService;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Join — equivalent to Colyseus onJoin
    // ─────────────────────────────────────────────────────────────────────
    public async Task JoinGame()
    {
        var player = await GetAuthenticatedPlayer();
        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", "Unable to resolve player from token.");
            Context.Abort();
            return;
        }

        Console.WriteLine($"[Join] Player joining with ID: {player.Id}");

        if (!ConnectedPlayers.TryAdd(Context.ConnectionId, player.Id))
        {
            ConnectedPlayers[Context.ConnectionId] = player.Id;
        }

        // Join a SignalR group for matchmaking lobby
        await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");

        // Send player info to lobby
        await Clients.Group("Lobby").SendAsync("PlayerJoined", new PlayerJoinedEventDto
        {
            SessionId = Context.ConnectionId,
            Id = player.Id,
            Name = player.Name
        });

        // Sync party to caller
        await SyncPartyToClient(player.Id);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Move — broadcast position to all players on same map
    // ─────────────────────────────────────────────────────────────────────
    public Task Move(float x, float y, float z)
    {
        return Task.CompletedTask; // OBSOLETE
    }

    // ─────────────────────────────────────────────────────────────────────
    // Heal — restore party HP at safe zone
    // ─────────────────────────────────────────────────────────────────────
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

    // ─────────────────────────────────────────────────────────────────────
    // Battle — create a new 1v1 match
    // ─────────────────────────────────────────────────────────────────────
    public async Task StartMatch(string opponentPlayerId)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var myPlayerId))
        {
            await Clients.Caller.SendAsync("Error", "You must join game first.");
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
            var battleGroup = GetBattleGroupName(battle.BattleId);

            await Groups.AddToGroupAsync(Context.ConnectionId, battleGroup);
            await Groups.AddToGroupAsync(opponentConnectionId, battleGroup);

            await Clients.Group(battleGroup).SendAsync("BattleStarted", new BattleStartedEventDto
            {
                BattleId = battle.BattleId,
                Player1Id = battle.Player1Id,
                Player2Id = battle.Player2Id,
                TurnNumber = battle.TurnNumber,
                TurnTimeoutSeconds = _battleService.TurnTimeoutSeconds,
                TurnDeadlineUtc = battle.TurnDeadlineUtc,
                State = battle.State.ToString(),
                ActiveIndex1 = battle.ActiveIndex1,
                ActiveIndex2 = battle.ActiveIndex2
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to start match: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Battle — choose move action for current turn
    // ─────────────────────────────────────────────────────────────────────
    public async Task ChooseMove(string battleId, int moveSlot)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
        {
            await Clients.Caller.SendAsync("Error", "You must join game first.");
            return;
        }

        try
        {
            var action = new BattleAction
            {
                PlayerId = playerId,
                Type = BattleActionType.Move,
                MoveSlot = moveSlot
            };

            await _battleService.SubmitActionAsync(battleId, action);
            await Clients.Caller.SendAsync("ActionAccepted", new ActionAcceptedEventDto
            {
                BattleId = battleId,
                Action = "Move",
                MoveSlot = moveSlot
            });

            await NotifyTurnWaiting(battleId);
            await TryResolveTurn(battleId);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to choose move: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Battle — switch pokemon action for current turn
    // ─────────────────────────────────────────────────────────────────────
    public async Task SwitchPokemon(string battleId, int partyIndex)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
        {
            await Clients.Caller.SendAsync("Error", "You must join game first.");
            return;
        }

        try
        {
            var action = new BattleAction
            {
                PlayerId = playerId,
                Type = BattleActionType.Switch,
                SwitchIndex = partyIndex
            };

            await _battleService.SubmitActionAsync(battleId, action);
            await Clients.Caller.SendAsync("ActionAccepted", new ActionAcceptedEventDto
            {
                BattleId = battleId,
                Action = "Switch",
                PartyIndex = partyIndex
            });

            await NotifyTurnWaiting(battleId);
            await TryResolveTurn(battleId);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Failed to switch pokemon: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Battle — client heartbeat to trigger timeout resolution
    // ─────────────────────────────────────────────────────────────────────
    public async Task SyncBattle(string battleId)
    {
        await TryResolveTurn(battleId);
        await NotifyTurnWaiting(battleId);
    }


    // ─────────────────────────────────────────────────────────────────────
    // Disconnect — equivalent to Colyseus onLeave
    // ─────────────────────────────────────────────────────────────────────
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
        {
            Console.WriteLine($"[Leave] Client {Context.ConnectionId} left.");

            var player = await _db.Players
                .Find(Builders<Player>.Filter.Eq(p => p.Id, playerId))
                .FirstOrDefaultAsync();

            if (player != null)
            {
                await Clients.Group("Lobby").SendAsync("PlayerLeft", new PlayerLeftEventDto
                {
                    SessionId = Context.ConnectionId
                });
            }

            var forfeitResult = await _battleService.ForfeitPlayerAsync(playerId, "Disconnected");
            if (forfeitResult != null)
            {
                var battleGroup = GetBattleGroupName(forfeitResult.BattleId);
                await Clients.Group(battleGroup).SendAsync("BattleEnded", new BattleEndedEventDto
                {
                    BattleId = forfeitResult.BattleId,
                    WinnerPlayerId = forfeitResult.WinnerPlayerId,
                    Events = forfeitResult.Events
                });
            }

            ConnectedPlayers.TryRemove(Context.ConnectionId, out _);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helper — send party data to the caller
    // ─────────────────────────────────────────────────────────────────────
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

    private async Task NotifyTurnWaiting(string battleId)
    {
        var battle = _battleService.GetBattle(battleId);
        if (battle == null)
            return;

        var isReady = _battleService.IsTurnReady(battleId);
        var battleGroup = GetBattleGroupName(battleId);

        await Clients.Group(battleGroup).SendAsync("TurnWaiting", new TurnWaitingEventDto
        {
            BattleId = battleId,
            TurnNumber = battle.TurnNumber,
            Ready = isReady,
            TurnDeadlineUtc = battle.TurnDeadlineUtc,
            SubmittedPlayerIds = battle.PendingActions.Keys.ToList()
        });
    }

    private async Task TryResolveTurn(string battleId)
    {
        var result = await _battleService.ResolveTurnIfReadyAsync(battleId);
        if (result == null)
            return;

        var battleGroup = GetBattleGroupName(battleId);
        await Clients.Group(battleGroup).SendAsync("TurnResolved", result);

        if (result.State == BattleState.Ended)
        {
            await Clients.Group(battleGroup).SendAsync("BattleEnded", new BattleEndedEventDto
            {
                BattleId = result.BattleId,
                WinnerPlayerId = result.WinnerPlayerId,
                Events = result.Events
            });
            return;
        }

        await Clients.Group(battleGroup).SendAsync("BattleUpdated", new BattleUpdatedEventDto
        {
            BattleId = result.BattleId,
            NextTurnNumber = result.NextTurnNumber,
            TurnDeadlineUtc = _battleService.GetBattle(result.BattleId)?.TurnDeadlineUtc,
            ActiveIndex1 = result.ActiveIndex1,
            ActiveIndex2 = result.ActiveIndex2,
            ActiveHp1 = result.ActiveHp1,
            ActiveHp2 = result.ActiveHp2
        });
    }

    private async Task<Player?> GetAuthenticatedPlayer()
    {
        var playerId = Context.User?.FindFirst("player_id")?.Value;
        if (!string.IsNullOrWhiteSpace(playerId))
        {
            var playerById = await _db.Players
                .Find(Builders<Player>.Filter.Eq(p => p.Id, playerId))
                .FirstOrDefaultAsync();
            if (playerById != null)
                return playerById;
        }

        var accountId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(accountId))
            return null;

        return await _db.Players
            .Find(Builders<Player>.Filter.Eq(p => p.AccountId, accountId))
            .FirstOrDefaultAsync();
    }

    private static string GetBattleGroupName(string battleId) => $"battle:{battleId}";
}
