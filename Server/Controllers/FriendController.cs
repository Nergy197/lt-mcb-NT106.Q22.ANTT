using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;
using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Hubs;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;
using PokemonMMO.Services;

namespace PokemonMMO.Controllers;

/// <summary>
/// REST API for managing friendships (add, accept, reject, unfriend, list, search).
/// All endpoints require JWT authentication.
/// </summary>
[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendController : ControllerBase
{
    private readonly FriendService _friendService;
    private readonly MongoDbContext _db;
    private readonly IHubContext<ChatHub> _chatHub;

    public FriendController(FriendService friendService, MongoDbContext db, IHubContext<ChatHub> chatHub)
    {
        _friendService = friendService;
        _db = db;
        _chatHub = chatHub;
    }

    // ── GET /api/friends — list accepted friends ─────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetFriends()
    {
        var playerId = await ResolvePlayerId();
        if (playerId == null)
            return Unauthorized(new { message = "Không xác định được người chơi." });

        var friends = await _friendService.GetFriendsAsync(playerId, ChatHub.IsPlayerOnline);
        return Ok(friends);
    }

    // ── GET /api/friends/requests — list pending incoming requests ───────
    [HttpGet("requests")]
    public async Task<IActionResult> GetPendingRequests()
    {
        var playerId = await ResolvePlayerId();
        if (playerId == null)
            return Unauthorized(new { message = "Không xác định được người chơi." });

        var requests = await _friendService.GetPendingRequestsAsync(playerId);
        return Ok(requests);
    }

    // ── GET /api/friends/search?q=abc — search players by name ───────────
    [HttpGet("search")]
    public async Task<IActionResult> SearchPlayers([FromQuery] string q)
    {
        var playerId = await ResolvePlayerId();
        if (playerId == null)
            return Unauthorized(new { message = "Không xác định được người chơi." });

        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest(new { message = "Từ khoá tìm kiếm phải có ít nhất 2 ký tự." });

        var results = await _friendService.SearchPlayersAsync(playerId, q, ChatHub.IsPlayerOnline);
        return Ok(results);
    }

    // ── POST /api/friends/request — send a friend request by name ────────
    [HttpPost("request")]
    public async Task<IActionResult> SendRequest([FromBody] FriendRequestDto dto)
    {
        var playerId = await ResolvePlayerId();
        if (playerId == null)
            return Unauthorized(new { message = "Không xác định được người chơi." });

        var (success, error, receiverPlayerId) = await _friendService.SendRequestAsync(playerId, dto.PlayerName);
        if (!success)
            return BadRequest(new { message = error });

        // ── Real-time notification: push to receiver via ChatHub ─────────
        if (receiverPlayerId != null)
        {
            var receiverConnId = ChatHub.GetConnectionId(receiverPlayerId);
            if (receiverConnId != null)
            {
                // Resolve sender name for the notification
                var senderPlayer = await _db.Players
                    .Find(p => p.Id == playerId)
                    .FirstOrDefaultAsync();

                await _chatHub.Clients.Client(receiverConnId).SendAsync("FriendRequestReceived", new
                {
                    RequesterId   = playerId,
                    RequesterName = senderPlayer?.Name ?? "Unknown"
                });
            }
        }

        return Ok(new { message = $"Đã gửi lời mời kết bạn đến \"{dto.PlayerName}\"." });
    }

    // ── POST /api/friends/respond — accept or reject a request ───────────
    [HttpPost("respond")]
    public async Task<IActionResult> Respond([FromBody] FriendRespondDto dto)
    {
        var playerId = await ResolvePlayerId();
        if (playerId == null)
            return Unauthorized(new { message = "Không xác định được người chơi." });

        var (success, error) = await _friendService.RespondAsync(playerId, dto.FriendshipId, dto.Accept);
        if (!success)
            return BadRequest(new { message = error });

        var action = dto.Accept ? "chấp nhận" : "từ chối";
        return Ok(new { message = $"Đã {action} lời mời kết bạn." });
    }

    // ── DELETE /api/friends/{friendPlayerId} — unfriend ───────────────────
    [HttpDelete("{friendPlayerId}")]
    public async Task<IActionResult> Unfriend(string friendPlayerId)
    {
        var playerId = await ResolvePlayerId();
        if (playerId == null)
            return Unauthorized(new { message = "Không xác định được người chơi." });

        var (success, error) = await _friendService.UnfriendAsync(playerId, friendPlayerId);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Đã hủy kết bạn." });
    }

    // ── Helper: resolve PlayerId from JWT ────────────────────────────────
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
