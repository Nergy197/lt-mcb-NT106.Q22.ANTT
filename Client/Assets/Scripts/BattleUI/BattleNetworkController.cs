using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using Game.Battle.UI;
using Game.Network;

namespace Game.Battle.Logic
{
    /// <summary>
    /// Cầu nối SignalR ↔ toàn bộ hệ thống UI Battle VGC.
    ///
    /// State machine:
    ///   TeamPreview → Running (chọn chiêu / switch từng slot) → ForcedSwitch → Running → ... → Ended
    ///
    /// Events gửi ra:
    ///   OnTeamPreviewStart    → BattleTeamPreviewPanel
    ///   OnPartyPanelOpen      → BattlePartyPanel
    ///   OnFieldConditionUpdated → BattleFieldPanel
    ///   OnTeraAvailabilityChanged → BattleSkillPanel
    ///   OnBattleResult        → Result panel
    /// </summary>
    public class BattleNetworkController : MonoBehaviour
    {
        [Header("Demo")]
        [Tooltip("Bật để tự động tạo bot battle khi mở scene trực tiếp (không qua matchmaking)")]
        public bool demoAutoMatch = false;

        [Header("HUD Phe Ta")]
        public EntityHUD playerHUD1;
        public EntityHUD playerHUD2;

        [Header("HUD Phe Địch")]
        public EntityHUD enemyHUD1;
        public EntityHUD enemyHUD2;

        [Header("Panels")]
        public BattleSkillPanel  skillPanel;
        public BattleCommandPanel commandPanel;
        public BattleDialogPanel  dialogPanel;
        public BattleTeamPreviewPanel teamPreviewPanel;
        public BattlePartyPanel   partyPanel;
        public BattleFieldPanel   fieldPanel;

        // ── Runtime identifiers ─────────────────────────────────────────────
        private string _battleId;
        private string _myPlayerId;

        // ── Field snapshot (cập nhật mỗi lượt) ──────────────────────────────
        private FieldSnapshot _field;

        // ── Trạng thái lượt ─────────────────────────────────────────────────
        private int  _srcSlot         = 0;   // 0=Slot A, 1=Slot B đang chọn
        private int  _selectedMove    = -1;
        private bool _pickingTarget   = false;
        private bool _waitingOpponent = false;

        // ── Party / team data ────────────────────────────────────────────────
        // 4 thành viên battle team theo thứ tự (sau khi SubmitTeamOrder)
        private readonly string[] _teamName   = new string[4];
        private readonly string[] _teamType1  = new string[4];
        private readonly string[] _teamType2  = new string[4];
        private readonly int[]    _teamMaxHp  = new int[4];
        private readonly int[]    _teamCurHp  = new int[4];
        private readonly string[] _teamStatus = new string[4];
        private readonly bool[]   _teamFainted= new bool[4];
        private int _activeIdxA = 0, _activeIdxB = 1; // battle team index hiện trên sân

        // Team Preview data
        private PreviewTeamData _previewData;

        // ── Forced / Voluntary switch ────────────────────────────────────────
        private bool _inForcedSwitch    = false;
        private int  _forcedSwitchSlot  = -1;

        private bool _inVoluntarySwitch = false;
        private int  _volSwitchSrcSlot  = -1;

        // ── Tera (Gen 9) ─────────────────────────────────────────────────────
        private bool _teraUsedThisBattle = false;

        // ── Field conditions ─────────────────────────────────────────────────
        private FieldConditionData _fieldCond = new();

        // ── Main-thread queue (SignalR callbacks chạy background thread) ─────
        private readonly Queue<Action> _mq   = new();
        private readonly object        _lock = new();

        // ── Unity Lifecycle ─────────────────────────────────────────────────

        private void OnEnable()
        {
            BattleEvents.OnPlayerUseSkill          += OnSkillOrTarget;
            BattleEvents.OnTeamOrderConfirmed      += DoSubmitTeamOrder;
            BattleEvents.OnPartySlotChosen         += OnPartySlotChosen;
            BattleEvents.OnPartyPanelCancelled     += OnPartyPanelCancelled;
            BattleEvents.OnVoluntarySwitchRequested += OnVoluntarySwitchRequested;
        }

        private void OnDisable()
        {
            BattleEvents.OnPlayerUseSkill           -= OnSkillOrTarget;
            BattleEvents.OnTeamOrderConfirmed       -= DoSubmitTeamOrder;
            BattleEvents.OnPartySlotChosen          -= OnPartySlotChosen;
            BattleEvents.OnPartyPanelCancelled      -= OnPartyPanelCancelled;
            BattleEvents.OnVoluntarySwitchRequested -= OnVoluntarySwitchRequested;
        }

        private void Update()
        {
            lock (_lock)
                while (_mq.Count > 0) _mq.Dequeue()?.Invoke();
        }

        private void Enqueue(Action a) { lock (_lock) _mq.Enqueue(a); }

