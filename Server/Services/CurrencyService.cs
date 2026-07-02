using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;

namespace PokemonMMO.Services;

/// <summary>
/// Server-authoritative VP operations. All balance changes should go through
/// this service so checks and updates stay atomic in MongoDB.
/// Every successful change is also persisted to <c>vp_transactions</c>
/// for audit / purchase-history queries.
/// </summary>
public class CurrencyService
{
    public const int BattleWinReward = 200;
    public const int BattleLoseReward = 80;

    // Reason / type identifiers used in vp_transactions.type
    public const string ReasonBattleWin     = "battle_win";
    public const string ReasonBattleLose    = "battle_lose";
    public const string ReasonPurchase      = "purchase_pokemon";
    public const string ReasonStatRespec    = "stat_respec";
    public const string ReasonMoveSwap      = "move_swap";
    public const string ReasonDebugAdd      = "debug_add";
    public const string ReasonDebugSpend    = "debug_spend";
    public const string ReasonWelcomeBonus  = "welcome_bonus";
    public const string ReasonRecruitRoll   = "recruit_roll";
    public const string ReasonRecruitPerm   = "recruit_permanent";

    public const int WelcomeBonus = 30000;

    private readonly MongoDbContext _db;
    private readonly ILogger<CurrencyService> _logger;

    public CurrencyService(MongoDbContext db, ILogger<CurrencyService> logger)
    {
        _db = db;
        _logger = logger;
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

    public async Task<int?> ClaimWelcomeBonusAsync(string accountId)
    {
        var player = await GetPlayerByAccountIdAsync(accountId);
        if (player == null) return null;
        if (player.WelcomeClaimed) return null;

        var filter = Builders<Player>.Filter.And(
            Builders<Player>.Filter.Eq(p => p.Id, player.Id),
            Builders<Player>.Filter.Ne(p => p.WelcomeClaimed, true));

        var update = Builders<Player>.Update
            .Inc(p => p.VP, WelcomeBonus)
            .Set(p => p.WelcomeClaimed, true);

        var options = new FindOneAndUpdateOptions<Player, Player>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updated = await _db.Players.FindOneAndUpdateAsync(filter, update, options);

        if (updated != null)
        {
            await LogTransactionAsync(player.Id, WelcomeBonus, updated.VP, ReasonWelcomeBonus);
            return updated.VP;
        }

        return null;
    }

    /// <summary>
    /// Atomically add VP and (when <paramref name="reason"/> is provided)
    /// write a row to <c>vp_transactions</c>.
    /// </summary>
    public async Task<int?> AddVPAsync(
        string playerId,
        int amount,
        string? reason = null,
        string? refId = null,
        Dictionary<string, string>? metadata = null)
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

        if (updated == null) return null;

        if (reason != null)
            await LogTransactionAsync(playerId, amount, updated.VP, reason, refId, metadata);

        return updated.VP;
    }

    /// <summary>
    /// Atomically spend VP (only if player has enough) and (when
    /// <paramref name="reason"/> is provided) write a row to <c>vp_transactions</c>.
    /// Returns the remaining balance, or <c>null</c> if the spend was rejected.
    /// </summary>
    public async Task<int?> SpendVPAsync(
        string playerId,
        int cost,
        string? reason = null,
        string? refId = null,
        Dictionary<string, string>? metadata = null)
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

        if (updated == null) return null;

        if (reason != null)
            await LogTransactionAsync(playerId, -cost, updated.VP, reason, refId, metadata);

        return updated.VP;
    }

    /// <summary>
    /// Persist an immutable VP transaction record. Failure to log must never
    /// roll back the VP change, so we swallow exceptions but log them.
    /// </summary>
    public async Task LogTransactionAsync(
        string playerId,
        int delta,
        int balanceAfter,
        string type,
        string? refId = null,
        Dictionary<string, string>? metadata = null)
    {
        try
        {
            await _db.VpTransactions.InsertOneAsync(new VpTransactionLog
            {
                PlayerId     = playerId,
                Type         = type,
                Delta        = delta,
                BalanceAfter = balanceAfter,
                RefId        = refId,
                Metadata     = metadata,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to write vp_transactions row (player={PlayerId}, type={Type}, delta={Delta})",
                playerId, type, delta);
        }
    }

    public async Task<BattleVpRewardResult> AwardBattleVPAsync(
        string player1Id,
        string player2Id,
        string winnerPlayerId,
        string botPlayerId,
        string? battleId = null)
    {
        if (winnerPlayerId == "draw")
            return new BattleVpRewardResult();

        var loserPlayerId = winnerPlayerId == player1Id ? player2Id : player1Id;

        int? winnerVp = null;
        int? loserVp = null;

        if (winnerPlayerId != botPlayerId)
            winnerVp = await AddVPAsync(
                winnerPlayerId,
                BattleWinReward,
                reason: ReasonBattleWin,
                refId: battleId);

        if (loserPlayerId != botPlayerId)
            loserVp = await AddVPAsync(
                loserPlayerId,
                BattleLoseReward,
                reason: ReasonBattleLose,
                refId: battleId);

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
