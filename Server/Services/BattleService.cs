using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Options;

namespace PokemonMMO.Services;

/// <summary>
/// VGC (Video Game Championship) double battle engine.
/// Format: bring 4 from 6, 2 active per side, Level 50 cap.
/// Turn order: Switches (priority 6) → Moves (by priority desc, then speed desc, random tiebreak).
/// </summary>
public class BattleService
{
    private readonly MongoDbContext _db;
    private readonly BattleOptions _opts;
    private static readonly Random _rng = new();

    // In-memory session store (BattleId → BattleSession)
    private static readonly ConcurrentDictionary<string, BattleSession> _sessions = new();

    // Move data cache (MoveId → MoveEntry)
    private readonly ConcurrentDictionary<int, MoveEntry> _moveCache = new();

    public const int BringCount = 4;   // VGC: bring 4 from 6
    public const int ActivePerSide = 2; // 2 active slots per side
    public const int BattleLevel = 50;

    public int TurnTimeoutSeconds => _opts.TurnTimeoutSeconds;

    // ── Type chart (Gen 6+, with Fairy) ─────────────────────────────────────
    // Outer key = attacking type, inner key = defending type, value = multiplier.
    // Only non-1.0 entries are listed; missing = 1.0.
    private static readonly Dictionary<string, Dictionary<string, double>> TypeChart =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["normal"]   = new(StringComparer.OrdinalIgnoreCase) { ["rock"]=.5, ["ghost"]=0, ["steel"]=.5 },
            ["fire"]     = new(StringComparer.OrdinalIgnoreCase) { ["fire"]=.5, ["water"]=.5, ["rock"]=.5, ["dragon"]=.5, ["grass"]=2, ["ice"]=2, ["bug"]=2, ["steel"]=2 },
            ["water"]    = new(StringComparer.OrdinalIgnoreCase) { ["water"]=.5, ["grass"]=.5, ["dragon"]=.5, ["fire"]=2, ["ground"]=2, ["rock"]=2 },
            ["electric"] = new(StringComparer.OrdinalIgnoreCase) { ["electric"]=.5, ["grass"]=.5, ["dragon"]=.5, ["ground"]=0, ["flying"]=2, ["water"]=2 },
            ["grass"]    = new(StringComparer.OrdinalIgnoreCase) { ["fire"]=.5, ["grass"]=.5, ["poison"]=.5, ["flying"]=.5, ["bug"]=.5, ["dragon"]=.5, ["steel"]=.5, ["water"]=2, ["ground"]=2, ["rock"]=2 },
            ["ice"]      = new(StringComparer.OrdinalIgnoreCase) { ["water"]=.5, ["ice"]=.5, ["steel"]=.5, ["fire"]=.5, ["dragon"]=2, ["flying"]=2, ["grass"]=2, ["ground"]=2 },
            ["fighting"] = new(StringComparer.OrdinalIgnoreCase) { ["poison"]=.5, ["bug"]=.5, ["psychic"]=.5, ["flying"]=.5, ["fairy"]=.5, ["ghost"]=0, ["normal"]=2, ["ice"]=2, ["rock"]=2, ["dark"]=2, ["steel"]=2 },
            ["poison"]   = new(StringComparer.OrdinalIgnoreCase) { ["poison"]=.5, ["ground"]=.5, ["rock"]=.5, ["ghost"]=.5, ["steel"]=0, ["grass"]=2, ["fairy"]=2 },
            ["ground"]   = new(StringComparer.OrdinalIgnoreCase) { ["grass"]=.5, ["bug"]=.5, ["flying"]=0, ["fire"]=2, ["electric"]=2, ["poison"]=2, ["rock"]=2, ["steel"]=2 },
            ["flying"]   = new(StringComparer.OrdinalIgnoreCase) { ["electric"]=.5, ["rock"]=.5, ["steel"]=.5, ["grass"]=2, ["fighting"]=2, ["bug"]=2 },
            ["psychic"]  = new(StringComparer.OrdinalIgnoreCase) { ["psychic"]=.5, ["steel"]=.5, ["dark"]=0, ["fighting"]=2, ["poison"]=2 },
            ["bug"]      = new(StringComparer.OrdinalIgnoreCase) { ["fire"]=.5, ["fighting"]=.5, ["flying"]=.5, ["ghost"]=.5, ["steel"]=.5, ["fairy"]=.5, ["poison"]=.5, ["grass"]=2, ["psychic"]=2, ["dark"]=2 },
            ["rock"]     = new(StringComparer.OrdinalIgnoreCase) { ["fighting"]=.5, ["ground"]=.5, ["steel"]=.5, ["fire"]=2, ["ice"]=2, ["flying"]=2, ["bug"]=2 },
            ["ghost"]    = new(StringComparer.OrdinalIgnoreCase) { ["normal"]=0, ["dark"]=.5, ["ghost"]=2, ["psychic"]=2 },
            ["dragon"]   = new(StringComparer.OrdinalIgnoreCase) { ["steel"]=.5, ["fairy"]=0, ["dragon"]=2 },
            ["dark"]     = new(StringComparer.OrdinalIgnoreCase) { ["fighting"]=.5, ["dark"]=.5, ["fairy"]=.5, ["ghost"]=2, ["psychic"]=2 },
            ["steel"]    = new(StringComparer.OrdinalIgnoreCase) { ["fire"]=.5, ["water"]=.5, ["electric"]=.5, ["steel"]=.5, ["ice"]=2, ["rock"]=2, ["fairy"]=2 },
            ["fairy"]    = new(StringComparer.OrdinalIgnoreCase) { ["fire"]=.5, ["poison"]=.5, ["steel"]=.5, ["fighting"]=2, ["dragon"]=2, ["dark"]=2 },
        };

    // Nature → (boosted StatIndex, weakened StatIndex). Neutral natures absent.
    private static readonly Dictionary<string, (StatIndex Up, StatIndex Down)> NatureEffects =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Lonely"]  = (StatIndex.ATK, StatIndex.DEF),
            ["Brave"]   = (StatIndex.ATK, StatIndex.SPE),
            ["Adamant"] = (StatIndex.ATK, StatIndex.SPA),
            ["Naughty"] = (StatIndex.ATK, StatIndex.SPD),
            ["Bold"]    = (StatIndex.DEF, StatIndex.ATK),
            ["Relaxed"] = (StatIndex.DEF, StatIndex.SPE),
            ["Impish"]  = (StatIndex.DEF, StatIndex.SPA),
            ["Lax"]     = (StatIndex.DEF, StatIndex.SPD),
            ["Timid"]   = (StatIndex.SPE, StatIndex.ATK),
            ["Hasty"]   = (StatIndex.SPE, StatIndex.DEF),
            ["Jolly"]   = (StatIndex.SPE, StatIndex.SPA),
            ["Naive"]   = (StatIndex.SPE, StatIndex.SPD),
            ["Modest"]  = (StatIndex.SPA, StatIndex.ATK),
            ["Mild"]    = (StatIndex.SPA, StatIndex.DEF),
            ["Quiet"]   = (StatIndex.SPA, StatIndex.SPE),
            ["Rash"]    = (StatIndex.SPA, StatIndex.SPD),
            ["Calm"]    = (StatIndex.SPD, StatIndex.ATK),
            ["Gentle"]  = (StatIndex.SPD, StatIndex.DEF),
            ["Sassy"]   = (StatIndex.SPD, StatIndex.SPE),
            ["Careful"] = (StatIndex.SPD, StatIndex.SPA),
        };

    // ── Weather/terrain constants ────────────────────────────────────────────
    private static readonly HashSet<string> SandstormImmune = new(StringComparer.OrdinalIgnoreCase) { "rock", "steel", "ground" };
    private static readonly HashSet<string> SnowImmune = new(StringComparer.OrdinalIgnoreCase) { "ice" };

    // Move IDs with special Gen 9 handling
    private const int MoveIdTeraBlast    = 851; // type→TerType, uses higher Atk/SpAtk
    private const int MoveIdSnowscape    = 883; // sets Snow weather
    private const int MoveIdChillyReception = 881; // sets Snow + switches out

    // Terrain duration (turns)
    private const int TerrainDuration = 5;

    public BattleService(MongoDbContext db, IOptions<BattleOptions> opts)
    {
        _db = db;
        _opts = opts.Value;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Public API
    // ═══════════════════════════════════════════════════════════════════════

    public BattleSession? GetSession(string battleId)
        => _sessions.TryGetValue(battleId, out var s) ? s : null;

    public MoveEntry? GetMoveData(int moveId)
        => _moveCache.TryGetValue(moveId, out var m) ? m : null;

    /// <summary>Returns sessions in Running state where the turn deadline has expired.</summary>
    public IEnumerable<BattleSession> GetExpiredSessions()
        => _sessions.Values.Where(s =>
            s.State == BattleState.Running &&
            s.TurnDeadlineUtc < DateTime.UtcNow &&
            !AllActionsSubmitted(s));

    /// <summary>
    /// Auto-fills missing actions with a default move, then resolves the turn.
    /// Returns null if the session state changed before this could act.
    /// </summary>
    public async Task<BattleTurnResult?> TryAutoResolveExpiredTurn(BattleSession session)
    {
        if (session.State != BattleState.Running) return null;
        if (AllActionsSubmitted(session)) return null;

        Console.WriteLine($"[TurnTimeout] Auto-resolving battle {session.BattleId} (Deadline: {session.TurnDeadlineUtc}, Now: {DateTime.UtcNow})");

        AutoFillMissingPlayerActions(session);
        BotFillActions(session);

        if (!AllActionsSubmitted(session)) return null;

        var result = await ResolveTurn(session);
        BotResolveForcedSwitches(session);

        if (session.State == BattleState.ForcedSwitch && session.PendingForcedSwitches.Count == 0)
        {
            session.State = BattleState.Running;
            session.TurnNumber++;
            session.TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(_opts.TurnTimeoutSeconds);
            result.State = session.State;
        }
        return result;
    }

    private void AutoFillMissingPlayerActions(BattleSession session)
    {
        foreach (var (playerId, slot) in AllActiveSlots(session))
        {
            string key = $"{playerId}:{slot}";
            if (session.PendingActions.ContainsKey(key)) continue;

            var pokemon = GetActiveSlot(session, playerId, slot);
            if (pokemon == null || pokemon.IsFainted) continue;

            // First move with PP, else slot 0 (Struggle fallback)
            int moveSlot = 0;
            for (int i = 0; i < pokemon.Moves.Count; i++)
                if (pokemon.Moves[i].CurrentPp > 0) { moveSlot = i; break; }

            var oppId     = GetOpponentId(session, playerId);
            var tA        = GetActiveSlot(session, oppId, 0);
            int targetSlot = tA != null && !tA.IsFainted ? 0 : 1;

            session.PendingActions[key] = new BattleAction
            {
                PlayerId    = playerId,
                Type        = BattleActionType.Move,
                SourceIndex = slot,
                MoveSlot    = moveSlot,
                TargetSlot  = targetSlot,
            };
        }
    }

    /// <summary>Returns available bench indices a player can send in for a forced switch.</summary>
    public List<int> GetAvailableReplacements(BattleSession session, string playerId, int faintedSlot)
    {
        var team    = playerId == session.Player1Id ? session.Team1 : session.Team2;
        int activeA = GetActiveIndex(session, playerId, 0);
        int activeB = GetActiveIndex(session, playerId, 1);
        return team
            .Select((p, i) => (p, i))
            .Where(x => !x.p.IsFainted && x.i != activeA && x.i != activeB)
            .Select(x => x.i)
            .ToList();
    }

    public const string BotPlayerId = "BOT_PLAYER";

    // Species IDs dùng cho bot team (6 Pokemon, bring 4)
    private static readonly int[] BotSpeciesIds = { 6, 9, 150, 445, 282, 248 };

    public async Task<BattleSession> CreateBattle(string player1Id, string player2Id)
    {
        var session = new BattleSession
        {
            Player1Id = player1Id,
            Player2Id = player2Id,
            State     = BattleState.TeamPreview,
        };

        session.Team1 = await LoadTeamSnapshots(player1Id);
        if (session.Team1.Count == 0) session.Team1 = await GenerateBotTeam();

        if (player2Id == BotPlayerId)
        {
            session.Team2 = await GenerateBotTeam();
        }
        else
        {
            session.Team2 = await LoadTeamSnapshots(player2Id);
        }

        await PreloadMoveData(session.Team1.Concat(session.Team2));

        if (player1Id == BotPlayerId)
        {
            var t1Count = Math.Min(BringCount, session.Team1.Count);
            session.TeamOrder1 = Enumerable.Range(0, t1Count).ToList();
        }
        if (player2Id == BotPlayerId)
        {
            var t2Count = Math.Min(BringCount, session.Team2.Count);
            session.TeamOrder2 = Enumerable.Range(0, t2Count).ToList();
        }

        _sessions[session.BattleId] = session;
        return session;
    }

    /// <summary>Generate một team 6 Pokemon từ Pokedex cho Bot.</summary>
    private async Task<List<BattlePokemonSnapshot>> GenerateBotTeam()
    {
        var snapshots = new List<BattlePokemonSnapshot>();
        var allMoveIds = new List<int>();

        foreach (var speciesId in BotSpeciesIds)
        {
            var dex = await _db.Pokedex.Find(d => d.Id == speciesId).FirstOrDefaultAsync();
            
            // FALLBACK: If DB is empty, create a dummy pokemon so the battle doesn't crash
            string botName = dex?.Name ?? $"BotPkm#{speciesId}";
            string t1 = (dex?.Types != null && dex.Types.Count > 0) ? dex.Types[0] : "normal";
            string? t2 = (dex?.Types != null && dex.Types.Count > 1) ? dex.Types[1] : null;
            int baseHp = dex?.BaseStats.GetValueOrDefault("hp", 80) ?? 80;

            int hp = ComputeHp(baseHp, 31, 0);
            var moveset = dex?.DefaultMoves?.Take(4).ToList() ?? new List<int>();
            if (moveset.Count == 0) moveset.Add(1); // Force 'Tackle' (ID 1) if empty
            allMoveIds.AddRange(moveset);

            snapshots.Add(new BattlePokemonSnapshot
            {
                InstanceId  = $"bot_{speciesId}_{Guid.NewGuid().ToString()[..4]}",
                SpeciesId   = speciesId,
                SpeciesName = botName,
                Nickname    = botName,
                Level       = BattleLevel,
                MaxHp       = hp, CurrentHp = hp,
                IsFainted   = false,
                Type1 = t1, Type2 = t2, OrigType1 = t1, OrigType2 = t2,
                TerType = t1,
                Atk   = ComputeStat(dex?.BaseStats.GetValueOrDefault("attack", 80) ?? 80, 31, 0, 1.0),
                Def   = ComputeStat(dex?.BaseStats.GetValueOrDefault("defense", 80) ?? 80, 31, 0, 1.0),
                SpAtk = ComputeStat(dex?.BaseStats.GetValueOrDefault("special-attack", 80) ?? 80, 31, 0, 1.0),
                SpDef = ComputeStat(dex?.BaseStats.GetValueOrDefault("special-defense", 80) ?? 80, 31, 0, 1.0),
                Spd   = ComputeStat(dex?.BaseStats.GetValueOrDefault("speed", 80) ?? 80, 31, 0, 1.0),
                Moves = moveset.Select(id => new PokemonMove { MoveId = id, CurrentPp = 15, MaxPp = 15 }).ToList(),
            });
        }

        if (snapshots.Count == 0) {
            // Ultimate fallback if even loop fails
            snapshots.Add(new BattlePokemonSnapshot { SpeciesName = "MissingNo", MaxHp = 100, CurrentHp = 100, Level = 50, Type1 = "normal" });
        }

        // Preload moves for bot team
        var missing = allMoveIds.Distinct().Where(id => !_moveCache.ContainsKey(id)).ToList();
        if (missing.Any())
        {
            var entries = await _db.Moves.Find(m => missing.Contains(m.Id)).ToListAsync();
            foreach (var e in entries) _moveCache[e.Id] = e;
        }

        return snapshots;
    }

    /// <summary>
    /// Player submits ordered 4-pick from their 6-Pokemon party.
    /// Returns true if both players have picked and battle transitions to Running.
    /// </summary>
    public (BattleSession session, bool battleStarted) SubmitTeamOrder(
        string battleId, string playerId, List<int> orderedIndices)
    {
        var session = GetSessionOrThrow(battleId);
        if (session.State != BattleState.TeamPreview)
            throw new InvalidOperationException("Battle is not in Team Preview phase.");

        if (orderedIndices.Count != BringCount)
            throw new ArgumentException($"VGC requires exactly {BringCount} Pokemon.");

        if (orderedIndices.Distinct().Count() != BringCount)
            throw new ArgumentException("Duplicate indices are not allowed.");

        if (playerId == session.Player1Id)
        {
            if (orderedIndices.Any(i => i < 0 || i >= session.Team1.Count))
                throw new ArgumentException("Invalid Pokemon index for Team 1.");
            session.TeamOrder1 = orderedIndices;
        }
        else if (playerId == session.Player2Id)
        {
            if (orderedIndices.Any(i => i < 0 || i >= session.Team2.Count))
                throw new ArgumentException("Invalid Pokemon index for Team 2.");
            session.TeamOrder2 = orderedIndices;
        }
        else
        {
            throw new InvalidOperationException("Player is not in this battle.");
        }

        if (session.TeamOrder1.Count == BringCount && session.TeamOrder2.Count == BringCount)
        {
            // Lock in the 4 selected Pokemon in submission order
            var orig1 = session.Team1.ToList();
            var orig2 = session.Team2.ToList();
            session.Team1 = session.TeamOrder1.Select(i => orig1[i]).ToList();
            session.Team2 = session.TeamOrder2.Select(i => orig2[i]).ToList();

            // First 2 in pick order go to the field
            session.ActiveIndex1  = 0;
            session.ActiveIndex1b = 1;
            session.ActiveIndex2  = 0;
            session.ActiveIndex2b = 1;

            session.State = BattleState.Running;
            session.TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(999);
            return (session, true);
        }

        return (session, false);
    }

    /// <summary>
    /// Player submits an action for one of their active slots.
    /// Key format: "{playerId}:{action.SourceIndex}".
    /// Returns a resolved BattleTurnResult when all 4 slots have submitted.
    /// </summary>
    public async Task<(BattleSession session, BattleTurnResult? result)> SubmitBattleAction(
        string battleId, string playerId, BattleAction action)
    {
        var session = GetSessionOrThrow(battleId);
        if (session.State != BattleState.Running)
            throw new InvalidOperationException($"Battle state is '{session.State}', expected Running.");

        action.PlayerId = playerId;
        ValidateAction(session, playerId, action);

        // Validate Tera request — player can only Tera once per battle
        if (action.UseTera)
        {
            bool teraUsed = playerId == session.Player1Id ? session.TeraUsed1 : session.TeraUsed2;
            if (teraUsed) action.UseTera = false; // silently ignore if already used
        }

        string key = $"{playerId}:{action.SourceIndex}";
        Console.WriteLine($"[Battle] Player {playerId} submitted action for slot {action.SourceIndex}: {action.Type} (MoveSlot: {action.MoveSlot}, TargetSlot: {action.TargetSlot})");
        session.PendingActions[key] = action;

        // Bot battle: auto-fill bot actions immediately after human submits
        BotFillActions(session);

        if (AllActionsSubmitted(session))
        {
            var result = await ResolveTurn(session);
            // After turn: auto-resolve bot forced switches
            BotResolveForcedSwitches(session);
            // If bot resolved ALL forced switches, transition back to Running immediately
            if (session.State == BattleState.ForcedSwitch && session.PendingForcedSwitches.Count == 0)
            {
                session.State = BattleState.Running;
                session.TurnNumber++;
                session.TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(999);
                result.State = session.State;
            }
            return (session, result);
        }

        return (session, null);
    }

    /// <summary>
    /// Player picks a replacement Pokemon for a fainted slot.
    /// Returns true when all pending forced switches are resolved.
    /// </summary>
    public (BattleSession session, bool allResolved) SubmitForcedSwitch(
        string battleId, string playerId, int slot, int partyIndex)
    {
        var session = GetSessionOrThrow(battleId);
        if (session.State != BattleState.ForcedSwitch)
            throw new InvalidOperationException("Battle is not in ForcedSwitch phase.");

        // Bot battles: auto-resolve bot's forced switches first
        BotResolveForcedSwitches(session);

        string key = $"{playerId}:{slot}";
        if (!session.PendingForcedSwitches.Contains(key))
            throw new InvalidOperationException($"No forced switch pending for {key}.");

        var team = playerId == session.Player1Id ? session.Team1 : session.Team2;
        var activeA = GetActiveIndex(session, playerId, 0);
        var activeB = GetActiveIndex(session, playerId, 1);

        if (partyIndex < 0 || partyIndex >= team.Count)
            throw new ArgumentException("Invalid party index.");

        var incoming = team[partyIndex];
        if (incoming.IsFainted)
            throw new ArgumentException("Cannot send in a fainted Pokemon.");
        if (partyIndex == activeA || partyIndex == activeB)
            throw new ArgumentException("That Pokemon is already on the field.");

        // Place the new Pokemon into the vacated slot
        SetActiveIndex(session, playerId, slot, partyIndex);
        session.PendingForcedSwitches.Remove(key);

        // Auto-resolve remaining bot forced switches
        BotResolveForcedSwitches(session);

        if (session.PendingForcedSwitches.Count == 0)
        {
            session.State = BattleState.Running;
            session.TurnNumber++;
            session.TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(999);
            return (session, true);
        }

        return (session, false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Bot AI
    // ═══════════════════════════════════════════════════════════════════════

    private bool IsBotBattle(BattleSession s) =>
        s.Player1Id == BotPlayerId || s.Player2Id == BotPlayerId;

    private string BotId(BattleSession s) =>
        s.Player1Id == BotPlayerId ? s.Player1Id : s.Player2Id;

    private string HumanId(BattleSession s) =>
        s.Player1Id == BotPlayerId ? s.Player2Id : s.Player1Id;

    /// <summary>Bot tự điền actions cho mọi slot chưa có action.</summary>
    private void BotFillActions(BattleSession session)
    {
        if (!IsBotBattle(session)) return;

        string botId   = BotId(session);
        string humanId = HumanId(session);

        for (int slot = 0; slot <= 1; slot++)
        {
            string key = $"{botId}:{slot}";
            if (session.PendingActions.ContainsKey(key)) continue;

            var botPkm = GetActiveSlot(session, botId, slot);
            if (botPkm == null || botPkm.IsFainted) continue;

            // Chọn move có PP còn lại, ưu tiên move có power cao nhất
            int moveSlot = 0;
            if (botPkm.Moves.Count > 0)
            {
                int best = -1; int bestPow = -1;
                for (int i = 0; i < botPkm.Moves.Count; i++)
                {
                    var m = botPkm.Moves[i];
                    if (m.CurrentPp <= 0) continue;
                    int pow = _moveCache.TryGetValue(m.MoveId, out var entry) ? (entry.Power ?? 0) : 0;
                    if (pow > bestPow) { bestPow = pow; best = i; }
                }
                moveSlot = best >= 0 ? best : 0;
            }

            // Chọn target: ưu tiên human slot có HP thấp hơn
            int targetSlot = 0;
            var tA = GetActiveSlot(session, humanId, 0);
            var tB = GetActiveSlot(session, humanId, 1);
            bool aAlive = tA != null && !tA.IsFainted;
            bool bAlive = tB != null && !tB.IsFainted;

            if (!aAlive && bAlive)       targetSlot = 1;
            else if (aAlive && !bAlive)  targetSlot = 0;
            else if (aAlive && bAlive)
            {
                // target Pokemon có HP thấp hơn (dễ KO hơn)
                float pctA = (float)tA!.CurrentHp / tA.MaxHp;
                float pctB = (float)tB!.CurrentHp / tB.MaxHp;
                targetSlot = pctA <= pctB ? 0 : 1;
            }

            session.PendingActions[key] = new BattleAction
            {
                PlayerId    = botId,
                Type        = BattleActionType.Move,
                SourceIndex = slot,
                MoveSlot    = moveSlot,
                TargetSlot  = targetSlot,
            };
        }
    }

    /// <summary>Tự động thay Pokemon cho bot khi bị hạ gục.</summary>
    private void BotResolveForcedSwitches(BattleSession session)
    {
        if (!IsBotBattle(session)) return;

        string botId = BotId(session);
        var botKeys  = session.PendingForcedSwitches
            .Where(k => k.StartsWith($"{botId}:"))
            .ToList();

        foreach (var fsKey in botKeys)
        {
            int slot     = int.Parse(fsKey.Split(':')[1]);
            var team     = botId == session.Player1Id ? session.Team1 : session.Team2;
            int activeA  = GetActiveIndex(session, botId, 0);
            int activeB  = GetActiveIndex(session, botId, 1);

            // Chọn Pokemon có HP cao nhất trong bench
            var replacement = team
                .Select((p, i) => (p, i))
                .Where(x => !x.p.IsFainted && x.i != activeA && x.i != activeB)
                .OrderByDescending(x => (float)x.p.CurrentHp / x.p.MaxHp)
                .FirstOrDefault();

            if (replacement.p != null)
            {
                SetActiveIndex(session, botId, slot, replacement.i);
                session.PendingForcedSwitches.Remove(fsKey);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Turn resolution
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<BattleTurnResult> ResolveTurn(BattleSession session)
    {
        var events = new List<BattleEvent>();

        // Snapshot & clear actions
        var actions = session.PendingActions.Values.ToList();
        session.PendingActions.Clear();

        // Sort by VGC priority
        var ordered = SortActions(session, actions);

        // Execute each action
        foreach (var action in ordered)
        {
            var actor = GetActiveSlot(session, action.PlayerId, action.SourceIndex);
            if (actor == null || actor.IsFainted) continue;

            if (action.Type == BattleActionType.Switch)
                ExecuteSwitch(session, action, events);
            else
                await ExecuteMove(session, action, events);
        }

        // End-of-turn effects
        ResolveEndOfTurn(session, events);
        ResetVolatileStatuses(session);

        // Check win
        var winner = CheckWinCondition(session);
        var forcedSlots = CollectFaintedSlots(session);

        var result = BuildTurnResult(session, events, winner, forcedSlots);

        if (winner != null)
        {
            session.State = BattleState.Ended;
            session.WinnerPlayerId = winner;
        }
        else if (forcedSlots.Count > 0)
        {
            session.State = BattleState.ForcedSwitch;
            foreach (var fs in forcedSlots)
                session.PendingForcedSwitches.Add($"{fs.PlayerId}:{fs.Slot}");
        }
        else
        {
            session.TurnNumber++;
            session.TurnDeadlineUtc = DateTime.UtcNow.AddSeconds(999);
        }

        result.State = session.State;
        return result;
    }

    private void ResetVolatileStatuses(BattleSession session)
    {
        foreach (var (playerId, slot) in AllActiveSlots(session))
        {
            var p = GetActiveSlot(session, playerId, slot);
            if (p != null)
            {
                p.IsFlinched = false;
                // Add more resets here if needed (Protect, etc.)
            }
        }
    }

    // ── Action ordering ──────────────────────────────────────────────────────

    private List<BattleAction> SortActions(BattleSession session, List<BattleAction> actions)
    {
        int GetPriority(BattleAction a)
        {
            if (a.Type == BattleActionType.Switch) return _opts.SwitchActionPriority;
            var actor = GetActiveSlot(session, a.PlayerId, a.SourceIndex);
            if (actor == null || !a.MoveSlot.HasValue || a.MoveSlot.Value >= actor.Moves.Count) return 0;
            int moveId = actor.Moves[a.MoveSlot.Value].MoveId;
            return _moveCache.TryGetValue(moveId, out var entry) ? entry.Priority : 0;
        }

        int GetSpeed(BattleAction a)
        {
            var actor = GetActiveSlot(session, a.PlayerId, a.SourceIndex);
            return actor == null ? 0 : GetEffectiveSpeed(actor);
        }

        return [.. actions
            .OrderByDescending(GetPriority)
            .ThenByDescending(GetSpeed)
            .ThenBy(_ => _rng.Next())];
    }

    // ── Switch ───────────────────────────────────────────────────────────────

    private void ExecuteSwitch(BattleSession session, BattleAction action, List<BattleEvent> events)
    {
        if (!action.SwitchIndex.HasValue) return;

        var team = action.PlayerId == session.Player1Id ? session.Team1 : session.Team2;
        int newIdx = action.SwitchIndex.Value;

        if (newIdx < 0 || newIdx >= team.Count || team[newIdx].IsFainted) return;

        // Cannot switch in a Pokemon already on the field
        int activeA = GetActiveIndex(session, action.PlayerId, 0);
        int activeB = GetActiveIndex(session, action.PlayerId, 1);
        if (newIdx == activeA || newIdx == activeB) return;

        var outgoing = GetActiveSlot(session, action.PlayerId, action.SourceIndex);
        var incoming = team[newIdx];

        // Reset volatile status on switch-out
        if (outgoing != null)
        {
            outgoing.IsConfused = false;
            outgoing.ConfusionTurnsLeft = 0;
            Array.Clear(outgoing.StatStages, 0, outgoing.StatStages.Length);
        }

        SetActiveIndex(session, action.PlayerId, action.SourceIndex, newIdx);

        events.Add(new SwitchEvent
        {
            PlayerId             = action.PlayerId,
            WithdrawnPokemonName = outgoing?.SpeciesName ?? "",
            SentOutPokemonName   = incoming.SpeciesName,
            NewActiveIndex       = newIdx,
            IsAutoSwitch         = false,
            Message              = $"[TRAINER] withdrew {outgoing?.SpeciesName ?? "Pokemon"} and sent out {incoming.SpeciesName}!"
        });
    }

    // ── Move ─────────────────────────────────────────────────────────────────

    private async Task ExecuteMove(BattleSession session, BattleAction action, List<BattleEvent> events)
    {
        var attacker = GetActiveSlot(session, action.PlayerId, action.SourceIndex);
        if (attacker == null || attacker.IsFainted || !action.MoveSlot.HasValue) return;

        int moveSlot = action.MoveSlot.Value;
        if (moveSlot >= attacker.Moves.Count) return;

        var pokemonMove = attacker.Moves[moveSlot];

        // Lazy-load if somehow missing from cache
        if (!_moveCache.ContainsKey(pokemonMove.MoveId))
        {
            var fetched = await _db.Moves.Find(m => m.Id == pokemonMove.MoveId).FirstOrDefaultAsync();
            if (fetched != null) _moveCache[pokemonMove.MoveId] = fetched;
        }

        if (!_moveCache.TryGetValue(pokemonMove.MoveId, out var move)) return;

        // Status condition prevents acting?
        if (!CanAct(session, attacker, events)) return;

        // Confusion self-hit
        if (attacker.IsConfused)
        {
            attacker.ConfusionTurnsLeft--;
            if (attacker.ConfusionTurnsLeft <= 0)
            {
                attacker.IsConfused = false;
                events.Add(new MessageEvent { Message = $"{attacker.SpeciesName} snapped out of confusion!" });
            }
            else if (_rng.NextDouble() < 0.33)
            {
                int selfDmg = CalculateSelfConfusionDamage(attacker);
                ApplyDamage(session, action.PlayerId, attacker, selfDmg, events, false, 1.0);
                events.Add(new MessageEvent { Message = $"{attacker.SpeciesName} hurt itself in confusion!" });
                return;
            }
        }

        // ── Terastallization (Gen 9) ─────────────────────────────────────────
        if (action.UseTera)
        {
            ApplyTerastallization(session, action.PlayerId, attacker, events);
        }

        // ── Tera Blast special override ──────────────────────────────────────
        // Clone move data if we need to mutate it for Tera Blast
        string moveType     = move.Type;
        string moveCategory = move.Category;
        if (pokemonMove.MoveId == MoveIdTeraBlast && attacker.IsTerastallized)
        {
            moveType     = attacker.TerType;
            // Use physical if ATK > SpAtk (with stage mods)
            moveCategory = GetEffectiveStat(attacker, StatIndex.ATK) >= GetEffectiveStat(attacker, StatIndex.SPA)
                           ? "Physical" : "Special";
        }

        events.Add(new MoveUsedEvent
        {
            PlayerId    = action.PlayerId,
            PokemonName = attacker.SpeciesName,
            MoveName    = move.Name,
            MoveId      = pokemonMove.MoveId.ToString(),
            Message     = $"{attacker.SpeciesName} used {move.Name}!"
        });

        // Consume 1 PP
        attacker.Moves[moveSlot].CurrentPp = Math.Max(0, attacker.Moves[moveSlot].CurrentPp - 1);

        // Resolve targets
        var targets = GetTargets(session, action, move);
        if (targets.Count == 0) return;

        // ── Psychic Terrain: block priority moves vs grounded opponents ──────
        if (session.Terrain == TerrainCondition.Psychic && move.Priority > 0)
        {
            var opp = GetOpponentId(session, action.PlayerId);
            targets = targets
                .Where(t =>
                {
                    if (t.OwnerId != opp) return true; // own ally — not blocked
                    var defender = GetActiveSlot(session, t.OwnerId, t.Slot);
                    if (defender == null || !IsGrounded(defender)) return true;
                    events.Add(new MessageEvent { Message = $"Psychic Terrain protected {defender.SpeciesName}!" });
                    return false;
                })
                .ToList();
            if (targets.Count == 0) return;
        }

        bool isSpread = targets.Count > 1 && (move.TargetType == MoveTargetType.SpreadOpponents || move.TargetType == MoveTargetType.AllExceptUser);

        if (moveCategory.Equals("Status", StringComparison.OrdinalIgnoreCase))
        {
            // Single accuracy check for status moves (vs first target's evasion)
            if (move.Accuracy.HasValue && move.Accuracy.Value > 0)
            {
                var t0 = targets.FirstOrDefault();
                var firstDef = t0 != null ? GetActiveSlot(session, t0.OwnerId, t0.Slot) : null;
                double hitChance = move.Accuracy.Value / 100.0
                    * attacker.GetStageMultiplier(StatIndex.ACC)
                    / (firstDef?.GetStageMultiplier(StatIndex.EVA) ?? 1.0);
                if (_rng.NextDouble() > hitChance)
                {
                    events.Add(new MoveMissedEvent { UserId = action.PlayerId, PokemonName = attacker.SpeciesName, MoveName = move.Name, Message = $"{attacker.SpeciesName}'s attack missed!" });
                    return;
                }
            }
            ApplyStatusMoveEffect(session, action.PlayerId, action.SourceIndex, attacker, targets, move, events);
        }
        else
        {
            // Secondary effect chance: use move data if available, else default 10%
            var (effectiveEffect, effectiveChance) = GetEffectiveMoveEffect(move);
            double effectChance = effectiveChance / 100.0;

            foreach (var tRef in targets)
            {
                var defender = GetActiveSlot(session, tRef.OwnerId, tRef.Slot);
                if (defender == null || defender.IsFainted) continue;

                // Per-target accuracy check for damaging moves
                if (move.Accuracy.HasValue && move.Accuracy.Value > 0)
                {
                    double hitChance = move.Accuracy.Value / 100.0
                        * attacker.GetStageMultiplier(StatIndex.ACC)
                        / defender.GetStageMultiplier(StatIndex.EVA);
                    if (_rng.NextDouble() > hitChance)
                    {
                        events.Add(new MoveMissedEvent { UserId = action.PlayerId, PokemonName = attacker.SpeciesName, MoveName = move.Name, Message = $"{attacker.SpeciesName}'s attack missed!" });
                        continue;
                    }
                }

                double typeEff = GetTypeEffectiveness(moveType, defender);
                if (typeEff == 0)
                {
                    events.Add(new MoveNoEffectEvent { TargetName = defender.SpeciesName });
                    continue;
                }

                bool isCrit = _rng.NextDouble() < 0.0625;
                int damage = CalculateDamage(attacker, defender, move, session, isSpread, isCrit, moveType, moveCategory);

                if (typeEff >= 2) events.Add(new SuperEffectiveEvent { Multiplier = typeEff });
                else if (typeEff < 1) events.Add(new NotVeryEffectiveEvent { Multiplier = typeEff });

                ApplyDamage(session, tRef.OwnerId, defender, damage, events, isCrit, typeEff);

                // ── Secondary Effect ────────────────────────────────────────
                if (!string.IsNullOrEmpty(effectiveEffect) && _rng.NextDouble() < effectChance)
                {
                    if (effectiveEffect == "flinch")
                    {
                        // Flinch only works if target hasn't moved yet (handled by ResetVolatileStatuses + ExecuteMove order)
                        defender.IsFlinched = true;
                    }
                    else
                    {
                        TryInflictStatus(session, tRef.OwnerId, defender, effectiveEffect, events);
                    }
                }
            }
        }
    }

    // ── Status condition gate ────────────────────────────────────────────────

    private bool CanAct(BattleSession session, BattlePokemonSnapshot pokemon, List<BattleEvent> events)
    {
        switch (pokemon.NonVolatileStatus)
        {
            case PokemonStatusCondition.Paralysis:
                if (_rng.NextDouble() < 0.25)
                {
                    events.Add(new ParalysisStuckEvent { PokemonName = pokemon.SpeciesName });
                    return false;
                }
                break;

            case PokemonStatusCondition.Sleep:
                if (pokemon.SleepTurnsLeft > 0)
                {
                    pokemon.SleepTurnsLeft--;
                    events.Add(new SleepSkipEvent { PokemonName = pokemon.SpeciesName, TurnsLeft = pokemon.SleepTurnsLeft });
                    return false;
                }
                // Woke up
                pokemon.NonVolatileStatus = PokemonStatusCondition.None;
                events.Add(new StatusHealedEvent
                {
                    PokemonName = pokemon.SpeciesName,
                    Status      = PokemonStatusCondition.Sleep,
                });
                break;

            case PokemonStatusCondition.Freeze:
                if (_rng.NextDouble() < 0.20)
                {
                    // Thaw
                    pokemon.NonVolatileStatus = PokemonStatusCondition.None;
                    events.Add(new FreezeThawEvent { PokemonName = pokemon.SpeciesName });
                }
                else
                {
                    events.Add(new SleepSkipEvent { PokemonName = pokemon.SpeciesName, TurnsLeft = 0 });
                    return false;
                }
                break;
        }

        if (pokemon.IsFlinched)
        {
            events.Add(new MessageEvent { Message = $"{pokemon.SpeciesName} flinched and couldn't move!" });
            return false;
        }

        if (pokemon.IsConfused)
        {
            if (pokemon.ConfusionTurnsLeft <= 0)
            {
                pokemon.IsConfused = false;
                events.Add(new MessageEvent { Message = $"{pokemon.SpeciesName} snapped out of its confusion!" });
            }
            else
            {
                pokemon.ConfusionTurnsLeft--;
                events.Add(new MessageEvent { Message = $"{pokemon.SpeciesName} is confused!" });
                if (_rng.NextDouble() < 0.33) // Gen 7+ 33% self-hit chance
                {
                    // Confusion damage calculation (40 power, Physical, typeless)
                    int confDamage = CalculateConfusionDamage(pokemon);
                    events.Add(new MessageEvent { Message = "It hurt itself in its confusion!" });
                    ApplyDamage(session, "none", pokemon, confDamage, events, false, 1.0);
                    return false;
                }
            }
        }

        return true;
    }

    private int CalculateConfusionDamage(BattlePokemonSnapshot p)
    {
        // Simple formula: ((2*Level/5 + 2) * 40 * Atk/Def) / 50 + 2
        double atk = GetEffectiveStat(p, StatIndex.ATK);
        double def = GetEffectiveStat(p, StatIndex.DEF);
        double dmg = ((2.0 * p.Level / 5.0 + 2.0) * 40.0 * atk / def) / 50.0 + 2.0;
        return (int)dmg;
    }

    // ── Target resolution ────────────────────────────────────────────────────

    private record TargetRef(string OwnerId, int Slot);

    private List<TargetRef> GetTargets(BattleSession session, BattleAction action, MoveEntry move)
    {
        var opp = GetOpponentId(session, action.PlayerId);
        var self = action.PlayerId;

        return move.TargetType switch
        {
            MoveTargetType.SingleOpponent => action.TargetSlot switch
            {
                0 => [new(opp, 0)],
                1 => [new(opp, 1)],
                _ => [new(opp, 0)],
            },
            MoveTargetType.SpreadOpponents => [new(opp, 0), new(opp, 1)],
            MoveTargetType.Self            => [new(self, action.SourceIndex)],
            MoveTargetType.SingleAlly      => [new(self, action.SourceIndex == 0 ? 1 : 0)],
            MoveTargetType.AllExceptUser   =>
            [
                new(opp, 0), new(opp, 1),
                new(self, action.SourceIndex == 0 ? 1 : 0),
            ],
            MoveTargetType.RandomOpponent => PickRandomOpponentTarget(session, opp),
            _ => [new(opp, 0)],
        };
    }

    private List<TargetRef> PickRandomOpponentTarget(BattleSession session, string oppId)
    {
        var slotA = GetActiveSlot(session, oppId, 0);
        var slotB = GetActiveSlot(session, oppId, 1);
        bool aAlive = slotA != null && !slotA.IsFainted;
        bool bAlive = slotB != null && !slotB.IsFainted;
        if (aAlive && bAlive) return [_rng.NextDouble() < 0.5 ? new(oppId, 0) : new(oppId, 1)];
        if (aAlive) return [new(oppId, 0)];
        if (bAlive) return [new(oppId, 1)];
        return [];
    }

    // ── Damage calculation ───────────────────────────────────────────────────

    // moveType / moveCategory allow Tera Blast overrides
    private int CalculateDamage(
        BattlePokemonSnapshot attacker, BattlePokemonSnapshot defender,
        MoveEntry move, BattleSession session, bool isSpread, bool isCrit,
        string? moveType = null, string? moveCategory = null)
    {
        if (!move.Power.HasValue || move.Power <= 0) return 0;

        moveType     ??= move.Type;
        moveCategory ??= move.Category;
        bool isPhysical = moveCategory.Equals("Physical", StringComparison.OrdinalIgnoreCase);

        // Ignore negative stat stages for the attacking stat when landing a critical hit
        int atkStat, defStat;
        if (isPhysical)
        {
            atkStat = isCrit
                ? Math.Max(attacker.Atk, GetEffectiveStat(attacker, StatIndex.ATK))
                : GetEffectiveStat(attacker, StatIndex.ATK);
            defStat = isCrit
                ? Math.Min(defender.Def, GetEffectiveStat(defender, StatIndex.DEF))
                : GetEffectiveStat(defender, StatIndex.DEF);
        }
        else
        {
            atkStat = isCrit
                ? Math.Max(attacker.SpAtk, GetEffectiveStat(attacker, StatIndex.SPA))
                : GetEffectiveStat(attacker, StatIndex.SPA);
            defStat = isCrit
                ? Math.Min(defender.SpDef, GetEffectiveStat(defender, StatIndex.SPD))
                : GetEffectiveStat(defender, StatIndex.SPD);
        }

        if (defStat <= 0) defStat = 1;

        // Gen 5+ formula: floor(floor(floor(2*L/5+2) * Pwr * A/D / 50) + 2)
        int baseDmg = (int)Math.Floor(
            (double)((int)Math.Floor(22.0 * move.Power.Value * atkStat / defStat / 50.0)) + 2
        );

        double spread   = isSpread ? 0.75 : 1.0;
        double weather  = GetWeatherModifier(session, moveType);
        double terrain  = GetTerrainDamageModifier(session, attacker, defender, moveType);
        double crit     = isCrit ? 1.5 : 1.0;
        double random   = _opts.DamageRandomMin + _rng.NextDouble() * (_opts.DamageRandomMax - _opts.DamageRandomMin);
        double stab     = GetStab(attacker, moveType);
        double typeEff  = GetTypeEffectiveness(moveType, defender);
        // Snow: Ice-type Sp.Def boosted ×1.5 (applied as defender buff — lower incoming sp dmg)
        double snowSpDef = (!isPhysical
            && (session.Weather == WeatherCondition.Snow || session.Weather == WeatherCondition.Hail)
            && (SnowImmune.Contains(defender.Type1) || (defender.Type2 != null && SnowImmune.Contains(defender.Type2)))) ? (1.0 / 1.5) : 1.0;
        double burn     = isPhysical && attacker.NonVolatileStatus == PokemonStatusCondition.Burn ? 0.5 : 1.0;

        if (typeEff == 0) return 0;

        int damage = (int)(baseDmg * spread * weather * terrain * crit * random * stab * typeEff * snowSpDef * burn);
        return Math.Max(1, damage);
    }

    private int CalculateSelfConfusionDamage(BattlePokemonSnapshot pokemon)
    {
        int atk = GetEffectiveStat(pokemon, StatIndex.ATK);
        int def = GetEffectiveStat(pokemon, StatIndex.DEF);
        if (def <= 0) def = 1;
        // Confusion damage follows standard move formula with Power 40
        return (int)Math.Floor((double)((int)Math.Floor(22.0 * 40 * atk / def / 50.0)) + 2);
    }

    // ── Apply damage ─────────────────────────────────────────────────────────

    private void ApplyDamage(
        BattleSession session, string ownerId, BattlePokemonSnapshot pokemon,
        int damage, List<BattleEvent> events, bool isCrit, double typeEff)
    {
        int before = pokemon.CurrentHp;
        pokemon.CurrentHp = Math.Max(0, before - damage);

        events.Add(new PokemonDamageEvent
        {
            PlayerId       = ownerId,
            PokemonName    = pokemon.SpeciesName,
            Damage         = damage,
            HpBefore       = before,
            HpAfter        = pokemon.CurrentHp,
            MaxHp          = pokemon.MaxHp,
            TypeMultiplier = typeEff,
            IsCritical     = isCrit,
            Message        = (isCrit ? "A critical hit! " : "") + 
                             (typeEff > 1.0 ? "It's super effective!" : typeEff < 1.0 && typeEff > 0 ? "It's not very effective..." : "")
        });

        if (pokemon.IsFainted)
        {
            events.Add(new PokemonFaintEvent
            {
                PlayerId    = ownerId,
                PokemonName = pokemon.SpeciesName,
                Message     = $"{pokemon.SpeciesName} fainted!"
            });
        }
    }

    // ── Status move effects ──────────────────────────────────────────────────

    private void ApplyStatusMoveEffect(
        BattleSession session, string actingPlayerId, int sourceSlot,
        BattlePokemonSnapshot attacker, List<TargetRef> targets,
        MoveEntry move, List<BattleEvent> events)
    {
        bool hasEffectStr = !string.IsNullOrEmpty(move.Effect);
        string effect = hasEffectStr ? move.Effect!.ToLowerInvariant() : "";

        // 1. Process structured StatChanges
        if (move.StatChanges != null && move.StatChanges.Count > 0)
        {
            var statTargets = effect.StartsWith("self")
                ? new List<TargetRef> { new(actingPlayerId, sourceSlot) }
                : targets;

            foreach (var sc in move.StatChanges)
            {
                var statIdx = sc.Stat.ToLower() switch
                {
                    "atk" => StatIndex.ATK,
                    "def" => StatIndex.DEF,
                    "spa" => StatIndex.SPA,
                    "spd" => StatIndex.SPD,
                    "spe" => StatIndex.SPE,
                    "acc" => StatIndex.ACC,
                    "eva" => StatIndex.EVA,
                    _     => (StatIndex?)null,
                };
                if (statIdx == null) continue;

                foreach (var tRef in statTargets)
                {
                    var pokemon = GetActiveSlot(session, tRef.OwnerId, tRef.Slot);
                    if (pokemon == null || pokemon.IsFainted) continue;

                    ApplyStatChange(tRef.OwnerId, pokemon, statIdx.Value, sc.Stages, events);
                }
            }
            if (!hasEffectStr) return;
        }

        if (!hasEffectStr) return;

        // Weather-setting moves
        if (effect is "sun" or "rain" or "sandstorm" or "hail" or "snow")
        {
            var newWeather = effect switch
            {
                "sun"       => WeatherCondition.Sun,
                "rain"      => WeatherCondition.Rain,
                "sandstorm" => WeatherCondition.Sandstorm,
                "snow"      => WeatherCondition.Snow, // Gen 9 Snowscape
                "hail"      => WeatherCondition.Snow, // Treat legacy Hail as Snow
                _           => WeatherCondition.None,
            };
            session.Weather          = newWeather;
            session.WeatherTurnsLeft = 5;
            events.Add(new WeatherChangedEvent { NewWeather = newWeather, TurnsLeft = 5, Message = $"The weather changed to {newWeather}!" });
            return;
        }

        // Terrain-setting moves
        if (effect is "grassy-terrain" or "electric-terrain" or "psychic-terrain" or "misty-terrain")
        {
            var newTerrain = effect switch
            {
                "grassy-terrain"   => TerrainCondition.Grassy,
                "electric-terrain" => TerrainCondition.Electric,
                "psychic-terrain"  => TerrainCondition.Psychic,
                "misty-terrain"    => TerrainCondition.Misty,
                _                  => TerrainCondition.None,
            };
            session.Terrain          = newTerrain;
            session.TerrainTurnsLeft = TerrainDuration;
            events.Add(new TerrainChangedEvent { NewTerrain = newTerrain, TurnsLeft = TerrainDuration });
            return;
        }

        // Stat changes (self: "atk+2", "def-1" etc.) - Fallback for old effect string
        if (move.StatChanges == null || move.StatChanges.Count == 0)
        {
            if (effect.Length >= 4 && (effect.Contains('+') || effect.Contains('-')))
            {
                bool raise   = effect.Contains('+');
                int sepIdx   = effect.IndexOf(raise ? '+' : '-');
                string stat  = effect[..sepIdx];
                int stages   = int.Parse(effect[(sepIdx + 1)..]) * (raise ? 1 : -1);

                // Determine if targeting self or opponents (BUG-06 fix)
                var statTargets = effect.StartsWith("self")
                    ? new List<TargetRef> { new(actingPlayerId, sourceSlot) }
                    : targets;

                foreach (var tRef in statTargets)
                {
                    var pokemon = GetActiveSlot(session, tRef.OwnerId, tRef.Slot);
                    if (pokemon == null || pokemon.IsFainted) continue;

                    var statIdx = stat switch
                    {
                        "atk" => StatIndex.ATK,
                        "def" => StatIndex.DEF,
                        "spa" => StatIndex.SPA,
                        "spd" => StatIndex.SPD,
                        "spe" => StatIndex.SPE,
                        "acc" => StatIndex.ACC,
                        "eva" => StatIndex.EVA,
                        _     => (StatIndex?)null,
                    };
                    if (statIdx == null) continue;

                    ApplyStatChange(tRef.OwnerId, pokemon, statIdx.Value, stages, events);
                }
                return;
            }
        }

        // Status infliction
        foreach (var tRef in targets)
        {
            var pokemon = GetActiveSlot(session, tRef.OwnerId, tRef.Slot);
            if (pokemon == null || pokemon.IsFainted) continue;
            TryInflictStatus(session, tRef.OwnerId, pokemon, effect, events);
        }
    }

    private void TryInflictStatus(
        BattleSession session, string ownerId,
        BattlePokemonSnapshot pokemon, string effect, List<BattleEvent> events)
    {
        if (pokemon.NonVolatileStatus != PokemonStatusCondition.None) return;

        // Misty Terrain: blocks all status on grounded Pokemon
        if (session.Terrain == TerrainCondition.Misty && IsGrounded(pokemon))
        {
            events.Add(new StatusBlockedEvent { PokemonName = pokemon.SpeciesName, Reason = "Misty Terrain" });
            return;
        }

        // Electric Terrain: blocks sleep on grounded Pokemon
        if (effect == "sleep" && session.Terrain == TerrainCondition.Electric && IsGrounded(pokemon))
        {
            events.Add(new StatusBlockedEvent { PokemonName = pokemon.SpeciesName, Reason = "Electric Terrain" });
            return;
        }

        var status = effect switch
        {
            "burn"      => PokemonStatusCondition.Burn,
            "paralysis" => PokemonStatusCondition.Paralysis,
            "sleep"     => PokemonStatusCondition.Sleep,
            "poison"    => PokemonStatusCondition.Poison,
            "toxic"     => PokemonStatusCondition.Toxic,
            "freeze"    => PokemonStatusCondition.Freeze,
            _           => (PokemonStatusCondition?)null,
        };

        if (status == null)
        {
            if (effect == "confusion" && !pokemon.IsConfused)
            {
                pokemon.IsConfused = true;
                pokemon.ConfusionTurnsLeft = _rng.Next(2, 5);
                events.Add(new MessageEvent { Message = $"{pokemon.SpeciesName} became confused!" });
            }
            return;
        }

        // Type immunities
        if (status == PokemonStatusCondition.Burn &&
            (pokemon.Type1.Equals("fire", StringComparison.OrdinalIgnoreCase) ||
             pokemon.Type2?.Equals("fire", StringComparison.OrdinalIgnoreCase) == true))
        {
            events.Add(new StatusBlockedEvent { PokemonName = pokemon.SpeciesName, Reason = "immune" });
            return;
        }
        if (status == PokemonStatusCondition.Paralysis &&
            (pokemon.Type1.Equals("electric", StringComparison.OrdinalIgnoreCase) ||
             pokemon.Type2?.Equals("electric", StringComparison.OrdinalIgnoreCase) == true))
        {
            events.Add(new StatusBlockedEvent { PokemonName = pokemon.SpeciesName, Reason = "immune" });
            return;
        }
        if ((status == PokemonStatusCondition.Poison || status == PokemonStatusCondition.Toxic) &&
            (pokemon.Type1.Equals("poison", StringComparison.OrdinalIgnoreCase) ||
             pokemon.Type2?.Equals("poison", StringComparison.OrdinalIgnoreCase) == true ||
             pokemon.Type1.Equals("steel", StringComparison.OrdinalIgnoreCase) ||
             pokemon.Type2?.Equals("steel", StringComparison.OrdinalIgnoreCase) == true))
        {
            events.Add(new StatusBlockedEvent { PokemonName = pokemon.SpeciesName, Reason = "immune" });
            return;
        }

        // Gen 9: Ice types cannot be frozen
        if (status == PokemonStatusCondition.Freeze &&
            (pokemon.Type1.Equals("ice", StringComparison.OrdinalIgnoreCase) ||
             pokemon.Type2?.Equals("ice", StringComparison.OrdinalIgnoreCase) == true))
        {
            events.Add(new StatusBlockedEvent { PokemonName = pokemon.SpeciesName, Reason = "immune" });
            return;
        }

        pokemon.NonVolatileStatus = status.Value;

        if (status == PokemonStatusCondition.Sleep)
            pokemon.SleepTurnsLeft = _rng.Next(1, 4);

        events.Add(new StatusInflictedEvent
        {
            PlayerId    = ownerId,
            PokemonName = pokemon.SpeciesName,
            Status      = status.Value,
        });
    }

    private void ApplyStatChange(
        string ownerId, BattlePokemonSnapshot pokemon,
        StatIndex stat, int stages, List<BattleEvent> events)
    {
        int current = pokemon.StatStages[(int)stat];
        int clamped = Math.Clamp(current + stages, -6, 6);

        if (clamped == current)
        {
            events.Add(new StatChangeBlockedEvent
            {
                PokemonName = pokemon.SpeciesName,
                Stat        = stat,
                Reason      = stages > 0 ? "already at max" : "already at min",
            });
            return;
        }

        pokemon.StatStages[(int)stat] = clamped;
        events.Add(new StatChangeEvent
        {
            PlayerId    = ownerId,
            PokemonName = pokemon.SpeciesName,
            Stat        = stat,
            Stages      = clamped - current,
            NewStage    = clamped,
        });
    }

    private (string? effect, int chance) GetEffectiveMoveEffect(MoveEntry move)
    {
        // 1. Database-defined effect
        if (!string.IsNullOrEmpty(move.Effect))
        {
            return (move.Effect, move.EffectChance > 0 ? move.EffectChance : 100);
        }

        // 2. Hardcoded logic for iconic moves (fallback if DB incomplete)
        string name = move.Name.ToLower();
        
        // Flinch moves
        if (name.Contains("iron-head") || name.Contains("rock-slide") || name.Contains("air-slash") || name.Contains("bite") || name.Contains("fake-out"))
            return ("flinch", name.Contains("fake-out") ? 100 : 30);

        // Status infliction
        if (name.Contains("flamethrower") || name.Contains("fire-blast") || name.Contains("heat-wave") || name.Contains("ember"))
            return ("burn", 10);
        if (name.Contains("thunderbolt") || name.Contains("thunder") || name.Contains("discharge") || name.Contains("thundershock"))
            return ("paralysis", 10);
        if (name.Contains("ice-beam") || name.Contains("blizzard") || name.Contains("powder-snow"))
            return ("freeze", 10);
        if (name.Contains("sludge-bomb") || name.Contains("poison-jab") || name.Contains("sludge-wave"))
            return ("poison", 30);
        
        // Guaranteed status (Status category usually handled in ApplyStatusMoveEffect, but can be secondary here)
        if (name.Contains("nuzzle")) return ("paralysis", 100);
        
        return (null, 0);
    }

    // ── End-of-turn effects ──────────────────────────────────────────────────

    private void ResolveEndOfTurn(BattleSession session, List<BattleEvent> events)
    {
        // Weather tick
        if (session.Weather != WeatherCondition.None && session.WeatherTurnsLeft > 0)
        {
            session.WeatherTurnsLeft--;
            if (session.WeatherTurnsLeft == 0)
            {
                events.Add(new WeatherEndedEvent { EndedWeather = session.Weather });
                session.Weather = WeatherCondition.None;
            }
        }

        // Terrain tick
        if (session.Terrain != TerrainCondition.None && session.TerrainTurnsLeft > 0)
        {
            session.TerrainTurnsLeft--;
            if (session.TerrainTurnsLeft == 0)
            {
                events.Add(new TerrainEndedEvent { EndedTerrain = session.Terrain });
                session.Terrain = TerrainCondition.None;
            }
        }

        // Apply EOT effects to all active Pokemon
        foreach (var (playerId, slot) in AllActiveSlots(session))
        {
            var pokemon = GetActiveSlot(session, playerId, slot);
            if (pokemon == null || pokemon.IsFainted) continue;

            // Weather damage
            // Sandstorm: damages non-Rock/Steel/Ground
            if (session.Weather == WeatherCondition.Sandstorm &&
                !SandstormImmune.Contains(pokemon.Type1) &&
                (pokemon.Type2 == null || !SandstormImmune.Contains(pokemon.Type2)))
            {
                int dmg = Math.Max(1, pokemon.MaxHp / 16);
                ApplyDamage(session, playerId, pokemon, dmg, events, false, 1.0);
                if (!pokemon.IsFainted)
                    events.Add(new WeatherDamageEvent { PlayerId = playerId, PokemonName = pokemon.SpeciesName, Damage = dmg, Weather = WeatherCondition.Sandstorm });
            }
            // Snow / Hail (Gen 9): no EOT damage — Ice Sp.Def buff handled in CalculateDamage

            // Grassy Terrain: heal grounded Pokemon 1/16 HP
            if (!pokemon.IsFainted && session.Terrain == TerrainCondition.Grassy && IsGrounded(pokemon))
            {
                int heal = Math.Max(1, pokemon.MaxHp / 16);
                int before = pokemon.CurrentHp;
                pokemon.CurrentHp = Math.Min(pokemon.MaxHp, pokemon.CurrentHp + heal);
                int actual = pokemon.CurrentHp - before;
                if (actual > 0)
                    events.Add(new TerrainHealEvent { PlayerId = playerId, PokemonName = pokemon.SpeciesName, HealAmount = actual });
            }

            if (pokemon.IsFainted) continue;

            // Burn: 1/16 max HP
            if (pokemon.NonVolatileStatus == PokemonStatusCondition.Burn)
            {
                int dmg = Math.Max(1, pokemon.MaxHp / 16);
                ApplyDamage(session, playerId, pokemon, dmg, events, false, 1.0);
            }

            // Poison: 1/8 max HP
            if (!pokemon.IsFainted && pokemon.NonVolatileStatus == PokemonStatusCondition.Poison)
            {
                int dmg = Math.Max(1, pokemon.MaxHp / 8);
                ApplyDamage(session, playerId, pokemon, dmg, events, false, 1.0);
            }

            // Toxic: escalating (1/16 * counter, capped at 15/16)
            if (!pokemon.IsFainted && pokemon.NonVolatileStatus == PokemonStatusCondition.Toxic)
            {
                pokemon.ToxicCounter = Math.Min(pokemon.ToxicCounter + 1, 15);
                int dmg = Math.Max(1, pokemon.MaxHp * pokemon.ToxicCounter / 16);
                ApplyDamage(session, playerId, pokemon, dmg, events, false, 1.0);
            }
        }
    }

    // ── Win condition & forced switch detection ──────────────────────────────

    private string? CheckWinCondition(BattleSession session)
    {
        if (session.Team1.Count == 0 || session.Team2.Count == 0) return null;
        
        bool p1Lost = session.Team1.All(p => p.IsFainted || p.CurrentHp <= 0);
        bool p2Lost = session.Team2.All(p => p.IsFainted || p.CurrentHp <= 0);

        if (p1Lost || p2Lost) {
            Console.WriteLine($"[Battle] WinCheck: P1Lost={p1Lost} (HP: {string.Join(",", session.Team1.Select(p=>p.CurrentHp))}), P2Lost={p2Lost} (HP: {string.Join(",", session.Team2.Select(p=>p.CurrentHp))})");
        }

        if (p1Lost && p2Lost) return "draw";
        if (p1Lost) return session.Player2Id;
        if (p2Lost) return session.Player1Id;
        return null;
    }

    private List<ForcedSwitchSlot> CollectFaintedSlots(BattleSession session)
    {
        var result = new List<ForcedSwitchSlot>();

        foreach (var (playerId, slot) in AllActiveSlots(session))
        {
            var pokemon = GetActiveSlot(session, playerId, slot);
            if (pokemon == null || !pokemon.IsFainted) continue;

            // Only request switch if there's a non-fainted, non-active Pokemon available
            var team    = playerId == session.Player1Id ? session.Team1 : session.Team2;
            int activeA = GetActiveIndex(session, playerId, 0);
            int activeB = GetActiveIndex(session, playerId, 1);

            bool hasReplacement = team
                .Select((p, i) => (p, i))
                .Any(x => !x.p.IsFainted && x.i != activeA && x.i != activeB);

            if (hasReplacement)
                result.Add(new ForcedSwitchSlot { PlayerId = playerId, Slot = slot });
        }

        return result;
    }

    // ── Result builder ───────────────────────────────────────────────────────

    private BattleTurnResult BuildTurnResult(
        BattleSession session, List<BattleEvent> events,
        string? winner, List<ForcedSwitchSlot> forcedSlots)
    {
        return new BattleTurnResult
        {
            BattleId           = session.BattleId,
            ResolvedTurnNumber = session.TurnNumber,
            NextTurnNumber     = session.TurnNumber + 1,
            State              = session.State,
            WinnerPlayerId     = winner,
            TypedEvents        = events,
            Events             = events.Select(e => e.EventType).ToList(),
            ActiveIndex1       = session.ActiveIndex1,
            ActiveIndex1b      = session.ActiveIndex1b,
            ActiveIndex2       = session.ActiveIndex2,
            ActiveIndex2b      = session.ActiveIndex2b,
            ActiveHp1          = GetActiveHp(session, session.Player1Id, 0),
            ActiveHp1b         = GetActiveHp(session, session.Player1Id, 1),
            ActiveHp2          = GetActiveHp(session, session.Player2Id, 0),
            ActiveHp2b         = GetActiveHp(session, session.Player2Id, 1),
            Weather            = session.Weather.ToString().ToLower(),
            WeatherTurnsLeft   = session.WeatherTurnsLeft,
            Terrain            = session.Terrain.ToString().ToLower(),
            TerrainTurnsLeft   = session.TerrainTurnsLeft,
            Team1Hp            = session.Team1.Select(p => p.CurrentHp).ToList(),
            Team2Hp            = session.Team2.Select(p => p.CurrentHp).ToList(),
            ForcedSwitches     = forcedSlots,
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Team loading & stat computation
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<List<BattlePokemonSnapshot>> LoadTeamSnapshots(string playerId)
    {
        var party = await _db.PokemonInstances
            .Find(p => p.OwnerId == playerId && p.IsInParty)
            .ToListAsync();

        party = [.. party.OrderBy(p => p.PartySlot)];

        var snapshots = new List<BattlePokemonSnapshot>();

        foreach (var inst in party)
        {
            var dex = await _db.Pokedex.Find(d => d.Id == inst.SpeciesId).FirstOrDefaultAsync();
            if (dex == null) continue;

            int baseHp  = dex.BaseStats.GetValueOrDefault("hp", 45);
            int baseAtk = dex.BaseStats.GetValueOrDefault("attack", 50);
            int baseDef = dex.BaseStats.GetValueOrDefault("defense", 50);
            int baseSpa = dex.BaseStats.GetValueOrDefault("special-attack", 50);
            int baseSpd = dex.BaseStats.GetValueOrDefault("special-defense", 50);
            int baseSpePoke = dex.BaseStats.GetValueOrDefault("speed", 50);

            double atkMult = 1.0, defMult = 1.0, spaMult = 1.0, spdMult = 1.0, speMult = 1.0;
            if (NatureEffects.TryGetValue(inst.Nature ?? "", out var nat))
            {
                double Mod(StatIndex s, StatIndex up, StatIndex dn)
                    => s == up ? 1.1 : s == dn ? 0.9 : 1.0;
                atkMult = Mod(StatIndex.ATK, nat.Up, nat.Down);
                defMult = Mod(StatIndex.DEF, nat.Up, nat.Down);
                spaMult = Mod(StatIndex.SPA, nat.Up, nat.Down);
                spdMult = Mod(StatIndex.SPD, nat.Up, nat.Down);
                speMult = Mod(StatIndex.SPE, nat.Up, nat.Down);
            }

            int hp = ComputeHp(baseHp, inst.Ivs.Hp, inst.Evs.Hp);
            // Clamp current HP in case it exceeds the level-50 max
            int currentHp = Math.Min(inst.CurrentHp, hp);

            string type1 = dex.Types.Count > 0 ? dex.Types[0] : "normal";
            string? type2 = dex.Types.Count > 1 ? dex.Types[1] : null;

            // Ưu tiên: dex.Name → Nickname → "Pokemon#ID"
            string speciesName = !string.IsNullOrEmpty(dex.Name)    ? dex.Name
                               : !string.IsNullOrEmpty(inst.Nickname) ? inst.Nickname
                               : $"Pokemon#{inst.SpeciesId}";

            var snap = new BattlePokemonSnapshot
            {
                InstanceId  = inst.Id,
                SpeciesId   = inst.SpeciesId,
                SpeciesName = speciesName,
                Nickname    = !string.IsNullOrEmpty(inst.Nickname) ? inst.Nickname : speciesName,
                Level       = BattleLevel,
                MaxHp       = hp,
                CurrentHp   = currentHp,
                Type1       = type1,
                Type2       = type2,
                OrigType1   = type1,
                OrigType2   = type2,
                // Default Tera Type = primary type (can be overridden via held-item data later)
                TerType     = type1,
                Atk        = ComputeStat(baseAtk, inst.Ivs.Atk, inst.Evs.Atk, atkMult),
                Def        = ComputeStat(baseDef, inst.Ivs.Def, inst.Evs.Def, defMult),
                SpAtk      = ComputeStat(baseSpa, inst.Ivs.SpAtk, inst.Evs.SpAtk, spaMult),
                SpDef      = ComputeStat(baseSpd, inst.Ivs.SpDef, inst.Evs.SpDef, spdMult),
                Spd        = ComputeStat(baseSpePoke, inst.Ivs.Spd, inst.Evs.Spd, speMult),
                NonVolatileStatus = PokemonStatusCondition.None,
                Moves      = inst.Moves.Select(m => new PokemonMove
                {
                    MoveId    = m.MoveId,
                    MoveName  = m.MoveName,
                    MoveType  = m.MoveType,
                    Category  = m.Category,
                    MaxPp     = m.MaxPp > 0 ? m.MaxPp : 15,
                    CurrentPp = m.CurrentPp > 0 ? m.CurrentPp : (m.MaxPp > 0 ? m.MaxPp : 15),
                }).ToList(),
            };

            snapshots.Add(snap);
        }

        return snapshots;
    }

    private static int ComputeHp(int baseStat, int iv, int ev)
        => (int)Math.Floor((2.0 * baseStat + iv + Math.Floor(ev / 4.0)) * BattleLevel / 100.0) + BattleLevel + 10;

    private static int ComputeStat(int baseStat, int iv, int ev, double natureMult)
        => (int)Math.Floor(
            (int)Math.Floor((2.0 * baseStat + iv + Math.Floor(ev / 4.0)) * BattleLevel / 100.0 + 5) * natureMult);

    private async Task PreloadMoveData(IEnumerable<BattlePokemonSnapshot> snapshots)
    {
        var ids = snapshots.SelectMany(p => p.Moves.Select(m => m.MoveId)).Distinct()
            .Where(id => !_moveCache.ContainsKey(id)).ToList();

        if (ids.Count == 0) return;

        var entries = await _db.Moves.Find(m => ids.Contains(m.Id)).ToListAsync();
        foreach (var e in entries)
            _moveCache[e.Id] = e;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helper accessors
    // ═══════════════════════════════════════════════════════════════════════

    private BattleSession GetSessionOrThrow(string battleId)
        => _sessions.TryGetValue(battleId, out var s) ? s
            : throw new KeyNotFoundException($"Battle '{battleId}' not found.");

    private string GetOpponentId(BattleSession session, string playerId)
        => playerId == session.Player1Id ? session.Player2Id : session.Player1Id;

    private int GetActiveIndex(BattleSession session, string playerId, int slot)
    {
        bool isP1 = playerId == session.Player1Id;
        return slot == 0
            ? (isP1 ? session.ActiveIndex1  : session.ActiveIndex2)
            : (isP1 ? session.ActiveIndex1b : session.ActiveIndex2b);
    }

    private void SetActiveIndex(BattleSession session, string playerId, int slot, int idx)
    {
        bool isP1 = playerId == session.Player1Id;
        if (slot == 0) { if (isP1) session.ActiveIndex1  = idx; else session.ActiveIndex2  = idx; }
        else           { if (isP1) session.ActiveIndex1b = idx; else session.ActiveIndex2b = idx; }
    }

    public BattlePokemonSnapshot? GetActiveSlot(BattleSession session, string playerId, int slot)
    {
        var team = playerId == session.Player1Id ? session.Team1 : session.Team2;
        int idx  = GetActiveIndex(session, playerId, slot);
        if (idx < 0 || idx >= team.Count) return null;
        return team[idx];
    }

    private PokemonSlotDto MapSlot(BattlePokemonSnapshot? p)
    {
        if (p == null) return null;
        return new PokemonSlotDto
        {
            SpeciesId = p.SpeciesId,
            SpeciesName = p.SpeciesName ?? p.Nickname ?? "Unknown",
            Level = p.Level,
            MaxHp = p.MaxHp,
            CurrentHp = p.CurrentHp,
            Type1 = p.Type1,
            Type2 = p.Type2,
            Status = p.NonVolatileStatus.ToString(),
            TerType = p.TerType,
            IsTerastallized = p.IsTerastallized,
            Moves = p.Moves.Select(m => new MoveDto
            {
                MoveId = m.MoveId,
                Name = m.MoveName ?? "Move",
                Type = m.MoveType ?? "normal",
                Category = m.Category ?? "Physical",
                MaxPp = m.MaxPp,
                CurrentPp = m.CurrentPp,
                TargetType = 0 // Default for now
            }).ToList()
        };
    }

    private int GetActiveHp(BattleSession session, string playerId, int slot)
        => GetActiveSlot(session, playerId, slot)?.CurrentHp ?? 0;

    // All (playerId, slot) combos for the 4 active positions
    private IEnumerable<(string playerId, int slot)> AllActiveSlots(BattleSession session)
    {
        yield return (session.Player1Id, 0);
        yield return (session.Player1Id, 1);
        yield return (session.Player2Id, 0);
        yield return (session.Player2Id, 1);
    }

    private bool AllActionsSubmitted(BattleSession session)
    {
        foreach (var (playerId, slot) in AllActiveSlots(session))
        {
            var pokemon = GetActiveSlot(session, playerId, slot);
            if (pokemon != null && !pokemon.IsFainted &&
                !session.PendingActions.ContainsKey($"{playerId}:{slot}"))
                return false;
        }
        return true;
    }

    private void ValidateAction(BattleSession session, string playerId, BattleAction action)
    {
        var actor = GetActiveSlot(session, playerId, action.SourceIndex);
        if (actor == null || actor.IsFainted)
            throw new InvalidOperationException("Source slot has no active Pokemon.");

        if (action.Type == BattleActionType.Switch)
        {
            if (!action.SwitchIndex.HasValue)
                throw new ArgumentException("Switch action requires SwitchIndex.");
        }
        else
        {
            if (!action.MoveSlot.HasValue || action.MoveSlot.Value < 0 || action.MoveSlot.Value >= actor.Moves.Count)
                throw new ArgumentException("Invalid MoveSlot.");
        }
    }

    // ── Terastallization ─────────────────────────────────────────────────────

    private void ApplyTerastallization(
        BattleSession session, string playerId,
        BattlePokemonSnapshot pokemon, List<BattleEvent> events)
    {
        bool alreadyUsed = playerId == session.Player1Id ? session.TeraUsed1 : session.TeraUsed2;
        if (alreadyUsed || pokemon.IsTerastallized) return;

        // Lock original types if not yet saved (OrigType1/2 should already be set at load)
        pokemon.IsTerastallized = true;
        // Type1 → TerType, Type2 → null (defensive type becomes TerType only)
        pokemon.Type1 = pokemon.TerType;
        pokemon.Type2 = null;

        if (playerId == session.Player1Id) session.TeraUsed1 = true;
        else                               session.TeraUsed2 = true;

        events.Add(new TerastallizationEvent
        {
            PlayerId    = playerId,
            PokemonName = pokemon.SpeciesName,
            TerType     = pokemon.TerType,
        });
    }

    // ── Terrain helpers ──────────────────────────────────────────────────────

    private static bool IsGrounded(BattlePokemonSnapshot pokemon)
        => !pokemon.Type1.Equals("flying", StringComparison.OrdinalIgnoreCase)
        && (pokemon.Type2 == null || !pokemon.Type2.Equals("flying", StringComparison.OrdinalIgnoreCase));

    private static double GetTerrainDamageModifier(
        BattleSession session, BattlePokemonSnapshot attacker,
        BattlePokemonSnapshot defender, string moveType)
    {
        if (session.Terrain == TerrainCondition.None) return 1.0;

        return session.Terrain switch
        {
            // Grassy: Grass 1.3×; Ground moves (Earthquake) 0.5×
            TerrainCondition.Grassy when moveType.Equals("grass", StringComparison.OrdinalIgnoreCase)   => 1.3,
            TerrainCondition.Grassy when moveType.Equals("ground", StringComparison.OrdinalIgnoreCase)  => 0.5,
            // Electric: Electric 1.3× (attacker grounded)
            TerrainCondition.Electric when IsGrounded(attacker)
                                       && moveType.Equals("electric", StringComparison.OrdinalIgnoreCase) => 1.3,
            // Psychic: Psychic 1.3× (attacker grounded)
            TerrainCondition.Psychic when IsGrounded(attacker)
                                       && moveType.Equals("psychic", StringComparison.OrdinalIgnoreCase)  => 1.3,
            // Misty: Dragon 0.5× vs grounded defender
            TerrainCondition.Misty when IsGrounded(defender)
                                     && moveType.Equals("dragon", StringComparison.OrdinalIgnoreCase)    => 0.5,
            _ => 1.0,
        };
    }

    // ── Stat helpers ─────────────────────────────────────────────────────────

    private static int GetEffectiveStat(BattlePokemonSnapshot pokemon, StatIndex stat)
    {
        int base_ = stat switch
        {
            StatIndex.ATK => pokemon.Atk,
            StatIndex.DEF => pokemon.Def,
            StatIndex.SPA => pokemon.SpAtk,
            StatIndex.SPD => pokemon.SpDef,
            StatIndex.SPE => pokemon.Spd,
            _             => 0,
        };
        return Math.Max(1, (int)(base_ * pokemon.GetStageMultiplier(stat)));
    }

    private static int GetEffectiveSpeed(BattlePokemonSnapshot pokemon)
    {
        int spd = GetEffectiveStat(pokemon, StatIndex.SPE);
        // Paralysis halves speed
        if (pokemon.NonVolatileStatus == PokemonStatusCondition.Paralysis)
            spd /= 2;
        return Math.Max(1, spd);
    }

    // ── Type helpers ─────────────────────────────────────────────────────────

    private double GetTypeEffectiveness(string moveType, BattlePokemonSnapshot defender)
    {
        double mult = GetSingleTypeEff(moveType, defender.Type1);
        if (!string.IsNullOrEmpty(defender.Type2))
            mult *= GetSingleTypeEff(moveType, defender.Type2);
        return mult;
    }

    private static double GetSingleTypeEff(string atkType, string defType)
    {
        if (TypeChart.TryGetValue(atkType, out var chart) && chart.TryGetValue(defType, out var m))
            return m;
        return 1.0;
    }

    private static double GetStab(BattlePokemonSnapshot attacker, string moveType)
    {
        bool isOrigType = attacker.OrigType1.Equals(moveType, StringComparison.OrdinalIgnoreCase)
                       || (attacker.OrigType2 != null && attacker.OrigType2.Equals(moveType, StringComparison.OrdinalIgnoreCase));

        if (attacker.IsTerastallized)
        {
            bool isTerType = attacker.TerType.Equals(moveType, StringComparison.OrdinalIgnoreCase);
            if (isTerType && isOrigType) return 2.0; // Tera overlap bonus
            if (isTerType)              return 1.5; // Tera STAB
            if (isOrigType)             return 1.5; // Original type STAB preserved post-Tera
            return 1.0;
        }

        return isOrigType ? 1.5 : 1.0;
    }

    private static double GetWeatherModifier(BattleSession session, string moveType)
        => session.Weather switch
        {
            WeatherCondition.Sun  => moveType.Equals("fire",  StringComparison.OrdinalIgnoreCase) ? 1.5
                                   : moveType.Equals("water", StringComparison.OrdinalIgnoreCase) ? 0.5 : 1.0,
            WeatherCondition.Rain => moveType.Equals("water", StringComparison.OrdinalIgnoreCase) ? 1.5
                                   : moveType.Equals("fire",  StringComparison.OrdinalIgnoreCase) ? 0.5 : 1.0,
            _ => 1.0,
        };

    public (BattleSession session, BattleTurnResult result) Surrender(string battleId, string playerId)
    {
        var session = GetSessionOrThrow(battleId);
        string winnerId = session.Player1Id == playerId ? session.Player2Id : session.Player1Id;
        
        session.State = BattleState.Ended;
        session.WinnerPlayerId = winnerId;
        
        var result = new BattleTurnResult
        {
            State = "Ended",
            WinnerPlayerId = winnerId,
            Events = new List<BattleEvent> {
                new BattleEndEvent { WinnerPlayerId = winnerId, Reason = "Surrender", Message = $"[TRAINER] surrendered!" }
            }
        };
        return (session, result);
    }
}
