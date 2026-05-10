using UnityEngine;
using Game.Battle.UI;
using Game.Battle.Logic;
using System.Collections;
using System.Collections.Generic;

namespace Game.Battle.Demo
{
    /// <summary>
    /// Trận đấu demo hoàn chỉnh — flow y hệt BattleNetworkController nhưng đánh với AI cục bộ.
    /// Không cần đăng nhập, không cần server.
    /// Thêm vào scene thay cho BattleDemoSimulator. Tự tìm HUD, panels từ scene.
    /// </summary>
    public class BotBattleController : MonoBehaviour
    {
        [Header("Demo")]
        public bool skipTeamPreview = true;

        // ── Data ─────────────────────────────────────────────────────────────

        class BotMove
        {
            public string Name, Type, Category;
            public int    Power, Pp, MaxPp;
            public string StatusEffect; // "SLP" / "PAR" / "BRN" / null
        }

        class BotPokemon
        {
            public string   Name, Type1, Type2;
            public int      Level = 50, Hp, MaxHp;
            public BotMove[] Moves;
            public bool     Fainted;
            public string   Status;
        }

        struct BotAction
        {
            public BotPokemon Attacker, Target;
            public BotMove    Move;
            public bool       UseTera, IsSwitch;
        }

        // ── Scene refs ────────────────────────────────────────────────────────

        EntityHUD        _pHUD1, _pHUD2, _eHUD1, _eHUD2;
        BattleSkillPanel   _skill;
        BattleCommandPanel _cmd;
        BattleUIManager    _ui;

        // ── Battle data ───────────────────────────────────────────────────────

        BotPokemon[] _myPool;        // 6 Pokemon của player để chọn
        BotPokemon[] _myTeam;        // 4 Pokemon player đã chọn
        BotPokemon[] _botTeam;       // đội bot cố định 4 con

        BotPokemon[] _myField  = new BotPokemon[2]; // [0]=SlotA, [1]=SlotB
        BotPokemon[] _botField = new BotPokemon[2]; // [0]=EnemyA, [1]=EnemyB

        BotAction? _actA, _actB;

        int  _srcSlot;
        int  _selMove;
        bool _pickTarget, _teraUsed;
        int  _turn = 1;

        bool _waitSwitch, _volSwitch;
        int  _forcedSlot, _volSlot;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            var bnc = FindObjectOfType<BattleNetworkController>(true);
            if (bnc != null)
            {
                // Lấy HUD refs từ BNC trước khi disable
                _pHUD1 = bnc.playerHUD1; _pHUD2 = bnc.playerHUD2;
                _eHUD1 = bnc.enemyHUD1;  _eHUD2 = bnc.enemyHUD2;
                _skill = bnc.skillPanel;  _cmd   = bnc.commandPanel;
                bnc.enabled = false;
            }

            // Fallback: tìm HUD theo entityId trong Inspector
            if (_pHUD1 == null || _pHUD2 == null || _eHUD1 == null || _eHUD2 == null)
            {
                foreach (var h in FindObjectsByType<EntityHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    switch (h.entityId)
                    {
                        case "Player_Lead_Slot": _pHUD1 = _pHUD1 ?? h; break;
                        case "Player_Sub2_Slot": _pHUD2 = _pHUD2 ?? h; break;
                        case "Enemy_Lead_Slot":  _eHUD1 = _eHUD1 ?? h; break;
                        case "Enemy_Sub2_Slot":  _eHUD2 = _eHUD2 ?? h; break;
                    }
                }
            }

            if (_skill == null) _skill = FindObjectOfType<BattleSkillPanel>(true);
            if (_cmd   == null) _cmd   = FindObjectOfType<BattleCommandPanel>(true);
            if (_ui    == null) _ui    = FindObjectOfType<BattleUIManager>(true);

            BuildTeams();
        }

        void Start() => StartCoroutine(RunBattle());

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

        // ── Teams ─────────────────────────────────────────────────────────────

