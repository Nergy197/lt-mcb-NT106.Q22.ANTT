using System;
using System.Collections.Generic;

namespace Game.Battle.UI
{
    public static class BattleEvents
    {
        // ── Core ──────────────────────────────────────────────────────────────
        public static Action<string, int, int> OnHealthChanged;
        public static Action<string, bool>     OnPrintDialog;
        public static Action                   OnPlayerTurnStart;
        public static Action                   OnActionCompleted;
        public static Action<int>              OnPlayerUseSkill;
        public static Action<int>              OnPlayerSwitch;
        public static Action<int>              OnTargetSelected;

        // ── Team Preview ──────────────────────────────────────────────────────
        public static Action<List<TeamPreviewPokemonDto>, List<TeamPreviewPokemonDto>> OnTeamPreviewReady;
        public static Action<PreviewTeamData> OnTeamPreviewStart;
        public static Action<int[]>           OnTeamOrderConfirmed;

        // ── Party Panel ───────────────────────────────────────────────────────
        public static Action<PartyPanelData>  OnPartyPanelOpen;
        public static Action<int>             OnPartySlotChosen;
        public static Action                  OnPartyPanelCancelled;
        public static Action<int>             OnVoluntarySwitchRequested; // srcSlot

        // ── Field Conditions ──────────────────────────────────────────────────
        public static Action<FieldConditionData> OnFieldConditionUpdated;
        public static Action<PokemonSlot, PokemonSlot, PokemonSlot, PokemonSlot> OnFieldUpdated;

        // ── Tera ─────────────────────────────────────────────────────────────
        public static Action<bool> OnTeraAvailabilityChanged;

        // ── Battle Result ─────────────────────────────────────────────────────
        public static Action<bool, string> OnBattleResult;

        // ── UI Panel change ───────────────────────────────────────────────────
        public static Action<BattlePanelType> OnPanelChanged;
    }

    // ── Data classes ──────────────────────────────────────────────────────────

    public class PreviewTeamData
    {
        public PreviewPokemon[] MyTeam;
        public PreviewPokemon[] OppTeam;
    }

    public class PreviewPokemon
    {
        public int    SpeciesId;
        public string Name, Type1, Type2;
        public int    Level, MaxHp;
    }

    public class PartyPanelData
    {
        public PartyPokemon[] Pokemon;
        public int[]          AvailableIdxs;
        public bool           IsForcedSwitch;
        public int            ForcedSlot;
    }

    public class PartyPokemon
    {
        public int    PartyIndex;
        public string Name, Type1, Type2, Status;
        public int    CurrentHp, MaxHp;
        public bool   IsFainted, IsActive;
    }

    public class FieldConditionData
    {
        public string Weather;
        public int    WeatherTurns;
        public string Terrain;
        public int    TerrainTurns;
        public int    TurnNumber;
    }

    // ── DTO Models ────────────────────────────────────────────────────────────

    public class FieldDto
    {
        public string BattleId { get; set; }
        public string OpponentId { get; set; }
        public string Player2Id { get; set; }
        public int TurnNumber { get; set; }
        public string Weather { get; set; }
        public int WeatherTurnsLeft { get; set; }
        public string Terrain { get; set; }
        public int TerrainTurnsLeft { get; set; }
        public PokemonDto YourSlotA { get; set; }
        public PokemonDto YourSlotB { get; set; }
        public PokemonDto OppSlotA { get; set; }
        public PokemonDto OppSlotB { get; set; }
        public List<int> YourTeamHp { get; set; }
        public List<int> YourTeamMaxHp { get; set; }
    }

    public class PokemonDto
    {
        public int SpeciesId { get; set; }
        public string SpeciesName { get; set; }
        public string Nickname { get; set; }
        public string Type1 { get; set; }
        public string Type2 { get; set; }
        public string TerType { get; set; }
        public bool IsTerastallized { get; set; }
        public int Level { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public bool IsFainted { get; set; }
        public string Status { get; set; }
        public List<MoveDto> Moves { get; set; }
    }

    public class MoveDto
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public int CurrentPp { get; set; }
        public int MaxPp { get; set; }
    }

