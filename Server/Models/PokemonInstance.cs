using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PokemonMMO.Models;

/// <summary>
/// A specific Pokemon owned by a player — optimized with Embedded Documents for Matchmaking.
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
    public int Level { get; set; } = 50; // Usually fixed at 50 for Esports

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

    [BsonElement("is_trial")]
    public bool IsTrial { get; set; } = false;

    [BsonElement("trial_expiry")]
    [BsonIgnoreIfNull]
    public DateTime? TrialExpiry { get; set; }

    [BsonElement("held_item")]
    [BsonIgnoreIfNull]
    public string? HeldItem { get; set; }

    [BsonElement("ivs")]
    public StatBlock Ivs { get; set; } = new();

    [BsonElement("evs")]
    public StatBlock Evs { get; set; } = new();

    [BsonElement("moves")]
    public List<PokemonMove> Moves { get; set; } = new();
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

/// <summary>
/// Embedded move for a PokemonInstance instance.
/// </summary>
public class PokemonMove
{
    [BsonElement("move_id")]
    public int MoveId { get; set; }

    [BsonElement("move_name")]
    public string MoveName { get; set; } = "Unknown";

    [BsonElement("current_pp")]
    public int CurrentPp { get; set; }
}
