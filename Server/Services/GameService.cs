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

    /// <summary>
    /// Nạp 6 Pokemon khởi đầu cho người chơi mới.
    /// </summary>
    public async Task SeedInitialPokemonAsync(string playerId)
    {
        var starters = new[]
        {
            new { Id = 3,   Name = "Venusaur" }, 
            new { Id = 6,   Name = "Charizard" },
            new { Id = 9,   Name = "Blastoise" },
            new { Id = 25,  Name = "Pikachu" },
            new { Id = 448, Name = "Lucario" },
            new { Id = 445, Name = "Garchomp" }
        };

        var instances = new List<PokemonInstance>();
        int slot = 0;

        foreach (var s in starters)
        {
            var pData = await _pokeData.GetPokemonData(s.Id);
            // Lấy moveset thực tế từ Database (đã được seed từ PokeAPI)
            var speciesEntry = await _db.Pokedex.Find(x => x.Id == s.Id).FirstOrDefaultAsync();
            var moveset = speciesEntry?.DefaultMoves ?? new List<int> { 1, 45 }; // Fallback Pound, Growl

            var ivs = new StatBlock { Hp = 31, Atk = 31, Def = 31, SpAtk = 31, SpDef = 31, Spd = 31 };
            var evs = new StatBlock { Hp = 0, Atk = 0, Def = 0, SpAtk = 0, SpDef = 0, Spd = 0 };

            var maxHp = CalculateHp(pData.BaseHp, ivs.Hp, evs.Hp, 50);

            var instance = new PokemonInstance
            {
                OwnerId = playerId,
                SpeciesId = s.Id,
                Nickname = s.Name,
                Level = 50,
                Exp = 0,
                Nature = Natures[_rng.Next(Natures.Length)],
                CurrentHp = maxHp,
                MaxHp = maxHp,
                StatusCondition = "NONE",
                IsInParty = true,
                PartySlot = slot++,
                Ivs = ivs,
                Evs = evs,
                Moves = moveset.Select(mId => new PokemonMove { MoveId = mId, CurrentPp = 15 }).ToList()
            };
            instances.Add(instance);
        }

        await _db.PokemonInstances.InsertManyAsync(instances);
    }

    private int CalculateHp(int baseStat, int iv, int ev, int level)
    {
        return (int)Math.Floor((2.0 * baseStat + iv + Math.Floor(ev / 4.0)) * level / 100.0) + level + 10;
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
