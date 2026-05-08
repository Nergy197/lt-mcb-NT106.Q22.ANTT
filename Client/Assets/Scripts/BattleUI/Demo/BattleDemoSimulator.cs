using UnityEngine;
using Game.Battle.UI;
using Game.Battle.Logic;
using System.Collections;
using System.Collections.Generic;

namespace Game.Battle.Demo
{
    /// <summary>
    /// Demo battle đầy đủ tương tác — không cần server/SignalR.
    /// Tự động disable BattleNetworkController nếu có trong scene.
    /// Flow: TeamPreview (chọn 4/6) → Đấu (chọn chiêu → chọn mục tiêu) → Kết thúc.
    /// </summary>
    public class BattleDemoSimulator : MonoBehaviour
    {
        [Header("Panels (tự tìm nếu để trống)")]
        public BattleSkillPanel   skillPanel;
        public BattleCommandPanel commandPanel;

        [Header("Demo Options")]
        [Tooltip("Bỏ qua Team Preview, tự động dùng 4 Pokemon đầu tiên")]
        public bool skipTeamPreview = true;

        // ── Inner types ───────────────────────────────────────────────────────

        class DemoMove
        {
            public string Name, Type, Category;
            public int    Power, CurrentPp, MaxPp;
            public int    Priority = 0;   // 0 mac dinh, Fake Out la +3
            public string InflictsStatus; // "SLP", "PAR", "BRN", etc.
            public bool   CausesFlinch;   // Fake Out luon flinch
            public bool   IsFakeOut;      // Check dieu kien turn 1
            public bool   GrantsProtection; // Protect, Detect, Spiky Shield
            public bool   IsRedirect;     // Follow Me, Rage Powder
            public bool   IsSwitchMove;   // U-turn, Volt Switch, Parting Shot
            public bool   HitsAll;        // Astral Barrage, Dazzling Gleam, Icy Wind
            public bool   IsAllyMove;     // Helping Hand, Pollen Puff (khi chon ally)
            public bool   IgnoresProtect; // Urshifu moves
            public bool   AlwaysCrit;     // Surging Strikes
            
            public int    RecoilPercent;  // Flare Blitz (33%)
            public int    DrainPercent;   // Drain Punch (50%)
            public string SelfStatDrop;   // "DEF_SPDEF-1" (Close Combat), "SPATK-2" (Draco Meteor)
            public string TargetStatDrop; // "SPD-1" (Icy Wind), "ATK-1" (Parting Shot)
            
            public enum TargetType { Single, Self, BothFoes, Ally }
            public TargetType Target = TargetType.Single;
        }

        class DemoPokemon
        {
            public string    Name, Type1, Type2;
            public int       Level = 50, MaxHp, CurrentHp;
            public DemoMove[] Moves;
            public bool      IsFainted;
            public string    Status;        // BRN/PAR/PSN/TOX/SLP/FRZ
            public bool      IsFlinching;   // Bi flinch boi Fake Out
            public bool      IsProtected;   // Dang dung Protect
            public int       TurnsOnField;  // Turn 1 moi dung duoc Fake Out
            
            public int Atk, Def, SpA, SpD, Spe;
            
            // Stat Stages (-6 to +6)
            public int AtkStage, DefStage, SpAtkStage, SpDefStage, SpdStage;
            public string Ability; // Intimidate, GrassySurge, etc.
            
            public int ProtectCounter; // De tinh xac suat Protect thanh cong lien tiep
        }

        struct TurnAction 
        {
            public bool IsPlayer;
            public int Slot;
            public int MoveIdx;
            public int TargetIdx;
            public bool UseTera;
            public int Priority;
            public DemoPokemon Attacker; // Pokemon thuc hien hanh dong
        }

        // ── HUD references (lấy từ BattleNetworkController khi Awake) ────────

        EntityHUD        _pHUD1, _pHUD2, _eHUD1, _eHUD2;
        BattleUIManager  _uiManager;

        // ── Field state ───────────────────────────────────────────────────────

        DemoPokemon[] _team6;          // 6 Pokemon player chọn từ
        DemoPokemon[] _pTeam;          // 4 Pokemon player đã chọn
        DemoPokemon[] _eTeam;          // đội địch cố định 4 Pokemon

        int[] _pField = { 0, 1 };      // index vào _pTeam đang trên sân [slotA, slotB]
        int[] _eField = { 0, 1 };      // index vào _eTeam đang trên sân [slotA, slotB]

        TurnAction _actA, _actB;
        bool       _actADone, _actBDone;

        enum Phase { TeamPreview, PickingA, PickingB, Resolving, ForcedSwitch, Ended }
        Phase _phase = Phase.TeamPreview;

        bool _pickTarget;
        int  _selectedMove, _curSlot, _turnNum = 1;
        bool _teraUsed;

        int  _redirectPlayerIdx = -1; // Index 0 or 1 neu co Follow Me
        int  _redirectEnemyIdx  = -1; 

        bool _helpingHandA, _helpingHandB; // Boost cho slot tiep theo
        bool _eHelpingHandA, _eHelpingHandB;

        bool _waitSwitch, _volSwitch;
        int  _forcedSlot, _volSlot;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            // Tắt tất cả script khác trên cùng object để tránh xung đột
            foreach (var mb in GetComponents<MonoBehaviour>())
            {
                if (mb != this && mb is not UnityEngine.EventSystems.UIBehaviour)
                {
                    Debug.Log($"[Demo] Disabling conflicting script: {mb.GetType().Name}");
                    mb.enabled = false;
                }
            }

            // Lấy HUD refs và panel refs từ BNC trước khi disable nó
            var nc = FindObjectOfType<BattleNetworkController>(true);
            if (nc != null)
            {
                _pHUD1 = nc.playerHUD1;
                _pHUD2 = nc.playerHUD2;
                _eHUD1 = nc.enemyHUD1;
                _eHUD2 = nc.enemyHUD2;
                if (skillPanel   == null) skillPanel   = nc.skillPanel;
                if (commandPanel == null) commandPanel = nc.commandPanel;
                nc.enabled = false;
            }

            if (skillPanel   == null) skillPanel   = FindObjectOfType<BattleSkillPanel>();
            if (commandPanel == null) commandPanel = FindObjectOfType<BattleCommandPanel>();
            if (_uiManager   == null) _uiManager   = FindObjectOfType<BattleUIManager>();
        }

        void Start()
        {
            InitTeams();
            StartCoroutine(BeginBattle());
        }

        void OnEnable()
        {
            BattleEvents.OnPlayerUseSkill           += OnSkillOrTarget;
            BattleEvents.OnTeamOrderConfirmed       += OnTeamOrderConfirmed;
            BattleEvents.OnPartySlotChosen          += OnPartySlotChosen;
            BattleEvents.OnPartyPanelCancelled      += OnPartyPanelCancelled;
            BattleEvents.OnVoluntarySwitchRequested += OnVoluntarySwitchRequested;
        }

        void OnDisable()
        {
            BattleEvents.OnPlayerUseSkill           -= OnSkillOrTarget;
            BattleEvents.OnTeamOrderConfirmed       -= OnTeamOrderConfirmed;
            BattleEvents.OnPartySlotChosen          -= OnPartySlotChosen;
            BattleEvents.OnPartyPanelCancelled      -= OnPartyPanelCancelled;
            BattleEvents.OnVoluntarySwitchRequested -= OnVoluntarySwitchRequested;
        }

        // ── Data init ─────────────────────────────────────────────────────────

