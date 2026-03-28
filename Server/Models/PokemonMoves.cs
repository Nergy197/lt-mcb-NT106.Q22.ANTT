using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PokemonMMO.Models;

/// <summary>
/// A move equipped on a specific Pokemon instance (max 4 slots).
/// </summary>
public class PokemonMoves
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("pokemon_instance_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PokemonInstanceId { get; set; } = null!;

    [BsonElement("move_id")]
    public int MoveId { get; set; }

    [BsonElement("slot")]
    public int Slot { get; set; }

    [BsonElement("current_pp")]
    public int CurrentPp { get; set; }
}
