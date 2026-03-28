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
/// Client → Server messages:  JoinGame, Move, Heal, CatchPokemon
/// Server → Client messages:  PlayerJoined, PlayerLeft, PlayerMoved, PartyUpdated, Error
/// </summary>
public class GameHub : Hub
{
    private readonly MongoDbContext _db;
    private readonly GameService _gameService;

    // In-memory player tracking (sessionId → playerId)
    private static readonly Dictionary<string, string> ConnectedPlayers = new();

    public GameHub(MongoDbContext db, GameService gameService)
    {
        _db = db;
        _gameService = gameService;
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

        // Join a SignalR group by map name (equivalent to Colyseus room)
        await Groups.AddToGroupAsync(Context.ConnectionId, player.CurrentMap);

        // Send player info to all clients on the same map
        await Clients.Group(player.CurrentMap).SendAsync("PlayerJoined", new
        {
            sessionId = Context.ConnectionId,
            id        = player.Id,
            name      = player.Name,
            x         = player.Position.X,
            y         = player.Position.Y,
            z         = player.Position.Z
        });

        // Sync party to caller
        await SyncPartyToClient(playerId);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Move — broadcast position to all players on same map
    // ─────────────────────────────────────────────────────────────────────
    public async Task Move(float x, float y, float z)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
            return;

        var player = await _db.Players
            .Find(Builders<Player>.Filter.Eq(p => p.Id, playerId))
            .FirstOrDefaultAsync();

        if (player == null) return;

        await Clients.Group(player.CurrentMap).SendAsync("PlayerMoved", new
        {
            sessionId = Context.ConnectionId,
            x, y, z
        });
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
    // Catch — catch a wild Pokemon
    // ─────────────────────────────────────────────────────────────────────
    public async Task CatchPokemon(int speciesId)
    {
        if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
            return;

        try
        {
            var newPoke = await _gameService.CatchPokemon(playerId, speciesId);
            await SyncPartyToClient(playerId);
            await Clients.Caller.SendAsync("PokemonCaught", new
            {
                id        = newPoke.Id,
                speciesId = newPoke.SpeciesId,
                level     = newPoke.Level,
                hp        = newPoke.CurrentHp,
                maxHp     = newPoke.MaxHp
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", $"Catch failed: {ex.Message}");
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
                await Clients.Group(player.CurrentMap).SendAsync("PlayerLeft", new
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
}
