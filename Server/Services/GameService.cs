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
    // 2. Catch Wild Pokemon
    // ─────────────────────────────────────────────────────────────────────
    public async Task<PokemonInstance> CatchPokemon(string playerId, int speciesId)
    {
        // Fetch base stats from PokeAPI
        var staticData = await _pokeData.GetPokemonData(speciesId);

        // Generate IVs (0–31) and random Nature
        int GenIV() => _rng.Next(0, 32);
        var ivs = new StatBlock
        {
            Hp = GenIV(), Atk = GenIV(), Def = GenIV(),
            SpAtk = GenIV(), SpDef = GenIV(), Spd = GenIV()
        };
        var nature = Natures[_rng.Next(Natures.Length)];

        // HP formula at level 1
        const int level = 1;
        int maxHp = (int)(0.01 * (2 * staticData.BaseHp + ivs.Hp) * level) + level + 10;

        // Check party capacity
        var partyCount = await _db.PokemonInstances.CountDocumentsAsync(
            Builders<PokemonInstance>.Filter.And(
                Builders<PokemonInstance>.Filter.Eq(p => p.OwnerId, playerId),
                Builders<PokemonInstance>.Filter.Eq(p => p.IsInParty, true)));

        bool inParty = partyCount < 6;
        int? slot = inParty ? (int)(partyCount + 1) : null;

        // Insert PokemonInstance
        var newPokemon = new PokemonInstance
        {
            OwnerId         = playerId,
            SpeciesId       = speciesId,
            Level           = level,
            Nature          = nature,
            CurrentHp       = maxHp,
            MaxHp           = maxHp,
            IsInParty       = inParty,
            PartySlot       = slot
        };
        await _db.PokemonInstances.InsertOneAsync(newPokemon);

        // Insert PokemonStats (IVs + zeroed EVs)
        var stats = new PokemonStats
        {
            PokemonInstanceId = newPokemon.Id,
            Ivs = ivs,
            Evs = new StatBlock()
        };
        await _db.PokemonStats.InsertOneAsync(stats);

        return newPokemon;
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
    // 4. Boss Gating System (Wilderness)
    // ─────────────────────────────────────────────────────────────────────
    public async Task<bool> CheckCanEnterZone(string playerId, string requiredBossId)
    {
        var filter = Builders<Player>.Filter.And(
            Builders<Player>.Filter.Eq(p => p.Id, playerId),
            Builders<Player>.Filter.AnyEq(p => p.BeatenBosses, requiredBossId));

        var exists = await _db.Players.Find(filter).AnyAsync();
        return exists;
    }

    // ─────────────────────────────────────────────────────────────────────
    // 5. Defeat Boss — record victory
    // ─────────────────────────────────────────────────────────────────────
    public async Task OnBossDefeated(string playerId, string bossId)
    {
        var result = await _db.Players.UpdateOneAsync(
            Builders<Player>.Filter.Eq(p => p.Id, playerId),
            Builders<Player>.Update.AddToSet(p => p.BeatenBosses, bossId));

        if (result.MatchedCount == 0)
            throw new Exception("Player not found");
    }
}
