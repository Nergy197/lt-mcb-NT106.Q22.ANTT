using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace PokemonMMO.Services;

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

    public async Task SeedInitialPokemonAsync(string playerId)
    {
        var starters = new[]
        {
            new { Id = 3,   Name = "Venusaur",  Moves = new[] { "sludge-bomb", "solar-beam", "giga-drain", "sleep-powder" } }, 
            new { Id = 6,   Name = "Charizard", Moves = new[] { "flamethrower", "dragon-pulse", "air-slash", "overheat" } },
            new { Id = 9,   Name = "Blastoise", Moves = new[] { "hydro-pump", "ice-beam", "flash-cannon", "surf" } },
            new { Id = 25,  Name = "Pikachu",   Moves = new[] { "thunderbolt", "quick-attack", "iron-tail", "volt-tackle" } },
            new { Id = 448, Name = "Lucario",   Moves = new[] { "aura-sphere", "dragon-pulse", "close-combat", "extreme-speed" } },
            new { Id = 445, Name = "Garchomp",  Moves = new[] { "dragon-claw", "earthquake", "rock-slide", "swords-dance" } }
        };

        var instances = new List<PokemonInstance>();
        int slot = 0;

        foreach (var s in starters)
        {
            var dex = await _db.Pokedex.Find(x => x.Id == s.Id).FirstOrDefaultAsync();
            int baseHp = (dex != null && dex.BaseStats.ContainsKey("hp")) ? dex.BaseStats["hp"] : 60;

            var pokemonMoves = new List<PokemonMove>();
            foreach(var moveName in s.Moves) {
                var moveEntry = await _db.Moves.Find(mv => mv.Name == moveName).FirstOrDefaultAsync();
                if (moveEntry != null) {
                    pokemonMoves.Add(new PokemonMove { 
                        MoveId = moveEntry.Id, 
                        MoveName = moveEntry.Name, 
                        MoveType = moveEntry.Type, 
                        Category = moveEntry.Category, 
                        MaxPp = moveEntry.PP, 
                        CurrentPp = moveEntry.PP 
                    });
                }
            }

            // Fallback nếu không có chiêu trong DB
            if (pokemonMoves.Count == 0) {
                pokemonMoves.Add(new PokemonMove { MoveId = 1, MoveName = "Pound", MoveType = "normal", Category = "Physical", MaxPp = 35, CurrentPp = 35 });
            }

            var ivs = new StatBlock { Hp = 31, Atk = 31, Def = 31, SpAtk = 31, SpDef = 31, Spd = 31 };
            var evs = new StatBlock { Hp = 0,  Atk = 0,  Def = 0,  SpAtk = 0,  SpDef = 0,  Spd = 0  };
            var maxHp = CalculateHp(baseHp, ivs.Hp, evs.Hp, 50);

            var instance = new PokemonInstance
            {
                OwnerId        = playerId,
                SpeciesId      = s.Id,
                Nickname       = s.Name,
                Level          = 50,
                Exp            = 0,
                Nature         = Natures[_rng.Next(Natures.Length)],
                CurrentHp      = maxHp,
                MaxHp          = maxHp,
                StatusCondition = "NONE",
                IsInParty      = true,
                PartySlot      = slot++,
                Ivs            = ivs,
                Evs            = evs,
                Moves          = pokemonMoves
            };
            instances.Add(instance);
        }

        await _db.PokemonInstances.InsertManyAsync(instances);
    }

    private int CalculateHp(int baseStat, int iv, int ev, int level)
    {
        return (int)Math.Floor((2.0 * baseStat + iv + Math.Floor(ev / 4.0)) * level / 100.0) + level + 10;
    }

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

    public async Task<bool> ExecuteTrade(string player1Id, string player2Id, string pokemonId1, string pokemonId2)
    {
        var p1Poke = await _db.PokemonInstances.Find(Builders<PokemonInstance>.Filter.And(Builders<PokemonInstance>.Filter.Eq(p => p.Id, pokemonId1), Builders<PokemonInstance>.Filter.Eq(p => p.OwnerId, player1Id))).FirstOrDefaultAsync();
        var p2Poke = await _db.PokemonInstances.Find(Builders<PokemonInstance>.Filter.And(Builders<PokemonInstance>.Filter.Eq(p => p.Id, pokemonId2), Builders<PokemonInstance>.Filter.Eq(p => p.OwnerId, player2Id))).FirstOrDefaultAsync();

        if (p1Poke == null || p2Poke == null) return false;

        var update1 = Builders<PokemonInstance>.Update.Set(p => p.OwnerId, player2Id).Set(p => p.IsInParty, false).Unset(p => p.PartySlot);
        var update2 = Builders<PokemonInstance>.Update.Set(p => p.OwnerId, player1Id).Set(p => p.IsInParty, false).Unset(p => p.PartySlot);

        await _db.PokemonInstances.UpdateOneAsync(Builders<PokemonInstance>.Filter.Eq(p => p.Id, pokemonId1), update1);
        await _db.PokemonInstances.UpdateOneAsync(Builders<PokemonInstance>.Filter.Eq(p => p.Id, pokemonId2), update2);

        return true;
    }

    public Task<bool> CheckCanEnterZone(string playerId, string requiredBossId) => Task.FromResult(true);
    public Task OnBossDefeated(string playerId, string bossId) => Task.CompletedTask;
}
