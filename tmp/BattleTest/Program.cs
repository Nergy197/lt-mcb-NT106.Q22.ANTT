using Microsoft.Extensions.Options;
using PokemonMMO.Data;
using PokemonMMO.Models;
using PokemonMMO.Options;
using PokemonMMO.Services;

// ── Setup ────────────────────────────────────────────────────────────────────
Console.OutputEncoding = System.Text.Encoding.UTF8;

var db = new MongoDbContext("mongodb://localhost:27017", "pokemon_mmo");

var opts = Options.Create(new BattleOptions
{
    TurnTimeoutSeconds   = 30,
    SwitchActionPriority = 6,
    DamageRandomMin      = 0.92,  // narrow range for reproducibility
    DamageRandomMax      = 0.96,
});

var svc = new BattleService(db, opts);

string p1Id = "69dbbbf43c3d19044eecdad3"; // cuong
string p2Id = "69dbbbf43c3d19044eecdad5"; // ai_bot

static void Sep(string title = "")
{
    if (title.Length > 0)
        Console.WriteLine($"\n{'─',1}{'─',1} {title} {'─',50}".PadRight(70,'─'));
    else
        Console.WriteLine(new string('─', 70));
}

void PrintField(BattleSession s)
{
    void Slot(string label, BattlePokemonSnapshot? p)
    {
        if (p == null) { Console.WriteLine($"  {label}: [empty]"); return; }
        string tera   = p.IsTerastallized ? $" [TERA:{p.TerType.ToUpper()}]" : "";
        string status = p.NonVolatileStatus != PokemonStatusCondition.None ? $" ({p.NonVolatileStatus})" : "";
        Console.WriteLine($"  {label}: {p.SpeciesName,-12} {p.CurrentHp,4}/{p.MaxHp,-4} HP{status}{tera}");
    }

    Sep("FIELD");
    Console.WriteLine($"  Weather: {s.Weather}({s.WeatherTurnsLeft})  Terrain: {s.Terrain}({s.TerrainTurnsLeft})");
    Console.WriteLine($"  ── cuong (P1) ──");
    Slot("SlotA", svc.GetActiveSlot(s, p1Id, 0));
    Slot("SlotB", svc.GetActiveSlot(s, p1Id, 1));
    Console.WriteLine($"  ── ai_bot (P2) ──");
    Slot("SlotA", svc.GetActiveSlot(s, p2Id, 0));
    Slot("SlotB", svc.GetActiveSlot(s, p2Id, 1));
}

