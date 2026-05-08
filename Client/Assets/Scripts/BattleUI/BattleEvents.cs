using System;

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

        // ── Team Preview ──────────────────────────────────────────────────────
        /// Controller → UI: mở panel team preview với dữ liệu team
        public static Action<PreviewTeamData> OnTeamPreviewStart;
        /// UI → Controller: player đã xác nhận thứ tự 4 Pokemon
        public static Action<int[]>           OnTeamOrderConfirmed;

        // ── Party Panel ───────────────────────────────────────────────────────
        /// Controller → UI: mở party panel
        public static Action<PartyPanelData>  OnPartyPanelOpen;
        /// UI → Controller: player chọn party slot (battle team index)
        public static Action<int>             OnPartySlotChosen;
        /// UI → Controller: player bấm Back (chỉ khi voluntary switch)
        public static Action                  OnPartyPanelCancelled;
        /// CommandPanel → Controller: player muốn đổi Pokemon tự nguyện
        public static Action<int>             OnVoluntarySwitchRequested; // srcSlot

        // ── Field Conditions ──────────────────────────────────────────────────
        public static Action<FieldConditionData> OnFieldConditionUpdated;

        // ── Tera ─────────────────────────────────────────────────────────────
        /// Controller → SkillPanel: cập nhật trạng thái nút Tera
        public static Action<bool> OnTeraAvailabilityChanged;

        // ── Battle Result ─────────────────────────────────────────────────────
        public static Action<bool, string> OnBattleResult; // won, opponentLabel

        // ── UI Panel change (fired by BattleUIManager.SwitchPanel) ────────────
        public static Action<BattlePanelType> OnPanelChanged;
    }

    // ── Data classes passed through events ────────────────────────────────────

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
        public PartyPokemon[] Pokemon;       // Tất cả 4 thành viên battle team
        public int[]          AvailableIdxs; // Chỉ số battle team được phép chọn
        public bool           IsForcedSwitch;
        public int            ForcedSlot;    // 0 = Slot A, 1 = Slot B (-1 nếu voluntary)
    }

    public class PartyPokemon
    {
        public int    PartyIndex;            // Index trong battle team (0-3)
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
}
