using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PokemonMMO.Models;

/// <summary>
/// A single chat message — stored in MongoDB for history.
/// Channel = "world" for global chat, "dm" for direct messages between friends.
/// </summary>
public class ChatMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>"world" or "dm"</summary>
    [BsonElement("channel")]
    public string Channel { get; set; } = "world";

    /// <summary>Player ID of the sender.</summary>
    [BsonElement("sender_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string SenderId { get; set; } = null!;

    /// <summary>Display name of the sender (denormalized for fast reads).</summary>
    [BsonElement("sender_name")]
    public string SenderName { get; set; } = null!;

    /// <summary>Player ID of the receiver — only used when Channel == "dm".</summary>
    [BsonElement("receiver_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfNull]
    public string? ReceiverId { get; set; }

    /// <summary>The text content of the message.</summary>
    [BsonElement("content")]
    public string Content { get; set; } = null!;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
