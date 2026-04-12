namespace PokemonMMO.Models;

public enum BattleState
{
    Waiting,
    Running,
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
/// Field weather conditions. Inspired by pbs-unity BattleCondition weather.
/// </summary>
public enum WeatherCondition
{
    None,
    Sun,        // Boosts Fire moves x1.5, weakens Water x0.5
    Rain,       // Boosts Water moves x1.5, weakens Fire x0.5
    Sandstorm,  // Rock/Steel/Ground immune; others lose 1/16 HP/turn
    Hail        // Ice immune; others lose 1/16 HP/turn
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