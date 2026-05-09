using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PokemonMMO.Models;

public class RevokedToken
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("token")]
    public string Token { get; set; } = null!;

    [BsonElement("expiry")]
    public DateTime Expiry { get; set; }
}