        void BuildTeams()
        {
            _myPool = new[]
            {
                P("Incineroar",  "fire",     "dark",     167,
                    M("Fake Out",        "normal",   "Physical", 40,  10),
                    M("Flare Blitz",     "fire",     "Physical", 120, 15),
                    M("Close Combat",    "fighting", "Physical", 120, 5),
                    M("Parting Shot",    "dark",     "Status",   0,   16)),
                P("Rillaboom",   "grass",    null,       193,
                    M("Fake Out",        "normal",   "Physical", 40,  10),
                    M("Grassy Glide",    "grass",    "Physical", 70,  20),
                    M("Wood Hammer",     "grass",    "Physical", 120, 15),
                    M("U-turn",          "bug",      "Physical", 70,  20)),
                P("Flutter Mane","ghost",    "fairy",    130,
                    M("Moonblast",       "fairy",    "Special",  95,  15),
                    M("Shadow Ball",     "ghost",    "Special",  80,  15),
                    M("Dazzling Gleam",  "fairy",    "Special",  80,  10),
                    M("Calm Mind",       "psychic",  "Status",   0,   20)),
                P("Garchomp",    "dragon",   "ground",   183,
                    M("Earthquake",      "ground",   "Physical", 100, 10),
                    M("Dragon Claw",     "dragon",   "Physical", 80,  15),
                    M("Scale Shot",      "dragon",   "Physical", 60,  20),
                    M("Iron Head",       "steel",    "Physical", 80,  15)),
                P("Tornadus",    "flying",   null,       150,
                    M("Tailwind",        "flying",   "Status",   0,   30),
                    M("Hurricane",       "flying",   "Special",  110, 10),
                    M("Bleakwind Storm", "flying",   "Special",  100, 10),
                    M("Protect",         "normal",   "Status",   0,   10)),
                P("Amoonguss",   "grass",    "poison",   209,
                    M("Spore",           "grass",    "Status",   0,   15, "SLP"),
                    M("Rage Powder",     "bug",      "Status",   0,   20),
                    M("Pollen Puff",     "bug",      "Special",  90,  15),
                    M("Protect",         "normal",   "Status",   0,   10)),
            };

            _botTeam = new[]
            {
                P("Charizard",   "fire",     "flying",   153,
                    M("Heat Wave",       "fire",     "Special",  95,  10),
                    M("Flamethrower",    "fire",     "Special",  90,  15),
                    M("Air Slash",       "flying",   "Special",  75,  15),
                    M("Protect",         "normal",   "Status",   0,   10)),
                P("Iron Hands",  "fighting", "electric", 250,
                    M("Fake Out",        "normal",   "Physical", 40,  10),
                    M("Close Combat",    "fighting", "Physical", 120, 5),
                    M("Wild Charge",     "electric", "Physical", 90,  15),
                    M("Heavy Slam",      "steel",    "Physical", 80,  10)),
                P("Urshifu",     "fighting", "water",    175,
                    M("Surging Strikes", "water",    "Physical", 25,  5),
                    M("Close Combat",    "fighting", "Physical", 120, 5),
                    M("Aqua Jet",        "water",    "Physical", 40,  20),
                    M("U-turn",          "bug",      "Physical", 70,  20)),
                P("Landorus-T",  "ground",   "flying",   185,
                    M("Earthquake",      "ground",   "Physical", 100, 10),
                    M("Rock Slide",      "rock",     "Physical", 75,  10),
                    M("U-turn",          "bug",      "Physical", 70,  20),
                    M("Protect",         "normal",   "Status",   0,   10)),
            };
        }

        static BotPokemon P(string n, string t1, string t2, int hp, params BotMove[] mv)
            => new BotPokemon { Name=n, Type1=t1, Type2=t2, MaxHp=hp, Hp=hp, Moves=mv };

        static BotMove M(string n, string t, string cat, int pow, int pp, string eff=null)
            => new BotMove { Name=n, Type=t, Category=cat, Power=pow, Pp=pp, MaxPp=pp, StatusEffect=eff };

        // ── Battle flow ───────────────────────────────────────────────────────

        IEnumerator RunBattle()
        {
            yield return new WaitForSeconds(0.5f);
            if (skipTeamPreview)
            {
                _myTeam = new[] { _myPool[0], _myPool[1], _myPool[2], _myPool[3] };
                InitField();
                StartCoroutine(EnterField());
            }
            else
            {
                var my  = ToPreviewArr(_myPool);
                var opp = ToPreviewArr(_botTeam);
                BattleEvents.OnTeamPreviewStart?.Invoke(new PreviewTeamData { MyTeam=my, OppTeam=opp });
            }
        }

        void OnTeamOrderConfirmed(int[] order)
        {
            _myTeam = new BotPokemon[4];
            for (int i = 0; i < 4; i++) _myTeam[i] = _myPool[order[i]];
            InitField();
            StartCoroutine(EnterField());
        }