        void InitTeams()
        {
            // Player Team: Reg G / Meta Team
            _team6 = new[]
            {
                P("Calyrex", "psychic", "ghost", 175, 85, 80, 165, 100, 150, "As One",
                    M("Astral Barrage",    "ghost",    "Special",  120, 5, all: true, target: DemoMove.TargetType.BothFoes),
                    M("Psyshock",          "psychic",  "Special",  80,  10),
                    M("Tera Blast",        "normal",   "Special",  80,  10),
                    M("Protect",           "normal",   "Status",   0,   10, pr: 4, prot: true, target: DemoMove.TargetType.Self)),

                P("Incineroar", "fire", "dark", 167, 115, 90, 80, 90, 60, "Intimidate",
                    M("Fake Out",          "normal",   "Physical", 40,  10, pr: 3, flinch: true, fakeOut: true),
                    M("Flare Blitz",       "fire",     "Physical", 120, 15, recoil: 33),
                    M("Knock Off",         "dark",     "Physical", 65,  20),
                    M("Parting Shot",      "dark",     "Status",   0,   16, pr: 1, sw: true, tgtDrop: "ATK_SPATK-1")),

                P("Rillaboom", "grass", null, 193, 125, 90, 60, 70, 85, "Grassy Surge",
                    M("Fake Out",          "normal",   "Physical", 40,  10, pr: 3, flinch: true, fakeOut: true),
                    M("Grassy Glide",      "grass",    "Physical", 70,  20, pr: 1),
                    M("Wood Hammer",       "grass",    "Physical", 120, 15, recoil: 33),
                    M("U-turn",            "bug",      "Physical", 70,  20, sw: true)),

                P("Urshifu", "fighting", "water", 175, 130, 100, 63, 60, 97, "Unseen Fist",
                    M("Surging Strikes",   "water",    "Physical", 25,  5, crit: true, ignore: true),
                    M("Aqua Jet",          "water",    "Physical", 40,  20, pr: 1, ignore: true),
                    M("Close Combat",      "fighting", "Physical", 120, 5, ignore: true, selfDrop: "DEF_SPDEF-1"),
                    M("Detect",            "fighting", "Status",   0,   5, pr: 4, prot: true, target: DemoMove.TargetType.Self)),

                P("Amoonguss", "grass", "poison", 209, 70, 70, 85, 80, 30, "Regenerator",
                    M("Spore",             "grass",    "Status",   0,   15),
                    M("Rage Powder",       "bug",      "Status",   0,   20, pr: 4, redir: true, target: DemoMove.TargetType.Self),
                    M("Pollen Puff",       "bug",      "Special",  90,  15, ally: true, target: DemoMove.TargetType.Ally),
                    M("Protect",           "normal",   "Status",   0,   10, pr: 4, prot: true, target: DemoMove.TargetType.Self)),

                P("Flutter Mane", "ghost", "fairy", 130, 55, 55, 135, 135, 135, "Protosynthesis",
                    M("Moonblast",         "fairy",    "Special",  95,  15, tgtDrop: "SPATK-1"),
                    M("Shadow Ball",       "ghost",    "Special",  80,  15, tgtDrop: "SPDEF-1"),
                    M("Dazzling Gleam",    "fairy",    "Special",  80,  10, all: true, target: DemoMove.TargetType.BothFoes),
                    M("Icy Wind",          "ice",      "Special",  55,  15, all: true, target: DemoMove.TargetType.BothFoes, tgtDrop: "SPD-1")),
            };

            // Opponent Team: Miraidon Team
            _eTeam = new[]
            {
                P("Miraidon", "electric", "dragon", 175, 85, 100, 135, 115, 135, "Hadron Engine",
                    M("Electro Drift",     "electric", "Special",  100, 5),
                    M("Draco Meteor",      "dragon",   "Special",  130, 5, selfDrop: "SPATK-2"),
                    M("Volt Switch",       "electric", "Special",  70,  20, sw: true),
                    M("Dazzling Gleam",    "fairy",    "Special",  80,  10, all: true, target: DemoMove.TargetType.BothFoes)),

                P("Iron Hands", "fighting", "electric", 250, 140, 108, 50, 68, 50, "Quark Drive",
                    M("Fake Out",          "normal",   "Physical", 40,  10, pr: 3, flinch: true, fakeOut: true),
                    M("Drain Punch",       "fighting", "Physical", 75,  10, drain: 50),
                    M("Wild Charge",       "electric", "Physical", 90,  15, recoil: 25),
                    M("Heavy Slam",        "steel",    "Physical", 80,  10)),

                P("Farigiraf", "normal", "psychic", 195, 90, 70, 110, 70, 60, "Armor Tail",
                    M("Armor Cannon",      "fire",     "Special",  120, 5, selfDrop: "DEF_SPDEF-1"),
                    M("Psychic",           "psychic",  "Special",  90,  10),
                    M("Trick Room",        "psychic",  "Status",   0,   5, pr: -7, target: DemoMove.TargetType.Self),
                    M("Helping Hand",      "normal",   "Status",   0,   20, pr: 5, ally: true, target: DemoMove.TargetType.Ally)),

                P("Ogerpon", "grass", "fire", 155, 120, 84, 60, 96, 110, "Embody Aspect",
                    M("Ivy Cudgel",        "fire",     "Physical", 100, 10),
                    M("Horn Leech",        "grass",    "Physical", 75,  10, drain: 50),
                    M("Spiky Shield",      "grass",    "Status",   0,   10, pr: 4, prot: true, target: DemoMove.TargetType.Self),
                    M("Follow Me",         "normal",   "Status",   0,   20, pr: 4, redir: true, target: DemoMove.TargetType.Self)),
            };
        }

        static DemoPokemon P(string name, string t1, string t2, int hp, int atk, int def, int spa, int spd, int spe, string ability, params DemoMove[] moves)
            => new DemoPokemon { 
                Name = name, Type1 = t1, Type2 = t2, 
                MaxHp = hp, CurrentHp = hp, 
                Atk = atk, Def = def, SpA = spa, SpD = spd, Spe = spe,
                Ability = ability, Moves = moves 
            };

        static DemoMove M(string name, string type, string cat, int power, int pp, string status = null, int pr = 0, bool flinch = false, bool fakeOut = false, bool prot = false, bool redir = false, bool sw = false, bool all = false, bool ally = false, bool ignore = false, bool crit = false, DemoMove.TargetType target = DemoMove.TargetType.Single, int recoil = 0, int drain = 0, string selfDrop = null, string tgtDrop = null)
            => new DemoMove { 
                Name = name, Type = type, Category = cat, Power = power, 
                CurrentPp = pp, MaxPp = pp, InflictsStatus = status, 
                Priority = pr, CausesFlinch = flinch, IsFakeOut = fakeOut,
                GrantsProtection = prot, IsRedirect = redir, IsSwitchMove = sw, HitsAll = all,
                IsAllyMove = ally, IgnoresProtect = ignore, AlwaysCrit = crit,
                Target = target, RecoilPercent = recoil, DrainPercent = drain,
                SelfStatDrop = selfDrop, TargetStatDrop = tgtDrop
            };

        // ── Field accessors ───────────────────────────────────────────────────

        DemoPokemon PA() => _pTeam != null ? _pTeam[_pField[0]] : null;
        DemoPokemon PB() => _pTeam != null ? _pTeam[_pField[1]] : null;
        DemoPokemon EA() => _eTeam[_eField[0]];
        DemoPokemon EB() => _eTeam[_eField[1]];

        // ── Battle start ──────────────────────────────────────────────────────

