using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PokemonMMO.Models;

/// <summary>
/// User account — authentication credentials.
/// </summary>
public class Account
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("username")]
    public string Username { get; set; } = null!;

    [BsonElement("password_hash")]
    public string PasswordHash { get; set; } = null!;

    [BsonElement("email")]
    public string Email { get; set; } = null!;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("password_reset_token")]
    public string? PasswordResetToken { get; set; }

    [BsonElement("password_reset_expiry")]
    public DateTime? PasswordResetExpiry { get; set; }

    [BsonElement("is_verified")]
    public bool IsVerified { get; set; } = true; // Cho phép acc cũ login bình thường

    [BsonElement("registration_token")]
    public string? RegistrationToken { get; set; }

    [BsonElement("registration_token_expiry")]
    public DateTime? RegistrationTokenExpiry { get; set; }
}