        void InitField()
        {
            _myField[0]  = _myTeam[0];  _myField[1]  = _myTeam[1];
            _botField[0] = _botTeam[0]; _botField[1] = _botTeam[1];
        }

        IEnumerator EnterField()
        {
            RefreshAllHUDs();
            BattleEvents.OnFieldConditionUpdated?.Invoke(new FieldConditionData { TurnNumber=1, Weather="Normal", Terrain="Normal" });
            yield return new WaitForSeconds(0.4f);
            Say("Champion Bot muốn thi đấu!", true);
            yield return new WaitForSeconds(1.5f);
            NextTurn();
        }

        static PreviewPokemon[] ToPreviewArr(BotPokemon[] arr)
        {
            var r = new PreviewPokemon[arr.Length];
            for (int i = 0; i < arr.Length; i++)
                r[i] = new PreviewPokemon { Name=arr[i].Name, Type1=arr[i].Type1, Type2=arr[i].Type2, Level=50, MaxHp=arr[i].MaxHp };
            return r;
        }

        // ── HUD helpers ───────────────────────────────────────────────────────

        void RefreshAllHUDs()
        {
            ApplyHUD(_pHUD1, "Player_Lead_Slot", _myField[0],  isBack:true);
            ApplyHUD(_pHUD2, "Player_Sub2_Slot", _myField[1],  isBack:true);
            ApplyHUD(_eHUD1, "Enemy_Lead_Slot",  _botField[0], isBack:false);
            ApplyHUD(_eHUD2, "Enemy_Sub2_Slot",  _botField[1], isBack:false);
        }

        // Ghi thẳng lên HUD — hoạt động kể cả khi HUD đang inactive (bị UIManager ẩn khi Dialog mở).
        // Đây là lý do KHÔNG dùng BattleEvents.OnHealthChanged:
        //   UIManager.SwitchPanel(Dialog) → globalHudRoot.SetActive(false)
        //   → EntityHUD.OnDisable → StopAllCoroutines + unsubscribe OnHealthChanged
        //   → event bị drop, HP bar không đổi.
        void ApplyHUD(EntityHUD hud, string slotId, BotPokemon p, bool isBack)
        {
            if (hud == null) return;
            if (p == null || p.Fainted) { hud.gameObject.SetActive(false); return; }
            hud.gameObject.SetActive(true);
            hud.SetupEntity(slotId, p.Name, p.Hp, p.MaxHp);
            hud.SetLevel(p.Level);
            hud.SetTypes(p.Type1, p.Type2);
            hud.SetStatus(p.Status);
            FindObjectOfType<BattleSpriteLoader>()
                ?.LoadSpriteForSlot(slotId, slotId, p.Name.ToLower(), isBack);
        }

        // Chỉ cập nhật HP (và status) — gọi sau mỗi lần nhận damage, không cần sprite lại
        void UpdateHUD(BotPokemon p)
        {
            if (_myField[0] == p) { _pHUD1?.SetupEntity("Player_Lead_Slot", p.Name, p.Hp, p.MaxHp); _pHUD1?.SetStatus(p.Status); return; }
            if (_myField[1] == p) { _pHUD2?.SetupEntity("Player_Sub2_Slot", p.Name, p.Hp, p.MaxHp); _pHUD2?.SetStatus(p.Status); return; }
            if (_botField[0]== p) { _eHUD1?.SetupEntity("Enemy_Lead_Slot",  p.Name, p.Hp, p.MaxHp); _eHUD1?.SetStatus(p.Status); return; }
            if (_botField[1]== p) { _eHUD2?.SetupEntity("Enemy_Sub2_Slot",  p.Name, p.Hp, p.MaxHp); _eHUD2?.SetStatus(p.Status); }
        }

        EntityHUD HUDOf(BotPokemon p)
        {
            if (_myField[0]  == p) return _pHUD1;
            if (_myField[1]  == p) return _pHUD2;
            if (_botField[0] == p) return _eHUD1;
            if (_botField[1] == p) return _eHUD2;
            return null;
        }

        // ── Turn ─────────────────────────────────────────────────────────────

        void NextTurn()
        {
            _actA = null; _actB = null;
            _srcSlot = 0;
            PromptSlot();
        }

