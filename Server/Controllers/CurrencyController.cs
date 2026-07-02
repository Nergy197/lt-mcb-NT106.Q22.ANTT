using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokemonMMO.Services;

namespace PokemonMMO.Controllers;

[ApiController]
[Authorize]
[Route("api/currency")]
public class CurrencyController : ControllerBase
{
    private readonly CurrencyService _currency;
    private readonly IWebHostEnvironment _env;

    public CurrencyController(CurrencyService currency, IWebHostEnvironment env)
    {
        _currency = currency;
        _env = env;
    }

    [HttpGet("vp")]
    public async Task<IActionResult> GetVP()
    {
        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        var player = await _currency.GetPlayerByAccountIdAsync(accountId);
        if (player == null)
            return NotFound(new { Message = "Player not found." });

        return Ok(new CurrencyBalanceResponse { Vp = player.VP, WelcomeClaimed = player.WelcomeClaimed });
    }

    [HttpPost("claim-welcome")]
    public async Task<IActionResult> ClaimWelcome()
    {
        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        var newVp = await _currency.ClaimWelcomeBonusAsync(accountId);
        if (newVp == null)
            return BadRequest(new { Message = "Already claimed or player not found." });

        return Ok(new CurrencyBalanceResponse { Vp = newVp.Value, WelcomeClaimed = true });
    }

    [HttpPost("vp/debug/add")]
    public async Task<IActionResult> DebugAddVP([FromBody] DebugVPRequest request)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        var player = await _currency.GetPlayerByAccountIdAsync(accountId);
        if (player == null)
            return NotFound(new { Message = "Player not found." });

        var vp = await _currency.AddVPAsync(player.Id, request.Amount, reason: CurrencyService.ReasonDebugAdd);
        if (vp == null)
            return BadRequest(new { Message = "Amount must be greater than 0." });

        return Ok(new CurrencyBalanceResponse { Vp = vp.Value });
    }

    [HttpPost("vp/debug/spend")]
    public async Task<IActionResult> DebugSpendVP([FromBody] DebugVPRequest request)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        var player = await _currency.GetPlayerByAccountIdAsync(accountId);
        if (player == null)
            return NotFound(new { Message = "Player not found." });

        var vp = await _currency.SpendVPAsync(player.Id, request.Amount, reason: CurrencyService.ReasonDebugSpend);
        if (vp == null)
            return BadRequest(new { Message = "Not enough VP or invalid amount." });

        return Ok(new CurrencyBalanceResponse { Vp = vp.Value });
    }

    private string? GetAccountId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
    }
}

public class CurrencyBalanceResponse
{
    public int Vp { get; set; }
    public bool WelcomeClaimed { get; set; }
}

public class DebugVPRequest
{
    public int Amount { get; set; }
}
