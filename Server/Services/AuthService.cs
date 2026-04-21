using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;

namespace PokemonMMO.Services;

public class AuthService
{
    private readonly MongoDbContext _db;
    private readonly ILogger<AuthService> _log;
    private readonly EmailService _emailService;
    private readonly GameService _gameService;
    private readonly ChatService _chatService;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly int _jwtExpiryHours;

    public AuthService(MongoDbContext db, IConfiguration config, ILogger<AuthService> log, EmailService emailService, GameService gameService, ChatService chatService)
    {
        _db           = db;
        _log          = log;
        _emailService = emailService;
        _gameService  = gameService;
        _chatService  = chatService;
        _jwtSecret   = config["Jwt:Secret"]   ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        _jwtIssuer   = config["Jwt:Issuer"]   ?? "PokemonMMO";
        _jwtAudience = config["Jwt:Audience"] ?? "PokemonMMO";
        _jwtExpiryHours = int.TryParse(config["Jwt:ExpiryHours"], out var h) ? h : 24;
    }

    // ── Register ──────────────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> RegisterAsync(RegisterRequest req)
    {
        _log.LogInformation("[Register] Attempt — Username: {Username}, Email: {Email}", req.Username, req.Email);

        if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length < 3)
        {
            _log.LogWarning("[Register] Rejected — Username too short: {Username}", req.Username);
            return (false, "Username phải có ít nhất 3 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
        {
            _log.LogWarning("[Register] Rejected — Password too short for user: {Username}", req.Username);
            return (false, "Password phải có ít nhất 6 ký tự.");
        }

        if (!req.Email.Contains('@'))
        {
            _log.LogWarning("[Register] Rejected — Invalid email: {Email}", req.Email);
            return (false, "Email không hợp lệ.");
        }

        var existingByUsername = await _db.Accounts
            .Find(a => a.Username == req.Username)
            .FirstOrDefaultAsync();
        if (existingByUsername is not null)
        {
            _log.LogWarning("[Register] Rejected — Username already taken: {Username}", req.Username);
            return (false, "Username đã tồn tại.");
        }

        var existingByEmail = await _db.Accounts
            .Find(a => a.Email == req.Email)
            .FirstOrDefaultAsync();
        if (existingByEmail is not null)
        {
            _log.LogWarning("[Register] Rejected — Email already in use: {Email}", req.Email);
            return (false, "Email đã được sử dụng.");
        }

        var account = new Account
        {
            Username     = req.Username,
            Email        = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            CreatedAt    = DateTime.UtcNow
        };

        await _db.Accounts.InsertOneAsync(account);

        var player = new Player
        {
            AccountId = account.Id,
            Name = account.Username,
            VP = 0,
            MMR = 1000,
            RankedWins = 0,
            RankedMatches = 0
        };
        await _db.Players.InsertOneAsync(player);

        // Nạp 6 Pokemon mặc định cho Player
        await _gameService.SeedInitialPokemonAsync(player.Id);

        _log.LogInformation("[Register] Success — AccountId: {Id}, Username: {Username}, PlayerId: {PlayerId}", account.Id, account.Username, player.Id);
        return (true, null);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    public async Task<(AuthResponse? Response, string? Error)> LoginAsync(LoginRequest req)
    {
        _log.LogInformation("[Login] Attempt — Username: {Username}", req.Username);

        var account = await _db.Accounts
            .Find(a => a.Username == req.Username)
            .FirstOrDefaultAsync();

        if (account is null || !BCrypt.Net.BCrypt.Verify(req.Password, account.PasswordHash))
        {
            _log.LogWarning("[Login] Failed — Invalid credentials for Username: {Username}", req.Username);
            return (null, "Username hoặc password không đúng.");
        }

        var player = await _db.Players
            .Find(p => p.AccountId == account.Id)
            .FirstOrDefaultAsync();

        if (player is null)
        {
            _log.LogWarning("[Login] Failed — Player profile missing for AccountId: {Id}", account.Id);
            return (null, "Lỗi kết cấu: Tài khoản này chưa có hồ sơ người chơi (Player).");
        }

        // Tự động nạp Pokemon cho tài khoản cũ nếu chưa có
        var hasPokemon = await _db.PokemonInstances.Find(p => p.OwnerId == player.Id).AnyAsync();
        if (!hasPokemon)
        {
            await _gameService.SeedInitialPokemonAsync(player.Id);
        }

        var token = GenerateJwt(account, player.Id);
        _log.LogInformation("[Login] Success — AccountId: {Id}, Username: {Username}", account.Id, account.Username);
        return (new AuthResponse(token, account.Username, account.Id, player.Id), null);
    }

    // ── Logout (blacklist token) ──────────────────────────────────────────────

    public async Task LogoutAsync(string rawToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt     = handler.ReadJwtToken(rawToken);
        var expiry  = jwt.ValidTo;
        var username = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.UniqueName)?.Value ?? "unknown";

        var revoked = new RevokedToken { Token = rawToken, Expiry = expiry };
        await _db.RevokedTokens.InsertOneAsync(revoked);

        // Reset DM chat history on logout
        var accountId = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        if (!string.IsNullOrEmpty(accountId))
        {
            var player = await _db.Players
                .Find(p => p.AccountId == accountId)
                .FirstOrDefaultAsync();

            if (player != null)
            {
                await _chatService.DeleteDirectMessagesAsync(player.Id);
                _log.LogInformation("[Logout] DM history cleared for player: {PlayerId}", player.Id);
            }
        }

        _log.LogInformation("[Logout] Username: {Username}, token expires: {Expiry:u}", username, expiry);
    }

    public async Task<bool> IsTokenRevokedAsync(string rawToken)
    {
        var found = await _db.RevokedTokens
            .Find(r => r.Token == rawToken)
            .FirstOrDefaultAsync();
        return found is not null;
    }

    // ── Forgot Password ───────────────────────────────────────────────────────

    /// <summary>
    /// Tạo reset token OTP 6 số lưu vào DB và gửi email cho người dùng.
    /// </summary>
    public async Task<(string? ResetToken, string? Error)> ForgotPasswordAsync(ForgotPasswordRequest req)
    {
        _log.LogInformation("[ForgotPassword] Request — Email: {Email}", req.Email);

        var account = await _db.Accounts
            .Find(a => a.Email == req.Email)
            .FirstOrDefaultAsync();

        if (account is null)
        {
            _log.LogWarning("[ForgotPassword] Not found — Email: {Email}", req.Email);
            return (null, "Không tìm thấy tài khoản với email này.");
        }

        // Sinh mã OTP 6 số ngẫu nhiên
        var resetToken = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

        var update = Builders<Account>.Update
            .Set(a => a.PasswordResetToken,  resetToken)
            .Set(a => a.PasswordResetExpiry, DateTime.UtcNow.AddHours(1));

        await _db.Accounts.UpdateOneAsync(a => a.Id == account.Id, update);

        // Gửi email chứa token cho người dùng
        await _emailService.SendResetTokenAsync(account.Email, resetToken);

        _log.LogInformation("[ForgotPassword] Reset token issued and email sent — AccountId: {Id}, Username: {Username}", account.Id, account.Username);
        return (resetToken, null);
    }

    // ── Reset Password ────────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(ResetPasswordRequest req)
    {
        var safeLogToken = req.Token.Length > 4 ? req.Token[..4] + "…" : req.Token;
        _log.LogInformation("[ResetPassword] Attempt with token: {Token}", safeLogToken);

        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
            return (false, "Password mới phải có ít nhất 6 ký tự.");

        var account = await _db.Accounts
            .Find(a => a.PasswordResetToken == req.Token)
            .FirstOrDefaultAsync();

        if (account is null)
        {
            _log.LogWarning("[ResetPassword] Invalid token");
            return (false, "Token không hợp lệ.");
        }

        if (account.PasswordResetExpiry < DateTime.UtcNow)
        {
            _log.LogWarning("[ResetPassword] Expired token — AccountId: {Id}", account.Id);
            return (false, "Token đã hết hạn.");
        }

        var update = Builders<Account>.Update
            .Set(a => a.PasswordHash,        BCrypt.Net.BCrypt.HashPassword(req.NewPassword))
            .Unset(a => a.PasswordResetToken)
            .Unset(a => a.PasswordResetExpiry);

        await _db.Accounts.UpdateOneAsync(a => a.Id == account.Id, update);
        _log.LogInformation("[ResetPassword] Success — AccountId: {Id}, Username: {Username}", account.Id, account.Username);
        return (true, null);
    }

    // ── JWT helper ────────────────────────────────────────────────────────────

    private string GenerateJwt(Account account, string playerId)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.Id),
            new Claim("player_id", playerId), // Quan trọng: Dùng cho BattleHub
            new Claim(JwtRegisteredClaimNames.UniqueName, account.Username),
            new Claim(JwtRegisteredClaimNames.Email, account.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             _jwtIssuer,
            audience:           _jwtAudience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(_jwtExpiryHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
