using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokemonMMO.Models.DTOs;
using PokemonMMO.Services;
using System.Security.Claims;

namespace PokemonMMO.Controllers;

[ApiController]
[Route("api/box")]
[Authorize]
public class BoxController : ControllerBase
{
    private readonly BoxService _box;

    public BoxController(BoxService box) => _box = box;

    /// <summary>GET /api/box/party — trả về 6 Pokemon đang trong đội</summary>
    [HttpGet("party")]
    public async Task<IActionResult> GetParty()
    {
        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(accountId)) return Unauthorized();

        try
        {
            var result = await _box.GetPartyAsync(accountId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>POST /api/box/withdraw — rút Pokemon từ box vào đội</summary>
    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] BoxActionRequest req)
    {
        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(accountId)) return Unauthorized();
        try   { await _box.WithdrawAsync(accountId, req.PokemonId); return Ok(new { success = true }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>POST /api/box/deposit — gửi Pokemon từ đội vào box</summary>
    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] BoxActionRequest req)
    {
        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(accountId)) return Unauthorized();
        try   { await _box.DepositAsync(accountId, req.PokemonId); return Ok(new { success = true }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>GET /api/box/{boxIndex} — trả về 30 slot của box đó</summary>
    [HttpGet("{boxIndex:int}")]
    public async Task<IActionResult> GetBox(int boxIndex)
    {
        if (boxIndex < 0) return BadRequest("boxIndex phải >= 0");

        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(accountId)) return Unauthorized();

        try
        {
            var result = await _box.GetBoxAsync(accountId, boxIndex);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }
}
