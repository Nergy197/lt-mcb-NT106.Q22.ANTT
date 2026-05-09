using UnityEngine;
using Game.Battle.UI;
using Game.Battle.Logic;
using System.Collections;
using System.Collections.Generic;

namespace Game.Battle.Demo
{
    /// <summary>
    /// Demo battle đầy đủ tương tác — không cần server/SignalR.
    /// [FIXED] Đã gỡ bỏ logic tự động disable script khác.
    /// </summary>
    public class BattleDemoSimulator : MonoBehaviour
    {
        [Header("Manual Activation")]
        public bool activateSimulator = false; 

        [Header("Panels (tự tìm nếu để trống)")]
        public BattleSkillPanel   skillPanel;
        public BattleCommandPanel commandPanel;

        [Header("Demo Options")]
        public bool skipTeamPreview = true;

        // ... (các class nội bộ giữ nguyên) ...
        class DemoMove { public string Name, Type, Category; public int Power, CurrentPp, MaxPp; public int Priority = 0; public string InflictsStatus; public bool CausesFlinch; public bool IsFakeOut; public bool GrantsProtection; public bool IsRedirect; public bool IsSwitchMove; public bool HitsAll; public bool IsAllyMove; public bool IgnoresProtect; public bool AlwaysCrit; public int RecoilPercent; public int DrainPercent; public string SelfStatDrop; public string TargetStatDrop; public enum TargetType { Single, Self, BothFoes, Ally } public TargetType Target = TargetType.Single; }
        class DemoPokemon { public string Name, Type1, Type2; public int Level = 50, MaxHp, CurrentHp; public DemoMove[] Moves; public bool IsFainted; public string Status; public bool IsFinching; public bool IsProtected; public int TurnsOnField; public int Atk, Def, SpA, SpD, Spe; public int AtkStage, DefStage, SpAtkStage, SpDefStage, SpdStage; public string Ability; public int ProtectCounter; }
        struct TurnAction { public bool IsPlayer; public int Slot; public int MoveIdx; public int TargetIdx; public bool UseTera; public int Priority; public DemoPokemon Attacker; }

        EntityHUD        _pHUD1, _pHUD2, _eHUD1, _eHUD2;
        BattleUIManager  _uiManager;
        DemoPokemon[] _team6; DemoPokemon[] _pTeam; DemoPokemon[] _eTeam;
        int[] _pField = { 0, 1 }; int[] _eField = { 0, 1 };
        TurnAction _actA, _actB; bool _actADone, _actBDone;
        enum Phase { TeamPreview, PickingA, PickingB, Resolving, ForcedSwitch, Ended }
        Phase _phase = Phase.TeamPreview;
        bool _pickTarget; int _selectedMove, _curSlot, _turnNum = 1; bool _teraUsed;
        int _redirectPlayerIdx = -1; int _redirectEnemyIdx = -1; 
        bool _helpingHandA, _helpingHandB; bool _eHelpingHandA, _eHelpingHandB;
        bool _waitSwitch, _volSwitch; int _forcedSlot, _volSlot;

        void Awake()
        {
            if (!activateSimulator)
            {
                Debug.Log("[Demo] Simulator is NOT activated. Disabling self.");
                this.enabled = false;
                return;
            }

            Debug.LogWarning("[Demo] SIMULATOR ACTIVATED - Hijacking battle scene!");
            var nc = FindObjectOfType<BattleNetworkController>(true);
            if (nc != null)
            {
                _pHUD1 = nc.playerHUD1; _pHUD2 = nc.playerHUD2;
                _eHUD1 = nc.enemyHUD1;  _eHUD2 = nc.enemyHUD2;
                if (skillPanel == null) skillPanel = nc.skillPanel;
                if (commandPanel == null) commandPanel = nc.commandPanel;
                nc.enabled = false;
            }
            if (skillPanel == null) skillPanel = FindObjectOfType<BattleSkillPanel>();
            if (commandPanel == null) commandPanel = FindObjectOfType<BattleCommandPanel>();
            if (_uiManager == null) _uiManager = FindObjectOfType<BattleUIManager>();
        }

        void Start() { if (!activateSimulator) return; InitTeams(); StartCoroutine(BeginBattle()); }
        void OnEnable() { if (!activateSimulator) return; BattleEvents.OnPlayerUseSkill += OnSkillOrTarget; BattleEvents.OnTeamOrderConfirmed += OnTeamOrderConfirmed; BattleEvents.OnPartySlotChosen += OnPartySlotChosen; BattleEvents.OnPartyPanelCancelled += OnPartyPanelCancelled; BattleEvents.OnVoluntarySwitchRequested += OnVoluntarySwitchRequested; }
        void OnDisable() { BattleEvents.OnPlayerUseSkill -= OnSkillOrTarget; BattleEvents.OnTeamOrderConfirmed -= OnTeamOrderConfirmed; BattleEvents.OnPartySlotChosen -= OnPartySlotChosen; BattleEvents.OnPartyPanelCancelled -= OnPartyPanelCancelled; BattleEvents.OnVoluntarySwitchRequested -= OnVoluntarySwitchRequested; }