        private async void Start()
        {
            _myPlayerId = PlayerPrefs.GetString("player_id", "");

            if (demoAutoMatch)
            {
                // Demo mode: luôn tạo bot battle mới, bỏ qua CurrentBattleId cũ
                MatchmakingManager.ResetBattleId();
                await StartDemoAutoMatch();
                if (string.IsNullOrEmpty(_battleId)) return;
            }
            else
            {
                _battleId = MatchmakingManager.CurrentBattleId;
                if (string.IsNullOrEmpty(_battleId)) return;
            }

            while (SignalRClient.Instance?.Battle?.State != HubConnectionState.Connected)
                await System.Threading.Tasks.Task.Yield();

            var hub = SignalRClient.Instance.Battle;

            // Xoá handler cũ tránh duplicate
            foreach (var ev in new[] { "TeamPreviewReady", "TeamOrderAccepted", "BattleRunning",
                                       "TurnReady", "TurnResolved", "ActionAccepted",
                                       "ForcedSwitchRequired", "ForcedSwitchAccepted",
                                       "BattleEnded", "Error" })
                hub.Remove(ev);

            hub.On<object>("TeamPreviewReady",     raw => Enqueue(() => OnTeamPreviewReady(raw)));
            hub.On<object>("TeamOrderAccepted",    _   => { /* chờ BattleRunning */ });
            hub.On<string>("Debug",                msg => Enqueue(() => Debug.Log(msg)));
            hub.On<string>("Error",                msg => Enqueue(() => Debug.LogError(msg)));
            hub.On<object>("BattleRunning",        raw => Enqueue(() => OnBattleRunning(raw)));
            hub.On<object>("TurnReady",            raw => Enqueue(() => OnBattleRunning(raw)));
            hub.On<object>("TurnResolved",         raw => Enqueue(() => OnTurnResolved(raw)));
            hub.On<object>("ActionAccepted",       raw => Enqueue(() => OnActionAccepted(raw)));
            hub.On<object>("ForcedSwitchRequired", raw => Enqueue(() => OnForcedSwitchRequired(raw)));
            hub.On<object>("ForcedSwitchAccepted", _   => { /* state xử lý qua TurnReady */ });
            hub.On<object>("BattleEnded",          raw => Enqueue(() => OnBattleEnded(raw)));
            hub.On<string>("Error",                msg => Enqueue(() => Debug.LogError("[Battle] " + msg)));

            try   { await hub.InvokeAsync("JoinBattle", _battleId); }
            catch (Exception ex) { Debug.LogError("[Battle] JoinBattle: " + ex.Message); }
        }

        // ════════════════════════════════════════════════════════════════════
        // DEMO AUTO-MATCH
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Kết nối MatchmakingHub và gọi FightBot để tạo bot battle ngay lập tức.
        /// Dùng khi mở Battle scene trực tiếp không qua matchmaking (chế độ demo).
        /// </summary>
        private async System.Threading.Tasks.Task StartDemoAutoMatch()
        {
            // Chờ tối đa 3s để SignalRClient.Awake() khởi tạo singleton
            float waitSrc = 3f;
            while (SignalRClient.Instance == null && waitSrc > 0)
            {
                await System.Threading.Tasks.Task.Delay(100);
                waitSrc -= 0.1f;
            }
            if (SignalRClient.Instance == null)
            {
                Debug.LogError("[Battle] Demo: Không tìm thấy SignalRClient trong scene. " +
                               "Hãy thêm GameObject có SignalRClient vào Battle scene hoặc khởi động từ Menu scene.");
                return;
            }

            await SignalRClient.Instance.ConnectAsync();

            var matchHub = SignalRClient.Instance.Matchmaking;
            float timeout = 5f;
            while (matchHub.State != HubConnectionState.Connected && timeout > 0)
            {
                await System.Threading.Tasks.Task.Delay(100);
                timeout -= 0.1f;
            }
            if (matchHub.State != HubConnectionState.Connected)
            {
                Debug.LogError("[Battle] Demo: Không kết nối được MatchmakingHub.");
                return;
            }

            // Dùng TaskCompletionSource để chờ MatchFound an toàn từ background thread
            var tcs = new System.Threading.Tasks.TaskCompletionSource<BattleStartedDto>();

            matchHub.Remove("MatchFound");
            matchHub.On<BattleStartedDto>("MatchFound", dto => tcs.TrySetResult(dto));

            try { await matchHub.InvokeAsync("FightBot"); }
            catch (Exception ex) { Debug.LogError("[Battle] Demo FightBot: " + ex.Message); return; }

            var winner = await System.Threading.Tasks.Task.WhenAny(tcs.Task, System.Threading.Tasks.Task.Delay(5000));
            if (winner != tcs.Task) { Debug.LogError("[Battle] Demo: Timeout chờ MatchFound."); return; }

            var found = tcs.Task.Result;
            _battleId = found.BattleId;
            if (string.IsNullOrEmpty(_myPlayerId)) _myPlayerId = found.Player1Id;
        }

        // ════════════════════════════════════════════════════════════════════
        // TEAM PREVIEW
        // ════════════════════════════════════════════════════════════════════

