using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle.Logic;

namespace Game.Battle.UI
{
    public static class BattleEvents
    {
        public static Action<PokemonSlot, PokemonSlot, PokemonSlot, PokemonSlot> OnFieldUpdated;
        public static Action<string, int, int> OnHealthChanged;
        public static Action<string, bool> OnPrintDialog;
        public static Action<BattlePanelType> OnPanelChanged;
        public static Action<FieldConditionData> OnFieldConditionUpdated;
        public static Action<PreviewTeamData> OnTeamPreviewStart;
        public static Action<int[]>           OnTeamOrderConfirmed;
        public static Action                 OnPlayerTurnStart;
        public static Action<int>            OnPlayerUseSkill;
        public static Action<int>            OnTargetSelected;
        public static Action<bool, string>   OnBattleResult;
        public static Action                 OnActionCompleted;
        public static Action<PartyPanelData> OnPartyPanelOpen;
        public static Action<int>            OnPartySlotChosen;
        public static Action<int>            OnVoluntarySwitchRequested;
        public static Action                 OnPartyPanelCancelled;
        public static Action                 OnSkillPanelCancelled;
        public static Action                 OnPlayerSurrender;
        public static Action<bool>           OnTeraAvailabilityChanged;
        public static Action                 OnBattleConnected;
    }

    [Serializable]
    public class PokemonSlot
    {
        public string SpeciesId;
        public string SpeciesName;
        public int Level;
        public int MaxHp;
        public int CurrentHp;
        public bool IsFainted;
        public string Type1;
        public string Type2;
        public string Status;        // Bổ sung
        public string TerType;       // Bổ sung
        public bool IsTerastallized; // Bổ sung
        public List<MoveSlot> Moves = new();
    }

    [Serializable]
    public class MoveSlot
    {
        public int MoveId;
        public string Name;
        public string Type;
        public string Category;
        public int MaxPp;
        public int CurrentPp;
        public int TargetType;
        public string Effect;
        public List<StatChangeDto> StatChanges = new(); // Bổ sung
    }

    public class StatChangeDto {
        public string Stat; public int Stages;
    }

    public class FieldSnapshot
    {
        public PokemonSlot YourA; public PokemonSlot YourB;
        public PokemonSlot OppA;  public PokemonSlot OppB;

        public static FieldSnapshot From(FieldDto dto)
        {
            return new FieldSnapshot {
                YourA = ToSlot(dto.YourSlotA), YourB = ToSlot(dto.YourSlotB),
                OppA  = ToSlot(dto.OppSlotA),  OppB  = ToSlot(dto.OppSlotB)
            };
        }

        private static PokemonSlot ToSlot(PokemonSlotDto d)
        {
            if (d == null) return null;
            return new PokemonSlot {
                SpeciesId = d.SpeciesId.ToString(), SpeciesName = d.SpeciesName, Level = d.Level,
                MaxHp = d.MaxHp, CurrentHp = d.CurrentHp, IsFainted = d.CurrentHp <= 0,
                Type1 = d.Type1, Type2 = d.Type2, Status = d.Status,
                TerType = d.TerType, IsTerastallized = d.IsTerastallized,
                Moves = d.Moves?.Select(m => new MoveSlot {
                    MoveId = m.MoveId, Name = m.Name, Type = m.Type, Category = m.Category,
                    MaxPp = m.MaxPp, CurrentPp = m.CurrentPp, TargetType = m.TargetType,
                    StatChanges = m.StatChanges ?? new List<StatChangeDto>()
                }).ToList() ?? new List<MoveSlot>()
            };
        }
    }

    public class FieldDto {
        public PokemonSlotDto YourSlotA; public PokemonSlotDto YourSlotB;
        public PokemonSlotDto OppSlotA;  public PokemonSlotDto OppSlotB;
        public List<int> YourTeamHp;
    }

    public class PokemonSlotDto {
        public int SpeciesId; public string SpeciesName; public int Level;
        public int MaxHp; public int CurrentHp;
        public string Type1; public string Type2; public string Status;
        public string TerType; public bool IsTerastallized;
        public List<MoveDto> Moves;
    }

    public class MoveDto {
        public int MoveId; public string Name; public string Type; public string Category;
        public int MaxPp; public int CurrentPp; public int TargetType;
        public List<StatChangeDto> StatChanges;
    }

    public class TurnDto {
        public string State; public string WinnerPlayerId;
        public bool YouWon; public bool IsDraw; // Server-authoritative: kết quả thắng/thua cho chính người nhận
        public List<string> Events;
        public List<EventDto> TypedEvents; // Sửa từ object về EventDto
    }

    public class EventDto {
        public string EventType; public string PlayerId; public string PokemonName;
        public string TargetName; public string MoveName; public int Damage;
        public int HpAfter; public string Status; public string StatName; public int Stages;
        public string Message; public string WithdrawnName; public string SentOutName; // Bổ sung
    }

    public class TeamPreviewDto {
        public List<TeamPreviewPokemonDto> YourTeam;
        public List<TeamPreviewPokemonDto> OpponentTeam;
    }

    public class TeamPreviewPokemonDto {
        public string SpeciesId; public string SpeciesName; public int Level; public int MaxHp;
        public string Type1; public string Type2; // Bổ sung
    }

    public class FsRequiredDto {
        public int Slot; public List<int> AvailableIndices;
    }

    public class FieldConditionData {
        public string Weather; public int WeatherTurns;
        public string Terrain; public int TerrainTurns; public int TurnNumber;
    }

    public class PreviewTeamData {
        public PreviewPokemon[] MyTeam; public PreviewPokemon[] OppTeam;
    }

    public class PreviewPokemon {
        public string Name; public int Level; public string SpeciesId;
        public string Type1; public string Type2; public int MaxHp;
    }

    public class PartyPanelData {
        public PartyPokemon[] Pokemon; public int[] AvailableIdxs;
        public bool IsForcedSwitch; public int ForcedSlot;
    }

    public class PartyPokemon {
        public int PartyIndex; public string Name; public int Level;
        public int CurrentHp; public int MaxHp; public bool IsFainted;
        public bool IsActive; public string Type1; public string Type2; public string Status;
    }
}
