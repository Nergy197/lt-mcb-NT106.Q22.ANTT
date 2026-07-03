using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PokemonMMO.Hubs;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;
using PokemonMMO.Options;

namespace PokemonMMO.Services;

/// <summary>
/// Xử lý mất kết nối giữa trận: khi một người chơi rớt mạng, bắt đầu đếm ngược ân hạn và
/// báo cho đối thủ. Nếu người đó vào lại kịp (JoinBattle) thì huỷ đếm ngược; nếu không, xử
/// thua người mất kết nối và kết thúc trận.
///
/// Singleton (chạy độc lập với vòng đời Hub transient) — gửi thông điệp qua IHubContext,
/// dùng scope để lấy các service scoped (CurrencyService, RankService) khi trao thưởng.
/// </summary>
public class DisconnectForfeitService
{
    private readonly BattleService          _battleService;
    private readonly IHubContext<BattleHub> _hub;
    private readonly IServiceScopeFactory   _scopeFactory;
    private readonly int                    _graceSeconds;

    // "battleId:playerId" → nguồn huỷ đếm ngược (huỷ khi người chơi vào lại).
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new();

    public DisconnectForfeitService(
        BattleService battleService,
        IHubContext<BattleHub> hub,
        IServiceScopeFactory scopeFactory,
        IOptions<BattleOptions> opts)
    {
        _battleService = battleService;
        _hub           = hub;
        _scopeFactory  = scopeFactory;
        _graceSeconds  = opts.Value.DisconnectGraceSeconds;
    }

    private static string Key(string battleId, string playerId) => $"{battleId}:{playerId}";

    /// <summary>Bắt đầu đếm ngược ân hạn cho người chơi vừa mất kết nối giữa trận.</summary>
    public void ScheduleForfeit(string battleId, string playerId)
    {
        var session = _battleService.GetSession(battleId);
        if (session == null || session.State == BattleState.Ended) return;

        var key = Key(battleId, playerId);
        var cts = new CancellationTokenSource();
        if (_pending.TryRemove(key, out var old)) old.Cancel();
        _pending[key] = cts;

        _ = RunCountdown(battleId, playerId, cts.Token);
    }

    /// <summary>Người chơi đã vào lại — huỷ đếm ngược và báo đối thủ.</summary>
    public void CancelForfeit(string battleId, string playerId)
    {
        if (_pending.TryRemove(Key(battleId, playerId), out var cts))
        {
            cts.Cancel();
            _ = NotifyOpponent(battleId, playerId, "OpponentReconnected",
                new { BattleId = battleId });
        }
    }

    private async Task RunCountdown(string battleId, string playerId, CancellationToken token)
    {
        try
        {
            for (int s = _graceSeconds; s > 0; s--)
            {
                await NotifyOpponent(battleId, playerId, "OpponentDisconnected",
                    new { BattleId = battleId, SecondsLeft = s });
                await Task.Delay(1000, token);
            }

            await ForfeitDisconnected(battleId, playerId);
        }
        catch (OperationCanceledException)
        {
            // Người chơi đã vào lại kịp — không làm gì.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DisconnectForfeit] {battleId}/{playerId}: {ex.Message}");
        }
        finally
        {
            _pending.TryRemove(Key(battleId, playerId), out _);
        }
    }

    private async Task ForfeitDisconnected(string battleId, string playerId)
    {
        var session = _battleService.GetSession(battleId);
        if (session == null || session.State == BattleState.Ended) return;

        // playerId là người mất kết nối → họ thua.
        var (_, result) = _battleService.Surrender(battleId, playerId);

        await AwardRewards(session, result);

        await SendToPlayer(session.Player1Id, "TurnResolved", result);
        await SendToPlayer(session.Player2Id, "TurnResolved", result.ForPlayer2Perspective());
        await SendBattleEnded(session, result);
    }

    // ── Trao thưởng (mirror BattleHub.AwardBattleRewards, dùng scope cho service scoped) ──
    private async Task AwardRewards(BattleSession session, BattleTurnResult result)
    {
        if (string.IsNullOrEmpty(result.WinnerPlayerId)) return;
        if (session.RewardsAwarded) return;
        session.RewardsAwarded = true;

        using var scope = _scopeFactory.CreateScope();
        var currency = scope.ServiceProvider.GetRequiredService<CurrencyService>();
        var rank     = scope.ServiceProvider.GetRequiredService<RankService>();

        var reward = await currency.AwardBattleVPAsync(
            session.Player1Id, session.Player2Id,
            result.WinnerPlayerId, BattleService.BotPlayerId, session.BattleId);

        if (reward.WinnerPlayerId != null && reward.WinnerVP.HasValue)
            await SendToPlayer(reward.WinnerPlayerId, "VPChanged",
                new VPChangedDto { Vp = reward.WinnerVP.Value, Delta = reward.WinnerDelta, Reason = "battle_win" });

        if (reward.LoserPlayerId != null && reward.LoserVP.HasValue)
            await SendToPlayer(reward.LoserPlayerId, "VPChanged",
                new VPChangedDto { Vp = reward.LoserVP.Value, Delta = reward.LoserDelta, Reason = "battle_lose" });

        var rankReward = await rank.ApplyRankedBattleResultAsync(session, result.WinnerPlayerId);
        if (rankReward.WinnerPlayerId != null && rankReward.WinnerRankPoints.HasValue)
            await SendToPlayer(rankReward.WinnerPlayerId, "RankChanged",
                new RankChangedDto { RankPoints = rankReward.WinnerRankPoints.Value, Delta = rankReward.WinnerDelta, Reason = "ranked_win" });

        if (rankReward.LoserPlayerId != null && rankReward.LoserRankPoints.HasValue)
            await SendToPlayer(rankReward.LoserPlayerId, "RankChanged",
                new RankChangedDto { RankPoints = rankReward.LoserRankPoints.Value, Delta = rankReward.LoserDelta, Reason = "ranked_lose" });

        await rank.ApplyCasualBattleResultAsync(session, result.WinnerPlayerId);
    }

    private async Task SendBattleEnded(BattleSession session, BattleTurnResult result)
    {
        bool isDraw = string.IsNullOrEmpty(result.WinnerPlayerId)
                      || result.WinnerPlayerId!.Equals("draw", StringComparison.OrdinalIgnoreCase);

        foreach (var pid in new[] { session.Player1Id, session.Player2Id })
        {
            await SendToPlayer(pid, "BattleEnded", new BattleEndedEventDto
            {
                BattleId       = session.BattleId,
                WinnerPlayerId = result.WinnerPlayerId,
                IsDraw         = isDraw,
                YouWon         = !isDraw && result.WinnerPlayerId!.Equals(pid, StringComparison.OrdinalIgnoreCase),
                TypedEvents    = result.TypedEvents,
                Events         = result.Events,
            });
        }
    }

    // Gửi tới đối thủ của người mất kết nối (người còn lại trong trận).
    private async Task NotifyOpponent(string battleId, string disconnectedPlayerId, string method, object payload)
    {
        var session = _battleService.GetSession(battleId);
        if (session == null) return;
        var opponentId = session.Player1Id == disconnectedPlayerId ? session.Player2Id : session.Player1Id;
        await SendToPlayer(opponentId, method, payload);
    }

    private async Task SendToPlayer(string playerId, string method, object payload)
    {
        if (BattleHub.PlayerConnections.TryGetValue(playerId, out var connId))
            await _hub.Clients.Client(connId).SendAsync(method, payload);
    }
}
