using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using System.Security.Claims;
using PokemonMMO.Services;

namespace PokemonMMO.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PokemonController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly PokemonDataService _pokeData;

    public PokemonController(MongoDbContext db, PokemonDataService pokeData)
    {
        _db = db;
        _pokeData = pokeData;
    }

    private string? GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public class SwapMoveRequest
    {
        public string InstanceId { get; set; } = null!;
        public int MoveSlotIndex { get; set; } // 0, 1, 2, or 3
        public int NewMoveId { get; set; }
    }

    public class UpdateSpAlignmentRequest
    {
        public string InstanceId { get; set; } = null!;
        public string StatAlignment { get; set; } = null!; // Nature
        public StatBlock Sps { get; set; } = null!; // Stat Points (max 66)
    }

    [HttpPost("update-stats")]
    public async Task<IActionResult> UpdateStats([FromBody] UpdateSpAlignmentRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // VP Validation (Player must afford the respec)
        var player = await _db.Players.Find(x => x.AccountId == userId).FirstOrDefaultAsync();
        if (player == null) return Unauthorized();

        // Validate Stat Points (Max 66 total, Max 63 per stat to map to 252 EVs)
        int spTotal = req.Sps.Hp + req.Sps.Atk + req.Sps.Def + req.Sps.SpAtk + req.Sps.SpDef + req.Sps.Spd;
        if (spTotal > 66) return BadRequest("Total Stat Points (SP) cannot exceed 66.");
        
        if (req.Sps.Hp > 63 || req.Sps.Atk > 63 || req.Sps.Def > 63 || 
            req.Sps.SpAtk > 63 || req.Sps.SpDef > 63 || req.Sps.Spd > 63)
            return BadRequest("Single SP cannot exceed 63.");

        // Define cost: 100 VP to rebuild stats/alignment
        int costVp = 100;
        if (player.VP < costVp)
            return BadRequest($"Not enough Victory Points (VP). Requires {costVp} VP.");

        // Fetch Pokemon
        var p = await _db.PokemonInstances.Find(x => x.Id == req.InstanceId && x.OwnerId == userId).FirstOrDefaultAsync();
        if (p == null) return NotFound("Pokemon not found or you do not own it.");

        // Convert SP to standard EVs underneath for accurate Champion battle arithmetic
        var actualEvs = new StatBlock
        {
            Hp = req.Sps.Hp * 4,
            Atk = req.Sps.Atk * 4,
            Def = req.Sps.Def * 4,
            SpAtk = req.Sps.SpAtk * 4,
            SpDef = req.Sps.SpDef * 4,
            Spd = req.Sps.Spd * 4
        };

        // Recalculate max HP
        var species = await _db.Pokedex.Find(x => x.Id == p.SpeciesId).FirstOrDefaultAsync();
        int baseHp = 50;
        if (species?.BaseStats.TryGetValue("hp", out var bhp) == true) baseHp = bhp;

        int newMaxHp = CalculateHp(baseHp, p.Ivs.Hp, actualEvs.Hp, p.Level);

        // Deduct VP
        await _db.Players.UpdateOneAsync(x => x.Id == player.Id, Builders<Player>.Update.Inc(x => x.VP, -costVp));

        // Update Pokemon
        var update = Builders<PokemonInstance>.Update
            .Set(x => x.Nature, req.StatAlignment)
            .Set(x => x.Evs, actualEvs)
            .Set(x => x.MaxHp, newMaxHp);

        // If current Hp is higher than new max, cap it.
        if (p.CurrentHp > newMaxHp || p.CurrentHp == p.MaxHp)
            update = update.Set(x => x.CurrentHp, newMaxHp);

        await _db.PokemonInstances.UpdateOneAsync(x => x.Id == req.InstanceId, update);

        return Ok(new { success = true, maxHp = newMaxHp, vpRemaining = player.VP - costVp });
    }

    private int CalculateHp(int baseStat, int iv, int ev, int level)
    {
        return (int)Math.Floor((2.0 * baseStat + iv + Math.Floor(ev / 4.0)) * level / 100.0) + level + 10;
    }

    [HttpPost("swap-move")]
    public async Task<IActionResult> SwapMove([FromBody] SwapMoveRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var player = await _db.Players.Find(x => x.AccountId == userId).FirstOrDefaultAsync();
        if (player == null) return Unauthorized();

        int costVp = 50; // Cost to swap a single move is 50 VP
        if (player.VP < costVp)
            return BadRequest($"Not enough Victory Points (VP). Requires {costVp} VP.");

        if (req.MoveSlotIndex < 0 || req.MoveSlotIndex > 3)
            return BadRequest("MoveSlotIndex must be between 0 and 3.");

        var p = await _db.PokemonInstances.Find(x => x.Id == req.InstanceId && x.OwnerId == userId).FirstOrDefaultAsync();
        if (p == null) return NotFound("Pokemon not found.");

        // Fetch Move details to get proper PP
        var moveData = await _db.Moves.Find(x => x.Id == req.NewMoveId).FirstOrDefaultAsync();
        if (moveData == null) return NotFound("Move not found in database.");

        var newMove = new PokemonMove
        {
            MoveId = req.NewMoveId,
            MoveName = moveData.Name,
            CurrentPp = moveData.PP // Set max PP
        };

        if (req.MoveSlotIndex < p.Moves.Count)
        {
            p.Moves[req.MoveSlotIndex] = newMove;
        }
        else
        {
            p.Moves.Add(newMove);
        }

        // Deduct VP
        await _db.Players.UpdateOneAsync(x => x.Id == player.Id, Builders<Player>.Update.Inc(x => x.VP, -costVp));

        // Save Pokemon
        await _db.PokemonInstances.ReplaceOneAsync(x => x.Id == p.Id, p);

        return Ok(new { success = true, moves = p.Moves, vpRemaining = player.VP - costVp });
    }
}
