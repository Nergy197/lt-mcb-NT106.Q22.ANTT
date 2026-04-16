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

    // Key = playerId
    public ConcurrentDictionary<string, BattleAction> PendingActions { get; set; } = new();

    public DateTime TurnDeadlineUtc { get; set; } = DateTime.UtcNow;
    public string? WinnerPlayerId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // ── Field conditions (inspired by pbs-unity Battle.Model) ────────────────

    /// <summary>Current weather affecting the field.</summary>
    public WeatherCondition Weather { get; set; } = WeatherCondition.None;

    /// <summary>
    /// Turns remaining for the current weather (-1 = permanent, 0 = none).
    /// Standard weather lasts 5 turns; extended by items to 8 (not implemented yet).
    /// </summary>
    public int WeatherTurnsLeft { get; set; } = 0;
}