    public class TurnDto
    {
        public string State { get; set; }
        public string WinnerPlayerId { get; set; }
        public int ActiveHp1 { get; set; }
        public int ActiveHp1b { get; set; }
        public int ActiveHp2 { get; set; }
        public int ActiveHp2b { get; set; }
        public List<FsInfo> ForcedSwitches { get; set; }
        public List<EventDto> TypedEvents { get; set; }
        public string Weather { get; set; }
        public int WeatherTurnsLeft { get; set; }
        public string Terrain { get; set; }
        public int TerrainTurnsLeft { get; set; }
    }

    public class EventDto
    {
        public string EventType { get; set; }
        public string PokemonName { get; set; }
        public string MoveName { get; set; }
        public int Damage { get; set; }
        public bool IsCritical { get; set; }
        public string TargetName { get; set; }
        public string Status { get; set; }
        public string StatName { get; set; }
        public int Stages { get; set; }
        public string NewWeather { get; set; }
        public string NewTerrain { get; set; }
        public string TerType { get; set; }
        public string WithdrawnName { get; set; }
        public string SentOutName { get; set; }
        public string Message { get; set; }
        public int HpAfter { get; set; }
    }

    public class TeamPreviewDto
    {
        public string BattleId { get; set; }
        public string YourPlayerId { get; set; }
        public string OpponentPlayerId { get; set; }
        public List<TeamPreviewPokemonDto> YourTeam { get; set; }
        public List<TeamPreviewPokemonDto> OpponentTeam { get; set; }
    }

    public class TeamPreviewPokemonDto
    {
        public int SpeciesId { get; set; }
        public string SpeciesName { get; set; }
        public string Nickname { get; set; }
        public string Type1 { get; set; }
        public string Type2 { get; set; }
        public int Level { get; set; }
        public int MaxHp { get; set; }
    }

    public class FsRequiredDto
    {
        public string BattleId { get; set; }
        public string PlayerId { get; set; }
        public int Slot { get; set; }
        public List<int> AvailableIndices { get; set; }
    }

    public class FsInfo
    {
        public string PlayerId { get; set; }
        public int Slot { get; set; }
    }

    public class ActionAcceptedDto
    {
        public string BattleId { get; set; }
        public int Slot { get; set; }
    }

    // ── Snapshots & Helpers ───────────────────────────────────────────────────

    public class FieldSnapshot
    {
        public PokemonSlot YourA, YourB, OppA, OppB;
        public static FieldSnapshot From(FieldDto d) => new FieldSnapshot
        {
            YourA = PokemonSlot.From(d.YourSlotA),
            YourB = PokemonSlot.From(d.YourSlotB),
            OppA = PokemonSlot.From(d.OppSlotA),
            OppB = PokemonSlot.From(d.OppSlotB)
        };
    }

    public class PokemonSlot
    {
        public int    SpeciesId;
        public string SpeciesName, Type1, Type2, TerType, Status;
        public int    Level, CurrentHp, MaxHp;
        public bool   IsFainted, IsTerastallized;
        public List<MoveSlot> Moves;

        public static PokemonSlot From(PokemonDto d)
        {
            if (d == null) return null;
            string name = !string.IsNullOrEmpty(d.SpeciesName) ? d.SpeciesName
                        : !string.IsNullOrEmpty(d.Nickname)    ? d.Nickname
                        : $"Pokemon#{d.SpeciesId}";
            return new PokemonSlot
            {
                SpeciesId       = d.SpeciesId,
                SpeciesName     = name,
                Type1           = d.Type1 ?? "normal",
                Type2           = d.Type2,
                TerType         = d.TerType,
                IsTerastallized = d.IsTerastallized,
                Level           = d.Level,
                CurrentHp       = d.CurrentHp,
                MaxHp           = d.MaxHp,
                IsFainted       = d.IsFainted,
                Status          = d.Status,
                Moves           = d.Moves?.ConvertAll(m => new MoveSlot
                {
                    Name      = m.Name,
                    Type      = m.Type,
                    Category  = m.Category,
                    CurrentPp = m.CurrentPp,
                    MaxPp     = m.MaxPp,
                }),
            };
        }
    }

    public class MoveSlot
    {
        public string Name, Type, Category;
        public int    CurrentPp, MaxPp;
    }
}
