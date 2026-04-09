using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace PokemonMMO.Models;

[BsonIgnoreExtraElements] // Bùa hộ mệnh để bỏ qua các cột lạ như 'pp'
public class MoveEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? InternalId { get; set; }

    [BsonElement("id")]
    public int Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("power")]
    [JsonPropertyName("power")]
    public int? Power { get; set; }

    [BsonElement("accuracy")]
    [JsonPropertyName("accuracy")]
    public int? Accuracy { get; set; }

    [BsonElement("type")]
    public string Type { get; set; } = null!;

    [BsonElement("priority")]
    public int Priority { get; set; } = 0;

    [BsonElement("category")]
    public string Category { get; set; } = "Physical";

    // Anh có thể thêm 'pp' vào nếu muốn dùng sau này
    [BsonElement("pp")]
    [JsonPropertyName("pp")]
    public int PP { get; set; }
}