namespace PokemonMMO.Models;

public class BattleTurnResult
{
    public string BattleId { get; set; } = null!;
    public int ResolvedTurnNumber { get; set; }
    public int NextTurnNumber { get; set; }
    public BattleState State { get; set; }
    public string? WinnerPlayerId { get; set; }

    // Slots active phe 1
    public int ActiveIndex1 { get; set; }
    public int ActiveIndex1b { get; set; }
    public int ActiveHp1 { get; set; }
    public int ActiveHp1b { get; set; }

    // Slots active phe 2
    public int ActiveIndex2 { get; set; }
    public int ActiveIndex2b { get; set; }
    public int ActiveHp2 { get; set; }
    public int ActiveHp2b { get; set; }

    // Field state snapshot sent to client
    public WeatherCondition Weather { get; set; } = WeatherCondition.None;
    public int WeatherTurnsLeft { get; set; } = 0;

    public TerrainCondition Terrain { get; set; } = TerrainCondition.None;
    public int TerrainTurnsLeft { get; set; } = 0;

    // Full HP của cả đội (4 Pokemon được mang đi)
    public List<int> Team1Hp { get; set; } = new();
    public List<int> Team2Hp { get; set; } = new();

    // Forced-switch slots cần thay thế sau lượt này
    public List<ForcedSwitchSlot> ForcedSwitches { get; set; } = new();

    /// <summary>Typed event list — primary source of truth for client rendering.</summary>
    public List<BattleEvent> TypedEvents { get; set; } = new();

    /// <summary>Legacy string events kept for backward compat.</summary>
    public List<string> Events { get; set; } = new();
}

public class ForcedSwitchSlot
{
    public string PlayerId { get; set; } = "";
    public int Slot { get; set; } // 0 = Slot A, 1 = Slot B
}