        void PromptSlot()
        {
            var pkmn = _myField[_srcSlot];
            if (pkmn == null || pkmn.Fainted)
            {
                if (_srcSlot == 0) { _srcSlot = 1; PromptSlot(); return; }
                StartCoroutine(ResolveTurn());
                return;
            }

            _selMove = -1; _pickTarget = false;
            if (_cmd != null) _cmd.CurrentSrcSlot = _srcSlot;

            if (_skill != null)
                for (int i = 0; i < 4; i++)
                    if (i < pkmn.Moves.Length) {
                        var bm = pkmn.Moves[i];
                        _skill.SetMove(i, new MoveSlot {
                            Name = bm.Name,
                            Type = bm.Type,
                            Category = bm.Category,
                            CurrentPp = bm.Pp,
                            MaxPp = bm.MaxPp,
                            Effect = bm.StatusEffect
                        });
                    }
                    else _skill.SetMove(i, null);

            BattleEvents.OnTeraAvailabilityChanged?.Invoke(!_teraUsed);
            BattleEvents.OnPlayerTurnStart?.Invoke(); // → UIManager: SwitchPanel(Command)
        }

        // ── Input ─────────────────────────────────────────────────────────────

        void OnSkillOrTarget(int idx)
        {
            var pkmn = _myField[_srcSlot];
            if (pkmn == null || pkmn.Fainted) return;

            if (!_pickTarget)
            {
                // Lần 1: chọn chiêu
                _selMove = idx; _pickTarget = true;

                _skill?.SetTargetLabel(0, Alive(_botField[0]) ? _botField[0].Name          : "---");
                _skill?.SetTargetLabel(1, Alive(_botField[1]) ? _botField[1].Name          : "---");
                _skill?.SetTargetLabel(2, Alive(_myField[0])  ? $"Ally {_myField[0].Name}" : "---");
                _skill?.SetTargetLabel(3, Alive(_myField[1])  ? $"Ally {_myField[1].Name}" : "---");

                _ui?.SwitchPanel(BattlePanelType.Skill);
            }
            else
            {
                // Lần 2: chọn mục tiêu
                _pickTarget = false;
                bool tera = _skill != null && _skill.IsTeraActive;
                if (tera) { _teraUsed = true; _skill.ResetTeraToggle(); BattleEvents.OnTeraAvailabilityChanged?.Invoke(false); }

                var target = GetTarget(true, idx);
                var act    = new BotAction { Attacker=pkmn, Target=target, Move=pkmn.Moves[_selMove], UseTera=tera };

                if (_srcSlot == 0) { _actA = act; _srcSlot = 1; PromptSlot(); }
                else               { _actB = act; StartCoroutine(ResolveTurn()); }
            }
        }

        // ── Resolution ────────────────────────────────────────────────────────

        IEnumerator ResolveTurn()
        {
            _ui?.SwitchPanel(BattlePanelType.Dialog);

            var botA = BotAct(0);
            var botB = BotAct(1);

            // Player → Bot (bỏ qua speed trong demo)
            if (_actA.HasValue && !_actA.Value.IsSwitch) { yield return StartCoroutine(Exec(_actA.Value)); if (Over()) yield break; }
            if (_actB.HasValue && !_actB.Value.IsSwitch) { yield return StartCoroutine(Exec(_actB.Value)); if (Over()) yield break; }
            if (botA.HasValue) { yield return StartCoroutine(Exec(botA.Value)); if (Over()) yield break; }
            if (botB.HasValue) { yield return StartCoroutine(Exec(botB.Value)); if (Over()) yield break; }

            yield return StartCoroutine(EndOfTurnStatus());
            if (Over()) yield break;

            yield return StartCoroutine(ForcedSwitches());
            if (Over()) yield break;

            _turn++;
            BattleEvents.OnFieldConditionUpdated?.Invoke(new FieldConditionData { TurnNumber=_turn });
            yield return new WaitForSeconds(0.3f);
            NextTurn();
        }

        BotAction? BotAct(int slot)
        {
            var p = _botField[slot];
            if (!Alive(p)) return null;

            var atk = new List<int>();
            var any = new List<int>();
            for (int i = 0; i < p.Moves.Length; i++)
            {
                if (p.Moves[i].Pp <= 0) continue;
                any.Add(i);
                if (p.Moves[i].Power > 0) atk.Add(i);
            }
            var pool = atk.Count > 0 ? atk : any;
            if (pool.Count == 0) return null;

            int mi = pool[Random.Range(0, pool.Count)];

            var tgts = new List<BotPokemon>();
            if (Alive(_myField[0])) tgts.Add(_myField[0]);
            if (Alive(_myField[1])) tgts.Add(_myField[1]);
            if (tgts.Count == 0) return null;

            return new BotAction { Attacker=p, Target=tgts[Random.Range(0, tgts.Count)], Move=p.Moves[mi] };
        }

