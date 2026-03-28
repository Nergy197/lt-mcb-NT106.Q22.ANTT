using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PokemonMMO.Models;

/// <summary>
/// Individual Values (IVs) and Effort Values (EVs) for a pokemon instance.
/// </summary>
public class PokemonStats
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("pokemon_instance_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PokemonInstanceId { get; set; } = null!;

    [BsonElement("ivs")]
    public StatBlock Ivs { get; set; } = new();

    [BsonElement("evs")]
    public StatBlock Evs { get; set; } = new();
}

/// <summary>
/// Six core stat values (HP, ATK, DEF, SpATK, SpDEF, SPD).
/// Range: 0–31 for IVs, 0–255 for EVs.
/// </summary>
public class StatBlock
{
    [BsonElement("hp")]    public int Hp    { get; set; }
    [BsonElement("atk")]   public int Atk   { get; set; }
    [BsonElement("def")]   public int Def   { get; set; }
    [BsonElement("spatk")] public int SpAtk { get; set; }
    [BsonElement("spdef")] public int SpDef { get; set; }
    [BsonElement("spd")]   public int Spd   { get; set; }
}
