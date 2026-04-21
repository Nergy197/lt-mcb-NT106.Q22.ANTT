using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PokemonMMO.Models;

/// <summary>
/// In-game player character — linked to an Account.
/// Refactored for PvP Esports Matchmaking.
/// </summary>
public class Player
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("account_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AccountId { get; set; } = null!;

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("vp")]
    public int VP { get; set; } = 0; // Victory Points

    [BsonElement("mmr")]
    public int MMR { get; set; } = 1000; // Matchmaking Rating

    [BsonElement("ranked_wins")]
    public int RankedWins { get; set; } = 0;

    [BsonElement("ranked_matches")]
    public int RankedMatches { get; set; } = 0;

    [BsonElement("last_seen_at")]
    public DateTime? LastSeenAt { get; set; }
}
