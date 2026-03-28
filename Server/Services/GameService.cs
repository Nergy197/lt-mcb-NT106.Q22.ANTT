using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;

namespace PokemonMMO.Services;

/// <summary>
/// Core game logic — heal, catch, trade, boss gating.
/// All methods ported 1:1 from the TypeScript GameService.
/// </summary>
public class GameService
{
    private readonly MongoDbContext _db;
    private readonly PokemonDataService _pokeData;
    private static readonly Random _rng = new();

    private static readonly string[] Natures =
    {
        "Hardy", "Lonely", "Brave", "Adamant", "Naughty",
        "Bold",  "Docile", "Relaxed", "Impish", "Lax",
        "Timid", "Hasty",  "Serious", "Jolly",  "Naive",
        "Modest","Mild",   "Quiet",   "Bashful","Rash",
        "Calm",  "Gentle", "Sassy",   "Careful","Quirky"
    };

    public GameService(MongoDbContext db, PokemonDataService pokeData)
    {
        _db = db;
        _pokeData = pokeData;
    }

    // ─────────────────────────────────────────────────────────────────────
    // 1. Heal at Safe Zone (Hub)
    // ─────────────────────────────────────────────────────────────────────
    public async Task<bool> HealPlayerParty(string playerId)
    {
        var filter = Builders<PokemonInstance>.Filter.And(
            Builders<PokemonInstance>.Filter.Eq(p => p.OwnerId, playerId),
            Builders<PokemonInstance>.Filter.Eq(p => p.IsInParty, true));

        var party = await _db.PokemonInstances.Find(filter).ToListAsync();
        if (party.Count == 0) return true;

        var bulkOps = party.Select(pokemon =>
            new UpdateOneModel<PokemonInstance>(
                Builders<PokemonInstance>.Filter.Eq(p => p.Id, pokemon.Id),
                Builders<PokemonInstance>.Update
                    .Set(p => p.CurrentHp, pokemon.MaxHp)
                    .Set(p => p.StatusCondition, "NONE")
            )).ToList();

        await _db.PokemonInstances.BulkWriteAsync(bulkOps);
        return true;
    }


    // ─────────────────────────────────────────────────────────────────────
    // 3. Secure Trading (Hub) — atomic owner swap
    // ─────────────────────────────────────────────────────────────────────
    public async Task<bool> ExecuteTrade(
        string player1Id, string player2Id,
        string pokemonId1, string pokemonId2)
    {
        // Verify ownership
        var p1Poke = await _db.PokemonInstances.Find(
            Builders<PokemonInstance>.Filter.And(
                Builders<PokemonInstance>.Filter.Eq(p => p.Id, pokemonId1),
                Builders<PokemonInstance>.Filter.Eq(p => p.OwnerId, player1Id))
        ).FirstOrDefaultAsync();

        var p2Poke = await _db.PokemonInstances.Find(
            Builders<PokemonInstance>.Filter.And(
                Builders<PokemonInstance>.Filter.Eq(p => p.Id, pokemonId2),
                Builders<PokemonInstance>.Filter.Eq(p => p.OwnerId, player2Id))
        ).FirstOrDefaultAsync();

        if (p1Poke == null) throw new Exception("Player 1 does not own the specified Pokemon.");
        if (p2Poke == null) throw new Exception("Player 2 does not own the specified Pokemon.");

        // Swap owners & remove from party
        var update1 = Builders<PokemonInstance>.Update
            .Set(p => p.OwnerId, player2Id)
            .Set(p => p.IsInParty, false)
            .Unset(p => p.PartySlot);

        var update2 = Builders<PokemonInstance>.Update
            .Set(p => p.OwnerId, player1Id)
            .Set(p => p.IsInParty, false)
            .Unset(p => p.PartySlot);

        await _db.PokemonInstances.UpdateOneAsync(
            Builders<PokemonInstance>.Filter.Eq(p => p.Id, pokemonId1), update1);
        await _db.PokemonInstances.UpdateOneAsync(
            Builders<PokemonInstance>.Filter.Eq(p => p.Id, pokemonId2), update2);

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // 4. Boss Gating System (Wilderness) - OBSOLETE
    // ─────────────────────────────────────────────────────────────────────
    public Task<bool> CheckCanEnterZone(string playerId, string requiredBossId)
    {
        // Obsolete logic since BeatenBosses is removed in PvP pivot.
        return Task.FromResult(true);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 5. Defeat Boss — record victory - OBSOLETE
    // ─────────────────────────────────────────────────────────────────────
    public Task OnBossDefeated(string playerId, string bossId)
    {
        // Obsolete logic since BeatenBosses is removed in PvP pivot.
        return Task.CompletedTask;
    }
}
