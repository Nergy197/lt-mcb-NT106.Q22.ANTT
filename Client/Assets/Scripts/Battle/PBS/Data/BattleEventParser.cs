// BattleEventParser.cs
// Parses TypedEvents từ JSON thô vì JsonUtility không hỗ trợ polymorphism.
// Dùng SimpleJSON (https://github.com/Bunny83/SimpleJSON) cho lightweight parsing.
// Nếu dự án chưa có SimpleJSON, dùng fallback regex-based parser bên dưới.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PokemonMMO.Battle
{
    /// <summary>
    /// Parses a JSON array of typed battle events into BattleEventBase subclasses.
    /// Uses minimal JSON parsing — không yêu cầu thư viện bên ngoài.
    /// </summary>
    public static class BattleEventParser
    {
        /// <summary>
        /// Parse raw JSON string của TypedEvents array.
        /// Input: "[{\"EventType\":\"MoveUsedEvent\", ...}, ...]"
        /// </summary>
        public static List<BattleEventBase> ParseEventArray(string json)
        {
            var results = new List<BattleEventBase>();
            if (string.IsNullOrWhiteSpace(json) || json == "null") return results;

            // Extract each JSON object from the array
            var objects = SplitJsonArray(json);
            foreach (var obj in objects)
            {
                var evt = ParseSingleEvent(obj);
                if (evt != null)
                    results.Add(evt);
            }
            return results;
        }

        private static BattleEventBase ParseSingleEvent(string json)
        {
            var eventType = GetStringField(json, "eventType") ?? GetStringField(json, "EventType");
            if (string.IsNullOrEmpty(eventType)) return null;

            switch (eventType)
            {
                case "MoveUsedEvent":
                    return new MoveUsedEvent
                    {
                        EventType    = eventType,
                        UserId       = GetStringField(json, "UserId"),
                        PokemonName  = GetStringField(json, "PokemonName"),
                        MoveName     = GetStringField(json, "MoveName"),
                        MoveId       = GetStringField(json, "MoveId")
                    };

                case "MoveMissedEvent":
                    return new MoveMissedEvent
                    {
                        EventType   = eventType,
                        UserId      = GetStringField(json, "UserId"),
                        PokemonName = GetStringField(json, "PokemonName"),
                        MoveName    = GetStringField(json, "MoveName")
                    };

                case "MoveNoEffectEvent":
                    return new MoveNoEffectEvent
                    {
                        EventType  = eventType,
                        TargetName = GetStringField(json, "TargetName")
                    };

                case "PokemonDamageEvent":
                    return new PokemonDamageEvent
                    {
                        EventType      = eventType,
                        PlayerId       = GetStringField(json, "PlayerId"),
                        PokemonName    = GetStringField(json, "PokemonName"),
                        Damage         = GetIntField(json, "Damage"),
                        HpBefore       = GetIntField(json, "HpBefore"),
                        HpAfter        = GetIntField(json, "HpAfter"),
                        MaxHp          = GetIntField(json, "MaxHp"),
                        TypeMultiplier = GetDoubleField(json, "TypeMultiplier"),
                        IsCritical     = GetBoolField(json, "IsCritical"),
                        IsEndOfTurn    = GetBoolField(json, "IsEndOfTurn")
                    };

                case "PokemonFaintEvent":
                    return new PokemonFaintEvent
                    {
                        EventType   = eventType,
                        PlayerId    = GetStringField(json, "PlayerId"),
                        PokemonName = GetStringField(json, "PokemonName")
                    };

                case "PokemonHealEvent":
                    return new PokemonHealEvent
                    {
                        EventType   = eventType,
                        PlayerId    = GetStringField(json, "PlayerId"),
                        PokemonName = GetStringField(json, "PokemonName"),
                        HealAmount  = GetIntField(json, "HealAmount"),
                        HpAfter     = GetIntField(json, "HpAfter")
                    };

                case "StatusInflictedEvent":
                    return new StatusInflictedEvent
                    {
                        EventType   = eventType,
                        PlayerId    = GetStringField(json, "PlayerId"),
                        PokemonName = GetStringField(json, "PokemonName"),
                        Status      = ParseEnum<PokemonStatusCondition>(GetStringField(json, "Status"))
                    };

                case "StatusHealedEvent":
                    return new StatusHealedEvent
                    {
                        EventType   = eventType,
                        PlayerId    = GetStringField(json, "PlayerId"),
                        PokemonName = GetStringField(json, "PokemonName"),
                        Status      = ParseEnum<PokemonStatusCondition>(GetStringField(json, "Status"))
                    };

                case "StatusBlockedEvent":
                    return new StatusBlockedEvent
                    {
                        EventType   = eventType,
                        PokemonName = GetStringField(json, "PokemonName"),
                        Reason      = GetStringField(json, "Reason")
                    };

                case "ParalysisStuckEvent":
                    return new ParalysisStuckEvent
                    {
                        EventType   = eventType,
                        PokemonName = GetStringField(json, "PokemonName")
                    };

                case "SleepSkipEvent":
                    return new SleepSkipEvent
                    {
                        EventType   = eventType,
                        PokemonName = GetStringField(json, "PokemonName"),
                        TurnsLeft   = GetIntField(json, "TurnsLeft")
                    };

                case "FreezeThawEvent":
                    return new FreezeThawEvent
                    {
                        EventType   = eventType,
                        PokemonName = GetStringField(json, "PokemonName")
                    };

                case "StatChangeEvent":
                    return new StatChangeEvent
                    {
                        EventType   = eventType,
                        PlayerId    = GetStringField(json, "PlayerId"),
                        PokemonName = GetStringField(json, "PokemonName"),
                        Stat        = ParseEnum<StatIndex>(GetStringField(json, "Stat")),
                        Stages      = GetIntField(json, "Stages"),
                        NewStage    = GetIntField(json, "NewStage")
                    };

                case "StatChangeBlockedEvent":
                    return new StatChangeBlockedEvent
                    {
                        EventType   = eventType,
                        PokemonName = GetStringField(json, "PokemonName"),
                        Stat        = ParseEnum<StatIndex>(GetStringField(json, "Stat")),
                        Reason      = GetStringField(json, "Reason")
                    };

                case "SwitchEvent":
                    return new SwitchEvent
                    {
                        EventType              = eventType,
                        PlayerId               = GetStringField(json, "PlayerId"),
                        WithdrawnPokemonName   = GetStringField(json, "WithdrawnPokemonName"),
                        SentOutPokemonName     = GetStringField(json, "SentOutPokemonName"),
                        NewActiveIndex         = GetIntField(json, "NewActiveIndex"),
                        IsAutoSwitch           = GetBoolField(json, "IsAutoSwitch")
                    };

                case "WeatherChangedEvent":
                    return new WeatherChangedEvent
                    {
                        EventType  = eventType,
                        NewWeather = ParseEnum<WeatherCondition>(GetStringField(json, "NewWeather")),
                        TurnsLeft  = GetIntField(json, "TurnsLeft")
                    };

                case "WeatherEndedEvent":
                    return new WeatherEndedEvent
                    {
                        EventType    = eventType,
                        EndedWeather = ParseEnum<WeatherCondition>(GetStringField(json, "EndedWeather"))
                    };

                case "WeatherDamageEvent":
                    return new WeatherDamageEvent
                    {
                        EventType   = eventType,
                        PlayerId    = GetStringField(json, "PlayerId"),
                        PokemonName = GetStringField(json, "PokemonName"),
                        Damage      = GetIntField(json, "Damage"),
                        Weather     = ParseEnum<WeatherCondition>(GetStringField(json, "Weather"))
                    };

                case "SuperEffectiveEvent":
                    return new SuperEffectiveEvent
                    {
                        EventType  = eventType,
                        Multiplier = GetDoubleField(json, "Multiplier")
                    };

                case "NotVeryEffectiveEvent":
                    return new NotVeryEffectiveEvent
                    {
                        EventType  = eventType,
                        Multiplier = GetDoubleField(json, "Multiplier")
                    };

                case "BattleEndEvent":
                    return new BattleEndEvent
                    {
                        EventType       = eventType,
                        WinnerPlayerId  = GetStringField(json, "WinnerPlayerId"),
                        Reason          = GetStringField(json, "Reason")
                    };

                case "MessageEvent":
                    return new MessageEvent
                    {
                        EventType = eventType,
                        Message   = GetStringField(json, "Message")
                    };

                default:
                    return new MessageEvent { EventType = eventType, Message = $"[{eventType}]" };
            }
        }

        // ── Minimal JSON helpers ──────────────────────────────────────────────

        private static string GetStringField(string json, string key)
        {
            // Matches: "key":"value" or "key": "value"
            var pattern = $@"""{key}""\s*:\s*""((?:[^""\\]|\\.)*)""";
            var m = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static int GetIntField(string json, string key)
        {
            var pattern = $@"""{key}""\s*:\s*(-?\d+)";
            var m = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : 0;
        }

        private static double GetDoubleField(string json, string key)
        {
            var pattern = $@"""{key}""\s*:\s*(-?[\d.]+)";
            var m = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            return m.Success && double.TryParse(m.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0.0;
        }

        private static bool GetBoolField(string json, string key)
        {
            var pattern = $@"""{key}""\s*:\s*(true|false)";
            var m = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            return m.Success && m.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static T ParseEnum<T>(string value) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(value)) return default;
            return Enum.TryParse<T>(value, true, out var result) ? result : default;
        }

        /// <summary>Splits a JSON array string into individual object strings.</summary>
        private static List<string> SplitJsonArray(string json)
        {
            var objects = new List<string>();
            json = json.Trim();
            if (json.StartsWith("[")) json = json.Substring(1);
            if (json.EndsWith("]"))   json = json.Substring(0, json.Length - 1);

            int depth = 0;
            int start = -1;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        objects.Add(json.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }
            return objects;
        }
    }
}
