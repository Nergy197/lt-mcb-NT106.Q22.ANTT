using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;

namespace PokemonMMO.Services;

/// <summary>
/// Manages friendships between players: send request, accept, reject, unfriend, list, search.
/// </summary>
public class FriendService
{
    private readonly MongoDbContext _db;

    public FriendService(MongoDbContext db)
    {
        _db = db;
    }

    // ── Send Friend Request ──────────────────────────────────────────────

    /// <summary>
    /// Sends a friend request from <paramref name="requesterId"/> to a player found by <paramref name="targetPlayerName"/>.
    /// Returns (success, error, receiverPlayerId).
    /// </summary>
    public async Task<(bool Success, string? Error, string? ReceiverPlayerId)> SendRequestAsync(string requesterId, string targetPlayerName)
    {
        // Find target player by name
        var target = await _db.Players
            .Find(p => p.Name == targetPlayerName)
            .FirstOrDefaultAsync();

        if (target == null)
            return (false, $"Không tìm thấy người chơi \"{targetPlayerName}\".", null);

        if (target.Id == requesterId)
            return (false, "Bạn không thể kết bạn với chính mình.", null);

        // Check if a friendship/request already exists in either direction
        var existing = await _db.Friendships
            .Find(f =>
                (f.RequesterId == requesterId && f.ReceiverId == target.Id) ||
                (f.RequesterId == target.Id && f.ReceiverId == requesterId))
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            if (existing.Status == "accepted")
                return (false, $"Bạn đã là bạn bè với \"{targetPlayerName}\" rồi.", null);
            if (existing.Status == "pending")
                return (false, $"Đã có lời mời kết bạn đang chờ xử lý.", null);
        }

        var friendship = new Friendship
        {
            RequesterId = requesterId,
            ReceiverId  = target.Id,
            Status      = "pending",
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        await _db.Friendships.InsertOneAsync(friendship);
        return (true, null, target.Id);
    }

    // ── Accept / Reject ──────────────────────────────────────────────────

    /// <summary>
    /// Responds to a pending friend request.
    /// Only the receiver can accept/reject.
    /// </summary>
    public async Task<(bool Success, string? Error)> RespondAsync(string playerId, string friendshipId, bool accept)
    {
        var friendship = await _db.Friendships
            .Find(f => f.Id == friendshipId)
            .FirstOrDefaultAsync();

        if (friendship == null)
            return (false, "Không tìm thấy lời mời kết bạn.");

        if (friendship.ReceiverId != playerId)
            return (false, "Bạn không có quyền phản hồi lời mời này.");

        if (friendship.Status != "pending")
            return (false, "Lời mời này đã được xử lý.");

        var newStatus = accept ? "accepted" : "rejected";
        var update = Builders<Friendship>.Update
            .Set(f => f.Status, newStatus)
            .Set(f => f.UpdatedAt, DateTime.UtcNow);

        await _db.Friendships.UpdateOneAsync(f => f.Id == friendshipId, update);
        return (true, null);
    }

    // ── Unfriend ─────────────────────────────────────────────────────────

    /// <summary>
    /// Removes a friendship between two players (either side can unfriend).
    /// </summary>
    public async Task<(bool Success, string? Error)> UnfriendAsync(string playerId, string friendPlayerId)
    {
        var result = await _db.Friendships.DeleteOneAsync(f =>
            f.Status == "accepted" &&
            ((f.RequesterId == playerId && f.ReceiverId == friendPlayerId) ||
             (f.RequesterId == friendPlayerId && f.ReceiverId == playerId)));

        return result.DeletedCount > 0
            ? (true, null)
            : (false, "Không tìm thấy mối quan hệ bạn bè.");
    }

    // ── Queries ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the accepted friends list for a player, including LastSeenAt.
    /// </summary>
    public async Task<List<FriendInfoDto>> GetFriendsAsync(string playerId, Func<string, bool>? isOnlineCheck = null)
    {
        var friendships = await _db.Friendships
            .Find(f => f.Status == "accepted" &&
                       (f.RequesterId == playerId || f.ReceiverId == playerId))
            .ToListAsync();

        var friendIds = friendships
            .Select(f => f.RequesterId == playerId ? f.ReceiverId : f.RequesterId)
            .ToList();

        if (friendIds.Count == 0)
            return new List<FriendInfoDto>();

        var friends = await _db.Players
            .Find(Builders<Player>.Filter.In(p => p.Id, friendIds))
            .ToListAsync();

        return friends.Select(p =>
        {
            var online = isOnlineCheck?.Invoke(p.Id) ?? false;
            return new FriendInfoDto
            {
                PlayerId   = p.Id,
                PlayerName = p.Name,
                IsOnline   = online,
                LastSeenAt = online ? null : p.LastSeenAt // null khi đang online
            };
        }).ToList();
    }

