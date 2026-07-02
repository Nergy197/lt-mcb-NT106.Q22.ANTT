using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokemonMMO.Models.DTOs;
using PokemonMMO.Services;

namespace PokemonMMO.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecruitController : ControllerBase
{
    private readonly RecruitService _recruitService;

    public RecruitController(RecruitService recruitService)
    {
        _recruitService = recruitService;
    }

    /// <summary>GET /api/recruit/roll — Roll ngẫu nhiên 10 Pokémon.</summary>
    [HttpGet("roll")]
    [Authorize]
    public async Task<IActionResult> Roll([FromQuery] int count = 10)
    {
        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(accountId))
            return Unauthorized(new { message = "Không xác định được người dùng." });

        if (count < 1 || count > 10)
            return BadRequest(new { message = "Số lượng phải từ 1 đến 10." });

        try
        {
            var results = await _recruitService.RollAsync(accountId, count);
            return Ok(results);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/recruit/confirm — Xác nhận recruit 1 Pokemon (trial hoặc permanent).
    /// Yêu cầu JWT Bearer token.
    /// </summary>
    [HttpPost("confirm")]
    [Authorize]
    public async Task<IActionResult> Confirm([FromBody] RecruitConfirmRequest request)
    {
        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(accountId))
            return Unauthorized(new { message = "Không xác định được người dùng." });

        if (request.SpeciesId <= 0)
            return BadRequest(new { message = "SpeciesId không hợp lệ." });

        if (request.RecruitType != "trial" && request.RecruitType != "permanent")
            return BadRequest(new { message = "RecruitType phải là 'trial' hoặc 'permanent'." });

        try
        {
            var result = await _recruitService.ConfirmAsync(accountId, request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
