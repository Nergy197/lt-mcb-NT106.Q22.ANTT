using Microsoft.AspNetCore.SignalR;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Services;
using MongoDB.Driver;

namespace PokemonMMO.Hubs;

/// <summary>
/// SignalR Hub — replaces the Colyseus GameRoom.
/// Unity clients connect via WebSocket to /game.
///
/// Client → Server messages:  JoinGame, Move, Heal
/// Server → Client messages:  PlayerJoined, PlayerLeft, PlayerMoved, PartyUpdated, Error
/// </summary>
public class GameHub : Hub
{
    private readonly MongoDbContext _db;
    private readonly GameService _gameService;
    private readonly BattleService _battleService;

    // In-memory player tracking (sessionId → playerId)
    private static readonly Dictionary<string, string> ConnectedPlayers = new();

    public GameHub(MongoDbContext db, GameService gameService, BattleService battleService)
    {
        _db = db;
        _gameService = gameService;
        _battleService = battleService;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Join — equivalent to Colyseus onJoin
    // ─────────────────────────────────────────────────────────────────────
    public async Task JoinGame(string playerId)
    {
        Console.WriteLine($"[Join] Player joining with ID: {playerId}");

        var player = await _db.Players
            .Find(Builders<Player>.Filter.Eq(p => p.Id, playerId))
            .FirstOrDefaultAsync();

        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", "Player not found");
            Context.Abort();
            return;
        }

        ConnectedPlayers[Context.ConnectionId] = playerId;

        // Join a SignalR group for matchmaking lobby
        await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");

        // Send player info to lobby
        await Clients.Group("Lobby").SendAsync("PlayerJoined", new
        {
            sessionId = Context.ConnectionId,
            id        = player.Id,
            name      = player.Name
        });

        // Sync party to caller
        await SyncPartyToClient(playerId);
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

            await Clients.Group(battleGroup).SendAsync("BattleStarted", new
            {
                battleId = battle.BattleId,
                player1Id = battle.Player1Id,
                player2Id = battle.Player2Id,
                turnNumber = battle.TurnNumber,
                state = battle.State.ToString(),
                activeIndex1 = battle.ActiveIndex1,
                activeIndex2 = battle.ActiveIndex2
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
            await Clients.Caller.SendAsync("ActionAccepted", new
            {
                battleId,
                action = "Move",
                moveSlot
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
            await Clients.Caller.SendAsync("ActionAccepted", new
            {
                battleId,
                action = "Switch",
                partyIndex
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
                await Clients.Group("Lobby").SendAsync("PlayerLeft", new
                {
                    sessionId = Context.ConnectionId
                });
            }

            ConnectedPlayers.Remove(Context.ConnectionId);
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

        var partyData = party.Select(p => new
        {
            id        = p.Id,
            speciesId = p.SpeciesId,
            level     = p.Level,
            hp        = p.CurrentHp,
            maxHp     = p.MaxHp
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

        await Clients.Group(battleGroup).SendAsync("TurnWaiting", new
        {
            battleId,
            turnNumber = battle.TurnNumber,
            ready = isReady,
            submittedPlayerIds = battle.PendingActions.Keys.ToList()
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
            await Clients.Group(battleGroup).SendAsync("BattleEnded", new
            {
                result.BattleId,
                winnerPlayerId = result.WinnerPlayerId,
                events = result.Events
            });
            return;
        }

        await Clients.Group(battleGroup).SendAsync("BattleUpdated", new
        {
            result.BattleId,
            nextTurnNumber = result.NextTurnNumber,
            result.ActiveIndex1,
            result.ActiveIndex2,
            result.ActiveHp1,
            result.ActiveHp2
        });
    }

    private static string GetBattleGroupName(string battleId) => $"battle:{battleId}";
}
