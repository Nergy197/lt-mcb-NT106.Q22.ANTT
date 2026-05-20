using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using System.Linq;
using System.Threading.Tasks;

namespace PokemonMMO.Services;

public class GameService(MongoDbContext db)
{
    private readonly MongoDbContext _db = db;

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
