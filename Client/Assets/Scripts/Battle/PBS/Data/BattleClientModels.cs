// BattleClientModels.cs
// Client-side mirror của server models (Server/Models/).
// Dùng [System.Serializable] để tương thích JsonUtility.
// TypedEvents dùng parser riêng (BattleEventParser) vì JsonUtility không hỗ trợ polymorphism.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokemonMMO.Battle
{
    // ── Enums (mirror Server/Models/BattleEnums.cs) ──────────────────────────

    public enum PokemonStatusCondition
    {
        None, Burn, Paralysis, Poison, Toxic, Sleep, Freeze
    }

    public enum WeatherCondition
    {
        None, Sun, Rain, Sandstorm, Hail
    }

    public enum StatIndex
    {
        ATK = 0, DEF = 1, SPA = 2, SPD = 3, SPE = 4, ACC = 5, EVA = 6
    }

    public enum BattleState
    {
        Waiting, Running, Ended
    }

    // ── Pokemon move slot ─────────────────────────────────────────────────────

    [Serializable]
    public class PokemonMoveSlot
    {
        public int MoveId;
        public int CurrentPp;
    }

    // ── Pokemon snapshot (mirror BattlePokemonSnapshot) ───────────────────────

    [Serializable]
    public class BattlePokemonSnapshot
    {
        public string InstanceId;
        public int SpeciesId;
        public string Nickname;
        public int Level;
        public int CurrentHp;
        public int MaxHp;
        public PokemonStatusCondition NonVolatileStatus;
        public int SleepTurnsLeft;
        public int ToxicCounter;
        public bool IsConfused;
        public int[] StatStages = new int[7]; // ATK/DEF/SPA/SPD/SPE/ACC/EVA
        public List<PokemonMoveSlot> Moves = new List<PokemonMoveSlot>();
        public string DisplayName => string.IsNullOrEmpty(Nickname) ? $"Pokemon {SpeciesId}" : Nickname;

        public bool IsFainted => CurrentHp <= 0;

        /// <summary>Gen 3+ stat stage multiplier.</summary>
        public float GetStageMultiplier(StatIndex stat)
        {
            if (StatStages == null || StatStages.Length <= (int)stat) return 1f;
            int stage = Mathf.Clamp(StatStages[(int)stat], -6, 6);
            return stage >= 0
                ? (2f + stage) / 2f
                : 2f / (2f - stage);
        }
    }

    // ── Battle session (mirror BattleSession) ─────────────────────────────────

    [Serializable]
    public class BattleSession
    {
        public string BattleId;
        public BattleState State;
        public int TurnNumber;
        public string Player1Id;
        public string Player2Id;
        public List<BattlePokemonSnapshot> Team1 = new List<BattlePokemonSnapshot>();
        public List<BattlePokemonSnapshot> Team2 = new List<BattlePokemonSnapshot>();
        public int[] ActiveIndices1;
        public int[] ActiveIndices2;
        public WeatherCondition Weather;
        public int WeatherTurnsLeft;
    }

    // ── Turn result (mirror BattleTurnResult) ─────────────────────────────────

    [Serializable]
    public class BattleTurnResult
    {
        public string BattleId;
        public int ResolvedTurnNumber;
        public int NextTurnNumber;
        public BattleState State;
        public string WinnerPlayerId;
        public int[] ActiveIndices1;
        public int[] ActiveIndices2;
        public int[] ActiveHps1;
        public int[] ActiveHps2;
        public WeatherCondition Weather;
        public int WeatherTurnsLeft;
        /// <summary>String events — fallback display, always populated.</summary>
        public List<string> Events = new List<string>();
        // TypedEvents deserialized manually by BattleEventParser
        [NonSerialized] public List<BattleEventBase> typedEvents = new List<BattleEventBase>();
    }

    // ── Typed battle events (mirror Server/Models/BattleEvent.cs) ────────────

    public abstract class BattleEventBase
    {
        public string EventType;
    }

    public class MoveUsedEvent         : BattleEventBase { public string UserId; public string PokemonName; public string MoveName; public string MoveId; }
    public class MoveMissedEvent       : BattleEventBase { public string UserId; public string PokemonName; public string MoveName; }
    public class MoveNoEffectEvent     : BattleEventBase { public string TargetName; }
    public class PokemonDamageEvent    : BattleEventBase { public string PlayerId; public string PokemonName; public int Damage; public int HpBefore; public int HpAfter; public int MaxHp; public double TypeMultiplier; public bool IsCritical; public bool IsEndOfTurn; }
    public class PokemonFaintEvent     : BattleEventBase { public string PlayerId; public string PokemonName; }
    public class PokemonHealEvent      : BattleEventBase { public string PlayerId; public string PokemonName; public int HealAmount; public int HpAfter; }
    public class StatusInflictedEvent  : BattleEventBase { public string PlayerId; public string PokemonName; public PokemonStatusCondition Status; }
    public class StatusHealedEvent     : BattleEventBase { public string PlayerId; public string PokemonName; public PokemonStatusCondition Status; }
    public class StatusBlockedEvent    : BattleEventBase { public string PokemonName; public string Reason; }
    public class ParalysisStuckEvent   : BattleEventBase { public string PokemonName; }
    public class SleepSkipEvent        : BattleEventBase { public string PokemonName; public int TurnsLeft; }
    public class FreezeThawEvent       : BattleEventBase { public string PokemonName; }
    public class StatChangeEvent       : BattleEventBase { public string PlayerId; public string PokemonName; public StatIndex Stat; public int Stages; public int NewStage; }
    public class StatChangeBlockedEvent: BattleEventBase { public string PokemonName; public StatIndex Stat; public string Reason; }
    public class SwitchEvent           : BattleEventBase { public string PlayerId; public string WithdrawnPokemonName; public string SentOutPokemonName; public int NewActiveIndex; public bool IsAutoSwitch; }
    public class WeatherChangedEvent   : BattleEventBase { public WeatherCondition NewWeather; public int TurnsLeft; }
    public class WeatherEndedEvent     : BattleEventBase { public WeatherCondition EndedWeather; }
    public class WeatherDamageEvent    : BattleEventBase { public string PlayerId; public string PokemonName; public int Damage; public WeatherCondition Weather; }
    public class SuperEffectiveEvent   : BattleEventBase { public double Multiplier; }
    public class NotVeryEffectiveEvent : BattleEventBase { public double Multiplier; }
    public class BattleEndEvent        : BattleEventBase { public string WinnerPlayerId; public string Reason; }
    public class MessageEvent          : BattleEventBase { public string Message; }

    // ── Shared UI Display Models ─────────────────────────────────────────────

    [Serializable]
    public class MoveDisplayInfo
    {
        public int MoveId;
        public string Name;
        public string Type;
        public string Category; // "Physical", "Special", "Status"
        public int Power;
        public int Accuracy;
        public int CurrentPp;
        public int MaxPp;
    }

    [Serializable]
    public class PartyPokemonSnapshot
    {
        public int Index;
        public string SpeciesId;
        public string Nickname;
        public int Level;
        public int CurrentHp;
        public int MaxHp;
        public string StatusCondition;
        public bool IsFainted => CurrentHp <= 0;
    }
}
