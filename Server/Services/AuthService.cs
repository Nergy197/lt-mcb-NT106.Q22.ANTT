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
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly int _jwtExpiryHours;

    public AuthService(MongoDbContext db, IConfiguration config, ILogger<AuthService> log)
    {
        _db  = db;
        _log = log;
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
        _log.LogInformation("[Register] Success — AccountId: {Id}, Username: {Username}", account.Id, account.Username);
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

        var token = GenerateJwt(account);
        _log.LogInformation("[Login] Success — AccountId: {Id}, Username: {Username}", account.Id, account.Username);
        return (new AuthResponse(token, account.Username, account.Id), null);
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
    /// Tạo reset token lưu vào DB. Trong production sẽ gửi email; ở đây trả
    /// token về để test qua CLI.
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

        var resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        var update = Builders<Account>.Update
            .Set(a => a.PasswordResetToken,  resetToken)
            .Set(a => a.PasswordResetExpiry, DateTime.UtcNow.AddHours(1));

        await _db.Accounts.UpdateOneAsync(a => a.Id == account.Id, update);

        _log.LogInformation("[ForgotPassword] Reset token issued — AccountId: {Id}, Username: {Username}", account.Id, account.Username);
        return (resetToken, null);
    }

    // ── Reset Password ────────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(ResetPasswordRequest req)
    {
        _log.LogInformation("[ResetPassword] Attempt with token: {Token}", req.Token[..8] + "…");

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

    private string GenerateJwt(Account account)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.Id),
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