        IEnumerator Exec(BotAction act)
        {
            var a = act.Attacker;
            if (!Alive(a)) yield break;

            // SLP
            if (a.Status == "SLP")
            {
                if (Random.value < 0.5f) { Say($"{a.Name} đang ngủ! Bỏ lượt.", true); yield return new WaitForSeconds(1f); yield break; }
                a.Status = null; UpdateHUD(a);
                Say($"{a.Name} thức dậy!", true);
                yield return new WaitForSeconds(0.8f);
            }

            var mv = act.Move;
            if (mv.Pp <= 0) { Say($"{a.Name}: hết PP!", true); yield return new WaitForSeconds(0.8f); yield break; }
            mv.Pp--;

            var t = act.Target;
            if (!Alive(t)) { Say($"{a.Name} dùng {mv.Name}! Không trúng ai.", true); yield return new WaitForSeconds(0.8f); yield break; }

            Say($"{a.Name} dùng {mv.Name}!", true);
            yield return new WaitForSeconds(0.9f);

            // Status move
            if (mv.Category == "Status")
            {
                if (!string.IsNullOrEmpty(mv.StatusEffect) && string.IsNullOrEmpty(t.Status))
                {
                    t.Status = mv.StatusEffect;
                    UpdateHUD(t);
                    Say($"{t.Name} bị {mv.StatusEffect}!", true);
                }
                else Say("Không có hiệu quả thêm!", true);
                yield return new WaitForSeconds(0.8f);
                yield break;
            }

            // Damage
            float pwr = mv.Power;
            if (act.UseTera) pwr *= 1.3f;
            if (mv.Category == "Physical" && a.Status == "BRN") pwr *= 0.5f;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(pwr * Random.Range(0.25f, 0.45f)));

            t.Hp = Mathf.Max(0, t.Hp - dmg);
            UpdateHUD(t);                          // ← ghi thẳng vào HUD (không qua event)
            Say($"{t.Name} mất {dmg} HP!", true);
            yield return new WaitForSeconds(0.9f);

