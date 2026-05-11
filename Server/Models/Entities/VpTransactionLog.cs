using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PokemonMMO.Models;

/// <summary>
/// Immutable log record for every VP balance change.
/// Captures who, how much, why, and the resulting balance — to power
/// transaction history, audits, and anti-fraud queries.
/// </summary>
public class VpTransactionLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("player_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PlayerId { get; set; } = null!;

    /// <summary>
    /// Source of the VP change. Examples:
    ///   "purchase_pokemon", "stat_respec", "move_swap",
    ///   "battle_win", "battle_lose",
    ///   "debug_add", "debug_spend".
    /// </summary>
    [BsonElement("type")]
    public string Type { get; set; } = null!;

    /// <summary>Signed VP change (positive = added, negative = spent).</summary>
    [BsonElement("delta")]
    public int Delta { get; set; }

    /// <summary>Player VP balance immediately after the change.</summary>
    [BsonElement("balance_after")]
    public int BalanceAfter { get; set; }

    /// <summary>
    /// Optional reference to the related entity
    /// (e.g. PokemonInstance.Id for a purchase, BattleSession.Id for a reward).
    /// </summary>
    [BsonElement("ref_id")]
    [BsonIgnoreIfNull]
    public string? RefId { get; set; }

    /// <summary>Free-form key/value bag — keeps schema flexible.</summary>
    [BsonElement("metadata")]
    [BsonIgnoreIfNull]
    public Dictionary<string, string>? Metadata { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