        IEnumerator BeginBattle()
        {
            yield return new WaitForSeconds(0.5f);

            if (skipTeamPreview)
            {
                BattleEvents.OnPrintDialog?.Invoke("DEMO BATTLE - Start!", true);
                yield return new WaitForSeconds(0.8f);
                OnTeamOrderConfirmed(new[] { 0, 1, 2, 3 });
            }
            else
            {
                // Mở Team Preview trực tiếp — KHÔNG gọi OnPrintDialog trước
                // vì Dialog coroutine sẽ tự SwitchPanel(None) sau khi type xong → giết TeamPreview
                var myArr  = new PreviewPokemon[_team6.Length];
                var oppArr = new PreviewPokemon[_eTeam.Length];
                for (int i = 0; i < _team6.Length; i++) myArr[i]  = ToPreview(_team6[i]);
                for (int i = 0; i < _eTeam.Length;  i++) oppArr[i] = ToPreview(_eTeam[i]);

                _phase = Phase.TeamPreview;
                Debug.Log("[Demo] Firing OnTeamPreviewStart");
                BattleEvents.OnTeamPreviewStart?.Invoke(new PreviewTeamData { MyTeam = myArr, OppTeam = oppArr });
            }
        }

        static PreviewPokemon ToPreview(DemoPokemon p)
            => new PreviewPokemon { Name = p.Name, Type1 = p.Type1, Type2 = p.Type2, Level = p.Level, MaxHp = p.MaxHp };

        void OnTeamOrderConfirmed(int[] order)
        {
            if (_phase != Phase.TeamPreview) return;
            _pTeam = new DemoPokemon[4];
            for (int i = 0; i < 4; i++) _pTeam[i] = _team6[order[i]];
            _pField[0] = 0; _pField[1] = 1;
            _eField[0] = 0; _eField[1] = 1;
            StartCoroutine(EnterBattle());
        }

        IEnumerator EnterBattle()
        {
            yield return new WaitForSeconds(0.5f);
            RefreshAllHUDs();
            BattleEvents.OnFieldConditionUpdated?.Invoke(new FieldConditionData { TurnNumber = 1, Weather = "Normal", Terrain = "Normal" });
            yield return new WaitForSeconds(0.4f);
            
            // Trigger abilities on entry
            yield return StartCoroutine(HandleAbilityEntry(PA(), "Player_Lead_Slot"));
            yield return StartCoroutine(HandleAbilityEntry(PB(), "Player_Sub2_Slot"));
            yield return StartCoroutine(HandleAbilityEntry(EA(), "Enemy_Lead_Slot"));
            yield return StartCoroutine(HandleAbilityEntry(EB(), "Enemy_Sub2_Slot"));

            BattleEvents.OnPrintDialog?.Invoke("Champion Cynthia muốn thi đấu!", true);
            yield return new WaitForSeconds(1.2f);
            StartNewTurn();
        }

        // ── HUD helpers ───────────────────────────────────────────────────────

        EntityHUD HUDForSlot(string slotId) => slotId switch
        {
            "Player_Lead_Slot" => _pHUD1,
            "Player_Sub2_Slot" => _pHUD2,
            "Enemy_Lead_Slot"  => _eHUD2,
            "Enemy_Sub2_Slot"  => _eHUD1,
            _ => null
        };

        string HudNameForSlot(string slotId) => slotId switch
        {
            "Player_Lead_Slot" => "Player1_HUD",
            "Player_Sub2_Slot" => "Player2_HUD",
            "Enemy_Lead_Slot"  => "Enemy2_HUD",
            "Enemy_Sub2_Slot"  => "Enemy1_HUD",
            _ => ""
        };

        void RefreshAllHUDs()
        {
            RefreshHUD(_pHUD1, "Player_Lead_Slot", PA());
            RefreshHUD(_pHUD2, "Player_Sub2_Slot", PB());
            RefreshHUD(_eHUD2, "Enemy_Lead_Slot",  EA());
            RefreshHUD(_eHUD1, "Enemy_Sub2_Slot",  EB());

            var loader = FindObjectOfType<BattleSpriteLoader>();
            if (loader == null) return;
            // Tham số 1: tên slot trên sân (để gắn sprite chiến đấu)
            // Tham số 2: tên HUD object (để gắn icon vào thanh máu Avatar_Box/Icon)
            if (PA() != null) loader.LoadSpriteForSlot("Player_Lead_Slot", "Player1_HUD", PA().Name.ToLower(), true);
            if (PB() != null) loader.LoadSpriteForSlot("Player_Sub2_Slot", "Player2_HUD", PB().Name.ToLower(), true);
            loader.LoadSpriteForSlot("Enemy_Lead_Slot", "Enemy2_HUD", EA().Name.ToLower(), false);
            loader.LoadSpriteForSlot("Enemy_Sub2_Slot", "Enemy1_HUD", EB().Name.ToLower(), false);
        }

        // Dùng direct EntityHUD ref — không tìm theo entityId để tránh lỗi Inspector
        void RefreshHUD(EntityHUD hud, string slotId, DemoPokemon p)
        {
            if (hud == null) return;
            if (p == null || p.IsFainted) { hud.gameObject.SetActive(false); return; }
            hud.gameObject.SetActive(true);
            hud.SetupEntity(slotId, p.Name, p.CurrentHp, p.MaxHp);
            hud.SetLevel(p.Level);
            hud.SetTypes(p.Type1, p.Type2);
            hud.SetStatus(p.Status);
        }

        // Overload tiện cho các chỗ gọi bằng slotId string
        void RefreshHUD(string slotId, DemoPokemon p) => RefreshHUD(HUDForSlot(slotId), slotId, p);

        // ── Turn flow ─────────────────────────────────────────────────────────

        void StartNewTurn()
        {
            _actA = default;
            _actB = default;
            _actADone = _actBDone = false;
            _curSlot  = 0;
            PromptSlot();
        }

        void PromptSlot()
        {
            var pkmn = _curSlot == 0 ? PA() : PB();
            if (pkmn == null || pkmn.IsFainted)
            {
                if (_curSlot == 0) { _curSlot = 1; PromptSlot(); return; }
                StartCoroutine(ResolveTurn());
                return;
            }

            _pickTarget = false; _selectedMove = -1;
            _phase = _curSlot == 0 ? Phase.PickingA : Phase.PickingB;

            if (commandPanel != null) commandPanel.CurrentSrcSlot = _curSlot;

            BattleEvents.OnPrintDialog?.Invoke($"[SLOT {(_curSlot == 0 ? "A" : "B")}] {pkmn.Name.ToUpper()} — Chọn hành động!", true);
            LoadMoves(pkmn);
            BattleEvents.OnTeraAvailabilityChanged?.Invoke(!_teraUsed);
            BattleEvents.OnPlayerTurnStart?.Invoke();
        }

        void LoadMoves(DemoPokemon p)
        {
            if (skillPanel == null) return;
            for (int i = 0; i < 4; i++)
            {
                if (i < p.Moves.Length)
                    skillPanel.SetMove(i, p.Moves[i].Name, p.Moves[i].Type,
                        p.Moves[i].Category, p.Moves[i].CurrentPp, p.Moves[i].MaxPp);
                else
                    skillPanel.SetMove(i, "---");
            }
        }

        // ── Input: chiêu / mục tiêu ───────────────────────────────────────────