        private void OnTeamPreviewReady(object raw)
        {
            var dto = J<TeamPreviewDto>(raw);
            if (dto == null) return;

            _previewData = new PreviewTeamData
            {
                MyTeam  = BuildPreviewArr(dto.YourTeam),
                OppTeam = BuildPreviewArr(dto.OpponentTeam),
            };

            BattleEvents.OnTeamPreviewStart?.Invoke(_previewData);
        }

        private static PreviewPokemon[] BuildPreviewArr(List<TeamPreviewPokemonDto> list)
        {
            if (list == null) return Array.Empty<PreviewPokemon>();
            var arr = new PreviewPokemon[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                arr[i] = new PreviewPokemon
                {
                    SpeciesId = list[i].SpeciesId,
                    Name      = list[i].SpeciesName ?? list[i].Nickname ?? "???",
                    Type1     = list[i].Type1 ?? "normal",
                    Type2     = list[i].Type2,
                    Level     = list[i].Level,
                    MaxHp     = list[i].MaxHp,
                };
            }
            return arr;
        }

        // Gọi khi player xác nhận thứ tự team trong TeamPreviewPanel
        private async void DoSubmitTeamOrder(int[] orderedIndices)
        {
            // Lưu tên/type từ preview data
            if (_previewData?.MyTeam != null)
            {
                for (int slot = 0; slot < 4 && slot < orderedIndices.Length; slot++)
                {
                    int orig = orderedIndices[slot];
                    if (orig < _previewData.MyTeam.Length)
                    {
                        var p = _previewData.MyTeam[orig];
                        _teamName[slot]  = p.Name;
                        _teamType1[slot] = p.Type1;
                        _teamType2[slot] = p.Type2;
                        _teamMaxHp[slot] = p.MaxHp;
                        _teamCurHp[slot] = p.MaxHp;
                    }
                }
            }

            try
            {
                await SignalRClient.Instance.Battle.InvokeAsync(
                    "SubmitTeamOrder", _battleId, new List<int>(orderedIndices));
            }
            catch (Exception ex) { Debug.LogError("[Battle] SubmitTeamOrder: " + ex.Message); }
        }

        // Auto-submit khi vs Bot (không cần player chọn)
        private async void AutoTeamOrder()
        {
            // Điền team data từ BattleRunning nếu chưa có preview
            try
            {
                await SignalRClient.Instance.Battle.InvokeAsync(
                    "SubmitTeamOrder", _battleId, new List<int> { 0, 1, 2, 3 });
            }
            catch (Exception ex) { Debug.LogError("[Battle] AutoTeamOrder: " + ex.Message); }
        }

        // ════════════════════════════════════════════════════════════════════
        // BATTLE RUNNING (cập nhật sân mỗi lượt)
        // ════════════════════════════════════════════════════════════════════

        private void OnBattleRunning(object raw)
        {
            var dto = J<FieldDto>(raw);
            if (dto == null) return;

            _field = FieldSnapshot.From(dto);

            // Cập nhật HP bench từ server
            if (dto.YourTeamHp != null)
                for (int i = 0; i < dto.YourTeamHp.Count && i < 4; i++)
                    _teamCurHp[i] = dto.YourTeamHp[i];
            if (dto.YourTeamMaxHp != null)
                for (int i = 0; i < dto.YourTeamMaxHp.Count && i < 4; i++)
                    _teamMaxHp[i] = dto.YourTeamMaxHp[i];

            // Sync team names nếu chưa có (bot game bỏ qua TeamPreview)
            SyncTeamNamesFromField(dto);

            // Reset turn state
            _waitingOpponent = false;
            _inForcedSwitch  = false;
            _inVoluntarySwitch = false;
            _srcSlot = 0; _selectedMove = -1; _pickingTarget = false;

            // Cập nhật HUD
            RefreshHUD(playerHUD1, "Player_Lead_Slot", "Player1_HUD", _field.YourA, isBack: true);
            RefreshHUD(playerHUD2, "Player_Sub2_Slot", "Player2_HUD", _field.YourB, isBack: true);
            RefreshHUD(enemyHUD1,  "Enemy_Lead_Slot",  "Enemy1_HUD",  _field.OppA, isBack: false);
            RefreshHUD(enemyHUD2,  "Enemy_Sub2_Slot",  "Enemy2_HUD",  _field.OppB, isBack: false);

            // Field conditions
            UpdateFieldConditions(dto.Weather, dto.WeatherTurnsLeft,
                                  dto.Terrain, dto.TerrainTurnsLeft,
                                  dto.TurnNumber);

            StartCoroutine(Intro());
        }

        private void SyncTeamNamesFromField(FieldDto dto)
        {
            // Chỉ sync khi chưa có tên (bot bỏ qua TeamPreview)
            TrySyncSlot(0, dto.YourSlotA);
            TrySyncSlot(1, dto.YourSlotB);
        }

