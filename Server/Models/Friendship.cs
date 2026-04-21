using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PokemonMMO.Models;

/// <summary>
/// Represents a friendship or friend request between two players.
/// Status flow: Pending → Accepted / Rejected.
/// </summary>
public class Friendship
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>Player who sent the friend request.</summary>
    [BsonElement("requester_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string RequesterId { get; set; } = null!;

    /// <summary>Player who received the friend request.</summary>
    [BsonElement("receiver_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ReceiverId { get; set; } = null!;

    /// <summary>"pending", "accepted", "rejected"</summary>
    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
