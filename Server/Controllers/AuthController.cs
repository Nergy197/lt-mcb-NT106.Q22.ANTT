using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokemonMMO.Models.DTOs;
using PokemonMMO.Services;

namespace PokemonMMO.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var (success, error) = await _auth.RegisterAsync(req);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Đăng ký thành công." });
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var (response, error) = await _auth.LoginAsync(req);
        if (response is null)
            return Unauthorized(new { message = error });

        return Ok(response);
    }

    // POST /api/auth/logout  (yêu cầu JWT)
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var rawToken = HttpContext.Request.Headers.Authorization
            .ToString()
            .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(rawToken))
            return BadRequest(new { message = "Không tìm thấy token." });

        await _auth.LogoutAsync(rawToken);
        return Ok(new { message = "Đăng xuất thành công." });
    }

    // POST /api/auth/forgot-password
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        var (resetToken, error) = await _auth.ForgotPasswordAsync(req);
        if (resetToken is null)
            return NotFound(new { message = error });

        // Trong production: gửi email. Ở đây trả token để test CLI.
        return Ok(new
        {
            message    = "Reset token đã được tạo. (Trong production sẽ gửi qua email)",
            resetToken
        });
    }

    // POST /api/auth/reset-password
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var (success, error) = await _auth.ResetPasswordAsync(req);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Đổi mật khẩu thành công." });
    }

    // GET /api/auth/me  — kiểm tra token còn hợp lệ không
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var username  = User.Identity?.Name;
        var accountId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;

        return Ok(new { username, accountId });
    }
}
