using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PokemonMMO.Models;

/// <summary>
/// In-game player character — linked to an Account.
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

    [BsonElement("money")]
    public int Money { get; set; } = 0;

    [BsonElement("current_map")]
    public string CurrentMap { get; set; } = "PalletTown";

    [BsonElement("position")]
    public Position Position { get; set; } = new();

    [BsonElement("beaten_bosses")]
    public List<string> BeatenBosses { get; set; } = new();
}

/// <summary>
/// 3D position in the game world.
/// </summary>
public class Position
{
    [BsonElement("x")]
    public float X { get; set; } = 0f;

    [BsonElement("y")]
    public float Y { get; set; } = 0f;

    [BsonElement("z")]
    public float Z { get; set; } = 0f;
}
