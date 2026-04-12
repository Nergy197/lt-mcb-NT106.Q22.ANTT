using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;
using PokemonMMO.Services;
using MongoDB.Driver;

namespace PokemonMMO.Hubs;

/// <summary>
/// Thư mục Hubs/ - BattleHub (Đấu turn-based)
/// Xử lý logic chiến đấu, tính toán lượt đánh và đồng bộ trạng thái giữa 2 người chơi.
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class BattleHub : Hub
{
    private readonly MongoDbContext _db;
    private readonly BattleService _battleService;

    // Theo dõi người chơi đang trong trận (ConnectionId -> PlayerId)
    private static readonly ConcurrentDictionary<string, string> ActiveBattlers = new();

    public BattleHub(MongoDbContext db, BattleService battleService)
    {
        _db = db;
        _battleService = battleService;
    }

    public async Task JoinBattle(string battleId)
    {
        var playerId = await GetPlayerId();
        if (playerId == null) return;

        ActiveBattlers[Context.ConnectionId] = playerId;
        await Groups.AddToGroupAsync(Context.ConnectionId, GetBattleGroupName(battleId));
        
        // Gửi trạng thái hiện tại của trận đấu cho người mới vào (reconnect)
        var battle = _battleService.GetBattle(battleId);
        if (battle != null)
        {
            await Clients.Caller.SendAsync("BattleSync", battle);
        }
    }

    public async Task ChooseMove(string battleId, int moveSlot)
    {
        if (!ActiveBattlers.TryGetValue(Context.ConnectionId, out var playerId)) return;

        try
        {
            var action = new BattleAction
            {
                PlayerId = playerId,
                Type = BattleActionType.Move,
                MoveSlot = moveSlot
            };

            await _battleService.SubmitActionAsync(battleId, action);
            await Clients.Caller.SendAsync("ActionAccepted", new { Action = "Move", Slot = moveSlot });

            await CheckAndResolveTurn(battleId);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    public async Task SwitchPokemon(string battleId, int partyIndex)
    {
        if (!ActiveBattlers.TryGetValue(Context.ConnectionId, out var playerId)) return;

        try
        {
            var action = new BattleAction
            {
                PlayerId = playerId,
                Type = BattleActionType.Switch,
                SwitchIndex = partyIndex
            };

            await _battleService.SubmitActionAsync(battleId, action);
            await Clients.Caller.SendAsync("ActionAccepted", new { Action = "Switch", Index = partyIndex });

            await CheckAndResolveTurn(battleId);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    private async Task CheckAndResolveTurn(string battleId)
    {
        var result = await _battleService.ResolveTurnIfReadyAsync(battleId);
        if (result != null)
        {
            var group = GetBattleGroupName(battleId);
            await Clients.Group(group).SendAsync("TurnResolved", result);

            if (result.State == BattleState.Ended)
            {
                await Clients.Group(group).SendAsync("BattleEnded", new BattleEndedEventDto
                {
                    BattleId = battleId,
                    WinnerPlayerId = result.WinnerPlayerId,
                    Events = result.Events
                });
            }
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ActiveBattlers.TryRemove(Context.ConnectionId, out var playerId))
        {
            // Xử lý xử thua nếu người chơi thoát giữa trận
            var forfeit = await _battleService.ForfeitPlayerAsync(playerId, "Disconnected");
            if (forfeit != null)
            {
                await Clients.Group(GetBattleGroupName(forfeit.BattleId)).SendAsync("BattleEnded", forfeit);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task<string?> GetPlayerId()
    {
        return Context.User?.FindFirst("player_id")?.Value 
            ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }

    private string GetBattleGroupName(string battleId) => $"battle:{battleId}";
}
