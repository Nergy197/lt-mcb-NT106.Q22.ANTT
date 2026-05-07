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

    // connectionId → playerId (shared across hub instances)
    public static readonly ConcurrentDictionary<string, string> ConnectedPlayers =
        MatchmakingHub.ConnectedPlayers;

    // playerId → connectionId for battle routing
    private static readonly ConcurrentDictionary<string, string> PlayerConnections = new();

    // battleId → (player1ConnId, player2ConnId)
    private static readonly ConcurrentDictionary<string, (string conn1, string conn2)> BattleConnections = new();

    public BattleHub(MongoDbContext db, BattleService battleService)
    {
        _db = db;
        _battleService = battleService;
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
        var playerId = GetPlayerId();
        if (playerId == null)
        {
            await Clients.Caller.SendAsync("Error", "Not authenticated.");
            return;
        }

        var session = _battleService.GetSession(battleId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", $"Battle '{battleId}' not found.");
            return;
        }

        if (playerId != session.Player1Id && playerId != session.Player2Id)
        {
            await Clients.Caller.SendAsync("Error", "You are not a participant in this battle.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, battleId);
        PlayerConnections[playerId] = Context.ConnectionId;

        // Track connection per battle
        BattleConnections.AddOrUpdate(
            battleId,
            _ => playerId == session.Player1Id
                ? (Context.ConnectionId, "")
                : ("", Context.ConnectionId),
            (_, old) => playerId == session.Player1Id
                ? (Context.ConnectionId, old.conn2)
                : (old.conn1, Context.ConnectionId)
        );

        // Send current state to the joining player
        await SendBattleStateToPlayer(session, playerId);
    }

    /// <summary>
    /// Team Preview: player picks 4 Pokemon in desired order.
    /// orderedIndices = indices into their 6-member party (e.g. [2, 0, 4, 1]).
    /// </summary>
    public async Task SubmitTeamOrder(string battleId, List<int> orderedIndices)
    {
        var playerId = GetPlayerId();
        if (playerId == null) { await Error("Not authenticated."); return; }

        try
        {
            var (session, started) = _battleService.SubmitTeamOrder(battleId, playerId, orderedIndices);

            await Clients.Caller.SendAsync("TeamOrderAccepted", new { BattleId = battleId });

            if (started)
            {
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
        var playerId = GetPlayerId();
        if (playerId == null) { await Error("Not authenticated."); return; }

        try
        {
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

            var (session, result) = await _battleService.SubmitBattleAction(battleId, playerId, action);

            await Clients.Caller.SendAsync("ActionAccepted", new { BattleId = battleId, Slot = sourceSlot });

            if (result != null)
                await BroadcastTurnResult(session, result);
        }
        catch (Exception ex)
        {
            await Error(ex.Message);
        }
    }

    /// <summary>
    /// After a Pokemon faints, player sends in a replacement.
    /// slot: 0 = Slot A, 1 = Slot B.
    /// partyIndex: index in the player's 4-member battle team.
    /// </summary>
    public async Task SubmitForcedSwitch(string battleId, int slot, int partyIndex)
    {
        var playerId = GetPlayerId();
        if (playerId == null) { await Error("Not authenticated."); return; }

        try
        {
            var (session, allResolved) = _battleService.SubmitForcedSwitch(
                battleId, playerId, slot, partyIndex);

            // Tell the caller their switch was accepted
            await Clients.Caller.SendAsync("ForcedSwitchAccepted", new { BattleId = battleId, Slot = slot });

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
        // Build personalised views (each player sees own moves, opponent limited)
        var p1Result = result; // Same typed-event list for both
        var p2Result = result;

        await SendToPlayer(session.Player1Id, "TurnResolved", p1Result);
        await SendToPlayer(session.Player2Id, "TurnResolved", p2Result);

        // Notify players who need to send in a replacement
        foreach (var fs in result.ForcedSwitches)
        {
            await SendToPlayer(fs.PlayerId, "ForcedSwitchRequired", new
            {
                BattleId = session.BattleId,
                Slot     = fs.Slot,
                AvailableIndices = GetAvailableReplacements(session, fs.PlayerId, fs.Slot),
            });
        }

        if (result.State == BattleState.Ended)
        {
            await Clients.Group(session.BattleId).SendAsync("BattleEnded", new BattleEndedEventDto
            {
                BattleId      = session.BattleId,
                WinnerPlayerId = result.WinnerPlayerId,
                TypedEvents   = result.TypedEvents,
                Events        = result.Events,
            });
        }
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
                    await Clients.Caller.SendAsync("ForcedSwitchRequired", new
                    {
                        BattleId         = session.BattleId,
                        Slot             = slot,
                        AvailableIndices = GetAvailableReplacements(session, playerId, slot),
                    });
                }
            }
        }
        else if (session.State == BattleState.Ended)
        {
            await Clients.Caller.SendAsync("BattleEnded", new BattleEndedEventDto
            {
                BattleId      = session.BattleId,
                WinnerPlayerId = session.WinnerPlayerId,
                Events        = new(),
                TypedEvents   = new(),
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

    private static FieldPokemonDto ToFieldPokemon(BattlePokemonSnapshot p, bool showMoves)
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
            CurrentHp       = p.CurrentHp,
            MaxHp           = p.MaxHp,
            Status          = p.NonVolatileStatus,
            StatStages      = p.StatStages,
            IsFainted       = p.IsFainted,
            Moves           = showMoves
                ? p.Moves.Select(m => new MoveSummaryDto
                  {
                      MoveId    = m.MoveId,
                      Name      = m.MoveName,
                      Type      = m.MoveType,
                      Category  = m.Category,
                      CurrentPp = m.CurrentPp,
                      MaxPp     = m.MaxPp,
                  }).ToList()
                : [],
        };

    private static List<int> GetAvailableReplacements(BattleSession session, string playerId, int faintedSlot)
    {
        var team    = playerId == session.Player1Id ? session.Team1 : session.Team2;
        bool isP1   = playerId == session.Player1Id;
        int activeA = isP1 ? session.ActiveIndex1  : session.ActiveIndex2;
        int activeB = isP1 ? session.ActiveIndex1b : session.ActiveIndex2b;

        return team
            .Select((p, i) => (p, i))
            .Where(x => !x.p.IsFainted && x.i != activeA && x.i != activeB)
            .Select(x => x.i)
            .ToList();
    }

    // ── Send to specific player ───────────────────────────────────────────────

    private async Task SendToPlayer(string playerId, string method, object payload)
    {
        if (PlayerConnections.TryGetValue(playerId, out var connId))
            await Clients.Client(connId).SendAsync(method, payload);
    }

    private async Task Error(string message)
        => await Clients.Caller.SendAsync("Error", message);

    // ── Auth helper ──────────────────────────────────────────────────────────

    private string? GetPlayerId()
    {
        if (ConnectedPlayers.TryGetValue(Context.ConnectionId, out var id)) return id;

        // Fall back to JWT sub claim (player may not have joined lobby first)
        var sub = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
               ?? Context.User?.FindFirst("sub")?.Value;

        return sub;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
            PlayerConnections.TryRemove(playerId, out _);

        await base.OnDisconnectedAsync(exception);
    }
}
