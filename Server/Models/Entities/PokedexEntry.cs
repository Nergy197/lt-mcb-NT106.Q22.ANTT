using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace PokemonMMO.Models;

// "Bùa hộ mệnh" này giúp máy không báo lỗi nếu thấy cột lạ trong DB
[BsonIgnoreExtraElements] 
public class PokedexEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? InternalId { get; set; }

    [BsonElement("id")] 
    public int Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("types")]
    public List<string> Types { get; set; } = new();

    [BsonElement("base_stats")]
    [JsonPropertyName("base_stats")]
    public Dictionary<string, int> BaseStats { get; set; } = new();

    [BsonElement("sprite_url")]
    [JsonPropertyName("sprite_url")]
    public string SpriteUrl { get; set; } = null!;

    // Em thêm luôn 2 ông này cho đủ bộ nhé
    [BsonElement("height")]
    public int Height { get; set; }

    [BsonElement("weight")]
    public int Weight { get; set; }

    [BsonElement("default_moves")]
    public List<int> DefaultMoves { get; set; } = new();
}