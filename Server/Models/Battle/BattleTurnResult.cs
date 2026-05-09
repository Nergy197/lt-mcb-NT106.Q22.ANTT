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
    public string Weather { get; set; } = "none";
    public int WeatherTurnsLeft { get; set; } = 0;

    public string Terrain { get; set; } = "none";
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

public static class BattleTurnResultExtensions
{
    /// <summary>
    /// Trả về bản copy với HP/Index của Player1 và Player2 được hoán đổi,
    /// dùng để Player 2 thấy HP của mình là "Hp1" và đối thủ là "Hp2".
    /// </summary>
    public static BattleTurnResult ForPlayer2Perspective(this BattleTurnResult r) => new()
    {
        BattleId           = r.BattleId,
        ResolvedTurnNumber = r.ResolvedTurnNumber,
        NextTurnNumber     = r.NextTurnNumber,
        State              = r.State,
        WinnerPlayerId     = r.WinnerPlayerId,
        ActiveIndex1       = r.ActiveIndex2,
        ActiveIndex1b      = r.ActiveIndex2b,
        ActiveHp1          = r.ActiveHp2,
        ActiveHp1b         = r.ActiveHp2b,
        ActiveIndex2       = r.ActiveIndex1,
        ActiveIndex2b      = r.ActiveIndex1b,
        ActiveHp2          = r.ActiveHp1,
        ActiveHp2b         = r.ActiveHp1b,
        Weather            = r.Weather,
        WeatherTurnsLeft   = r.WeatherTurnsLeft,
        Terrain            = r.Terrain,
        TerrainTurnsLeft   = r.TerrainTurnsLeft,
        Team1Hp            = r.Team2Hp,
        Team2Hp            = r.Team1Hp,
        ForcedSwitches     = r.ForcedSwitches,
        TypedEvents        = r.TypedEvents,
        Events             = r.Events,
    };
}
