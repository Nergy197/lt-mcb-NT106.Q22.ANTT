using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Services;

namespace PokemonMMO.Controllers;

[ApiController]
[Route("api/rank")]
public class RankController : ControllerBase
{
    private readonly RankService _rankService;
    private readonly MongoDbContext _db;

    public RankController(RankService rankService, MongoDbContext db)
    {
        _rankService = rankService;
        _db = db;
    }

    [HttpGet("top100")]
    public async Task<IActionResult> GetTop100()
    {
        var entries = await _rankService.GetTop100Async();
        return Ok(entries);
    }

    [Authorize]
    [HttpGet("friends")]
    public async Task<IActionResult> GetFriendsRank()
    {
        var playerId = await ResolvePlayerId();
        if (playerId == null)
            return Unauthorized(new { message = "Không xác định được người chơi." });

        var entries = await _rankService.GetFriendsRankAsync(playerId);
        return Ok(entries);
    }

    private async Task<string?> ResolvePlayerId()
    {
        var accountId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                     ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(accountId)) return null;

        var player = await _db.Players
            .Find(Builders<Player>.Filter.Eq(p => p.AccountId, accountId))
            .FirstOrDefaultAsync();

        return player?.Id;
    }
}
