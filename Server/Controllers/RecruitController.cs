using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// GET /api/recruit/roll
    /// Roll ngẫu nhiên 10 Pokémon từ Pokédex.
    /// Không yêu cầu đăng nhập để đơn giản (scene Recruit không cần auth).
    /// </summary>
    [HttpGet("roll")]
    public async Task<IActionResult> Roll([FromQuery] int count = 10)
    {
        if (count < 1 || count > 10)
            return BadRequest(new { message = "Số lượng phải từ 1 đến 10." });

        try
        {
            var results = await _recruitService.RollAsync(count);
            return Ok(results);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