        private void TrySyncSlot(int battleIdx, PokemonDto dto)
        {
            if (dto == null || !string.IsNullOrEmpty(_teamName[battleIdx])) return;
            _teamName[battleIdx]  = dto.SpeciesName ?? "";
            _teamType1[battleIdx] = dto.Type1 ?? "normal";
            _teamType2[battleIdx] = dto.Type2;
            _teamMaxHp[battleIdx] = dto.MaxHp;
            _teamCurHp[battleIdx] = dto.CurrentHp;
        }

        private void RefreshHUD(EntityHUD hud, string slotName, string hudName,
                                 PokemonSlot pkmn, bool isBack)
        {
            if (pkmn == null || pkmn.IsFainted) { hud?.gameObject.SetActive(false); return; }
            hud?.gameObject.SetActive(true);
            hud?.SetupEntity(slotName, pkmn.SpeciesName, pkmn.CurrentHp, pkmn.MaxHp);
            hud?.SetLevel(pkmn.Level);
            hud?.SetTypes(pkmn.Type1, pkmn.Type2);
            hud?.SetStatus(pkmn.Status);
            hud?.SetTeraType(pkmn.TerType, pkmn.IsTerastallized);

            var loader = GetComponent<BattleSpriteLoader>() ?? gameObject.AddComponent<BattleSpriteLoader>();
            // Dùng .ToLower() để khớp tên file sprite; fallback sang SpeciesId nếu tên rỗng
            string spriteName = !string.IsNullOrEmpty(pkmn.SpeciesName)
                ? pkmn.SpeciesName.ToLower()
                : pkmn.SpeciesId.ToString();
            loader.LoadSpriteForSlot(slotName, hudName, spriteName, isBack);
        }

        // ── Field conditions ────────────────────────────────────────────────

        private void UpdateFieldConditions(string weather, int weatherTurns,
                                           string terrain, int terrainTurns,
                                           int turnNum)
        {
            _fieldCond = new FieldConditionData
            {
                Weather      = weather,
                WeatherTurns = weatherTurns,
                Terrain      = terrain,
                TerrainTurns = terrainTurns,
                TurnNumber   = turnNum,
            };
            BattleEvents.OnFieldConditionUpdated?.Invoke(_fieldCond);
        }

        // ════════════════════════════════════════════════════════════════════
        // TURN FLOW
        // ════════════════════════════════════════════════════════════════════

        private IEnumerator Intro()
        {
            yield return new WaitForSeconds(0.4f);
            bool vsBot = string.IsNullOrEmpty(_field?.Player2Id) || _field.Player2Id == "BOT_PLAYER";
            string opp = vsBot ? "Champion Bot" : "Đối thủ";
            BattleEvents.OnPrintDialog?.Invoke($"{opp} muốn thi đấu!", true);
            yield return new WaitForSeconds(1.2f);
            StartTurn();
        }

        private void StartTurn()
        {
            _srcSlot = 0;
            PromptSlot();
        }

        private void PromptSlot()
        {
            if (_field == null) return;

            var pkmn = _srcSlot == 0 ? _field.YourA : _field.YourB;
            if (pkmn == null || pkmn.IsFainted)
            {
                if (_srcSlot == 0) { _srcSlot = 1; PromptSlot(); }
                else               WaitForOpponent();
                return;
            }

            _selectedMove = -1; _pickingTarget = false;

            // Thông báo slot
            string slotLabel = _srcSlot == 0 ? "SLOT A" : "SLOT B";
            BattleEvents.OnPrintDialog?.Invoke($"[{slotLabel}] {pkmn.SpeciesName.ToUpper()} – Chọn hành động!", true);

            // Cập nhật slot hiện tại cho CommandPanel (để Pokemon button biết dùng slot nào)
            if (commandPanel != null) commandPanel.CurrentSrcSlot = _srcSlot;

            // Load moves cho SkillPanel
            LoadMoves(pkmn);

            // Tera button chỉ available nếu chưa dùng
            BattleEvents.OnTeraAvailabilityChanged?.Invoke(!_teraUsedThisBattle);

            BattleEvents.OnPlayerTurnStart?.Invoke();
        }

