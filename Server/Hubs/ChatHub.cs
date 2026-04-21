using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;
using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;
using PokemonMMO.Services;

namespace PokemonMMO.Hubs;

/// <summary>
/// SignalR Hub for real-time chat — World Chat &amp; Direct Messages (DM).
///
/// Client events (server → client):
///   "WorldMessage"            — a new World Chat message
///   "DirectMessage"           — a new DM from a friend
///   "ChatHistory"             — historical messages (world or dm)
///   "FriendOnline"            — a friend came online
///   "FriendOffline"           — a friend went offline
///   "FriendRequestReceived"   — someone sent you a friend request (real-time)
///   "TypingIndicator"         — a friend is typing in DM
///   "Error"                   — error message
///
/// Hub methods (client → server):
///   SendWorldMessage(content)
///   SendDirectMessage(receiverPlayerId, content)
///   LoadWorldHistory()
///   LoadDirectHistory(otherPlayerId)
///   StartTyping(receiverPlayerId)
///   StopTyping(receiverPlayerId)
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ChatHub : Hub
{
    private readonly MongoDbContext _db;
    private readonly ChatService _chatService;
    private readonly FriendService _friendService;

    // Track online players: ConnectionId → PlayerId
    private static readonly ConcurrentDictionary<string, string> OnlinePlayers = new();
    // Reverse lookup: PlayerId → ConnectionId (for targeted DM delivery)
    private static readonly ConcurrentDictionary<string, string> PlayerConnections = new();

    // ── Rate limiting for World Chat ─────────────────────────────────────
    // PlayerId → list of message timestamps (sliding window)
    private static readonly ConcurrentDictionary<string, List<DateTime>> RateLimitTracker = new();
    private const int MaxWorldMessagesPerWindow = 5;    // max messages allowed
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromSeconds(10); // in this time window

    public ChatHub(MongoDbContext db, ChatService chatService, FriendService friendService)
    {
        _db = db;
        _chatService = chatService;
        _friendService = friendService;
    }

    // ── Connection lifecycle ─────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var player = await GetAuthenticatedPlayer();
        if (player == null)
        {
            Context.Abort();
            return;
        }

        OnlinePlayers[Context.ConnectionId] = player.Id;
        PlayerConnections[player.Id] = Context.ConnectionId;

        // Join the World Chat group
        await Groups.AddToGroupAsync(Context.ConnectionId, "WorldChat");

        // Notify friends that this player is online
        await NotifyFriendsStatusAsync(player.Id, player.Name, online: true);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (OnlinePlayers.TryRemove(Context.ConnectionId, out var playerId))
        {
            PlayerConnections.TryRemove(playerId, out _);

            // Update LastSeenAt in database
            await _friendService.UpdateLastSeenAsync(playerId);

            // Notify friends that this player went offline
            var player = await _db.Players
                .Find(p => p.Id == playerId)
                .FirstOrDefaultAsync();

            if (player != null)
                await NotifyFriendsStatusAsync(player.Id, player.Name, online: false);

            // Cleanup rate limit tracker
            RateLimitTracker.TryRemove(playerId, out _);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ── World Chat ───────────────────────────────────────────────────────

    /// <summary>
    /// Broadcasts a message to everyone in World Chat.
    /// Rate limited: max 5 messages per 10 seconds.
    /// </summary>
    public async Task SendWorldMessage(string content)
    {
        var player = await ResolveCurrentPlayer();
        if (player == null) return;

        // Rate limit check
        if (!CheckRateLimit(player.Id))
        {
            await Clients.Caller.SendAsync("Error", "Bạn đang gửi tin nhắn quá nhanh. Vui lòng chờ vài giây.");
            return;
        }

        try
        {
            var dto = await _chatService.SaveWorldMessageAsync(player.Id, player.Name, content);
            await Clients.Group("WorldChat").SendAsync("WorldMessage", dto);
        }
        catch (ArgumentException ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    /// <summary>
    /// Returns the last N World Chat messages for the caller.
    /// </summary>
    public async Task LoadWorldHistory()
    {
        var history = await _chatService.GetWorldHistoryAsync();
        await Clients.Caller.SendAsync("ChatHistory", new { Channel = "world", Messages = history });
    }

    // ── Direct Messages ──────────────────────────────────────────────────

    /// <summary>
    /// Sends a DM to a friend. Both players must be friends.
    /// </summary>
    public async Task SendDirectMessage(string receiverPlayerId, string content)
    {
        var player = await ResolveCurrentPlayer();
        if (player == null) return;

        // Verify friendship
        var areFriends = await _friendService.AreFriendsAsync(player.Id, receiverPlayerId);
        if (!areFriends)
        {
            await Clients.Caller.SendAsync("Error", "Bạn chỉ có thể nhắn tin cho bạn bè.");
            return;
        }

        try
        {
            var dto = await _chatService.SaveDirectMessageAsync(player.Id, player.Name, receiverPlayerId, content);

            // Send to caller
            await Clients.Caller.SendAsync("DirectMessage", dto);

            // Send to receiver if online
            if (PlayerConnections.TryGetValue(receiverPlayerId, out var receiverConnId))
            {
                await Clients.Client(receiverConnId).SendAsync("DirectMessage", dto);
            }
        }
        catch (ArgumentException ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    /// <summary>
    /// Returns the conversation history between the caller and another player.
    /// </summary>
    public async Task LoadDirectHistory(string otherPlayerId)
    {
        var player = await ResolveCurrentPlayer();
        if (player == null) return;

        var history = await _chatService.GetDirectHistoryAsync(player.Id, otherPlayerId);
        await Clients.Caller.SendAsync("ChatHistory", new { Channel = "dm", OtherPlayerId = otherPlayerId, Messages = history });
    }

    // ── Typing Indicator ─────────────────────────────────────────────────

    /// <summary>
    /// Notifies the receiver that this player started typing.
    /// </summary>
    public async Task StartTyping(string receiverPlayerId)
    {
        if (!OnlinePlayers.TryGetValue(Context.ConnectionId, out var playerId))
            return;

        if (PlayerConnections.TryGetValue(receiverPlayerId, out var receiverConnId))
        {
            await Clients.Client(receiverConnId).SendAsync("TypingIndicator", new
            {
                PlayerId = playerId,
                IsTyping = true
            });
        }
    }

    /// <summary>
    /// Notifies the receiver that this player stopped typing.
    /// </summary>
    public async Task StopTyping(string receiverPlayerId)
    {
        if (!OnlinePlayers.TryGetValue(Context.ConnectionId, out var playerId))
            return;

        if (PlayerConnections.TryGetValue(receiverPlayerId, out var receiverConnId))
        {
            await Clients.Client(receiverConnId).SendAsync("TypingIndicator", new
            {
                PlayerId = playerId,
                IsTyping = false
            });
        }
    }

    // ── Online status helpers ────────────────────────────────────────────

    /// <summary>
    /// Checks if a player is currently connected to the ChatHub.
    /// Called by FriendService and FriendController to populate IsOnline.
    /// </summary>
    public static bool IsPlayerOnline(string playerId)
        => PlayerConnections.ContainsKey(playerId);

    /// <summary>
    /// Gets the ConnectionId of a player (for sending real-time notifications).
    /// Returns null if the player is offline.
    /// </summary>
    public static string? GetConnectionId(string playerId)
        => PlayerConnections.TryGetValue(playerId, out var connId) ? connId : null;

    /// <summary>
    /// Notifies all online friends about a player's status change.
    /// </summary>
    private async Task NotifyFriendsStatusAsync(string playerId, string playerName, bool online)
    {
        var friends = await _friendService.GetFriendsAsync(playerId);

        foreach (var friend in friends)
        {
            if (PlayerConnections.TryGetValue(friend.PlayerId, out var friendConnId))
            {
                var eventName = online ? "FriendOnline" : "FriendOffline";
                await Clients.Client(friendConnId).SendAsync(eventName, new
                {
                    PlayerId   = playerId,
                    PlayerName = playerName
                });
            }
        }
    }

    // ── Rate limiting ────────────────────────────────────────────────────

    /// <summary>
    /// Sliding window rate limiter for World Chat.
    /// Returns true if the message is allowed, false if rate limited.
    /// </summary>
    private static bool CheckRateLimit(string playerId)
    {
        var now = DateTime.UtcNow;
        var timestamps = RateLimitTracker.GetOrAdd(playerId, _ => new List<DateTime>());

        lock (timestamps)
        {
            // Remove timestamps outside the window
            timestamps.RemoveAll(t => now - t > RateLimitWindow);

            if (timestamps.Count >= MaxWorldMessagesPerWindow)
                return false;

            timestamps.Add(now);
            return true;
        }
    }

    // ── Auth helpers (same pattern as MatchmakingHub) ────────────────────

    private async Task<Player?> ResolveCurrentPlayer()
    {
        if (!OnlinePlayers.TryGetValue(Context.ConnectionId, out var playerId))
        {
            await Clients.Caller.SendAsync("Error", "Bạn chưa kết nối. Hãy kết nối lại.");
            return null;
        }

        return await _db.Players
            .Find(p => p.Id == playerId)
            .FirstOrDefaultAsync();
    }

    private async Task<Player?> GetAuthenticatedPlayer()
    {
        var accountId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(accountId)) return null;

        return await _db.Players
            .Find(Builders<Player>.Filter.Eq(p => p.AccountId, accountId))
            .FirstOrDefaultAsync();
    }
}
