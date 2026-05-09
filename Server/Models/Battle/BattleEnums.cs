namespace PokemonMMO.Models;

public enum BattleState
{
    Waiting,
    TeamPreview,   // Cả 2 người đang chọn 4/6 Pokemon để mang đi
    Running,       // Đang trong lượt đánh
    ForcedSwitch,  // Chờ người chơi thay thế Pokemon vừa bị hạ
    Ended
}

public enum BattleActionType
{
    Move,
    Switch
}

/// <summary>
/// Non-volatile status conditions (persist after switching out).
/// Inspired by pbs-unity PokemonStatuses.
/// </summary>
public enum PokemonStatusCondition
{
    None,
    Burn,       // BRN — -50% ATK, 1/16 max HP end-of-turn
    Paralysis,  // PAR — -50% SPE, 25% chance to skip turn
    Poison,     // PSN — 1/8 max HP end-of-turn
    Toxic,      // TOX — escalating damage (1/16, 2/16, 3/16...)
    Sleep,      // SLP — skips turn for 1-3 turns
    Freeze      // FRZ — skips turn, 20% chance to thaw each turn
}

/// <summary>
/// Field weather conditions. Gen 9: Hail replaced by Snow (no EOT damage).
/// </summary>
public enum WeatherCondition
{
    None,
    Sun,        // Fire x1.5, Water x0.5
    Rain,       // Water x1.5, Fire x0.5
    Sandstorm,  // Rock/Steel/Ground immune; others -1/16 HP/turn
    Hail,       // Legacy (pre-Gen 9) — treated as Snow
    Snow,       // Gen 9: no EOT damage; Ice Sp.Def +50%; Blizzard 100% acc
}

/// <summary>
/// Field terrain conditions (Gen 6+). Duration 5 turns.
/// </summary>
public enum TerrainCondition
{
    None,
    Grassy,    // Grass x1.3; Earthquake x0.5; grounded heal 1/16 EOT
    Electric,  // Electric x1.3; grounded cannot be put to sleep
    Psychic,   // Psychic x1.3; priority moves blocked vs grounded opponents
    Misty,     // Dragon x0.5 vs grounded; status conditions blocked on grounded
}

/// <summary>
/// Stat indices for stat stage tracking. Mirrors pbs-unity's stat system.
/// </summary>
public enum StatIndex
{
    ATK = 0,
    DEF = 1,
    SPA = 2,
    SPD = 3,
    SPE = 4,
    ACC = 5,
    EVA = 6
}