        void OnSkillOrTarget(int idx)
        {
            if (_phase != Phase.PickingA && _phase != Phase.PickingB) return;

            var pkmn = _curSlot == 0 ? PA() : PB();
            if (pkmn == null) return;

            if (!_pickTarget)
            {
                _selectedMove = idx;
                var move = pkmn.Moves[idx];

                // Neu chieu la Self hoac BothFoes -> tu xac nhan TargetIdx luon
                if (move.Target == DemoMove.TargetType.Self)
                {
                    ConfirmAction(_selectedMove, _curSlot + 2); // Target chinh minh (2 hoac 3)
                    return;
                }
                if (move.Target == DemoMove.TargetType.BothFoes)
                {
                    ConfirmAction(_selectedMove, 0); // Target idx ko quan trong cho spread
                    return;
                }

                _pickTarget = true;
                skillPanel?.SetTargetLabel(0, (EA() != null && !EA().IsFainted) ? EA().Name : "---");
                skillPanel?.SetTargetLabel(1, (EB() != null && !EB().IsFainted) ? EB().Name : "---");
                skillPanel?.SetTargetLabel(2, (PA() != null && !PA().IsFainted) ? $"Ally {PA().Name}" : "---");
                skillPanel?.SetTargetLabel(3, (PB() != null && !PB().IsFainted) ? $"Ally {PB().Name}" : "---");
                _uiManager?.SwitchPanel(BattlePanelType.Skill);
            }
            else
            {
                ConfirmAction(_selectedMove, idx);
            }
        }

        void ConfirmAction(int moveIdx, int targetIdx)
        {
            _pickTarget = false;
            bool tera = skillPanel != null && skillPanel.IsTeraActive;
            if (tera)
            {
                _teraUsed = true;
                skillPanel.ResetTeraToggle();
                BattleEvents.OnTeraAvailabilityChanged?.Invoke(false);
            }

            if (_phase == Phase.PickingA)
            {
                _actA = new TurnAction { MoveIdx = moveIdx, TargetIdx = targetIdx, UseTera = tera, Attacker = PA() };
                _actADone = true; _curSlot = 1;
                PromptSlot();
            }
            else
            {
                _actB = new TurnAction { MoveIdx = moveIdx, TargetIdx = targetIdx, UseTera = tera, Attacker = PB() };
                _actBDone = true;
                StartCoroutine(ResolveTurn());
            }
        }

        // ── Resolution ────────────────────────────────────────────────────────

        IEnumerator ResolveTurn()
        {
            _phase = Phase.Resolving;

            var eActA = PickEnemyAction(0);
            var eActB = PickEnemyAction(1);

            // Gom tat ca action vao list de sap xep theo Priority
            var allActions = new List<TurnAction>();
            if (_actADone && _actA.Attacker != null && !_actA.Attacker.IsFainted)
            {
                _actA.IsPlayer = true; _actA.Slot = 0;
                _actA.Priority = _actA.Attacker.Moves[_actA.MoveIdx].Priority;
                allActions.Add(_actA);
            }
            if (_actBDone && _actB.Attacker != null && !_actB.Attacker.IsFainted)
            {
                _actB.IsPlayer = true; _actB.Slot = 1;
                _actB.Priority = _actB.Attacker.Moves[_actB.MoveIdx].Priority;
                allActions.Add(_actB);
            }

            if (EA() != null && !EA().IsFainted)
            {
                eActA.IsPlayer = false; eActA.Slot = 0;
                eActA.Attacker = EA();
                eActA.Priority = eActA.Attacker.Moves[eActA.MoveIdx].Priority;
                allActions.Add(eActA);
            }
            if (EB() != null && !EB().IsFainted)
            {
                eActB.IsPlayer = false; eActB.Slot = 1;
                eActB.Attacker = EB();
                eActB.Priority = eActB.Attacker.Moves[eActB.MoveIdx].Priority;
                allActions.Add(eActB);
            }

            // Sap xep theo Priority, neu bang thi xet Speed
            allActions.Sort((a, b) => {
                if (a.Priority != b.Priority) return b.Priority.CompareTo(a.Priority);
                
                var pA = a.Attacker;
                var pB = b.Attacker;
                float spdA = pA.Spe * GetStatMultiplier(pA.SpdStage);
                float spdB = pB.Spe * GetStatMultiplier(pB.SpdStage);
                
                if (Mathf.Abs(spdA - spdB) > 0.01f) return spdB.CompareTo(spdA);
                return a.IsPlayer ? -1 : 1;
            });

            foreach (var act in allActions)
            {
                yield return StartCoroutine(DoAction(act.IsPlayer, act.Slot, act));
                if (CheckBattleEnd()) yield break;
            }

            // Status damage cuối lượt (BRN/TOX)
            yield return StartCoroutine(ApplyEndOfTurnDamage());
            if (CheckBattleEnd()) yield break;

            // Forced switch nếu có Pokemon bị hạ
            yield return StartCoroutine(HandleForcedSwitches());
            if (CheckBattleEnd()) yield break;

            // Reset flinch va tang TurnsOnField
            ResetTurnStates();

            yield return StartCoroutine(WaitDialog());

            _turnNum++;
            BattleEvents.OnFieldConditionUpdated?.Invoke(new FieldConditionData { TurnNumber = _turnNum });
            yield return new WaitForSeconds(0.6f);
            StartNewTurn();
        }

        IEnumerator WaitDialog()
        {
            var dialog = FindObjectOfType<BattleDialogPanel>();
            if (dialog != null)
            {
                // Cho den khi dialog thuc su bat dau (neu co enqueue nhung chua kip type)
                yield return new WaitForSeconds(0.1f);
                while (dialog.IsBusy) yield return null;
            }
        }

        void ResetTurnStates()
        {
            // Reset ProtectCounter neu khong dung Protect o turn nay
            void CheckProtect(DemoPokemon p, TurnAction act) {
                if (p == null) return;
                if (act.MoveIdx != -1 && p.Moves[act.MoveIdx].GrantsProtection) 
                    p.ProtectCounter++;
                else 
                    p.ProtectCounter = 0;
            }
            CheckProtect(PA(), _actA); CheckProtect(PB(), _actB);
            // Enemy (tam thoi tinh logic don gian)
            
            DemoPokemon[] all = { PA(), PB(), EA(), EB() };
            foreach (var p in all)
            {
                if (p == null) continue;
                p.IsFlinching = false;
                p.IsProtected = false;
                if (!p.IsFainted) p.TurnsOnField++;
            }
            _redirectPlayerIdx = -1;
            _redirectEnemyIdx  = -1;
            _helpingHandA = _helpingHandB = false;
            _eHelpingHandA = _eHelpingHandB = false;
        }

        TurnAction PickEnemyAction(int slot)
        {
            var pkmn = slot == 0 ? EA() : EB();
            if (pkmn == null || pkmn.IsFainted) return default;

            // Ưu tiên chiêu tấn công còn PP
            var atkPool = new List<int>();
            var anyPool = new List<int>();
            for (int i = 0; i < pkmn.Moves.Length; i++)
            {
                if (pkmn.Moves[i].CurrentPp <= 0) continue;
                anyPool.Add(i);
                if (pkmn.Moves[i].Power > 0) atkPool.Add(i);
            }
            var pool = atkPool.Count > 0 ? atkPool : anyPool;
            int moveIdx = pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : 0;

            // Chọn mục tiêu player ngẫu nhiên còn sống
            var tgts = new List<int>();
            if (PA() != null && !PA().IsFainted) tgts.Add(0);
            if (PB() != null && !PB().IsFainted) tgts.Add(1);
            int tgt = tgts.Count > 0 ? tgts[Random.Range(0, tgts.Count)] : 0;

            return new TurnAction { MoveIdx = moveIdx, TargetIdx = tgt, Attacker = pkmn };
        }

