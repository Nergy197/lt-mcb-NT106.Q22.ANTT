using System.Linq;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;
using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Models.DTOs;
using PokemonMMO.Services;

namespace PokemonMMO.Hubs;

/// <summary>
/// SignalR hub for VGC double battle.
/// Client group name = battleId.
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class BattleHub : Hub
{
    private readonly MongoDbContext _db;
    private readonly BattleService _battleService;
    private readonly CurrencyService _currency;
    private readonly RankService _rankService;

    // connectionId → playerId (shared across hub instances)
    public static readonly ConcurrentDictionary<string, string> ConnectedPlayers =
        MatchmakingHub.ConnectedPlayers;

    // playerId → connectionId for battle routing (public so TurnTimeoutService can use it)
    public static readonly ConcurrentDictionary<string, string> PlayerConnections = new();

    // battleId → (player1ConnId, player2ConnId)
    private static readonly ConcurrentDictionary<string, (string conn1, string conn2)> BattleConnections = new();

    public BattleHub(
        MongoDbContext db,
        BattleService battleService,
        CurrencyService currency,
        RankService rankService)
    {
        _db = db;
        _battleService = battleService;
        _currency = currency;
        _rankService = rankService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Client → Server
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Player joins the battle room. Receives current battle state.
    /// Called after the client receives MatchFound from MatchmakingHub.
    /// </summary>
    public async Task JoinBattle(string battleId)
    {
        var playerId = await ResolvePlayerId();
        await Clients.Caller.SendAsync("Debug", $"[Server] JoinBattle: {battleId}, Player: {playerId}");

        if (playerId == null)
        {
            await Clients.Caller.SendAsync("Error", "Not authenticated.");
            return;
        }

        if (string.IsNullOrEmpty(battleId))
        {
            await Clients.Caller.SendAsync("Error", "BattleId is null or empty.");
            return;
        }

        var session = _battleService.GetSession(battleId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", $"Battle '{battleId}' not found.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, battleId);
        PlayerConnections[playerId] = Context.ConnectionId;

        if (session.State == BattleState.TeamPreview)
        {
            var myTeam = playerId == session.Player1Id ? session.Team1 : session.Team2;
            var oppTeam = playerId == session.Player1Id ? session.Team2 : session.Team1;

            await Clients.Caller.SendAsync("TeamPreviewReady", new TeamPreviewDto
            {
                BattleId = session.BattleId,
                YourPlayerId = playerId,
                OpponentPlayerId = playerId == session.Player1Id ? session.Player2Id : session.Player1Id,
                YourTeam = myTeam.Select(p => MapToPreview(p)).ToList(),
                OpponentTeam = oppTeam.Select(p => MapToPreview(p)).ToList()
            });
        }
        else
        {
            var fieldDto = BuildBattleRunningDto(session, playerId);
            await Clients.Caller.SendAsync("BattleRunning", fieldDto);
        }
    }

    private TeamPreviewPokemonDto MapToPreview(BattlePokemonSnapshot p)
    {
        return new TeamPreviewPokemonDto
        {
            SpeciesId = p.SpeciesId,
            SpeciesName = p.SpeciesName,
            Nickname = p.Nickname,
            Type1 = p.Type1,
            Type2 = p.Type2,
            Level = p.Level,
            MaxHp = p.MaxHp
        };
    }

    /// <summary>
    /// Team Preview: player picks 4 Pokemon in desired order.
    /// orderedIndices = indices into their 6-member party (e.g. [2, 0, 4, 1]).
    /// </summary>
    public async Task SubmitTeamOrder(string battleId, List<int> orderedIndices)
    {
        var playerId = await ResolvePlayerId();
        await Clients.Caller.SendAsync("Debug", $"[Server] SubmitTeamOrder: {battleId}, Player: {playerId}, Count: {orderedIndices.Count}");
        if (playerId == null) { await Error("Not authenticated."); return; }

        try
        {
            var (session, started) = _battleService.SubmitTeamOrder(battleId, playerId, orderedIndices);

            await Clients.Caller.SendAsync("TeamOrderAccepted", new { BattleId = battleId });

            if (started)
            {
                await Clients.Group(battleId).SendAsync("Debug", "[Server] Both teams confirmed! Starting battle...");
                // Both players have picked — broadcast battle start to both
                var p1Dto = BuildBattleRunningDto(session, session.Player1Id);
                var p2Dto = BuildBattleRunningDto(session, session.Player2Id);

                await SendToPlayer(session.Player1Id, "BattleRunning", p1Dto);
                await SendToPlayer(session.Player2Id, "BattleRunning", p2Dto);
            }
        }
        catch (Exception ex)
        {
            await Error(ex.Message);
        }
    }

    public async Task Surrender(string battleId)
    {
        var playerId = await ResolvePlayerId();
        if (playerId == null) { await Error("Not authenticated."); return; }

        try
        {
            var (session, result) = _battleService.Surrender(battleId, playerId);
            await AwardBattleRewards(session, result);
            await Clients.Group(battleId).SendAsync("TurnResolved", result);
            await SendBattleEnded(session, result.WinnerPlayerId, result.TypedEvents, result.Events);
        }
        catch (Exception ex)
        {
            await Error($"Surrender failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Submit an action for one active slot.
    /// sourceSlot: 0 = Slot A, 1 = Slot B.
    /// actionType: "Move" or "Switch".
    /// targetSlot: 0=opp A, 1=opp B, 2=own A, 3=own B.
    /// useTera: Gen 9 — true to Terastallize this turn (once per battle).
    /// </summary>
    public async Task SubmitBattleAction(
        string battleId,
        int sourceSlot,
        string actionType,
        int? moveSlot,
        int? switchIndex,
        int targetSlot,
        bool useTera = false)
    {
        var playerId = await ResolvePlayerId();
        if (playerId == null) { await Error("Not authenticated."); return; }

        var session = _battleService.GetSession(battleId);
        if (session == null) return;

        var action = new BattleAction
        {
            PlayerId    = playerId,
            Type        = actionType.Equals("Switch", StringComparison.OrdinalIgnoreCase)
                          ? BattleActionType.Switch
                          : BattleActionType.Move,
            SourceIndex = sourceSlot,
            TargetSlot  = targetSlot,
            MoveSlot    = moveSlot,
            SwitchIndex = switchIndex,
            UseTera     = useTera,
        };

        try
        {
            var (updatedSession, result) = await _battleService.SubmitBattleAction(battleId, playerId, action);
            
            await Clients.Caller.SendAsync("ActionAccepted", sourceSlot);

            if (result != null)
            {
                await BroadcastTurnResult(updatedSession, result);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BattleHub] SubmitBattleAction Error: {ex}");
            await Clients.Caller.SendAsync("Error", $"Battle Logic Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Client calls this after finishing turn animations to receive the fresh
    /// field snapshot needed to start the next turn's action selection.
    /// Server responds with "BattleRunning" (same payload as turn start).
    /// </summary>
    public async Task RequestCurrentState(string battleId)
    {
        var playerId = await ResolvePlayerId();
        if (playerId == null) return;

        if (string.IsNullOrEmpty(battleId))
        {
            await Clients.Caller.SendAsync("Error", "BattleId is null or empty.");
            return;
        }

        var session = _battleService.GetSession(battleId);
        if (session == null) return;

        if (playerId != session.Player1Id && playerId != session.Player2Id) return;

        await SendBattleStateToPlayer(session, playerId);
    }

    /// <summary>
    /// After a Pokemon faints, player sends in a replacement.
    /// slot: 0 = Slot A, 1 = Slot B.
    /// partyIndex: index in the player's 4-member battle team.
    /// </summary>
    public async Task SubmitForcedSwitch(string battleId, int slot, int partyIndex)
    {
        var playerId = await ResolvePlayerId();
        if (playerId == null) { await Error("Not authenticated."); return; }

        try
        {
            var (session, allResolved) = _battleService.SubmitForcedSwitch(
                battleId, playerId, slot, partyIndex);

            // Tell the caller their switch was accepted
            await Clients.Caller.SendAsync("ForcedSwitchAccepted", new ForcedSwitchAcceptedDto
            {
                BattleId = battleId,
                PlayerId = playerId,
                Slot = slot,
                NewPartyIndex = partyIndex
            });

            if (allResolved)
            {
                // All slots filled — broadcast next turn start to both players
                var p1Dto = BuildBattleRunningDto(session, session.Player1Id);
                var p2Dto = BuildBattleRunningDto(session, session.Player2Id);
                await SendToPlayer(session.Player1Id, "TurnReady", p1Dto);
                await SendToPlayer(session.Player2Id, "TurnReady", p2Dto);
            }
        }
        catch (Exception ex)
        {
            await Error(ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Internal broadcast helpers
    // ═══════════════════════════════════════════════════════════════════════

    private async Task BroadcastTurnResult(BattleSession session, BattleTurnResult result)
    {
        // Player 2 nhận bản HP hoán đổi để "Hp1" = phe mình, "Hp2" = đối thủ
        await SendToPlayer(session.Player1Id, "TurnResolved", result);
        await SendToPlayer(session.Player2Id, "TurnResolved", result.ForPlayer2Perspective());

        // Notify players who need to send in a replacement
        foreach (var fs in result.ForcedSwitches)
        {
            await SendToPlayer(fs.PlayerId, "ForcedSwitchRequired", new ForcedSwitchRequiredDto
            {
                BattleId         = session.BattleId,
                PlayerId         = fs.PlayerId,
                Slot             = fs.Slot,
                AvailableIndices = _battleService.GetAvailableReplacements(session, fs.PlayerId, fs.Slot),
            });
        }

        if (result.State == BattleState.Ended)
        {
            await AwardBattleRewards(session, result);
            await SendBattleEnded(session, result.WinnerPlayerId, result.TypedEvents, result.Events);
        }
    }

    private async Task AwardBattleRewards(BattleSession session, BattleTurnResult result)
    {
        if (string.IsNullOrEmpty(result.WinnerPlayerId))
            return;

        if (session.RewardsAwarded)
            return;

        session.RewardsAwarded = true;

        var reward = await _currency.AwardBattleVPAsync(
            session.Player1Id,
            session.Player2Id,
            result.WinnerPlayerId,
            BattleService.BotPlayerId,
            session.BattleId);

        if (reward.WinnerPlayerId != null && reward.WinnerVP.HasValue)
        {
            await SendToPlayer(reward.WinnerPlayerId, "VPChanged", new VPChangedDto
            {
                Vp = reward.WinnerVP.Value,
                Delta = reward.WinnerDelta,
                Reason = "battle_win"
            });
        }

        if (reward.LoserPlayerId != null && reward.LoserVP.HasValue)
        {
            await SendToPlayer(reward.LoserPlayerId, "VPChanged", new VPChangedDto
            {
                Vp = reward.LoserVP.Value,
                Delta = reward.LoserDelta,
                Reason = "battle_lose"
            });
        }

        var rankReward = await _rankService.ApplyRankedBattleResultAsync(session, result.WinnerPlayerId);
        if (rankReward.WinnerPlayerId != null && rankReward.WinnerRankPoints.HasValue)
        {
            await SendToPlayer(rankReward.WinnerPlayerId, "RankChanged", new RankChangedDto
            {
                RankPoints = rankReward.WinnerRankPoints.Value,
                Delta = rankReward.WinnerDelta,
                Reason = "ranked_win"
            });
        }

        if (rankReward.LoserPlayerId != null && rankReward.LoserRankPoints.HasValue)
        {
            await SendToPlayer(rankReward.LoserPlayerId, "RankChanged", new RankChangedDto
            {
                RankPoints = rankReward.LoserRankPoints.Value,
                Delta = rankReward.LoserDelta,
                Reason = "ranked_lose"
            });
        }

        // Casual: đếm số trận riêng (casual_matches/casual_wins), không đụng rank_points.
        await _rankService.ApplyCasualBattleResultAsync(session, result.WinnerPlayerId);
    }

    private async Task SendBattleStateToPlayer(BattleSession session, string playerId)
    {
        if (session.State == BattleState.TeamPreview)
        {
            var dto = BuildTeamPreviewDto(session, playerId);
            await Clients.Caller.SendAsync("TeamPreviewReady", dto);
        }
        else if (session.State == BattleState.Running || session.State == BattleState.ForcedSwitch)
        {
            var dto = BuildBattleRunningDto(session, playerId);
            await Clients.Caller.SendAsync("BattleRunning", dto);

            // If rejoining during forced-switch phase, remind player if they need to switch
            foreach (var slot in new[] { 0, 1 })
            {
                string key = $"{playerId}:{slot}";
                if (session.PendingForcedSwitches.Contains(key))
                {
                    await Clients.Caller.SendAsync("ForcedSwitchRequired", new ForcedSwitchRequiredDto
                    {
                        BattleId         = session.BattleId,
                        PlayerId         = playerId,
                        Slot             = slot,
                        AvailableIndices = _battleService.GetAvailableReplacements(session, playerId, slot),
                    });
                }
            }
        }
        else if (session.State == BattleState.Ended)
        {
            bool isDraw = string.IsNullOrEmpty(session.WinnerPlayerId)
                          || session.WinnerPlayerId!.Equals("draw", System.StringComparison.OrdinalIgnoreCase);
            await Clients.Caller.SendAsync("BattleEnded", new BattleEndedEventDto
            {
                BattleId       = session.BattleId,
                WinnerPlayerId = session.WinnerPlayerId,
                IsDraw         = isDraw,
                YouWon         = !isDraw && session.WinnerPlayerId!.Equals(playerId, System.StringComparison.OrdinalIgnoreCase),
                Events         = new(),
                TypedEvents    = new(),
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DTO builders
    // ═══════════════════════════════════════════════════════════════════════

    private static TeamPreviewDto BuildTeamPreviewDto(BattleSession session, string playerId)
    {
        bool isP1 = playerId == session.Player1Id;
        var myTeam  = isP1 ? session.Team1 : session.Team2;
        var oppTeam = isP1 ? session.Team2 : session.Team1;

        return new TeamPreviewDto
        {
            BattleId          = session.BattleId,
            YourPlayerId      = playerId,
            OpponentPlayerId  = isP1 ? session.Player2Id : session.Player1Id,
            YourTeam          = myTeam.Select(ToTeamPreviewPokemon).ToList(),
            OpponentTeam      = oppTeam.Select(ToTeamPreviewPokemon).ToList(),
        };
    }

    private static TeamPreviewPokemonDto ToTeamPreviewPokemon(BattlePokemonSnapshot p)
        => new()
        {
            SpeciesId   = p.SpeciesId,
            SpeciesName = p.SpeciesName,
            Nickname    = p.Nickname,
            Type1       = p.Type1,
            Type2       = p.Type2,
            Level       = p.Level,
            MaxHp       = p.MaxHp,
        };

    private BattleRunningDto BuildBattleRunningDto(BattleSession session, string playerId)
    {
        bool isP1  = playerId == session.Player1Id;
        var myTeam = isP1 ? session.Team1 : session.Team2;

        var mySlotA   = _battleService.GetActiveSlot(session, playerId, 0);
        var mySlotB   = _battleService.GetActiveSlot(session, playerId, 1);
        var oppId     = isP1 ? session.Player2Id : session.Player1Id;
        var oppSlotA  = _battleService.GetActiveSlot(session, oppId, 0);
        var oppSlotB  = _battleService.GetActiveSlot(session, oppId, 1);

        return new BattleRunningDto
        {
            BattleId        = session.BattleId,
            OpponentId      = oppId,
            TurnNumber      = session.TurnNumber,
            TurnDeadlineUtc = session.TurnDeadlineUtc,
            Weather          = session.Weather,
            WeatherTurnsLeft = session.WeatherTurnsLeft,
            Terrain          = session.Terrain,
            TerrainTurnsLeft = session.TerrainTurnsLeft,
            YourSlotA       = mySlotA  != null ? ToFieldPokemon(mySlotA,  showMoves: true)  : null,
            YourSlotB       = mySlotB  != null ? ToFieldPokemon(mySlotB,  showMoves: true)  : null,
            OppSlotA        = oppSlotA != null ? ToFieldPokemon(oppSlotA, showMoves: false) : null,
            OppSlotB        = oppSlotB != null ? ToFieldPokemon(oppSlotB, showMoves: false) : null,
            YourTeamHp      = myTeam.Select(p => p.CurrentHp).ToList(),
            YourTeamMaxHp   = myTeam.Select(p => p.MaxHp).ToList(),
        };
    }

    private FieldPokemonDto ToFieldPokemon(BattlePokemonSnapshot p, bool showMoves)
        => new()
        {
            SpeciesId       = p.SpeciesId,
            SpeciesName     = p.SpeciesName,
            Nickname        = p.Nickname,
            Type1           = p.Type1,
            Type2           = p.Type2,
            OrigType1       = p.OrigType1,
            OrigType2       = p.OrigType2,
            TerType         = p.TerType,
            IsTerastallized = p.IsTerastallized,
            Level           = p.Level,
            CurrentHp       = p.CurrentHp,
            MaxHp           = p.MaxHp,
            Status          = p.NonVolatileStatus == PokemonStatusCondition.None ? null : p.NonVolatileStatus.ToString().ToLower(),
            StatStages      = p.StatStages,
            IsFainted       = p.IsFainted,
            Moves           = showMoves
                ? p.Moves.Select(m => {
                    var moveData = _battleService.GetMoveData(m.MoveId);
                    return new MoveSummaryDto
                    {
                        MoveId    = m.MoveId,
                        Name      = m.MoveName,
                        Type      = m.MoveType,
                        Category  = m.Category,
                        CurrentPp = m.CurrentPp,
                        MaxPp     = m.MaxPp,
                        TargetType= moveData != null ? (int)moveData.TargetType : 0,
                        Effect    = moveData?.Effect,
                        StatChanges = moveData?.StatChanges?.Select(sc => new MoveStatChangeDto { Stat = sc.Stat, Stages = sc.Stages }).ToList() ?? new List<MoveStatChangeDto>()
                    };
                  }).ToList()
                : [],
        };

    // ── Send to specific player ───────────────────────────────────────────────

    private async Task SendToPlayer(string playerId, string method, object payload)
    {
        if (PlayerConnections.TryGetValue(playerId, out var connId))
            await Clients.Client(connId).SendAsync(method, payload);
    }

    /// <summary>
    /// Gửi "BattleEnded" riêng cho từng người chơi với cờ thắng/thua do server tính sẵn,
    /// để client không phải tự so WinnerPlayerId với player_id (dễ sai khi test chung máy).
    /// </summary>
    private async Task SendBattleEnded(BattleSession session, string? winnerId,
        List<BattleEvent>? typedEvents, List<string>? events)
    {
        bool isDraw = string.IsNullOrEmpty(winnerId)
                      || winnerId!.Equals("draw", System.StringComparison.OrdinalIgnoreCase);
        foreach (var pid in new[] { session.Player1Id, session.Player2Id })
        {
            await SendToPlayer(pid, "BattleEnded", new BattleEndedEventDto
            {
                BattleId       = session.BattleId,
                WinnerPlayerId = winnerId,
                IsDraw         = isDraw,
                YouWon         = !isDraw && winnerId!.Equals(pid, System.StringComparison.OrdinalIgnoreCase),
                TypedEvents    = typedEvents ?? new(),
                Events         = events ?? new(),
            });
        }
    }

    private async Task Error(string message)
        => await Clients.Caller.SendAsync("Error", message);

    // ── Auth helper ──────────────────────────────────────────────────────────

    private async Task<string?> ResolvePlayerId()
    {
        if (ConnectedPlayers.TryGetValue(Context.ConnectionId, out var id)) return id;

        // Fall back to JWT sub claim
        var accountId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? Context.User?.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(accountId)) return null;

        var player = await _db.Players
            .Find(Builders<Player>.Filter.Eq(p => p.AccountId, accountId))
            .FirstOrDefaultAsync();

        if (player == null)
        {
            // Auto-create player profile if missing (dev fallback)
            var username = Context.User?.FindFirst("username")?.Value ?? "Player_" + accountId[..5];
            player = new Player
            {
                AccountId = accountId,
                Name = username,
                MMR = 1000,
                RankPoints = 0,
                VP = 0
            };
            await _db.Players.InsertOneAsync(player);
        }
        
        ConnectedPlayers[Context.ConnectionId] = player.Id;
        return player.Id;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? playerId = PlayerConnections
            .FirstOrDefault(kv => kv.Value == Context.ConnectionId)
            .Key;

        if (playerId == null)
            ConnectedPlayers.TryGetValue(Context.ConnectionId, out playerId);

        if (!string.IsNullOrEmpty(playerId))
        {
            var session = _battleService.GetActiveSessionForPlayer(playerId);
            if (session != null)
            {
                var (_, result) = _battleService.Surrender(session.BattleId, playerId);
                await AwardBattleRewards(session, result);
                await Clients.Group(session.BattleId).SendAsync("TurnResolved", result);
                await SendBattleEnded(session, result.WinnerPlayerId, result.TypedEvents, result.Events);
            }
        }

        ConnectedPlayers.TryRemove(Context.ConnectionId, out _);
        if (!string.IsNullOrEmpty(playerId))
            PlayerConnections.TryRemove(playerId, out _);

        await base.OnDisconnectedAsync(exception);
    }
}