            if (t.Hp <= 0)
            {
                t.Fainted = true;
                UpdateHUD(t);
                Say($"{t.Name} bị hạ gục!", true);
                yield return new WaitForSeconds(0.9f);
                HUDOf(t)?.gameObject.SetActive(false);
            }
        }

        IEnumerator EndOfTurnStatus()
        {
            BotPokemon[] all = { _myField[0], _myField[1], _botField[0], _botField[1] };
            foreach (var p in all)
            {
                if (!Alive(p)) continue;
                int dot = p.Status == "BRN" || p.Status == "PSN" ? Mathf.Max(1, p.MaxHp / 16)
                        : p.Status == "TOX"                       ? Mathf.Max(1, p.MaxHp / 8) : 0;
                if (dot == 0) continue;

                p.Hp = Mathf.Max(0, p.Hp - dot);
                UpdateHUD(p);
                Say($"{p.Name} đau vì {p.Status}! -{dot} HP", true);
                yield return new WaitForSeconds(0.8f);

                if (p.Hp <= 0)
                {
                    p.Fainted = true;
                    UpdateHUD(p);
                    Say($"{p.Name} bị hạ gục!", true);
                    yield return new WaitForSeconds(0.9f);
                    HUDOf(p)?.gameObject.SetActive(false);
                }
            }
        }

        // ── Switch ────────────────────────────────────────────────────────────

        IEnumerator ForcedSwitches()
        {
            for (int s = 0; s < 2; s++)
            {
                if (Alive(_myField[s])) continue;
                var avail = AvailMyBench();
                if (avail.Count == 0) continue;

                _forcedSlot = s; _waitSwitch = true;
                BattleEvents.OnPartyPanelOpen?.Invoke(BuildPartyData(avail.ToArray(), true, s));
                while (_waitSwitch) yield return null;
            }

            for (int s = 0; s < 2; s++)
            {
                if (Alive(_botField[s])) continue;
                for (int i = 0; i < _botTeam.Length; i++)
                {
                    if (_botTeam[i].Fainted || _botTeam[i] == _botField[0] || _botTeam[i] == _botField[1]) continue;
                    _botField[s] = _botTeam[i];
                    string sid = s == 0 ? "Enemy_Lead_Slot" : "Enemy_Sub2_Slot";
                    Say($"Đối thủ gửi {_botTeam[i].Name} vào sân!", true);
                    ApplyHUD(HUDForSlotId(sid), sid, _botTeam[i], false);
                    yield return new WaitForSeconds(1f);
                    break;
                }
            }
        }

        void OnPartySlotChosen(int idx)
        {
            if (_waitSwitch)
            {
                _myField[_forcedSlot] = _myTeam[idx];
                string sid = _forcedSlot == 0 ? "Player_Lead_Slot" : "Player_Sub2_Slot";
                Say($"Tung {_myTeam[idx].Name} ra sân!", true);
                ApplyHUD(HUDForSlotId(sid), sid, _myTeam[idx], true);
                _waitSwitch = false;
                return;
            }
            if (_volSwitch)
            {
                _myField[_volSlot] = _myTeam[idx];
                string sid = _volSlot == 0 ? "Player_Lead_Slot" : "Player_Sub2_Slot";
                Say($"Tung {_myTeam[idx].Name} ra sân!", true);
                ApplyHUD(HUDForSlotId(sid), sid, _myTeam[idx], true);
                _volSwitch = false;

                if (_volSlot == 0) { _actA = new BotAction { IsSwitch=true }; _srcSlot=1; PromptSlot(); }
                else               { _actB = new BotAction { IsSwitch=true }; StartCoroutine(ResolveTurn()); }
            }
        }

        void OnPartyPanelCancelled() { _volSwitch=false; BattleEvents.OnPlayerTurnStart?.Invoke(); }

        void OnVoluntarySwitchRequested(int slot)
        {
            var avail = AvailMyBench();
            if (avail.Count == 0) { Say("Không còn Pokemon để đổi!", true); return; }
            _volSwitch=true; _volSlot=slot;
            BattleEvents.OnPartyPanelOpen?.Invoke(BuildPartyData(avail.ToArray(), false, -1));
        }

        List<int> AvailMyBench()
        {
            var list = new List<int>();
            for (int i = 0; i < _myTeam.Length; i++)
                if (!_myTeam[i].Fainted && _myTeam[i] != _myField[0] && _myTeam[i] != _myField[1])
                    list.Add(i);
            return list;
        }

        PartyPanelData BuildPartyData(int[] avail, bool forced, int fSlot)
        {
            var pkmns = new PartyPokemon[_myTeam.Length];
            for (int i = 0; i < _myTeam.Length; i++)
                pkmns[i] = new PartyPokemon
                {
                    PartyIndex=i, Name=_myTeam[i].Name, Type1=_myTeam[i].Type1, Type2=_myTeam[i].Type2,
                    CurrentHp=_myTeam[i].Hp, MaxHp=_myTeam[i].MaxHp,
                    IsFainted=_myTeam[i].Fainted, Status=_myTeam[i].Status,
                    IsActive=(_myTeam[i]==_myField[0] || _myTeam[i]==_myField[1]),
                };
            return new PartyPanelData { Pokemon=pkmns, AvailableIdxs=avail, IsForcedSwitch=forced, ForcedSlot=fSlot };
        }

        // ── Win/Loss ──────────────────────────────────────────────────────────

        bool Over()
        {
            bool myDead=true,  botDead=true;
            foreach (var p in _myTeam)  if (!p.Fainted) { myDead=false;  break; }
            foreach (var p in _botTeam) if (!p.Fainted) { botDead=false; break; }
            if (!myDead && !botDead) return false;

            bool won = botDead && !myDead;
            Say(won ? "🏆 BẠN THẮNG!" : "💀 BẠN THUA...", false);
            BattleEvents.OnBattleResult?.Invoke(won, "Bot");
            return true;
        }

        // ── Util ──────────────────────────────────────────────────────────────

        void Say(string msg, bool autoClose) => BattleEvents.OnPrintDialog?.Invoke(msg, autoClose);

        static bool Alive(BotPokemon p) => p != null && !p.Fainted;

        BotPokemon GetTarget(bool mine, int idx) => mine
            ? idx switch { 0=>_botField[0], 1=>_botField[1], 2=>_myField[0], 3=>_myField[1], _=>null }
            : idx switch { 0=>_myField[0],  1=>_myField[1],  _=>null };

        EntityHUD HUDForSlotId(string id) => id switch
        {
            "Player_Lead_Slot"=>_pHUD1, "Player_Sub2_Slot"=>_pHUD2,
            "Enemy_Lead_Slot" =>_eHUD1, "Enemy_Sub2_Slot" =>_eHUD2,
            _=>null
        };
    }
}
