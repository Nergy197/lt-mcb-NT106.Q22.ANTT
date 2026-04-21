using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;

namespace PokemonMMO.Services;

/// <summary>
/// Handles chat message persistence and retrieval for World and DM channels.
/// Real-time delivery is handled by ChatHub; this service manages MongoDB storage.
/// </summary>
public class ChatService
{
    private readonly MongoDbContext _db;

    /// <summary>Maximum characters allowed per message.</summary>
    private const int MaxMessageLength = 500;

    /// <summary>Default number of historical messages returned.</summary>
    private const int DefaultHistoryLimit = 50;

    public ChatService(MongoDbContext db)
    {
        _db = db;
    }

    // ── World Chat ───────────────────────────────────────────────────────

    /// <summary>
    /// Saves a World Chat message and returns its DTO.
    /// </summary>
    public async Task<ChatMessageDto> SaveWorldMessageAsync(string senderId, string senderName, string content)
    {
        var sanitized = SanitizeContent(content);

        var msg = new ChatMessage
        {
            Channel    = "world",
            SenderId   = senderId,
            SenderName = senderName,
            Content    = sanitized,
            CreatedAt  = DateTime.UtcNow
        };

        await _db.ChatMessages.InsertOneAsync(msg);
        return ToDto(msg);
    }

    /// <summary>
    /// Returns the latest World Chat messages (newest last).
    /// </summary>
    public async Task<List<ChatMessageDto>> GetWorldHistoryAsync(int limit = DefaultHistoryLimit)
    {
        var messages = await _db.ChatMessages
            .Find(m => m.Channel == "world")
            .SortByDescending(m => m.CreatedAt)
            .Limit(limit)
            .ToListAsync();

        messages.Reverse(); // oldest first
        return messages.Select(ToDto).ToList();
    }

    // ── Direct Messages ──────────────────────────────────────────────────

    /// <summary>
    /// Saves a DM and returns its DTO.
    /// </summary>
    public async Task<ChatMessageDto> SaveDirectMessageAsync(
        string senderId, string senderName, string receiverId, string content)
    {
        var sanitized = SanitizeContent(content);

        var msg = new ChatMessage
        {
            Channel    = "dm",
            SenderId   = senderId,
            SenderName = senderName,
            ReceiverId = receiverId,
            Content    = sanitized,
            CreatedAt  = DateTime.UtcNow
        };

        await _db.ChatMessages.InsertOneAsync(msg);
        return ToDto(msg);
    }

    /// <summary>
    /// Returns conversation history between two players (newest last).
    /// </summary>
    public async Task<List<ChatMessageDto>> GetDirectHistoryAsync(
        string playerId, string otherPlayerId, int limit = DefaultHistoryLimit)
    {
        var filter = Builders<ChatMessage>.Filter.And(
            Builders<ChatMessage>.Filter.Eq(m => m.Channel, "dm"),
            Builders<ChatMessage>.Filter.Or(
                Builders<ChatMessage>.Filter.And(
                    Builders<ChatMessage>.Filter.Eq(m => m.SenderId, playerId),
                    Builders<ChatMessage>.Filter.Eq(m => m.ReceiverId, otherPlayerId)),
                Builders<ChatMessage>.Filter.And(
                    Builders<ChatMessage>.Filter.Eq(m => m.SenderId, otherPlayerId),
                    Builders<ChatMessage>.Filter.Eq(m => m.ReceiverId, playerId))
            ));

        var messages = await _db.ChatMessages
            .Find(filter)
            .SortByDescending(m => m.CreatedAt)
            .Limit(limit)
            .ToListAsync();

        messages.Reverse();
        return messages.Select(ToDto).ToList();
    }

    // ── Cleanup (reset DM on logout) ───────────────────────────────────

    /// <summary>
    /// Deletes all DM messages sent or received by a player.
    /// Called on logout to reset friend chat history.
    /// </summary>
    public async Task DeleteDirectMessagesAsync(string playerId)
    {
        var filter = Builders<ChatMessage>.Filter.And(
            Builders<ChatMessage>.Filter.Eq(m => m.Channel, "dm"),
            Builders<ChatMessage>.Filter.Or(
                Builders<ChatMessage>.Filter.Eq(m => m.SenderId, playerId),
                Builders<ChatMessage>.Filter.Eq(m => m.ReceiverId, playerId)));

        var result = await _db.ChatMessages.DeleteManyAsync(filter);
        Console.WriteLine($"[Chat] Đã xoá {result.DeletedCount} tin nhắn DM của player {playerId}.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string SanitizeContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Nội dung tin nhắn không được để trống.");

        content = content.Trim();
        if (content.Length > MaxMessageLength)
            content = content[..MaxMessageLength];

        return content;
    }

    private static ChatMessageDto ToDto(ChatMessage m) => new()
    {
        Id         = m.Id,
        Channel    = m.Channel,
        SenderId   = m.SenderId,
        SenderName = m.SenderName,
        ReceiverId = m.ReceiverId,
        Content    = m.Content,
        CreatedAt  = m.CreatedAt
    };
}