        private void LoadMoves(PokemonSlot pkmn)
        {
            if (skillPanel == null || pkmn.Moves == null) return;
            for (int i = 0; i < 4; i++)
            {
                if (i < pkmn.Moves.Count)
                {
                    var m = pkmn.Moves[i];
                    skillPanel.SetMove(i, m.Name, m.Type, m.Category, m.CurrentPp, m.MaxPp);
                }
                else
                {
                    skillPanel.SetMove(i, "---");
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Chọn chiêu / chọn mục tiêu
        // ────────────────────────────────────────────────────────────────────

        private void OnSkillOrTarget(int idx)
        {
            if (_waitingOpponent || _field == null) return;

            if (!_pickingTarget)
            {
                // Lần 1: chọn chiêu → hiển thị mục tiêu
                _selectedMove  = idx;
                _pickingTarget = true;

                // Điền nhãn 4 nút thành tên Pokemon mục tiêu
                var oA = _field.OppA; var oB = _field.OppB;
                var yA = _field.YourA; var yB = _field.YourB;
                skillPanel?.SetTargetLabel(0, oA != null && !oA.IsFainted ? oA.SpeciesName : "---");
                skillPanel?.SetTargetLabel(1, oB != null && !oB.IsFainted ? oB.SpeciesName : "---");
                skillPanel?.SetTargetLabel(2, yA != null && !yA.IsFainted ? $"Ally {yA.SpeciesName}" : "---");
                skillPanel?.SetTargetLabel(3, yB != null && !yB.IsFainted ? $"Ally {yB.SpeciesName}" : "---");

                BattleEvents.OnPrintDialog?.Invoke("Đánh vào ai?", true);
                BattleEvents.OnPlayerTurnStart?.Invoke(); // giữ Skill panel mở
            }
            else
            {
                // Lần 2: chọn mục tiêu → gửi
                _pickingTarget = false;
                bool useTera = skillPanel != null && skillPanel.IsTeraActive;
                DoSendMove(_selectedMove, idx, useTera);

                if (useTera)
                {
                    _teraUsedThisBattle = true;
                    skillPanel?.ResetTeraToggle();
                    BattleEvents.OnTeraAvailabilityChanged?.Invoke(false);
                }
            }
        }

        private async void DoSendMove(int moveSlot, int targetSlot, bool useTera)
        {
            try
            {
                await SignalRClient.Instance.Battle.InvokeAsync(
                    "SubmitBattleAction",
                    _battleId, _srcSlot, "Move",
                    (int?)moveSlot, (int?)null,
                    targetSlot, useTera);
            }
            catch (Exception ex) { Debug.LogError("[Battle] SendMove: " + ex.Message); }
        }

        private void OnActionAccepted(object raw)
        {
            // Parse which slot was accepted so duplicate submissions don't advance _srcSlot twice
            var dto = J<ActionAcceptedDto>(raw);
            int acceptedSlot = dto?.Slot ?? _srcSlot;

            // Ignore if client already moved past this slot (e.g. duplicate ActionAccepted for slot 0)
            if (acceptedSlot != _srcSlot) return;

            if (_srcSlot == 0)
            {
                _srcSlot = 1;
                var b = _field?.YourB;
                if (b != null && !b.IsFainted) { PromptSlot(); return; }
            }
            WaitForOpponent();
        }

        private void WaitForOpponent()
        {
            _waitingOpponent = true;
            BattleEvents.OnPrintDialog?.Invoke("Đang chờ đối phương...", false);
        }

        // ════════════════════════════════════════════════════════════════════
        // VOLUNTARY SWITCH
        // ════════════════════════════════════════════════════════════════════

        private void OnVoluntarySwitchRequested(int srcSlot)
        {
            if (_waitingOpponent || _field == null) return;

            _inVoluntarySwitch = true;
            _volSwitchSrcSlot  = srcSlot;

            // Tính available: không fainted, không đang active
            var available = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                if (!_teamFainted[i] && i != _activeIdxA && i != _activeIdxB)
                    available.Add(i);
            }

            if (available.Count == 0)
            {
                BattleEvents.OnPrintDialog?.Invoke("Không còn Pokemon để đổi!", true);
                return;
            }

            BattleEvents.OnPartyPanelOpen?.Invoke(BuildPartyPanelData(available.ToArray(), false, -1));
        }

        private async void DoSendSwitch(int srcSlot, int partyIndex)
        {
            try
            {
                await SignalRClient.Instance.Battle.InvokeAsync(
                    "SubmitBattleAction",
                    _battleId, srcSlot, "Switch",
                    (int?)null, (int?)partyIndex,
                    0, false);
            }
            catch (Exception ex) { Debug.LogError("[Battle] SendSwitch: " + ex.Message); }
        }

        // ════════════════════════════════════════════════════════════════════
        // FORCED SWITCH
        // ════════════════════════════════════════════════════════════════════

        private void OnForcedSwitchRequired(object raw)
        {
            var dto = J<FsRequiredDto>(raw);
            if (dto == null) return;
            if (dto.PlayerId != null && dto.PlayerId != _myPlayerId) return; // không phải mình

            _inForcedSwitch   = true;
            _forcedSwitchSlot = dto.Slot;

            var available = dto.AvailableIndices ?? new List<int>();

            // Cập nhật team HP từ field trước khi hiển thị
            if (_field != null)
            {
                UpdateTeamHpFromField();
            }

            BattleEvents.OnPartyPanelOpen?.Invoke(
                BuildPartyPanelData(available.ToArray(), true, dto.Slot));
        }

        private async void DoSubmitForcedSwitch(int slot, int partyIndex)
        {
            try
            {
                await SignalRClient.Instance.Battle.InvokeAsync(
                    "SubmitForcedSwitch", _battleId, slot, partyIndex);
            }
            catch (Exception ex) { Debug.LogWarning("[Battle] ForcedSwitch: " + ex.Message); }
        }

        // ────────────────────────────────────────────────────────────────────
        // Party panel callback
        // ────────────────────────────────────────────────────────────────────

        private void OnPartySlotChosen(int partyIndex)
        {
            if (_inForcedSwitch)
            {
                _inForcedSwitch = false;
                // Cập nhật active index
                if (_forcedSwitchSlot == 0) _activeIdxA = partyIndex;
                else                        _activeIdxB = partyIndex;

                DoSubmitForcedSwitch(_forcedSwitchSlot, partyIndex);
                BattleEvents.OnPrintDialog?.Invoke($"Gửi {_teamName[partyIndex]} vào sân!", true);
            }
            else if (_inVoluntarySwitch)
            {
                _inVoluntarySwitch = false;
                // Cập nhật active index
                if (_volSwitchSrcSlot == 0) _activeIdxA = partyIndex;
                else                        _activeIdxB = partyIndex;

                DoSendSwitch(_volSwitchSrcSlot, partyIndex);
            }
        }

        private void OnPartyPanelCancelled()
        {
            _inVoluntarySwitch = false;
            // Quay lại Command panel
            BattleEvents.OnPlayerTurnStart?.Invoke();
        }

        // ════════════════════════════════════════════════════════════════════
        // TURN RESOLVED
        // ════════════════════════════════════════════════════════════════════

        private void OnTurnResolved(object raw)
        {
            var r = J<TurnDto>(raw);
            if (r == null) return;

            // Cập nhật HP qua event (EntityHUD tự nhận)
            if (_field != null)
            {
                FireHp("Player_Lead_Slot",  r.ActiveHp1,  _field.YourA?.MaxHp ?? 1);
                FireHp("Player_Sub2_Slot",  r.ActiveHp1b, _field.YourB?.MaxHp ?? 1);
                FireHp("Enemy_Lead_Slot",   r.ActiveHp2,  _field.OppA?.MaxHp  ?? 1);
                FireHp("Enemy_Sub2_Slot",   r.ActiveHp2b, _field.OppB?.MaxHp  ?? 1);
            }

            // Giảm field condition turns (ước chừng 1 lượt)
            if (_fieldCond != null)
            {
                if (_fieldCond.WeatherTurns > 0) _fieldCond.WeatherTurns--;
                if (_fieldCond.TerrainTurns > 0) _fieldCond.TerrainTurns--;
                _fieldCond.TurnNumber++;
                BattleEvents.OnFieldConditionUpdated?.Invoke(_fieldCond);
            }

            StartCoroutine(AnimEvents(r));
        }

        private void FireHp(string id, int hp, int max) =>
            BattleEvents.OnHealthChanged?.Invoke(id, hp, max);

        private IEnumerator AnimEvents(TurnDto r)
        {
            if (r.TypedEvents != null)
            {
                foreach (var ev in r.TypedEvents)
                {
                    // Cập nhật weather nếu thay đổi
                    if (ev.EventType == "WeatherChangedEvent" && _fieldCond != null)
                    {
                        _fieldCond.Weather      = ev.NewWeather ?? "";
                        _fieldCond.WeatherTurns = 5;
                        BattleEvents.OnFieldConditionUpdated?.Invoke(_fieldCond);
                    }

                    // Cập nhật status trên HUD
                    if (ev.EventType == "StatusInflictedEvent")
                        UpdateHudStatus(ev.PokemonName, ev.Status);

                    string msg = Fmt(ev);
                    if (string.IsNullOrEmpty(msg)) continue;
                    BattleEvents.OnPrintDialog?.Invoke(msg, true);
                    yield return new WaitForSeconds(1.0f);
                }
            }

            if (r.State == "Ended") { DoEndBattle(r.WinnerPlayerId); yield break; }

            // Forced switches sau khi resolve — ForcedSwitchRequired sẽ đến qua hub
            bool hasOwnForcedSwitch = false;
            if (r.ForcedSwitches != null)
            {
                foreach (var fs in r.ForcedSwitches)
                {
                    if (fs.PlayerId == _myPlayerId)
                    {
                        hasOwnForcedSwitch = true;
                        _teamFainted[fs.Slot == 0 ? _activeIdxA : _activeIdxB] = true;
                    }
                }
            }

            // Không có forced switch cho mình → yêu cầu server gửi lại field snapshot để bắt đầu lượt mới
            // Server gửi "BattleRunning" → OnBattleRunning → StartTurn
            if (!hasOwnForcedSwitch)
                DoRequestCurrentState();
        }

        private async void DoRequestCurrentState()
        {
            try
            {
                await SignalRClient.Instance.Battle.InvokeAsync("RequestCurrentState", _battleId);
            }
            catch (Exception ex) { Debug.LogWarning("[Battle] RequestCurrentState: " + ex.Message); }
        }

        private void UpdateHudStatus(string pokemonName, string status)
        {
            if (_field == null) return;
            if (_field.YourA?.SpeciesName == pokemonName) playerHUD1?.SetStatus(status);
            if (_field.YourB?.SpeciesName == pokemonName) playerHUD2?.SetStatus(status);
            if (_field.OppA?.SpeciesName  == pokemonName) enemyHUD1?.SetStatus(status);
            if (_field.OppB?.SpeciesName  == pokemonName) enemyHUD2?.SetStatus(status);
        }

        // ════════════════════════════════════════════════════════════════════
        // BATTLE ENDED
        // ════════════════════════════════════════════════════════════════════

        private void OnBattleEnded(object raw)
        {
            var d = J<EndDto>(raw);
            DoEndBattle(d?.WinnerPlayerId);
        }

        private void DoEndBattle(string winnerId)
        {
            bool won = winnerId == _myPlayerId;
            string msg = won ? "🏆 BẠN THẮNG!" : "💀 BẠN THUA...";
            BattleEvents.OnPrintDialog?.Invoke(msg, false);
            BattleEvents.OnBattleResult?.Invoke(won, "");
            StartCoroutine(GoMenu(5f));
        }

        private IEnumerator GoMenu(float delay)
        {
            yield return new WaitForSeconds(delay);
            SceneManager.LoadScene("Menu scene");
        }

        // ════════════════════════════════════════════════════════════════════
        // PARTY PANEL DATA BUILDER
        // ════════════════════════════════════════════════════════════════════

        private PartyPanelData BuildPartyPanelData(int[] availableIdxs, bool isForced, int forcedSlot)
        {
            var pokemons = new PartyPokemon[4];
            for (int i = 0; i < 4; i++)
            {
                pokemons[i] = new PartyPokemon
                {
                    PartyIndex = i,
                    Name       = !string.IsNullOrEmpty(_teamName[i]) ? _teamName[i] : $"Pokemon {i+1}",
                    Type1      = _teamType1[i] ?? "normal",
                    Type2      = _teamType2[i],
                    CurrentHp  = _teamCurHp[i],
                    MaxHp      = _teamMaxHp[i] > 0 ? _teamMaxHp[i] : 1,
                    IsFainted  = _teamFainted[i],
                    IsActive   = (i == _activeIdxA || i == _activeIdxB),
                    Status     = _teamStatus[i],
                };
            }

            return new PartyPanelData
            {
                Pokemon       = pokemons,
                AvailableIdxs = availableIdxs,
                IsForcedSwitch = isForced,
                ForcedSlot    = forcedSlot,
            };
        }

        private void UpdateTeamHpFromField()
        {
            if (_field.YourA != null && !_field.YourA.IsFainted)
                _teamCurHp[_activeIdxA] = _field.YourA.CurrentHp;
            if (_field.YourB != null && !_field.YourB.IsFainted)
                _teamCurHp[_activeIdxB] = _field.YourB.CurrentHp;
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT FORMATTERS
        // ════════════════════════════════════════════════════════════════════

        private static string Fmt(EventDto e) => e?.EventType switch
        {
            "MoveUsedEvent"         => $"{e.PokemonName} dùng {e.MoveName}!",
            "PokemonDamageEvent"    => $"{e.PokemonName} -{e.Damage} HP!{(e.IsCritical ? " ✦ Chí mạng!" : "")}",
            "PokemonFaintEvent"     => $"{e.PokemonName} bị hạ gục!",
            "SuperEffectiveEvent"   => "⬆ Hiệu quả cao!",
            "NotVeryEffectiveEvent" => "⬇ Không hiệu quả lắm...",
            "MoveNoEffectEvent"     => $"Không tác dụng tới {e.TargetName}!",
            "MoveMissedEvent"       => $"{e.PokemonName} đã trượt!",
            "StatusInflictedEvent"  => $"{e.PokemonName} bị {e.Status}!",
            "StatusDamageEvent"     => $"{e.PokemonName} đau vì {e.Status}!",
            "SwitchEvent"           => $"{e.WithdrawnName} ra, {e.SentOutName} vào!",
            "WeatherChangedEvent"   => $"Thời tiết: {e.NewWeather}!",
            "TerrainChangedEvent"   => $"Địa hình: {e.NewTerrain}!",
            "TerastallizationEvent" => $"{e.PokemonName} Terastallize → {e.TerType}!",
            "StatChangeEvent"       => $"{e.PokemonName} {e.StatName} {(e.Stages > 0 ? $"+{e.Stages}" : $"{e.Stages}")}!",
            "MessageEvent"          => e.Message,
            _ => null,
        };

        // ════════════════════════════════════════════════════════════════════
        // JSON HELPER
        // ════════════════════════════════════════════════════════════════════

        private static T J<T>(object raw)
        {
            try   { return JsonConvert.DeserializeObject<T>(raw?.ToString() ?? "{}"); }
            catch { return default; }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // INTERNAL MODELS
    // ══════════════════════════════════════════════════════════════════════════

    internal class FieldSnapshot
    {
        public string      Player2Id; // OpponentId (BOT_PLAYER hoặc playerId thật)
        public PokemonSlot YourA, YourB, OppA, OppB;

        public static FieldSnapshot From(FieldDto d) => new()
        {
            Player2Id = d.OpponentId ?? d.Player2Id, // ưu tiên OpponentId mới
            YourA = PokemonSlot.From(d.YourSlotA),
            YourB = PokemonSlot.From(d.YourSlotB),
            OppA  = PokemonSlot.From(d.OppSlotA),
            OppB  = PokemonSlot.From(d.OppSlotB),
        };
    }

    internal class PokemonSlot
    {
        public int    SpeciesId;
        public string SpeciesName, Type1, Type2, TerType, Status;
        public int    Level, CurrentHp, MaxHp;
        public bool   IsFainted, IsTerastallized;
        public List<MoveSlot> Moves;

        public static PokemonSlot From(PokemonDto d)
        {
            if (d == null) return null;
            // Ưu tiên: SpeciesName → Nickname → "Pokemon#ID"
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

    internal class MoveSlot
    {
        public string Name, Type, Category;
        public int    CurrentPp, MaxPp;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SERVER DTOs (client-side mapping)
    // ══════════════════════════════════════════════════════════════════════════

    // BattleRunningDto
    internal class FieldDto
    {
        public string BattleId        { get; set; }
        public string OpponentId      { get; set; } // ID đối thủ từ server
        public string Player2Id       { get; set; } // fallback cũ (không dùng nữa)
        public int    TurnNumber      { get; set; }
        public string Weather         { get; set; }
        public int    WeatherTurnsLeft{ get; set; }
        public string Terrain         { get; set; }
        public int    TerrainTurnsLeft{ get; set; }
        public PokemonDto YourSlotA   { get; set; }
        public PokemonDto YourSlotB   { get; set; }
        public PokemonDto OppSlotA    { get; set; }
        public PokemonDto OppSlotB    { get; set; }
        public List<int> YourTeamHp   { get; set; }
        public List<int> YourTeamMaxHp{ get; set; }
    }

    // FieldPokemonDto
    internal class PokemonDto
    {
        public int    SpeciesId       { get; set; }
        public string SpeciesName     { get; set; }
        public string Nickname        { get; set; }
        public string Type1           { get; set; }
        public string Type2           { get; set; }
        public string TerType         { get; set; }
        public bool   IsTerastallized { get; set; }
        public int    Level           { get; set; }
        public int    CurrentHp       { get; set; }
        public int    MaxHp           { get; set; }
        public bool   IsFainted       { get; set; }
        public string Status          { get; set; }
        public List<MoveDto> Moves    { get; set; }
    }

    // MoveSummaryDto
    internal class MoveDto
    {
        public string Name      { get; set; }
        public string Type      { get; set; }
        public string Category  { get; set; }
        public int    CurrentPp { get; set; }
        public int    MaxPp     { get; set; }
    }

    // BattleTurnResult
    internal class TurnDto
    {
        public string State           { get; set; }
        public string WinnerPlayerId  { get; set; }
        public int    ActiveHp1       { get; set; }
        public int    ActiveHp1b      { get; set; }
        public int    ActiveHp2       { get; set; }
        public int    ActiveHp2b      { get; set; }
        public List<FsInfo>   ForcedSwitches { get; set; }
        public List<EventDto> TypedEvents    { get; set; }
    }

    // TeamPreviewDto
    internal class TeamPreviewDto
    {
        public string BattleId         { get; set; }
        public string YourPlayerId     { get; set; }
        public string OpponentPlayerId { get; set; }
        public List<TeamPreviewPokemonDto> YourTeam     { get; set; }
        public List<TeamPreviewPokemonDto> OpponentTeam { get; set; }
    }

    internal class TeamPreviewPokemonDto
    {
        public int    SpeciesId   { get; set; }
        public string SpeciesName { get; set; }
        public string Nickname    { get; set; }
        public string Type1       { get; set; }
        public string Type2       { get; set; }
        public int    Level       { get; set; }
        public int    MaxHp       { get; set; }
    }

    // ForcedSwitchRequired payload
    internal class FsRequiredDto
    {
        public string BattleId         { get; set; }
        public string PlayerId         { get; set; }
        public int    Slot             { get; set; }
        public List<int> AvailableIndices { get; set; }
    }

    internal class FsInfo
    {
        public string PlayerId { get; set; }
        public int    Slot     { get; set; }
    }

    internal class EndDto
    {
        public string WinnerPlayerId { get; set; }
    }

    internal class ActionAcceptedDto
    {
        public string BattleId { get; set; }
        public int    Slot     { get; set; }
    }

    // TypedEvent (BattleTypedEventDto)
    internal class EventDto
    {
        public string EventType     { get; set; }
        public string PokemonName   { get; set; }
        public string MoveName      { get; set; }
        public int    Damage        { get; set; }
        public bool   IsCritical    { get; set; }
        public string TargetName    { get; set; }
        public string Status        { get; set; }
        public string StatName      { get; set; }
        public int    Stages        { get; set; }
        public string NewWeather    { get; set; }
        public string NewTerrain    { get; set; }
        public string TerType       { get; set; }
        public string WithdrawnName { get; set; }
        public string SentOutName   { get; set; }
        public string Message       { get; set; }
    }
}
