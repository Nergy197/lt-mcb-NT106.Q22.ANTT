namespace PokemonMMO.Models.DTOs;

// ── Chat DTOs ────────────────────────────────────────────────────────────────

/// <summary>Sent by client to post a World Chat message.</summary>
public record SendWorldMessageRequest(string Content);

/// <summary>Sent by client to post a DM to a friend.</summary>
public record SendDirectMessageRequest(string ReceiverPlayerId, string Content);

/// <summary>Returned to clients for each chat message.</summary>
public class ChatMessageDto
{
    public string Id { get; set; } = null!;
    public string Channel { get; set; } = null!;
    public string SenderId { get; set; } = null!;
    public string SenderName { get; set; } = null!;
    public string? ReceiverId { get; set; }
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

// ── Friend DTOs ──────────────────────────────────────────────────────────────

/// <summary>Sent by client to add a friend by player name.</summary>
public record FriendRequestDto(string PlayerName);

/// <summary>Sent by client to accept/reject a friend request.</summary>
public record FriendRespondDto(string FriendshipId, bool Accept);

/// <summary>Returned to client — one entry in the friend list.</summary>
public class FriendInfoDto
{
    public string PlayerId { get; set; } = null!;
    public string PlayerName { get; set; } = null!;
    public bool IsOnline { get; set; }

    /// <summary>Thời gian hoạt động gần nhất (null nếu đang online hoặc chưa từng online).</summary>
    public DateTime? LastSeenAt { get; set; }
}

/// <summary>Returned to client — a pending friend request.</summary>
public class FriendRequestInfoDto
{
    public string FriendshipId { get; set; } = null!;
    public string RequesterId { get; set; } = null!;
    public string RequesterName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Returned by search API — a player found by name.</summary>
public class PlayerSearchResultDto
{
    public string PlayerId { get; set; } = null!;
    public string PlayerName { get; set; } = null!;
    public bool IsOnline { get; set; }

    /// <summary>"none", "pending_sent", "pending_received", "accepted"</summary>
    public string FriendshipStatus { get; set; } = "none";
}
