using System;
using System.Linq;
using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;

namespace PokemonMMO.Services;

public class RankService
{
    public const int RankedWinPoints = 84;
    public const int RankedLossPoints = 77;

    private readonly MongoDbContext _db;

    public RankService(MongoDbContext db)
    {
        _db = db;
    }

    public async Task<RankBattleResult> ApplyRankedBattleResultAsync(BattleSession session, string winnerPlayerId)
    {
        RankBattleResult result = new();

        if (session.Mode != BattleMode.Ranked ||
            string.IsNullOrWhiteSpace(winnerPlayerId) ||
            winnerPlayerId == "draw" ||
            session.Player1Id == BattleService.BotPlayerId ||
            session.Player2Id == BattleService.BotPlayerId)
            return result;

        string loserPlayerId = winnerPlayerId == session.Player1Id ? session.Player2Id : session.Player1Id;

        Player? winner = await AddWinnerPointsAsync(winnerPlayerId);
        Player? loser = await SubtractLoserPointsAsync(loserPlayerId);

        result.WinnerPlayerId = winnerPlayerId;
        result.WinnerRankPoints = winner?.RankPoints;
        result.WinnerDelta = winner == null ? 0 : RankedWinPoints;
        result.LoserPlayerId = loserPlayerId;
        result.LoserRankPoints = loser?.RankPoints;
        result.LoserDelta = loser == null ? 0 : -RankedLossPoints;
        return result;
    }

    /// <summary>
    /// Đếm số trận Casual riêng (casual_matches) + số thắng Casual (casual_wins) cho mỗi
    /// người chơi thật. KHÔNG đụng tới rank_points/ranked_* (Casual không ảnh hưởng xếp hạng).
    /// Bỏ qua bot (không ghi thống kê cho bot).
    /// </summary>
    public async Task ApplyCasualBattleResultAsync(BattleSession session, string? winnerPlayerId)
    {
        if (session.Mode != BattleMode.Casual)
            return;

        bool hasWinner = !string.IsNullOrWhiteSpace(winnerPlayerId) && winnerPlayerId != "draw";

        foreach (var pid in new[] { session.Player1Id, session.Player2Id })
        {
            if (pid == BattleService.BotPlayerId)
                continue;

            var update = Builders<Player>.Update.Inc(p => p.CasualMatches, 1);
            if (hasWinner && winnerPlayerId == pid)
                update = update.Inc(p => p.CasualWins, 1);

            await _db.Players.UpdateOneAsync(
                Builders<Player>.Filter.Eq(p => p.Id, pid),
                update);
        }
    }

    public async Task<List<RankLeaderboardEntryDto>> GetTop100Async()
    {
        List<Player> players = await _db.Players
            .Find(p => p.RankedMatches >= 1)
            .SortByDescending(p => p.RankPoints)
            .ThenByDescending(p => p.RankedWins)
            .Limit(100)
            .ToListAsync();

        return players.Select((p, index) => new RankLeaderboardEntryDto
        {
            Rank = index + 1,
            PlayerId = p.Id,
            PlayerName = p.Name,
            Score = p.RankPoints,
            RankPoints = p.RankPoints,
            RankedWins = p.RankedWins,
            RankedMatches = p.RankedMatches
        }).ToList();
    }

    public async Task<List<RankLeaderboardEntryDto>> GetFriendsRankAsync(string playerId)
    {
        var friendships = await _db.Friendships
            .Find(f => f.Status == "accepted" &&
                       (f.RequesterId == playerId || f.ReceiverId == playerId))
            .ToListAsync();

        List<string> playerIds = friendships
            .Select(f => f.RequesterId == playerId ? f.ReceiverId : f.RequesterId)
            .Append(playerId)
            .Distinct()
            .ToList();

        List<Player> players = await _db.Players
            .Find(Builders<Player>.Filter.In(p => p.Id, playerIds))
            .ToListAsync();

        return players
            .OrderByDescending(p => p.RankPoints)
            .ThenByDescending(p => p.RankedWins)
            .ThenBy(p => p.Name)
            .Select((p, index) => new RankLeaderboardEntryDto
            {
                Rank = index + 1,
                PlayerId = p.Id,
                PlayerName = p.Name,
                Score = p.RankPoints,
                RankPoints = p.RankPoints,
                RankedWins = p.RankedWins,
                RankedMatches = p.RankedMatches,
                IsSelf = p.Id == playerId
            })
            .ToList();
    }

    private async Task<Player?> AddWinnerPointsAsync(string playerId)
    {
        var update = Builders<Player>.Update
            .Inc(p => p.RankPoints, RankedWinPoints)
            .Inc(p => p.RankedWins, 1)
            .Inc(p => p.RankedMatches, 1);

        var filter = Builders<Player>.Filter.Eq(p => p.Id, playerId);
        return await _db.Players.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Player, Player>
            {
                ReturnDocument = ReturnDocument.After
            });
    }

    private async Task<Player?> SubtractLoserPointsAsync(string playerId)
    {
        Player? loser = await _db.Players
            .Find(p => p.Id == playerId)
            .FirstOrDefaultAsync();

        if (loser == null)
            return null;

        int nextRankPoints = Math.Max(0, loser.RankPoints - RankedLossPoints);
        var update = Builders<Player>.Update
            .Set(p => p.RankPoints, nextRankPoints)
            .Inc(p => p.RankedMatches, 1);

        var filter = Builders<Player>.Filter.Eq(p => p.Id, playerId);
        return await _db.Players.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Player, Player>
            {
                ReturnDocument = ReturnDocument.After
            });
    }
}

public class RankBattleResult
{
    public string? WinnerPlayerId { get; set; }
    public int? WinnerRankPoints { get; set; }
    public int WinnerDelta { get; set; }
    public string? LoserPlayerId { get; set; }
    public int? LoserRankPoints { get; set; }
    public int LoserDelta { get; set; }
}