        IEnumerator DoAction(bool isPlayer, int fieldSlot, TurnAction act)
        {
            var attacker = act.Attacker;
            if (attacker == null || attacker.IsFainted) yield break;

            // Kiem tra xem attacker con tren san tai dung slot do khong (tranh loi switch move)
            var currentInSlot = isPlayer ? (fieldSlot == 0 ? PA() : PB()) : (fieldSlot == 0 ? EA() : EB());
            if (currentInSlot != attacker) yield break;
            if (act.MoveIdx < 0 || act.MoveIdx >= attacker.Moves.Length) yield break;

            // ── FLINCH check ──────────────────────────────────────────────────
            if (attacker.IsFlinching)
            {
                BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} bi flinch va khong the danh!", true);
                yield return new WaitForSeconds(0.8f);
                yield break;
            }

            // SLP: bỏ lượt với xác suất 50%
            if (attacker.Status == "SLP")
            {
                if (Random.value < 0.5f)
                {
                    BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} đang ngủ... Bỏ lượt!", true);
                    yield return new WaitForSeconds(0.8f);
                    yield break;
                }
                // Thức dậy
                attacker.Status = null;
                string wakeSlotId = isPlayer
                    ? (fieldSlot == 0 ? "Player_Lead_Slot" : "Player_Sub2_Slot")
                    : (fieldSlot == 0 ? "Enemy_Lead_Slot"  : "Enemy_Sub2_Slot");
                RefreshHUD(wakeSlotId, attacker);
                BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} thức dậy!", true);
                yield return StartCoroutine(WaitDialog());
            }

            var move = attacker.Moves[act.MoveIdx];
            if (move.CurrentPp <= 0)
            {
                BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} không còn PP!", true);
                yield return StartCoroutine(WaitDialog()); yield break;
            }
            move.CurrentPp--;

            // ── REDIRECTION set ──────────────────────────────────────────────
            if (move.IsRedirect)
            {
                if (isPlayer) _redirectPlayerIdx = fieldSlot;
                else          _redirectEnemyIdx  = fieldSlot;
                BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} dang thu hut su chu y!", true);
                yield return StartCoroutine(WaitDialog());
                yield break;
            }

            // ── MULTI-TARGET ─────────────────────────────────────────────────
            if (move.HitsAll)
            {
                BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} dung {move.Name} len toan bo doi thu!", true);
                yield return new WaitForSeconds(0.2f);
                
                int[] targets = { 0, 1 }; 
                foreach (int tIdx in targets)
                {
                    var t = GetTarget(isPlayer, tIdx);
                    if (t == null || t.IsFainted) continue;
                    yield return StartCoroutine(ProcessDamage(attacker, move, t, TargetSlotId(isPlayer, tIdx), isMulti: true));
                }
                
                yield return StartCoroutine(WaitDialog());
                if (move.IsSwitchMove) yield return StartCoroutine(HandleSwitchMove(isPlayer, fieldSlot));
                yield break;
            }

            // ── SINGLE TARGET (check redirection) ────────────────────────────
            int finalTgtIdx = act.TargetIdx;
            if (isPlayer && _redirectEnemyIdx != -1 && act.TargetIdx < 2) // Player tan cong Enemy
                finalTgtIdx = _redirectEnemyIdx;
            else if (!isPlayer && _redirectPlayerIdx != -1 && act.TargetIdx < 2) // Enemy tan cong Player
                finalTgtIdx = _redirectPlayerIdx;

            var target = GetTarget(isPlayer, finalTgtIdx);
            if (target == null || target.IsFainted)
            {
                BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} dùng {move.Name}! Không trúng ai.", true);
                yield return StartCoroutine(WaitDialog()); yield break;
            }

            BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} dùng {move.Name}!", true);
            yield return new WaitForSeconds(0.2f);

            yield return StartCoroutine(ProcessDamage(attacker, move, target, TargetSlotId(isPlayer, finalTgtIdx)));
            
            yield return StartCoroutine(WaitDialog());
            if (move.IsSwitchMove && !attacker.IsFainted) 
                yield return StartCoroutine(HandleSwitchMove(isPlayer, fieldSlot));
        }

        IEnumerator ProcessDamage(DemoPokemon attacker, DemoMove move, DemoPokemon target, string slotId, bool isMulti = false)
        {
            // ── ALLY MOVE (Heal / Boost) ─────────────────────────────────────
            bool isAttackerPlayer = (attacker == PA() || attacker == PB());
            bool isTargetPlayer = slotId.Contains("Player");
            bool isAlly = (isAttackerPlayer == isTargetPlayer);
            
            if (move.IsAllyMove && isAlly)
            {
                if (move.Name == "Helping Hand")
                {
                    if (slotId.Contains("Player1")) _helpingHandA = true;
                    else if (slotId.Contains("Player2")) _helpingHandB = true;
                    else if (slotId.Contains("Enemy1")) _eHelpingHandA = true;
                    else if (slotId.Contains("Enemy2")) _eHelpingHandB = true;
                    BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} hỗ trợ {target.Name}!", true);
                }
                else if (move.Name == "Pollen Puff")
                {
                    int heal = target.MaxHp / 2;
                    target.CurrentHp = Mathf.Min(target.MaxHp, target.CurrentHp + heal);
                    SetHPDirect(slotId, target);
                    BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} hoi phuc {heal} HP cho {target.Name}!", true);
                }
                yield return StartCoroutine(WaitDialog()); yield break;
            }

            // ── Fake Out logic ───────────────────────────────────────────────
            if (move.IsFakeOut)
            {
                if (attacker.TurnsOnField > 0)
                {
                    BattleEvents.OnPrintDialog?.Invoke($"Nhung {move.Name} chi dung duoc o turn dau tien!", true);
                    yield return StartCoroutine(WaitDialog()); yield break;
                }
            }

            // ── PROTECT use ──────────────────────────────────────────────────
            if (move.GrantsProtection)
            {
                float successChance = Mathf.Pow(0.33f, attacker.ProtectCounter - 1);
                if (Random.value < successChance)
                {
                    attacker.IsProtected = true;
                    BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} đang bảo vệ bản thân!", true);
                }
                else
                {
                    BattleEvents.OnPrintDialog?.Invoke($"Nhưng {attacker.Name} đã thất bại!", true);
                }
                yield return StartCoroutine(WaitDialog());
                yield break;
            }

            // ── PROTECT check on target ──────────────────────────────────────
            if (target.IsProtected && !move.IgnoresProtect)
            {
                BattleEvents.OnPrintDialog?.Invoke($"{target.Name} da chan don tan cong!", true);
                yield return StartCoroutine(WaitDialog());
                yield break;
            }

            // ── Chiêu Status ──────────────────────────────────────────────────
            if (move.Category == "Status")
            {
                if (!string.IsNullOrEmpty(move.InflictsStatus) && string.IsNullOrEmpty(target.Status))
                {
                    target.Status = move.InflictsStatus;
                    RefreshHUD(slotId, target);
                    BattleEvents.OnPrintDialog?.Invoke($"{target.Name} bị {move.InflictsStatus}!", true);
                }
                else if (move.Name == "Parting Shot")
                {
                    BattleEvents.OnPrintDialog?.Invoke($"Tan cong cua {target.Name} giam manh!", true);
                }
                else
                {
                    BattleEvents.OnPrintDialog?.Invoke($"Nhưng không có hiệu quả thêm!", true);
                }
                yield return StartCoroutine(WaitDialog());
                yield break;
            }

            // ── Flinch effect ────────────────────────────────────────────────
            if (move.CausesFlinch)
            {
                target.IsFlinching = true;
            }

            // ── Chiêu tấn công ───────────────────────────────────────────────
            float power = move.Power;
            
            // Terrain Multipliers
            string currentTerrain = "Normal"; 
            if (currentTerrain == "Grassy" && move.Type == "grass") power *= 1.3f;
            if (currentTerrain == "Electric" && move.Type == "electric") power *= 1.3f;

            // Official-like Formula: Damage = [((2*Level/5 + 2) * Power * A/D) / 50 + 2]
            float a = (move.Category == "Physical") ? attacker.Atk * GetStatMultiplier(attacker.AtkStage) : attacker.SpA * GetStatMultiplier(attacker.SpAtkStage);
            float d = (move.Category == "Physical") ? target.Def * GetStatMultiplier(target.DefStage) : target.SpD * GetStatMultiplier(target.SpDefStage);

            float damageTotal = (((2f * attacker.Level / 5f + 2f) * power * a / d) / 50f) + 2f;
            
            // ── MODIFIERS ────────────────────────────────────────────────────
            
            // Helping Hand boost (on damage) - reuse isAttackerPlayer from above
            bool isSlotA = (attacker == PA() || attacker == EA());
            bool hasHH_active = isAttackerPlayer 
                ? (isSlotA ? _helpingHandA : _helpingHandB) 
                : (isSlotA ? _eHelpingHandA : _eHelpingHandB);
            if (hasHH_active) damageTotal *= 1.5f;

            // Spread Damage reduction
            if (isMulti) damageTotal *= 0.75f;

            // STAB
            if (move.Type == attacker.Type1 || move.Type == attacker.Type2) damageTotal *= 1.5f;

            // Type Multiplier
            float typeMult = GetTypeMultiplier(move.Type, target.Type1, target.Type2);
            damageTotal *= typeMult;

            // BRN halves physical damage
            if (move.Category == "Physical" && attacker.Status == "BRN") damageTotal *= 0.5f;

            // Crit
            bool isCrit = move.AlwaysCrit || (Random.value < 0.06f);
            if (isCrit) damageTotal *= 1.5f;

            int dmg = Mathf.Max(1, Mathf.RoundToInt(damageTotal * Random.Range(0.85f, 1.0f)));

            target.CurrentHp = Mathf.Max(0, target.CurrentHp - dmg);

            // Cập nhật trực tiếp lên HUD
            SetHPDirect(slotId, target);
            
            string msg = $"{target.Name} mất {dmg} HP!";
            if (isCrit) msg = "CHI MANG! " + msg;
            if (typeMult > 1.1f) msg = "RAT HIEU QUA! " + msg;
            if (typeMult < 0.9f && typeMult > 0.01f) msg = "Khong hieu qua lam... " + msg;
            if (typeMult <= 0.01f) msg = "Khong co tac dung.";

            BattleEvents.OnPrintDialog?.Invoke(msg, true);
            yield return new WaitForSeconds(0.3f);
            
            // ── Drain ────────────────────────────────────────────────────────
            if (move.DrainPercent > 0 && dmg > 0 && attacker.CurrentHp < attacker.MaxHp)
            {
                int heal = Mathf.RoundToInt(dmg * move.DrainPercent / 100f);
                attacker.CurrentHp = Mathf.Min(attacker.MaxHp, attacker.CurrentHp + heal);
                RefreshHUD(HudForAttacker(attacker), attacker);
                BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} hút HP! Hồi {heal} HP.", true);
                yield return new WaitForSeconds(0.6f);
            }

            if (target.CurrentHp <= 0)
            {
                target.IsFainted = true;
                SetHPDirect(slotId, target);
                BattleEvents.OnPrintDialog?.Invoke($"{target.Name} bi ha guc!", true);
                yield return new WaitForSeconds(0.8f);
                RefreshHUD(slotId, null);
                var faintLoader = FindObjectOfType<BattleSpriteLoader>();
                faintLoader?.ClearBattleSprite(slotId);
            }
            else
            {
                // ── Target Stat Drops ────────────────────────────────────────
                if (!string.IsNullOrEmpty(move.TargetStatDrop))
                {
                    yield return StartCoroutine(HandleStatString(target, slotId, move.TargetStatDrop));
                }
            }
            
            // ── Self Effects ─────────────────────────────────────────────────
            if (!attacker.IsFainted)
            {
                if (!string.IsNullOrEmpty(move.SelfStatDrop))
                    yield return StartCoroutine(HandleStatString(attacker, HudForAttacker(attacker), move.SelfStatDrop));

                if (move.RecoilPercent > 0 && dmg > 0)
                {
                    int recoil = Mathf.RoundToInt(dmg * move.RecoilPercent / 100f);
                    attacker.CurrentHp = Mathf.Max(0, attacker.CurrentHp - recoil);
                    RefreshHUD(HudForAttacker(attacker), attacker);
                    BattleEvents.OnPrintDialog?.Invoke($"{attacker.Name} bị phản đòn! Mất {recoil} HP.", true);
                    yield return new WaitForSeconds(0.7f);
                    if (attacker.CurrentHp <= 0) attacker.IsFainted = true;
                }
            }
        }

        string HudForAttacker(DemoPokemon p)
        {
            if (p == PA()) return "Player_Lead_Slot";
            if (p == PB()) return "Player_Sub2_Slot";
            if (p == EA()) return "Enemy_Lead_Slot";
            return "Enemy_Sub2_Slot";
        }

        IEnumerator HandleStatString(DemoPokemon p, string sid, string dropStr)
        {
            // Format: "ATK-1", "DEF_SPDEF-1", "ATK_SPATK-1"
            string[] parts = dropStr.Split('-');
            int change = int.Parse(parts[1]);
            string[] stats = parts[0].Split('_');
            foreach (var s in stats)
                yield return StartCoroutine(ApplyStatChange(p, sid, s, change));
        }

        float GetTypeMultiplier(string atk, string t1, string t2)
        {
            float m1 = GetSingleMult(atk, t1);
            float m2 = GetSingleMult(atk, t2);
            return m1 * m2;
        }

        float GetSingleMult(string atk, string def)
        {
            if (string.IsNullOrEmpty(def)) return 1f;
            atk = atk.ToLower(); def = def.ToLower();

            if (atk == "fire") { if (def == "grass" || def == "steel" || def == "ice" || def == "bug") return 2f; if (def == "fire" || def == "water" || def == "rock" || def == "dragon") return 0.5f; }
            if (atk == "water") { if (def == "fire" || def == "ground" || def == "rock") return 2f; if (def == "water" || def == "grass" || def == "dragon") return 0.5f; }
            if (atk == "grass") { if (def == "water" || def == "ground" || def == "rock") return 2f; if (def == "fire" || def == "grass" || def == "poison" || def == "flying" || def == "bug" || def == "dragon" || def == "steel") return 0.5f; }
            if (atk == "electric") { if (def == "water" || def == "flying") return 2f; if (def == "electric" || def == "grass" || def == "dragon") return 0.5f; if (def == "ground") return 0f; }
            if (atk == "psychic") { if (def == "fighting" || def == "poison") return 2f; if (def == "psychic" || def == "steel") return 0.5f; if (def == "dark") return 0f; }
            if (atk == "ghost") { if (def == "psychic" || def == "ghost") return 2f; if (def == "dark") return 0.5f; if (def == "normal") return 0f; }
            if (atk == "fighting") { if (def == "normal" || def == "ice" || def == "rock" || def == "dark" || def == "steel") return 2f; if (def == "poison" || def == "flying" || def == "psychic" || def == "bug" || def == "fairy") return 0.5f; if (def == "ghost") return 0f; }
            if (atk == "fairy") { if (def == "fighting" || def == "dragon" || def == "dark") return 2f; if (def == "fire" || def == "poison" || def == "steel") return 0.5f; }
            if (atk == "dark") { if (def == "psychic" || def == "ghost") return 2f; if (def == "fighting" || def == "dark" || def == "fairy") return 0.5f; }
            if (atk == "dragon") { if (def == "dragon") return 2f; if (def == "steel") return 0.5f; if (def == "fairy") return 0f; }
            if (atk == "steel") { if (def == "ice" || def == "rock" || def == "fairy") return 2f; if (def == "fire" || def == "water" || def == "electric" || def == "steel") return 0.5f; }
            if (atk == "ice") { if (def == "grass" || def == "ground" || def == "flying" || def == "dragon") return 2f; if (def == "fire" || def == "water" || def == "ice" || def == "steel") return 0.5f; }
            
            return 1f;
        }

        IEnumerator HandleSwitchMove(bool isPlayer, int slot)
        {
            if (isPlayer)
            {
                var avail = new List<int>();
                for (int i = 0; i < _pTeam.Length; i++)
                    if (!_pTeam[i].IsFainted && i != _pField[0] && i != _pField[1])
                        avail.Add(i);

                if (avail.Count > 0)
                {
                    _volSwitch = true; _volSlot = slot;
                    BattleEvents.OnPartyPanelOpen?.Invoke(BuildPartyData(avail.ToArray(), false, -1));
                    while (_volSwitch) yield return null;
                }
            }
            else
            {
                // Enemy auto switch
                for (int i = 0; i < _eTeam.Length; i++)
                {
                    if (_eTeam[i].IsFainted || i == _eField[0] || i == _eField[1]) continue;
                    _eField[slot] = i;
                    string sid = slot == 0 ? "Enemy_Lead_Slot" : "Enemy_Sub2_Slot";
                    var loader = FindObjectOfType<BattleSpriteLoader>();
                    loader?.ClearBattleSprite(sid);
                    BattleEvents.OnPrintDialog?.Invoke($"Doi thu rut lui va gui {_eTeam[i].Name} vao san!", true);
                    RefreshHUD(sid, _eTeam[i]);
                    loader?.LoadSpriteForSlot(sid, HudNameForSlot(sid), _eTeam[i].Name.ToLower(), false);
                    yield return new WaitForSeconds(1f);
                    break;
                }
            }
        }

        IEnumerator ApplyEndOfTurnDamage()
        {
            DemoPokemon[] allField = { PA(), PB(), EA(), EB() };
            string[]      slotIds  = { "Player_Lead_Slot", "Player_Sub2_Slot", "Enemy_Lead_Slot", "Enemy_Sub2_Slot" };

            for (int i = 0; i < allField.Length; i++)
            {
                var p = allField[i];
                if (p == null || p.IsFainted) continue;

                int dot = 0;
                if (p.Status == "BRN" || p.Status == "PSN") dot = Mathf.Max(1, p.MaxHp / 16);
                if (p.Status == "TOX") dot = Mathf.Max(1, p.MaxHp / 8);

                if (dot <= 0) continue;
                p.CurrentHp = Mathf.Max(0, p.CurrentHp - dot);
                SetHPDirect(slotIds[i], p);
                BattleEvents.OnPrintDialog?.Invoke($"{p.Name} đau vì {p.Status}! -{dot} HP", true);
                yield return new WaitForSeconds(0.7f);

                if (p.CurrentHp <= 0)
                {
                    p.IsFainted = true;
                    SetHPDirect(slotIds[i], p);
                    BattleEvents.OnPrintDialog?.Invoke($"{p.Name} bị hạ gục!", true);
                    yield return new WaitForSeconds(0.8f);
                    RefreshHUD(slotIds[i], null);
                }
            }
        }

        // ── Forced / Voluntary switch ─────────────────────────────────────────

        IEnumerator HandleForcedSwitches()
        {
            // Player forced switch
            for (int s = 0; s < 2; s++)
            {
                var onField = s == 0 ? PA() : PB();
                if (onField == null || !onField.IsFainted) continue;

                var avail = new List<int>();
                for (int i = 0; i < _pTeam.Length; i++)
                    if (!_pTeam[i].IsFainted && i != _pField[0] && i != _pField[1])
                        avail.Add(i);

                if (avail.Count == 0) continue;

                _forcedSlot = s; _waitSwitch = true;
                BattleEvents.OnPartyPanelOpen?.Invoke(BuildPartyData(avail.ToArray(), true, s));
                while (_waitSwitch) yield return null;
            }

            // Enemy auto switch
            for (int s = 0; s < 2; s++)
            {
                var onField = s == 0 ? EA() : EB();
                if (onField == null || !onField.IsFainted) continue;

                for (int i = 0; i < _eTeam.Length; i++)
                {
                    if (_eTeam[i].IsFainted || i == _eField[0] || i == _eField[1]) continue;
                    _eField[s] = i;
                    string sid = s == 0 ? "Enemy_Lead_Slot" : "Enemy_Sub2_Slot";
                    var loader = FindObjectOfType<BattleSpriteLoader>();
                    loader?.ClearBattleSprite(sid);
                    BattleEvents.OnPrintDialog?.Invoke($"Doi thu gui {_eTeam[i].Name} vao san!", true);
                    RefreshHUD(sid, _eTeam[i]);
                    loader?.LoadSpriteForSlot(sid, HudNameForSlot(sid), _eTeam[i].Name.ToLower(), false);
                    yield return StartCoroutine(HandleAbilityEntry(_eTeam[i], sid));
                    yield return new WaitForSeconds(0.5f);
                    break;
                }
            }
        }

        IEnumerator HandleAbilityEntry(DemoPokemon p, string slotId)
        {
            if (p == null || p.IsFainted) yield break;
            
            if (p.Ability == "Intimidate")
            {
                BattleEvents.OnPrintDialog?.Invoke($"{p.Name} phat huy kha nang Intimidate!", true);
                yield return new WaitForSeconds(0.6f);
                
                bool isPlayer = slotId.Contains("Player");
                DemoPokemon[] targets = isPlayer ? new[] { EA(), EB() } : new[] { PA(), PB() };
                string[] targetSids = isPlayer ? new[] { "Enemy_Lead_Slot", "Enemy_Sub2_Slot" } : new[] { "Player_Lead_Slot", "Player_Sub2_Slot" };
                
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] != null && !targets[i].IsFainted)
                        yield return StartCoroutine(ApplyStatChange(targets[i], targetSids[i], "ATK", -1));
                }
            }
            
            if (p.Ability == "Grassy Surge")
            {
                BattleEvents.OnPrintDialog?.Invoke($"{p.Name} tao ra Grassy Terrain!", true);
                BattleEvents.OnFieldConditionUpdated?.Invoke(new FieldConditionData { TurnNumber = _turnNum, Terrain = "Grassy" });
                yield return new WaitForSeconds(0.6f);
            }

            if (p.Ability == "Hadron Engine")
            {
                BattleEvents.OnPrintDialog?.Invoke($"{p.Name} tao ra Electric Terrain!", true);
                BattleEvents.OnFieldConditionUpdated?.Invoke(new FieldConditionData { TurnNumber = _turnNum, Terrain = "Electric" });
                yield return new WaitForSeconds(0.6f);
            }
        }

        IEnumerator ApplyStatChange(DemoPokemon p, string slotId, string stat, int change)
        {
            if (p == null || p.IsFainted) yield break;
            
            string statName = "";
            switch (stat)
            {
                case "ATK": p.AtkStage = Mathf.Clamp(p.AtkStage + change, -6, 6); statName = "Attack"; break;
                case "DEF": p.DefStage = Mathf.Clamp(p.DefStage + change, -6, 6); statName = "Defense"; break;
                case "SPATK": p.SpAtkStage = Mathf.Clamp(p.SpAtkStage + change, -6, 6); statName = "Sp. Atk"; break;
                case "SPDEF": p.SpDefStage = Mathf.Clamp(p.SpDefStage + change, -6, 6); statName = "Sp. Def"; break;
                case "SPD": p.SpdStage = Mathf.Clamp(p.SpdStage + change, -6, 6); statName = "Speed"; break;
            }
            
            string direction = change > 0 ? "tang" : "giam";
            BattleEvents.OnPrintDialog?.Invoke($"{p.Name} có {statName} {direction}!", true);
            yield return new WaitForSeconds(0.6f);
        }

        float GetStatMultiplier(int stage)
        {
            if (stage >= 0) return (2f + stage) / 2f;
            return 2f / (2f + Mathf.Abs(stage));
        }

        void OnPartySlotChosen(int idx)
        {
            if (_waitSwitch)
            {
                _pField[_forcedSlot] = idx;
                string sid = _forcedSlot == 0 ? "Player_Lead_Slot" : "Player_Sub2_Slot";
                var loader = FindObjectOfType<BattleSpriteLoader>();
                loader?.ClearBattleSprite(sid);
                BattleEvents.OnPrintDialog?.Invoke($"Tung {_pTeam[idx].Name} ra san!", true);
                RefreshHUD(sid, _pTeam[idx]);
                loader?.LoadSpriteForSlot(sid, HudNameForSlot(sid), _pTeam[idx].Name.ToLower(), true);
                StartCoroutine(HandleAbilityEntry(_pTeam[idx], sid));
                _waitSwitch = false;
                return;
            }

            if (_volSwitch)
            {
                _pField[_volSlot] = idx;
                string sid = _volSlot == 0 ? "Player_Lead_Slot" : "Player_Sub2_Slot";
                var loader = FindObjectOfType<BattleSpriteLoader>();
                loader?.ClearBattleSprite(sid);
                BattleEvents.OnPrintDialog?.Invoke($"Tung {_pTeam[idx].Name} ra san!", true);
                RefreshHUD(sid, _pTeam[idx]);
                loader?.LoadSpriteForSlot(sid, HudNameForSlot(sid), _pTeam[idx].Name.ToLower(), true);
                StartCoroutine(HandleAbilityEntry(_pTeam[idx], sid));
                _volSwitch = false;

                // Đổi Pokemon = bỏ lượt tấn công của slot đó (MoveIdx=-1 → DoAction skip)
                if (_volSlot == 0) { _actA = new TurnAction { MoveIdx = -1 }; _actADone = true; _curSlot = 1; PromptSlot(); }
                else               { _actB = new TurnAction { MoveIdx = -1 }; _actBDone = true; StartCoroutine(ResolveTurn()); }
            }
        }

        void OnPartyPanelCancelled()
        {
            _volSwitch = false;
            BattleEvents.OnPlayerTurnStart?.Invoke();
        }

        void OnVoluntarySwitchRequested(int srcSlot)
        {
            if (_phase != Phase.PickingA && _phase != Phase.PickingB) return;

            var avail = new List<int>();
            for (int i = 0; i < _pTeam.Length; i++)
                if (!_pTeam[i].IsFainted && i != _pField[0] && i != _pField[1])
                    avail.Add(i);

            if (avail.Count == 0)
            {
                BattleEvents.OnPrintDialog?.Invoke("Không còn Pokemon để đổi!", true);
                return;
            }
            _volSwitch = true; _volSlot = srcSlot;
            BattleEvents.OnPartyPanelOpen?.Invoke(BuildPartyData(avail.ToArray(), false, -1));
        }

        PartyPanelData BuildPartyData(int[] avail, bool isForced, int forcedSlot)
        {
            var pkmns = new PartyPokemon[_pTeam.Length];
            for (int i = 0; i < _pTeam.Length; i++)
                pkmns[i] = new PartyPokemon
                {
                    PartyIndex = i,
                    Name       = _pTeam[i].Name,
                    Type1      = _pTeam[i].Type1,
                    Type2      = _pTeam[i].Type2,
                    CurrentHp  = _pTeam[i].CurrentHp,
                    MaxHp      = _pTeam[i].MaxHp,
                    IsFainted  = _pTeam[i].IsFainted,
                    IsActive   = (i == _pField[0] || i == _pField[1]),
                    Status     = _pTeam[i].Status,
                };
            return new PartyPanelData
            {
                Pokemon        = pkmns,
                AvailableIdxs  = avail,
                IsForcedSwitch = isForced,
                ForcedSlot     = forcedSlot,
            };
        }

        // ── Win / Loss ────────────────────────────────────────────────────────

        bool CheckBattleEnd()
        {
            bool pLost = true, eLost = true;
            if (_pTeam != null)
                for (int i = 0; i < _pTeam.Length; i++)
                    if (!_pTeam[i].IsFainted) { pLost = false; break; }
            for (int i = 0; i < _eTeam.Length; i++)
                if (!_eTeam[i].IsFainted) { eLost = false; break; }

            if (!pLost && !eLost) return false;
            _phase = Phase.Ended;
            bool won = eLost && !pLost;
            BattleEvents.OnPrintDialog?.Invoke(
                won ? "🏆 BẠN THẮNG! Demo kết thúc." : "💀 BẠN THUA... Demo kết thúc.", false);
            BattleEvents.OnBattleResult?.Invoke(won, "Demo Cynthia");
            return true;
        }

        // ── Utility ───────────────────────────────────────────────────────────

        // Trả về slot ID của mục tiêu tương ứng với idx do attacker chọn.
        // Khi player tấn công: 0=EnemyA, 1=EnemyB, 2=PlayerA(ally), 3=PlayerB(ally)
        // Khi địch tấn công:  0=PlayerA, 1=PlayerB
        string TargetSlotId(bool attackerIsPlayer, int targetIdx) => attackerIsPlayer
            ? targetIdx switch
            {
                0 => "Enemy_Lead_Slot", 1 => "Enemy_Sub2_Slot",
                2 => "Player_Lead_Slot", 3 => "Player_Sub2_Slot",
                _ => ""
            }
            : targetIdx switch { 0 => "Player_Lead_Slot", 1 => "Player_Sub2_Slot", _ => "" };

        DemoPokemon GetTarget(bool attackerIsPlayer, int idx) => attackerIsPlayer
            ? idx switch { 0 => EA(), 1 => EB(), 2 => PA(), 3 => PB(), _ => null }
            : idx switch { 0 => PA(), 1 => PB(), _ => null };

        // Gọi SetupEntity trực tiếp lên HUD — hoạt động kể cả khi HUD bị ẩn bởi Dialog.
        // Giải quyết lỗi: UIManager hide globalHUDs khi Dialog mở → EntityHUD.OnDisable
        // → unsubscribe OnHealthChanged → event bị bỏ qua → HP bar không đổi.
        void SetHPDirect(string slotId, DemoPokemon p)
        {
            var hud = HUDForSlot(slotId);
            if (hud == null) return;
            hud.SetupEntity(slotId, p.Name, p.CurrentHp, p.MaxHp);
        }
    }
}
