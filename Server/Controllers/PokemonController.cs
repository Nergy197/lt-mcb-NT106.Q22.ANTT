using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using System.Security.Claims;
using PokemonMMO.Services;

namespace PokemonMMO.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PokemonController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly PokemonDataService _pokeData;
    private readonly CurrencyService _currency;

    public PokemonController(MongoDbContext db, PokemonDataService pokeData, CurrencyService currency)
    {
        _db = db;
        _pokeData = pokeData;
        _currency = currency;
    }

    private string? GetAccountId()
        => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

    public class SwapMoveRequest
    {
        public string InstanceId { get; set; } = null!;
        public int MoveSlotIndex { get; set; } // 0, 1, 2, or 3
        public int NewMoveId { get; set; }
    }

    public class PurchasePokemonRequest
    {
        public string InstanceId { get; set; } = null!;
    }

    /// <summary>Giá mua đứt một Pokémon dùng thử (VP).</summary>
    public const int PurchasePriceVp = 500;

    /// <summary>
    /// Mua đứt một Pokémon đang ở chế độ dùng thử.
    /// Server-authoritative: trừ VP nguyên tử, gỡ cờ <c>is_trial</c>/<c>trial_expiry</c>,
    /// và ghi log giao dịch vào <c>vp_transactions</c>.
    /// </summary>
    [HttpPost("purchase")]
    public async Task<IActionResult> PurchaseTrialPokemon([FromBody] PurchasePokemonRequest req)
    {
        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req?.InstanceId))
            return BadRequest(new { Message = "InstanceId là bắt buộc." });

        var player = await _currency.GetPlayerByAccountIdAsync(accountId);
        if (player == null) return Unauthorized();

        var pokemon = await _db.PokemonInstances
            .Find(x => x.Id == req.InstanceId && x.OwnerId == player.Id)
            .FirstOrDefaultAsync();
        if (pokemon == null)
            return NotFound(new { Message = "Pokemon không tồn tại hoặc bạn không sở hữu." });

        if (!pokemon.IsTrial)
            return BadRequest(new { Message = "Pokemon này không ở chế độ dùng thử." });

        if (pokemon.TrialExpiry.HasValue && pokemon.TrialExpiry.Value < DateTime.UtcNow)
            return BadRequest(new { Message = "Bản dùng thử đã hết hạn, không thể mua đứt." });

        // Server-side VP check: SpendVPAsync chỉ trừ thành công khi đủ VP (atomic).
        var vpRemaining = await _currency.SpendVPAsync(
            player.Id,
            PurchasePriceVp,
            reason: CurrencyService.ReasonPurchase,
            refId: pokemon.Id,
            metadata: new Dictionary<string, string>
            {
                ["species_id"] = pokemon.SpeciesId.ToString(),
                ["nickname"]   = pokemon.Nickname ?? string.Empty,
            });

        if (vpRemaining == null)
            return BadRequest(new
            {
                Message  = $"Không đủ VP. Cần {PurchasePriceVp} VP để mua đứt Pokémon này.",
                PriceVp  = PurchasePriceVp,
                CurrentVp = player.VP,
            });

        // Gỡ cờ dùng thử trong DB.
        var update = Builders<PokemonInstance>.Update
            .Set(x => x.IsTrial, false)
            .Unset(x => x.TrialExpiry);

        var dbResult = await _db.PokemonInstances.UpdateOneAsync(
            x => x.Id == pokemon.Id && x.OwnerId == player.Id,
            update);

        if (dbResult.MatchedCount == 0)
        {
            // Cực hiếm: Pokémon bị xoá ngay sau khi đã trừ VP — hoàn lại.
            await _currency.AddVPAsync(
                player.Id,
                PurchasePriceVp,
                reason: CurrencyService.ReasonPurchase + "_refund",
                refId: pokemon.Id);
            return Conflict(new { Message = "Pokemon đã bị xoá trong khi giao dịch. VP đã được hoàn." });
        }

        return Ok(new
        {
            success      = true,
            instanceId   = pokemon.Id,
            isTrial      = false,
            priceVp      = PurchasePriceVp,
            vpRemaining  = vpRemaining.Value,
        });
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
        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        // VP Validation (Player must afford the respec)
        var player = await _currency.GetPlayerByAccountIdAsync(accountId);
        if (player == null) return Unauthorized();

        // Validate Stat Points (Max 66 total, Max 63 per stat to map to 252 EVs)
        int spTotal = req.Sps.Hp + req.Sps.Atk + req.Sps.Def + req.Sps.SpAtk + req.Sps.SpDef + req.Sps.Spd;
        if (spTotal > 66) return BadRequest("Total Stat Points (SP) cannot exceed 66.");
        
        if (req.Sps.Hp > 63 || req.Sps.Atk > 63 || req.Sps.Def > 63 || 
            req.Sps.SpAtk > 63 || req.Sps.SpDef > 63 || req.Sps.Spd > 63)
            return BadRequest("Single SP cannot exceed 63.");

        // Define cost: 100 VP to rebuild stats/alignment
        int costVp = 100;

        // Fetch Pokemon
        var p = await _db.PokemonInstances.Find(x => x.Id == req.InstanceId && x.OwnerId == player.Id).FirstOrDefaultAsync();
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

        var vpRemaining = await _currency.SpendVPAsync(
            player.Id,
            costVp,
            reason: CurrencyService.ReasonStatRespec,
            refId: req.InstanceId);
        if (vpRemaining == null)
            return BadRequest($"Not enough Victory Points (VP). Requires {costVp} VP.");

        // Update Pokemon
        var update = Builders<PokemonInstance>.Update
            .Set(x => x.Nature, req.StatAlignment)
            .Set(x => x.Evs, actualEvs)
            .Set(x => x.MaxHp, newMaxHp);

        // If current Hp is higher than new max, cap it.
        if (p.CurrentHp > newMaxHp || p.CurrentHp == p.MaxHp)
            update = update.Set(x => x.CurrentHp, newMaxHp);

        await _db.PokemonInstances.UpdateOneAsync(x => x.Id == req.InstanceId, update);

        return Ok(new { success = true, maxHp = newMaxHp, vpRemaining });
    }

    private int CalculateHp(int baseStat, int iv, int ev, int level)
    {
        return (int)Math.Floor((2.0 * baseStat + iv + Math.Floor(ev / 4.0)) * level / 100.0) + level + 10;
    }

    [HttpPost("swap-move")]
    public async Task<IActionResult> SwapMove([FromBody] SwapMoveRequest req)
    {
        var accountId = GetAccountId();
        if (accountId == null) return Unauthorized();

        var player = await _currency.GetPlayerByAccountIdAsync(accountId);
        if (player == null) return Unauthorized();

        int costVp = 50; // Cost to swap a single move is 50 VP

        if (req.MoveSlotIndex < 0 || req.MoveSlotIndex > 3)
            return BadRequest("MoveSlotIndex must be between 0 and 3.");

        var p = await _db.PokemonInstances.Find(x => x.Id == req.InstanceId && x.OwnerId == player.Id).FirstOrDefaultAsync();
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

        var vpRemaining = await _currency.SpendVPAsync(
            player.Id,
            costVp,
            reason: CurrencyService.ReasonMoveSwap,
            refId: req.InstanceId,
            metadata: new Dictionary<string, string>
            {
                ["slot"]     = req.MoveSlotIndex.ToString(),
                ["move_id"]  = req.NewMoveId.ToString(),
            });
        if (vpRemaining == null)
            return BadRequest($"Not enough Victory Points (VP). Requires {costVp} VP.");

        // Save Pokemon
        await _db.PokemonInstances.ReplaceOneAsync(x => x.Id == p.Id, p);

        return Ok(new { success = true, moves = p.Moves, vpRemaining });
    }
}