        void InitTeams() { _team6 = new[] { P("Calyrex", "psychic", "ghost", 175, 85, 80, 165, 100, 150, "As One", M("Astral Barrage", "ghost", "Special", 120, 5, all: true, target: DemoMove.TargetType.BothFoes)), P("Incineroar", "fire", "dark", 167, 115, 90, 80, 90, 60, "Intimidate", M("Fake Out", "normal", "Physical", 40, 10, pr: 3, flinch: true, fakeOut: true)) }; _eTeam = new[] { P("Miraidon", "electric", "dragon", 175, 85, 100, 135, 115, 135, "Hadron Engine", M("Electro Drift", "electric", "Special", 100, 5)) }; }
        static DemoPokemon P(string name, string t1, string t2, int hp, int atk, int def, int spa, int spd, int spe, string ability, params DemoMove[] moves) => new DemoPokemon { Name = name, Type1 = t1, Type2 = t2, MaxHp = hp, CurrentHp = hp, Atk = atk, Def = def, SpA = spa, SpD = spd, Spe = spe, Ability = ability, Moves = moves };
        static DemoMove M(string name, string type, string cat, int power, int pp, string status = null, int pr = 0, bool flinch = false, bool fakeOut = false, bool prot = false, bool redir = false, bool sw = false, bool all = false, bool ally = false, bool ignore = false, bool crit = false, DemoMove.TargetType target = DemoMove.TargetType.Single, int recoil = 0, int drain = 0, string selfDrop = null, string tgtDrop = null) => new DemoMove { Name = name, Type = type, Category = cat, Power = power, CurrentPp = pp, MaxPp = pp, Priority = pr, CausesFlinch = flinch, IsFakeOut = fakeOut, GrantsProtection = prot, IsRedirect = redir, IsSwitchMove = sw, HitsAll = all, IsAllyMove = ally, IgnoresProtect = ignore, AlwaysCrit = crit, Target = target };
        DemoPokemon PA() => _pTeam != null ? _pTeam[_pField[0]] : null; DemoPokemon PB() => _pTeam != null ? _pTeam[_pField[1]] : null; DemoPokemon EA() => _eTeam[_eField[0]]; DemoPokemon EB() => _eTeam[_eField[1]];
        IEnumerator BeginBattle() { yield return new WaitForSeconds(0.5f); if (skipTeamPreview) OnTeamOrderConfirmed(new[] { 0, 1 }); }
        void OnTeamOrderConfirmed(int[] order) { _pTeam = new DemoPokemon[order.Length]; for (int i = 0; i < order.Length; i++) _pTeam[i] = _team6[order[i]]; StartCoroutine(EnterBattle()); }
        IEnumerator EnterBattle() { yield return new WaitForSeconds(0.5f); RefreshAllHUDs(); StartNewTurn(); }
        void RefreshAllHUDs() { RefreshHUD("Player_Lead_Slot", PA()); RefreshHUD("Player_Sub2_Slot", PB()); RefreshHUD("Enemy_Lead_Slot", EA()); RefreshHUD("Enemy_Sub2_Slot", EB()); }
        void RefreshHUD(string slotId, DemoPokemon p) { var h = FindHUD(slotId); if (h != null && p != null) h.SetupEntity(slotId, p.Name, p.CurrentHp, p.MaxHp); }
        EntityHUD FindHUD(string id) { foreach (var h in FindObjectsOfType<EntityHUD>()) if (h.entityId == id) return h; return null; }
        void StartNewTurn() { _curSlot = 0; PromptSlot(); }
        void PromptSlot() { var p = _curSlot == 0 ? PA() : PB(); if (p == null) { StartCoroutine(ResolveTurn()); return; } _phase = _curSlot == 0 ? Phase.PickingA : Phase.PickingB; BattleEvents.OnPrintDialog?.Invoke($"Lệnh cho {p.Name}!", true); }
        void OnSkillOrTarget(int idx) { if (_phase == Phase.PickingA) { _actA = new TurnAction { MoveIdx = idx, Attacker = PA() }; _curSlot = 1; PromptSlot(); } else if (_phase == Phase.PickingB) { _actB = new TurnAction { MoveIdx = idx, Attacker = PB() }; StartCoroutine(ResolveTurn()); } }
        IEnumerator ResolveTurn() { _phase = Phase.Resolving; yield return StartCoroutine(DoAction(_actA)); yield return StartCoroutine(DoAction(_actB)); StartNewTurn(); }
        IEnumerator DoAction(TurnAction a) { if (a.Attacker == null) yield break; BattleEvents.OnPrintDialog?.Invoke($"{a.Attacker.Name} dùng chiêu!", true); yield return new WaitForSeconds(1f); }
        void OnPartySlotChosen(int i) {} void OnPartyPanelCancelled() {} void OnVoluntarySwitchRequested(int i) {}
    }
}
