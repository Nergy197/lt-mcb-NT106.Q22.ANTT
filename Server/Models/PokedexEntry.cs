using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

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
    public Dictionary<string, int> Base_Stats { get; set; } = new();

    [BsonElement("sprite_url")]
    public string Sprite_Url { get; set; } = null!;

    // Em thêm luôn 2 ông này cho đủ bộ nhé
    [BsonElement("height")]
    public int Height { get; set; }

    [BsonElement("weight")]
    public int Weight { get; set; }
}