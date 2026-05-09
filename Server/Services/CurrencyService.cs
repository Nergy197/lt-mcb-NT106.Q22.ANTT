using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;

namespace PokemonMMO.Services;

/// <summary>
/// Server-authoritative VP operations. All balance changes should go through
/// this service so checks and updates stay atomic in MongoDB.
/// </summary>
public class CurrencyService
{
    public const int BattleWinReward = 200;
    public const int BattleLoseReward = 80;

    private readonly MongoDbContext _db;

    public CurrencyService(MongoDbContext db)
    {
        _db = db;
    }

    public async Task<Player?> GetPlayerByAccountIdAsync(string accountId)
    {
        return await _db.Players
            .Find(p => p.AccountId == accountId)
            .FirstOrDefaultAsync();
    }

    public async Task<int?> GetVPByAccountIdAsync(string accountId)
    {
        var player = await GetPlayerByAccountIdAsync(accountId);
        return player?.VP;
    }

    public async Task<int?> AddVPAsync(string playerId, int amount)
    {
        if (amount <= 0) return null;

        var filter = Builders<Player>.Filter.Eq(p => p.Id, playerId);
        var update = Builders<Player>.Update.Inc(p => p.VP, amount);
        var options = new FindOneAndUpdateOptions<Player, Player>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updated = await _db.Players.FindOneAndUpdateAsync(
            filter,
            update,
            options);

        return updated?.VP;
    }

    public async Task<int?> SpendVPAsync(string playerId, int cost)
    {
        if (cost <= 0) return null;

        var filter = Builders<Player>.Filter.And(
            Builders<Player>.Filter.Eq(p => p.Id, playerId),
            Builders<Player>.Filter.Gte(p => p.VP, cost));
        var update = Builders<Player>.Update.Inc(p => p.VP, -cost);
        var options = new FindOneAndUpdateOptions<Player, Player>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updated = await _db.Players.FindOneAndUpdateAsync(
            filter,
            update,
            options);

        return updated?.VP;
    }

    public async Task<BattleVpRewardResult> AwardBattleVPAsync(
        string player1Id,
        string player2Id,
        string winnerPlayerId,
        string botPlayerId)
    {
        if (winnerPlayerId == "draw")
            return new BattleVpRewardResult();

        var loserPlayerId = winnerPlayerId == player1Id ? player2Id : player1Id;

        int? winnerVp = null;
        int? loserVp = null;

        if (winnerPlayerId != botPlayerId)
            winnerVp = await AddVPAsync(winnerPlayerId, BattleWinReward);

        if (loserPlayerId != botPlayerId)
            loserVp = await AddVPAsync(loserPlayerId, BattleLoseReward);

        return new BattleVpRewardResult
        {
            WinnerPlayerId = winnerPlayerId,
            WinnerVP = winnerVp,
            WinnerDelta = winnerVp.HasValue ? BattleWinReward : 0,
            LoserPlayerId = loserPlayerId,
            LoserVP = loserVp,
            LoserDelta = loserVp.HasValue ? BattleLoseReward : 0
        };
    }
}

public class BattleVpRewardResult
{
    public string? WinnerPlayerId { get; set; }
    public int? WinnerVP { get; set; }
    public int WinnerDelta { get; set; }
    public string? LoserPlayerId { get; set; }
    public int? LoserVP { get; set; }
    public int LoserDelta { get; set; }
}
