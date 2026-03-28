using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PokemonMMO.Models;

/// <summary>
/// A specific Pokemon owned by a player — level, HP, nature, party status.
/// </summary>
public class PokemonInstance
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("owner_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string OwnerId { get; set; } = null!;

    [BsonElement("species_id")]
    public int SpeciesId { get; set; }

    [BsonElement("nickname")]
    public string Nickname { get; set; } = "";

    [BsonElement("level")]
    public int Level { get; set; } = 1;

    [BsonElement("exp")]
    public int Exp { get; set; } = 0;

    [BsonElement("nature")]
    public string Nature { get; set; } = null!;

    [BsonElement("current_hp")]
    public int CurrentHp { get; set; }

    [BsonElement("max_hp")]
    public int MaxHp { get; set; }

    [BsonElement("status_condition")]
    public string StatusCondition { get; set; } = "NONE";

    [BsonElement("is_in_party")]
    public bool IsInParty { get; set; } = false;

    [BsonElement("party_slot")]
    [BsonIgnoreIfNull]
    public int? PartySlot { get; set; }
}
