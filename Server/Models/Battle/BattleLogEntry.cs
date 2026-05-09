using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PokemonMMO.Models;

/// <summary>
/// Immutable battle audit record persisted for replay/debug/reporting.
/// </summary>
public class BattleLogEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("battle_id")]
    public string BattleId { get; set; } = null!;

    [BsonElement("source")]
    public string Source { get; set; } = null!;

    [BsonElement("resolved_turn_number")]
    public int ResolvedTurnNumber { get; set; }

    [BsonElement("next_turn_number")]
    public int NextTurnNumber { get; set; }

    [BsonElement("state")]
    public string State { get; set; } = null!;

    [BsonElement("player1_id")]
    public string Player1Id { get; set; } = null!;

    [BsonElement("player2_id")]
    public string Player2Id { get; set; } = null!;

    [BsonElement("winner_player_id")]
    [BsonIgnoreIfNull]
    public string? WinnerPlayerId { get; set; }

    [BsonElement("events")]
    public List<string> Events { get; set; } = new();

    [BsonElement("created_at")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
