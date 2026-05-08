using Microsoft.AspNetCore.SignalR;
using PokemonMMO.Hubs;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;

namespace PokemonMMO.Services;

/// <summary>
/// Background service kiểm tra mỗi 2 giây: nếu một turn hết deadline mà vẫn chưa đủ action,
/// tự động điền action mặc định (chiêu đầu tiên còn PP) và resolve turn.
/// Đảm bảo game không bị kẹt khi một player disconnect hoặc không submit kịp.
/// </summary>
public class TurnTimeoutService : BackgroundService
{
    private readonly BattleService        _battleService;
    private readonly IHubContext<BattleHub> _hubContext;

    public TurnTimeoutService(BattleService battleService, IHubContext<BattleHub> hubContext)
    {
        _battleService = battleService;
        _hubContext    = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(2000, stoppingToken);

            foreach (var session in _battleService.GetExpiredSessions().ToList())
            {
                try
                {
                    var result = await _battleService.TryAutoResolveExpiredTurn(session);
                    if (result != null)
                        await BroadcastResult(session, result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TurnTimeout] {session.BattleId}: {ex.Message}");
                }
            }
        }
    }

    private async Task BroadcastResult(BattleSession session, BattleTurnResult result)
    {
        await SendToPlayer(session.Player1Id, "TurnResolved", result);
        await SendToPlayer(session.Player2Id, "TurnResolved", result.ForPlayer2Perspective());

        foreach (var fs in result.ForcedSwitches)
        {
            await SendToPlayer(fs.PlayerId, "ForcedSwitchRequired", new
            {
                BattleId         = session.BattleId,
                Slot             = fs.Slot,
                AvailableIndices = _battleService.GetAvailableReplacements(session, fs.PlayerId, fs.Slot),
            });
        }

        if (result.State == BattleState.Ended)
        {
            await _hubContext.Clients.Group(session.BattleId).SendAsync("BattleEnded", new BattleEndedEventDto
            {
                BattleId       = session.BattleId,
                WinnerPlayerId = result.WinnerPlayerId,
                TypedEvents    = result.TypedEvents,
                Events         = result.Events,
            });
        }
    }

    private async Task SendToPlayer(string playerId, string method, object payload)
    {
        if (BattleHub.PlayerConnections.TryGetValue(playerId, out var connId))
            await _hubContext.Clients.Client(connId).SendAsync(method, payload);
    }
}