static void PrintEvents(BattleTurnResult r)
{
    Sep($"TURN {r.ResolvedTurnNumber} EVENTS");
    foreach (var ev in r.TypedEvents)
    {
        string line = ev switch
        {
            MoveUsedEvent e       => $"  🎯 {e.PokemonName} used {e.MoveName}",
            MoveMissedEvent e     => $"  ✗  {e.PokemonName}'s {e.MoveName} missed!",
            MoveNoEffectEvent e   => $"  ∅  No effect on {e.TargetName}",
            PokemonDamageEvent e  => $"  💥 {e.PokemonName} took {e.Damage} dmg  → {e.HpAfter}/{e.MaxHp} HP" +
                                      (e.IsCritical ? " (CRIT!)" : "") +
                                      (e.TypeMultiplier != 1.0 ? $" x{e.TypeMultiplier:F1}" : ""),
            PokemonFaintEvent e   => $"  ☠  {e.PokemonName} fainted!",
            SwitchEvent e         => $"  🔄 {e.WithdrawnPokemonName} → {e.SentOutPokemonName}",
            StatusInflictedEvent e=> $"  🌡  {e.PokemonName} got {e.Status}!",
            ParalysisStuckEvent e => $"  ⚡ {e.PokemonName} is paralyzed!",
            SleepSkipEvent e      => $"  💤 {e.PokemonName} is asleep...",
            WeatherChangedEvent e => $"  🌦  Weather → {e.NewWeather}",
            WeatherEndedEvent e   => $"  ☀  {e.EndedWeather} ended",
            WeatherDamageEvent e  => $"  🌪  {e.PokemonName} hurt by {e.Weather}: -{e.Damage}",
            TerrainChangedEvent e => $"  🌱 Terrain → {e.NewTerrain}",
            TerastallizationEvent e => $"  💎 {e.PokemonName} Terastallized into {e.TerType.ToUpper()}!",
            SuperEffectiveEvent e => $"  ⚡ Super effective! (x{e.Multiplier:F1})",
            NotVeryEffectiveEvent e=> $"  🛡  Not very effective... (x{e.Multiplier:F1})",
            StatChangeEvent e     => $"  📈 {e.PokemonName} {e.Stat} {(e.Stages>0?"+":"")}{e.Stages} (→ {e.NewStage})",
            MessageEvent e        => $"     {e.Message}",
            _                     => $"  [{ev.EventType}]",
        };
        Console.WriteLine(line);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// START
// ═══════════════════════════════════════════════════════════════════════════
Sep("BATTLE SIM — cuong vs ai_bot (VGC Double)");

// ── 1. Create Battle ──────────────────────────────────────────────────────
var session = await svc.CreateBattle(p1Id, p2Id);
Console.WriteLine($"\nBattle ID: {session.BattleId}");
Console.WriteLine($"State: {session.State}");

Console.WriteLine("\n[cuong's team]");
foreach (var p in session.Team1)
    Console.WriteLine($"  #{p.SpeciesId} {p.SpeciesName,-12} | {p.Type1}{(p.Type2 != null ? "/" + p.Type2 : ""),-14} | HP:{p.MaxHp}  Atk:{p.Atk}  Def:{p.Def}  SpA:{p.SpAtk}  SpD:{p.SpDef}  Spe:{p.Spd}");

Console.WriteLine("\n[ai_bot's team]");
foreach (var p in session.Team2)
    Console.WriteLine($"  #{p.SpeciesId} {p.SpeciesName,-12} | {p.Type1}{(p.Type2 != null ? "/" + p.Type2 : ""),-14} | HP:{p.MaxHp}  Atk:{p.Atk}  Def:{p.Def}  SpA:{p.SpAtk}  SpD:{p.SpDef}  Spe:{p.Spd}");

// ── 2. Team Preview ───────────────────────────────────────────────────────
Sep("TEAM PREVIEW");
// cuong picks: Charizard(0), Lucario(2), Venusaur(1), Garchomp(3)
var (_, p1Started) = svc.SubmitTeamOrder(session.BattleId, p1Id, [0, 2, 1, 3]);
Console.WriteLine($"cuong picks: [Charizard, Lucario, Venusaur, Garchomp] → started={p1Started}");

// ai_bot picks: Mewtwo(0), Gardevoir(2), Steelix(1), Lugia(3)
var (s, p2Started) = svc.SubmitTeamOrder(session.BattleId, p2Id, [0, 2, 1, 3]);
Console.WriteLine($"ai_bot picks: [Mewtwo, Gardevoir, Steelix, Lugia] → started={p2Started}");

Console.WriteLine($"\nBattle state: {s.State}");
PrintField(s);

// ── Helper: submit a move action ─────────────────────────────────────────
async Task<BattleTurnResult?> Act(
    string bId, string playerId, int srcSlot, int moveSlot, int targetSlot,
    bool useTera = false)
{
    var action = new BattleAction
    {
        PlayerId    = playerId,
        Type        = BattleActionType.Move,
        SourceIndex = srcSlot,
        MoveSlot    = moveSlot,
        TargetSlot  = targetSlot,
        UseTera     = useTera,
    };
    var (_, result) = await svc.SubmitBattleAction(bId, playerId, action);
    return result;
}

async Task<BattleTurnResult?> Switch_(
    string bId, string playerId, int srcSlot, int switchTo)
{
    var action = new BattleAction
    {
        PlayerId    = playerId,
        Type        = BattleActionType.Switch,
        SourceIndex = srcSlot,
        SwitchIndex = switchTo,
    };
    var (_, result) = await svc.SubmitBattleAction(bId, playerId, action);
    return result;
}

string bid = session.BattleId;

// ══════════════════════════════════════════════════════════════════════════
// TURN 1 — Spread Earthquake vs Psychic + Flamethrower
// ══════════════════════════════════════════════════════════════════════════
// cuong: Charizard(SlotA) → Psychic(move3) → Mewtwo(oppA)
//        Lucario(SlotB)   → Earthquake(move4) spread → both opponents [TargetSlot=0 w/ SpreadOpponents]
// ai_bot: Mewtwo(SlotA)   → Psychic(move3) → Charizard(oppA)
//         Gardevoir(SlotB) → Flamethrower(move1) → Charizard(oppA)
Console.WriteLine("\n\n  cuong: Charizard uses Psychic on Mewtwo; Lucario uses Earthquake (spread)");
Console.WriteLine("  ai_bot: Mewtwo uses Psychic on Charizard; Gardevoir uses Flamethrower on Charizard");

await Act(bid, p1Id, srcSlot:0, moveSlot:2, targetSlot:0);          // Charizard → Psychic → opp SlotA
await Act(bid, p1Id, srcSlot:1, moveSlot:3, targetSlot:0);          // Lucario → Earthquake → spread
await Act(bid, p2Id, srcSlot:0, moveSlot:2, targetSlot:0);          // Mewtwo → Psychic → Charizard
var r1 = await Act(bid, p2Id, srcSlot:1, moveSlot:0, targetSlot:0); // Gardevoir → Flamethrower → Charizard

PrintEvents(r1!);
PrintField(svc.GetSession(bid)!);

// Resolve forced switches khi cần trước khi tiếp tục
void ResolveAllForcedSwitches()
{
    var cs = svc.GetSession(bid)!;
    if (cs.State != BattleState.ForcedSwitch) return;
    Sep($"⚑  FORCED SWITCH (turn {cs.TurnNumber})");
    foreach (var key in cs.PendingForcedSwitches.ToList())
    {
        var parts = key.Split(':');
        var pid   = parts[0];
        var slot  = int.Parse(parts[1]);
        var team  = pid == p1Id ? cs.Team1 : cs.Team2;
        int aA = pid == p1Id ? cs.ActiveIndex1  : cs.ActiveIndex2;
        int aB = pid == p1Id ? cs.ActiveIndex1b : cs.ActiveIndex2b;
        var rep = team.Select((p, i) => (p, i))
                      .FirstOrDefault(x => !x.p.IsFainted && x.i != aA && x.i != aB);
        if (rep.p == null) continue;
        Console.WriteLine($"  {(pid == p1Id ? "cuong" : "ai_bot")} slot {slot} → {rep.p.SpeciesName}");
        svc.SubmitForcedSwitch(bid, pid, slot, rep.i);
    }
    PrintField(svc.GetSession(bid)!);
}

// ══════════════════════════════════════════════════════════════════════════
// TURN 2 — cuong Terastallizes Charizard + Solar Beam; switch Steelix
// ══════════════════════════════════════════════════════════════════════════
ResolveAllForcedSwitches();
var s2 = svc.GetSession(bid)!;
if (s2.State == BattleState.Ended) goto done;
if (s2.State != BattleState.Running) goto autoFight;

Console.WriteLine("\n\n  cuong: Charizard TERA→Fire uses Flamethrower on Gardevoir; Lucario uses Earthquake");
Console.WriteLine("  ai_bot: Mewtwo uses Psychic on Lucario; Gardevoir switches → Steelix");

await Act(bid, p1Id, 0, 0, targetSlot:1, useTera:true); // Charizard Tera-Fire → Flamethrower → Gardevoir
await Act(bid, p1Id, 1, 3, targetSlot:0);                // Lucario Earthquake spread
await Switch_(bid, p2Id, srcSlot:1, switchTo:2);         // Gardevoir → Steelix (index 2 in team2)
var r2 = await Act(bid, p2Id, 0, 2, targetSlot:1);       // Mewtwo Psychic → Lucario

PrintEvents(r2!);
PrintField(svc.GetSession(bid)!);

// ══════════════════════════════════════════════════════════════════════════
// TURN 3 — Solar Beam + Earthquake; Mewtwo + Steelix
// ══════════════════════════════════════════════════════════════════════════
ResolveAllForcedSwitches();
var s3 = svc.GetSession(bid)!;
if (s3.State == BattleState.Ended) goto done;
if (s3.State != BattleState.Running) goto autoFight;

Console.WriteLine("\n\n  cuong: Charizard Solar Beam on Mewtwo; Lucario Psychic on Mewtwo");
Console.WriteLine("  ai_bot: Mewtwo Psychic on Charizard; Steelix Earthquake spread");

await Act(bid, p1Id, 0, 1, targetSlot:0); // Solar Beam → Mewtwo
await Act(bid, p1Id, 1, 2, targetSlot:0); // Lucario Psychic → Mewtwo
await Act(bid, p2Id, 0, 2, targetSlot:0); // Mewtwo Psychic → Charizard
var r3 = await Act(bid, p2Id, 1, 3, targetSlot:0); // Steelix Earthquake spread

PrintEvents(r3!);
PrintField(svc.GetSession(bid)!);

ResolveAllForcedSwitches();

// ══════════════════════════════════════════════════════════════════════════
// TURN 4..N — Auto-fight until someone wins
// ══════════════════════════════════════════════════════════════════════════
autoFight:
int maxTurns = 20;
for (int t = 4; t <= maxTurns; t++)
{
    var cur = svc.GetSession(bid)!;

    if (cur.State == BattleState.Ended) break;

    if (cur.State == BattleState.ForcedSwitch)
    {
        ResolveAllForcedSwitches();
        continue;
    }

    Sep($"TURN {t} — auto-fight");
    // Simple AI: each slot uses its strongest move on closest opponent
    BattleTurnResult? res = null;
    foreach (var (pid, slotIdx) in new[]{ (p1Id,0),(p1Id,1),(p2Id,0),(p2Id,1) })
    {
        var cs = svc.GetSession(bid)!;
        if (cs.State != BattleState.Running) break;
        var actor = svc.GetActiveSlot(cs, pid, slotIdx);
        if (actor == null || actor.IsFainted) continue;
        if (cs.PendingActions.ContainsKey($"{pid}:{slotIdx}")) continue;

        // Pick first non-fainted opponent slot
        var oppId  = pid == p1Id ? p2Id : p1Id;
        var tgtSlot = 0;
        if (svc.GetActiveSlot(cs, oppId, 0)?.IsFainted != false)
            tgtSlot = 1; // slot 0 fainted → target slot 1

        res = await Act(bid, pid, slotIdx, 0, targetSlot: tgtSlot);
    }
    if (res != null) { PrintEvents(res); PrintField(svc.GetSession(bid)!); }
}

done:
var final = svc.GetSession(bid)!;
Sep("RESULT");
if (final.State == BattleState.Ended)
{
    string winner = final.WinnerPlayerId == p1Id ? "cuong" : final.WinnerPlayerId == p2Id ? "ai_bot" : "draw";
    Console.WriteLine($"\n  🏆 Winner: {winner.ToUpper()} ({final.WinnerPlayerId})");
}
else
{
    Console.WriteLine($"\n  Battle still ongoing — state: {final.State}  turn: {final.TurnNumber}");
}
Sep();