    /// <summary>
    /// Returns pending friend requests received by a player.
    /// </summary>
    public async Task<List<FriendRequestInfoDto>> GetPendingRequestsAsync(string playerId)
    {
        var requests = await _db.Friendships
            .Find(f => f.ReceiverId == playerId && f.Status == "pending")
            .SortByDescending(f => f.CreatedAt)
            .ToListAsync();

        var requesterIds = requests.Select(r => r.RequesterId).ToList();
        var requesters = await _db.Players
            .Find(Builders<Player>.Filter.In(p => p.Id, requesterIds))
            .ToListAsync();

        var nameMap = requesters.ToDictionary(p => p.Id, p => p.Name);

        return requests.Select(r => new FriendRequestInfoDto
        {
            FriendshipId  = r.Id,
            RequesterId   = r.RequesterId,
            RequesterName = nameMap.GetValueOrDefault(r.RequesterId, "Unknown"),
            CreatedAt     = r.CreatedAt
        }).ToList();
    }

    /// <summary>
    /// Checks whether two players are friends.
    /// </summary>
    public async Task<bool> AreFriendsAsync(string playerId1, string playerId2)
    {
        return await _db.Friendships
            .Find(f => f.Status == "accepted" &&
                       ((f.RequesterId == playerId1 && f.ReceiverId == playerId2) ||
                        (f.RequesterId == playerId2 && f.ReceiverId == playerId1)))
            .AnyAsync();
    }

    // ── Search ───────────────────────────────────────────────────────────

    /// <summary>
    /// Searches for players by name (partial match, case-insensitive).
    /// Returns results with friendship status relative to the searching player.
    /// </summary>
    public async Task<List<PlayerSearchResultDto>> SearchPlayersAsync(
        string currentPlayerId, string query, Func<string, bool>? isOnlineCheck = null, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new List<PlayerSearchResultDto>();

        // Case-insensitive partial match on Player.Name
        var filter = Builders<Player>.Filter.Regex(
            p => p.Name,
            new MongoDB.Bson.BsonRegularExpression(query, "i"));

        var players = await _db.Players
            .Find(filter)
            .Limit(limit)
            .ToListAsync();

        // Remove self from results
        players = players.Where(p => p.Id != currentPlayerId).ToList();

        if (players.Count == 0)
            return new List<PlayerSearchResultDto>();

        // Batch-load friendships for all found players
        var playerIds = players.Select(p => p.Id).ToList();
        var friendships = await _db.Friendships
            .Find(f =>
                (f.RequesterId == currentPlayerId && playerIds.Contains(f.ReceiverId)) ||
                (f.ReceiverId == currentPlayerId && playerIds.Contains(f.RequesterId)))
            .ToListAsync();

        return players.Select(p =>
        {
            var friendship = friendships.FirstOrDefault(f =>
                (f.RequesterId == currentPlayerId && f.ReceiverId == p.Id) ||
                (f.RequesterId == p.Id && f.ReceiverId == currentPlayerId));

            string status = "none";
            if (friendship != null)
            {
                if (friendship.Status == "accepted")
                    status = "accepted";
                else if (friendship.Status == "pending" && friendship.RequesterId == currentPlayerId)
                    status = "pending_sent";
                else if (friendship.Status == "pending" && friendship.ReceiverId == currentPlayerId)
                    status = "pending_received";
            }

            return new PlayerSearchResultDto
            {
                PlayerId         = p.Id,
                PlayerName       = p.Name,
                IsOnline         = isOnlineCheck?.Invoke(p.Id) ?? false,
                FriendshipStatus = status
            };
        }).ToList();
    }

    // ── LastSeen ─────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the LastSeenAt timestamp for a player (called on ChatHub disconnect).
    /// </summary>
    public async Task UpdateLastSeenAsync(string playerId)
    {
        var update = Builders<Player>.Update.Set(p => p.LastSeenAt, DateTime.UtcNow);
        await _db.Players.UpdateOneAsync(p => p.Id == playerId, update);
    }
}
