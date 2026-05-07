using System.Collections.Concurrent;

namespace PokemonMMO.Models;

public class BattleSession
{
    public string BattleId { get; set; } = Guid.NewGuid().ToString("N");
    public BattleState State { get; set; } = BattleState.Waiting;
    public int TurnNumber { get; set; } = 1;

    public string Player1Id { get; set; } = null!;
    public string Player2Id { get; set; } = null!;

    public List<BattlePokemonSnapshot> Team1 { get; set; } = new();
    public List<BattlePokemonSnapshot> Team2 { get; set; } = new();

    // Trong đấu đôi, ta có 2 Pokemon đứng sân cùng lúc
    public int ActiveIndex1 { get; set; } = 0; // Slot A phe 1
    public int ActiveIndex1b { get; set; } = 1; // Slot B phe 1

    public int ActiveIndex2 { get; set; } = 0; // Slot A phe 2
    public int ActiveIndex2b { get; set; } = 1; // Slot B phe 2

    // ── Team Preview – indices người chơi chọn (4 trong 6), theo thứ tự pick ──
    public List<int> TeamOrder1 { get; set; } = new();
    public List<int> TeamOrder2 { get; set; } = new();

    // Key = "{playerId}:{sourceSlot}" (sourceSlot: 0=SlotA, 1=SlotB)
    public ConcurrentDictionary<string, BattleAction> PendingActions { get; set; } = new();

    // Forced-switch phase: set of "{playerId}:{slot}" đang chờ người chơi chọn thay thế
    public HashSet<string> PendingForcedSwitches { get; set; } = new();

    // ── Terastallization (Gen 9) — mỗi người chỉ dùng 1 lần/trận ────────
    public bool TeraUsed1 { get; set; } = false;
    public bool TeraUsed2 { get; set; } = false;

    public DateTime TurnDeadlineUtc { get; set; } = DateTime.UtcNow;
    public string? WinnerPlayerId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // ── Field conditions ─────────────────────────────────────────────────────

    public WeatherCondition Weather { get; set; } = WeatherCondition.None;
    /// <summary>-1 = permanent (e.g. Drought ability). 0 = no weather.</summary>
    public int WeatherTurnsLeft { get; set; } = 0;

    // ── Terrain (Gen 6+) ─────────────────────────────────────────────────────
    public TerrainCondition Terrain { get; set; } = TerrainCondition.None;
    public int TerrainTurnsLeft { get; set; } = 0;
}
