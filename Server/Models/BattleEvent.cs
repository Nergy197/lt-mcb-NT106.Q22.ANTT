namespace PokemonMMO.Models;

/// <summary>
/// Base class for all typed battle events.
/// Inspired by pbs-unity Battle/View/Event.cs — enables rich client-side rendering.
/// </summary>
public abstract class BattleEvent
{
    public string EventType => GetType().Name;
}

// ─── Battle lifecycle ────────────────────────────────────────────────────────

public class BattleStartEvent : BattleEvent { }

public class BattleEndEvent : BattleEvent
{
    public string? WinnerPlayerId { get; init; }
    public string Reason { get; init; } = "";
}

// ─── Move events ─────────────────────────────────────────────────────────────

public class MoveUsedEvent : BattleEvent
{
    public string UserId { get; init; } = "";
    public string PokemonName { get; init; } = "";
    public string MoveName { get; init; } = "";
    public string MoveId { get; init; } = "";
}

public class MoveMissedEvent : BattleEvent
{
    public string UserId { get; init; } = "";
    public string PokemonName { get; init; } = "";
    public string MoveName { get; init; } = "";
}

public class MoveNoEffectEvent : BattleEvent
{
    public string TargetName { get; init; } = "";
}

// ─── Damage / HP events ──────────────────────────────────────────────────────

/// <summary>Mirrors pbs-unity PokemonHealthDamage event.</summary>
public class PokemonDamageEvent : BattleEvent
{
    public string PlayerId { get; init; } = "";
    public string PokemonName { get; init; } = "";
    public int Damage { get; init; }
    public int HpBefore { get; init; }
    public int HpAfter { get; init; }
    public int MaxHp { get; init; }
    public double TypeMultiplier { get; init; } = 1.0;
    public bool IsCritical { get; init; }
    public bool IsEndOfTurn { get; init; }
}

/// <summary>Mirrors pbs-unity PokemonHealthFaint event.</summary>
public class PokemonFaintEvent : BattleEvent
{
    public string PlayerId { get; init; } = "";
    public string PokemonName { get; init; } = "";
}

/// <summary>Mirrors pbs-unity PokemonHealthHeal event.</summary>
public class PokemonHealEvent : BattleEvent
{
    public string PlayerId { get; init; } = "";
    public string PokemonName { get; init; } = "";
    public int HealAmount { get; init; }
    public int HpAfter { get; init; }
}

// ─── Status events ───────────────────────────────────────────────────────────

/// <summary>Mirrors pbs-unity status inflict events.</summary>
public class StatusInflictedEvent : BattleEvent
{
    public string PlayerId { get; init; } = "";
    public string PokemonName { get; init; } = "";
    public PokemonStatusCondition Status { get; init; }
}

public class StatusHealedEvent : BattleEvent
{
    public string PlayerId { get; init; } = "";
    public string PokemonName { get; init; } = "";
    public PokemonStatusCondition Status { get; init; }
}

public class StatusBlockedEvent : BattleEvent
{
    public string PokemonName { get; init; } = "";
    public string Reason { get; init; } = "";
}

/// <summary>Paralysis: 25% chance pokemon cannot move this turn.</summary>
public class ParalysisStuckEvent : BattleEvent
{
    public string PokemonName { get; init; } = "";
}

/// <summary>Sleep: pokemon skips turn.</summary>
public class SleepSkipEvent : BattleEvent
{
    public string PokemonName { get; init; } = "";
    public int TurnsLeft { get; init; }
}

/// <summary>Freeze: pokemon cannot move, may thaw.</summary>
public class FreezeThawEvent : BattleEvent
{
    public string PokemonName { get; init; } = "";
}

// ─── Stat change events ──────────────────────────────────────────────────────

/// <summary>Mirrors pbs-unity PokemonStatChange event.</summary>
public class StatChangeEvent : BattleEvent
{
    public string PlayerId { get; init; } = "";
    public string PokemonName { get; init; } = "";
    public StatIndex Stat { get; init; }
    public int Stages { get; init; }       // +2, -1, etc.
    public int NewStage { get; init; }     // resulting stage (-6 to +6)
}

public class StatChangeBlockedEvent : BattleEvent
{
    public string PokemonName { get; init; } = "";
    public StatIndex Stat { get; init; }
    public string Reason { get; init; } = ""; // "already at max/min"
}

// ─── Switch events ───────────────────────────────────────────────────────────

/// <summary>Mirrors pbs-unity TrainerWithdraw + TrainerSendOut events.</summary>
public class SwitchEvent : BattleEvent
{
    public string PlayerId { get; init; } = "";
    public string WithdrawnPokemonName { get; init; } = "";
    public string SentOutPokemonName { get; init; } = "";
    public int NewActiveIndex { get; init; }
    public bool IsAutoSwitch { get; init; }
}

// ─── Weather events ──────────────────────────────────────────────────────────

public class WeatherChangedEvent : BattleEvent
{
    public WeatherCondition NewWeather { get; init; }
    public int TurnsLeft { get; init; }
}

public class WeatherEndedEvent : BattleEvent
{
    public WeatherCondition EndedWeather { get; init; }
}

public class WeatherDamageEvent : BattleEvent
{
    public string PlayerId { get; init; } = "";
    public string PokemonName { get; init; } = "";
    public int Damage { get; init; }
    public WeatherCondition Weather { get; init; }
}

// ─── Type effectiveness messages ─────────────────────────────────────────────

public class SuperEffectiveEvent : BattleEvent
{
    public double Multiplier { get; init; }
}

public class NotVeryEffectiveEvent : BattleEvent
{
    public double Multiplier { get; init; }
}

// ─── Generic message ─────────────────────────────────────────────────────────

/// <summary>Mirrors pbs-unity Message event — fallback for edge cases.</summary>
public class MessageEvent : BattleEvent
{
    public string Message { get; init; } = "";
}
