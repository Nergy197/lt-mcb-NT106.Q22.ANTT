namespace PokemonMMO.Models;

public class BattleTurnResult
{
    public string BattleId { get; set; } = null!;
    public int ResolvedTurnNumber { get; set; }
    public int NextTurnNumber { get; set; }
    public BattleState State { get; set; }
    public string? WinnerPlayerId { get; set; }

    public int ActiveIndex1 { get; set; }
    public int ActiveIndex2 { get; set; }
    public int ActiveHp1 { get; set; }
    public int ActiveHp2 { get; set; }

    // Field state snapshot sent to client
    public WeatherCondition Weather { get; set; } = WeatherCondition.None;
    public int WeatherTurnsLeft { get; set; } = 0;

    /// <summary>
    /// Typed event list — primary source of truth for client rendering.
    /// Inspired by pbs-unity Battle.View.Events hierarchy.
    /// </summary>
    public List<BattleEvent> TypedEvents { get; set; } = new();

    /// <summary>
    /// Legacy string events kept for battle log persistence and backward compat.
    /// Generated from TypedEvents automatically.
    /// </summary>
    public List<string> Events { get; set; } = new();
}
