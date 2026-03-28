namespace PokemonMMO.Models.DTOs;

public record RegisterRequest(string Username, string Email, string Password);

public record LoginRequest(string Username, string Password);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword);

public record AuthResponse(string Token, string Username, string AccountId, string PlayerId);
