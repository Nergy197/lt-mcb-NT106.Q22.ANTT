using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using Game.Battle.UI;
using Game.Network;
using PokemonMMO.Audio;

namespace Game.Battle.Logic
{
    public class BattleNetworkController : MonoBehaviour
    {
        [Header("Settings")]
        public bool demoAutoMatch = true;

        [Header("HUD Phe Ta")]
        public EntityHUD playerHUD1;
        public EntityHUD playerHUD2;

        [Header("HUD Phe Dich")]
        public EntityHUD enemyHUD1;
        public EntityHUD enemyHUD2;

        [Header("BGM Clips")]
        [SerializeField] private AudioClip bgmPreview;
        [SerializeField] private AudioClip bgmBattle;
        [SerializeField] private AudioClip bgmWin;
        [SerializeField] private AudioClip bgmLose;

        [Header("Battle SFX")]
        [SerializeField] private AudioClip sfxHit;
        [SerializeField] private AudioClip sfxCrit;
        [SerializeField] private AudioClip sfxFaint;
        [SerializeField] private AudioClip sfxHpLow;
        [SerializeField] private AudioClip sfxStatusBurn;
        [SerializeField] private AudioClip sfxStatusPara;
        [SerializeField] private AudioClip sfxStatusSleep;
        [SerializeField] private AudioClip sfxStatUp;
        [SerializeField] private AudioClip sfxStatDown;
        [SerializeField] private AudioClip sfxWeatherRain;
        [SerializeField] private AudioClip sfxWeatherSun;
        [SerializeField] private AudioClip sfxWeatherSand;
        [SerializeField] private AudioClip sfxWeatherSnow;
        [SerializeField] private AudioClip sfxTera;

        [Header("Panels")]
        public BattleUIManager uiManager;
        public BattleSkillPanel skillPanel;
        public BattleCommandPanel commandPanel;
        public BattleDialogPanel dialogPanel;
        public BattleTeamPreviewPanel teamPreviewPanel;
        public BattlePartyPanel partyPanel;

        private string _battleId;
        private HubConnection _hub;
        private FieldSnapshot _field;
        private List<TeamPreviewPokemonDto> _myFullTeam = new();
        private List<int> _myTeamCurrentHp = new();
        private bool _isMyTurn = false;
        private int? _voluntarySwitchSrcSlot;
        private Dictionary<int, int> _pendingSwitches = new();
        private int _currentSrcSlot = 0;     // slot dang chon hanh dong (0=A, 1=B)
        private int _pendingMoveSlot = -1;    // move index dang cho chon muc tieu
        private bool _isForcedSwitchPending = false;
        private int _pendingForcedSlot = 0;
        private bool _isProcessingTurn = false;
        private bool _hasBattleStarted = false;
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

        private void OnEnable()
        {
            Debug.Log("[Battle] BNC OnEnable");
            if (uiManager == null) uiManager = FindObjectOfType<BattleUIManager>();
            if (skillPanel == null) skillPanel = FindObjectOfType<BattleSkillPanel>();
            if (commandPanel == null) commandPanel = FindObjectOfType<BattleCommandPanel>();

            BattleEvents.OnTeamOrderConfirmed += SendTeamOrder;
            BattleEvents.OnPlayerUseSkill += OnMoveSelected;
            BattleEvents.OnTargetSelected += OnTargetSelected;
            BattleEvents.OnPartySlotChosen += OnPartySlotChosen;
            BattleEvents.OnVoluntarySwitchRequested += OnVoluntarySwitchRequested;
            BattleEvents.OnPartyPanelCancelled += OnPartyBackClicked;
            BattleEvents.OnSkillPanelCancelled += OnSkillBackClicked;
            BattleEvents.OnPlayerSurrender += OnPlayerSurrender;

            if (playerHUD1 == null) {
                foreach (var h in FindObjectsOfType<EntityHUD>()) {
                    if (h.entityId == "Player_Lead_Slot") playerHUD1 = h;
                    else if (h.entityId == "Player_Sub2_Slot") playerHUD2 = h;
                    else if (h.entityId == "Enemy_Lead_Slot") enemyHUD1 = h;
                    else if (h.entityId == "Enemy_Sub2_Slot") enemyHUD2 = h;
                }
            }
        }

        private void OnDisable()
        {
            BattleEvents.OnTeamOrderConfirmed -= SendTeamOrder;
            BattleEvents.OnPlayerUseSkill -= OnMoveSelected;
            BattleEvents.OnTargetSelected -= OnTargetSelected;
            BattleEvents.OnPartySlotChosen -= OnPartySlotChosen;
            BattleEvents.OnVoluntarySwitchRequested -= OnVoluntarySwitchRequested;
            BattleEvents.OnPartyPanelCancelled -= OnPartyBackClicked;
            BattleEvents.OnSkillPanelCancelled -= OnSkillBackClicked;
            BattleEvents.OnPlayerSurrender -= OnPlayerSurrender;
        }

        private async void OnPlayerSurrender()
        {
            try {
                var hub = SignalRClient.Instance.Battle;
                if (hub != null && hub.State == HubConnectionState.Connected) {
                    await hub.InvokeAsync("Surrender", _battleId);
                }
            } catch (Exception ex) {
                Debug.LogError("[Battle] Surrender error: " + ex.Message);
            }
        }

        private void OnSkillBackClicked()
        {
            // Refresh move names and icons
            PopulateMoves(_currentSrcSlot);
            uiManager?.SwitchPanel(BattlePanelType.Skill);
        }

        private void OnPartyBackClicked()
        {
            uiManager?.SwitchPanel(BattlePanelType.Command);
        }

        private void Start()
        {
            _battleId = MatchmakingManager.CurrentBattleId;
            Debug.Log($"[Battle] BNC Start. BattleId: '{(_battleId ?? "NULL")}', demoAutoMatch: {demoAutoMatch}");
            
            if (string.IsNullOrEmpty(_battleId)) {
                if (demoAutoMatch) {
                    Debug.Log("[Battle] BattleId is empty, starting DemoAutoMatch...");
                    StartCoroutine(StartDemoAutoMatchRoutine());
                } else {
                    Debug.LogWarning("[Battle] BattleId is empty and demoAutoMatch is OFF. Returning to menu...");
                    ReturnToMenu();
                }
            } else {
                Debug.Log("[Battle] BattleId found, starting ConnectRoutine...");
                StartCoroutine(ConnectRoutine());
            }
        }

        private void Update()
        {
            lock (_mainThreadQueue) {
                while (_mainThreadQueue.Count > 0) _mainThreadQueue.Dequeue()?.Invoke();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Connection
        // ═══════════════════════════════════════════════════════════════════════

        private IEnumerator StartDemoAutoMatchRoutine()
        {
            string token = PlayerPrefs.GetString("jwt_token", "");
            if (string.IsNullOrEmpty(token)) yield break;

            yield return SignalRClient.Instance.ConnectAsync();
            var matchHub = SignalRClient.Instance.Matchmaking;
            while (matchHub.State != HubConnectionState.Connected) yield return null;

            // Vào lobby trước để server nhận ra player
            var joinTask = matchHub.InvokeAsync("JoinLobby");
            yield return new WaitUntil(() => joinTask.IsCompleted);

            // Lắng nghe MatchFound để nhận battleId
            matchHub.Remove("MatchFound");
            matchHub.On<object>("MatchFound", raw => {
                try {
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<BattleStartedEventDto>(raw.ToString());
                    if (data != null && !string.IsNullOrEmpty(data.battleId)) {
                        MatchmakingManager.CurrentBattleId = data.battleId;
                        _battleId = data.battleId;
                    }
                } catch (Exception ex) {
                    Debug.LogError("[Battle] MatchFound parse error: " + ex.Message);
                }
            });

            // Bắt đầu tìm trận (server xử lý countdown, fallback bot sau 20s)
            matchHub.InvokeAsync("FindMatch");

            // Đợi server trả về battleId qua MatchFound (tối đa 30s)
            float elapsed = 0f;
            while (string.IsNullOrEmpty(_battleId) && elapsed < 30f) {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!string.IsNullOrEmpty(_battleId)) {
                StartCoroutine(ConnectRoutine());
            } else {
                Debug.LogError("[Battle] Matchmaking timed out, no battle received.");
                ReturnToMenu();
            }
        }

        private IEnumerator ConnectRoutine()
        {
            Debug.Log("[Battle] ConnectRoutine started.");
            if (string.IsNullOrEmpty(_battleId)) {
                Debug.LogError("[Battle] ConnectRoutine aborted: BattleId is null.");
                yield return StartCoroutine(HandleBattleCrash("Lỗi: Không tìm thấy ID trận đấu."));
                yield break;
            }

            // Ensure SignalR is connected (vital if demoAutoMatch is off or scene started directly)
            var hub = SignalRClient.Instance.Battle;
            if (hub == null || hub.State == HubConnectionState.Disconnected)
            {
                Debug.Log("[Battle] SignalR not connected. Connecting now...");
                var connectTask = SignalRClient.Instance.ConnectAsync();
                yield return new WaitUntil(() => connectTask.IsCompleted);
                hub = SignalRClient.Instance.Battle; // Re-get hub reference
            }

            int retryCount = 0;
            while (hub.State != HubConnectionState.Connected && retryCount < 10)
            {
                retryCount++;
                Debug.Log($"[Battle] Waiting for Hub connection (Attempt {retryCount})... Current State: {hub.State}");
                yield return new WaitForSeconds(1.0f);
            }

            if (hub.State != HubConnectionState.Connected)
            {
                yield return StartCoroutine(HandleBattleCrash("Lỗi: Không thể kết nối tới máy chủ trận đấu."));
                yield break;
            }

            SetupHubHandlers(hub);
            Debug.Log("[Battle] Joining Battle Hub: " + _battleId);

            bool joinFailed = false;
            try {
                hub.InvokeAsync("JoinBattle", _battleId);
                BattleEvents.OnBattleConnected?.Invoke();
            } catch (Exception ex) {
                Debug.LogError("[Battle] JoinBattle Invoke Error: " + ex.Message);
                joinFailed = true;
            }

            if (joinFailed)
            {
                yield return StartCoroutine(HandleBattleCrash("Lỗi kết nối trận đấu."));
            }
        }

        private IEnumerator WaitAndShowForcedSwitch(FsRequiredDto dto)
        {
            // Đợi cho đến khi các dòng thoại TurnResolved chạy xong
            while (_isProcessingTurn) yield return null;
            
            // Đợi thêm một chút để người chơi kịp định thần
            yield return new WaitForSeconds(0.5f);

            _isForcedSwitchPending = true;
            _pendingForcedSlot = dto.Slot;

            var data = new PartyPanelData {
                Pokemon = GetCurrentPartyData(),
                AvailableIdxs = dto.AvailableIndices.ToArray(),
                IsForcedSwitch = true
            };
            BattleEvents.OnPartyPanelOpen?.Invoke(data);
            uiManager?.SwitchPanel(BattlePanelType.Party);
        }

        private IEnumerator HandleBattleCrash(string message)
        {
            Debug.LogError("[Battle] CRASH HANDLER: " + message);
            uiManager?.SwitchPanel(BattlePanelType.Dialog); // Hiện hộp thoại để xem lỗi
            if (dialogPanel != null)
            {
                dialogPanel.EnqueueMessage(message, false);
                yield return new WaitForSeconds(5f);
            }
            ReturnToMenu();
        }

        private void ReturnToMenu()
        {
            Debug.Log("[Battle] Returning to Menu...");
            MatchmakingManager.ResetBattleId();
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu scene");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Hub Event Handlers
        // ═══════════════════════════════════════════════════════════════════════

        private void SetupHubHandlers(HubConnection hub)
        {
            // Ensure EffectManager exists
            if (FindObjectOfType<BattleEffectManager>() == null)
            {
                new GameObject("BattleEffectManager").AddComponent<BattleEffectManager>();
            }

            hub.Remove("TeamPreviewReady");
            hub.Remove("BattleRunning");
            hub.Remove("TurnResolved");
            hub.Remove("ActionAccepted");
            hub.Remove("Error");

            hub.On<object>("TeamPreviewReady", raw => Enqueue(() => {
                Debug.Log($"[Battle] Event: TeamPreviewReady. Current State: {uiManager?.gameObject.name}");
                AudioManager.Instance?.PlayBGM(bgmPreview);
                var dto = J<TeamPreviewDto>(raw);
                _myFullTeam = dto.YourTeam;
                _myTeamCurrentHp = dto.YourTeam.Select(p => p.MaxHp).ToList();

                const int bringCount = 4;
                if (dto.YourTeam.Count < bringCount)
                {
                    // Không đủ 4 Pokemon — tự động chọn tất cả, chờ đối thủ
                    Debug.Log($"[Battle] Team chỉ có {dto.YourTeam.Count} Pokemon, tự động chọn.");
                    BattleEvents.OnPrintDialog?.Invoke(
                        $"Đội của bạn chỉ có {dto.YourTeam.Count} Pokemon.\nĐang chờ đối thủ chọn đội...", false);
                    var allIndices = System.Linq.Enumerable.Range(0, dto.YourTeam.Count).ToArray();
                    SendTeamOrder(allIndices);
                }
                else
                {
                    uiManager?.SwitchPanel(BattlePanelType.TeamPreview);
                    var data = new PreviewTeamData {
                        MyTeam = dto.YourTeam.Select(p => new PreviewPokemon { Name = p.SpeciesName, Level = p.Level, SpeciesId = p.SpeciesId }).ToArray(),
                        OppTeam = dto.OpponentTeam.Select(p => new PreviewPokemon { Name = p.SpeciesName, Level = p.Level, SpeciesId = p.SpeciesId }).ToArray()
                    };
                    BattleEvents.OnTeamPreviewStart?.Invoke(data);
                }
            }));

            hub.On<object>("BattleRunning", raw => Enqueue(() => {
                Debug.Log("[Battle] Event: BattleRunning");
                if (!_hasBattleStarted)
                {
                    _hasBattleStarted = true;
                    AudioManager.Instance?.PlayBGM(bgmBattle);
                }
                var dto = J<FieldDto>(raw);
                _field = FieldSnapshot.From(dto);
                if (dto.YourTeamHp != null) _myTeamCurrentHp = dto.YourTeamHp;
                
                _isMyTurn = false;
                _pendingSwitches.Clear();
                uiManager?.SwitchPanel(BattlePanelType.None);
                BattleEvents.OnFieldUpdated?.Invoke(_field.YourA, _field.YourB, _field.OppA, _field.OppB);
                UpdateHUDs();
                UpdateSprites();
                _currentSrcSlot = 0;
                PopulateMoves(0);
                ShowCommandPanel(0);
            }));

            hub.On<object>("ActionAccepted", raw => Enqueue(() => {
                Debug.Log("[Battle] Action accepted by server");
                // Optimistic transition is already handled in MoveToNextActionSlot
            }));

            hub.On<object>("TurnResolved", raw => Enqueue(() => {
                Debug.Log("[Battle] Event: TurnResolved");
                var dto = J<TurnDto>(raw);
                StartCoroutine(ResolveTurn(dto));
            }));

            hub.On<string>("Error", msg => Enqueue(() => {
                Debug.LogError("[Battle] Server Error: " + msg);
                StartCoroutine(HandleBattleCrash("Lỗi Server: " + msg));
            }));

            hub.On<object>("ForcedSwitchRequired", raw => Enqueue(() => {
                Debug.Log("[Battle] Event: ForcedSwitchRequired");
                var dto = J<FsRequiredDto>(raw);
                StartCoroutine(WaitAndShowForcedSwitch(dto));
            }));

            hub.On<object>("ForcedSwitchAccepted", raw => Enqueue(() => {
                Debug.Log("[Battle] Forced switch accepted");
                uiManager?.SwitchPanel(BattlePanelType.None);
                BattleEvents.OnPrintDialog?.Invoke("Dang cho doi thu...", true);
            }));

            hub.On<object>("TurnReady", raw => Enqueue(() => {
                Debug.Log("[Battle] Turn Ready (after switch)");
                var dto = J<FieldDto>(raw);
                _field = FieldSnapshot.From(dto);
                _pendingSwitches.Clear();
                BattleEvents.OnFieldUpdated?.Invoke(_field.YourA, _field.YourB, _field.OppA, _field.OppB);
                UpdateHUDs();
                UpdateSprites();
                
                // Show command panel for the first non-fainted slot
                _currentSrcSlot = (_field.YourA != null && !_field.YourA.IsFainted) ? 0 : 1;
                PopulateMoves(_currentSrcSlot);
                ShowCommandPanel(_currentSrcSlot);
            }));

            hub.On<object>("BattleEnded", raw => Enqueue(() => {
                Debug.Log("[Battle] Event: BattleEnded");
                var dto = J<TurnDto>(raw); // BattleEnded uses similar structure or explicit DTO
                // Kết quả thắng/thua do server tính riêng cho người nhận (YouWon/IsDraw),
                // không tự so WinnerPlayerId với player_id (sai khi 2 client chung PlayerPrefs).
                bool isDraw = dto.IsDraw;
                bool iWon   = dto.YouWon;
                string winnerId = isDraw ? "" : (string.IsNullOrEmpty(dto.WinnerPlayerId) ? "opponent" : dto.WinnerPlayerId);

                StartCoroutine(HandleBattleEnd(iWon, winnerId));
            }));
        }

        private IEnumerator HandleBattleEnd(bool iWon, string winnerId)
        {
            AudioManager.Instance?.PlayBGM(iWon ? bgmWin : bgmLose, fadeDuration: 0.3f, loop: false);
            string msg = string.IsNullOrEmpty(winnerId)
                ? "TRAN DAU KET THUC HOA!"
                : (iWon ? "BAN DA CHIEN THANG!" : "BAN DA THAT BAI...");
            // Cache kết quả TRƯỚC khi phát sự kiện: BattleResultPanel đang inactive nên sẽ
            // đọc lại giá trị này trong OnEnable (khi BattleUIManager bật nó lên).
            BattleEvents.SetPendingResult(iWon, winnerId);
            BattleEvents.OnPrintDialog?.Invoke(msg, false);
            BattleEvents.OnBattleResult?.Invoke(iWon, winnerId ?? "");
            // Không tự về menu nữa: BattleResultPanel sẽ về menu khi người chơi click vào màn hình.
            yield break;
        }

        private PartyPokemon[] GetCurrentPartyData()
        {
            var result = new List<PartyPokemon>();
            for (int i = 0; i < _myFullTeam.Count; i++)
            {
                var p = _myFullTeam[i];
                int hp = (i < _myTeamCurrentHp.Count) ? _myTeamCurrentHp[i] : p.MaxHp;
                
                bool isActive = false;
                if (_field != null) {
                    if (_field.YourA != null && _field.YourA.SpeciesId == p.SpeciesId && _field.YourA.CurrentHp == hp) isActive = true;
                    if (_field.YourB != null && _field.YourB.SpeciesId == p.SpeciesId && _field.YourB.CurrentHp == hp) isActive = true;
                }

                result.Add(new PartyPokemon {
                    PartyIndex = i,
                    Name = p.SpeciesName,
                    Level = p.Level,
                    CurrentHp = hp,
                    MaxHp = p.MaxHp,
                    IsFainted = hp <= 0,
                    IsActive = isActive,
                    Type1 = p.Type1,
                    Type2 = p.Type2,
                    Status = "" // Status update logic can be added later
                });
            }
            return result.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HUD + Sprite Updates
        // ═══════════════════════════════════════════════════════════════════════

        private void UpdateHUDs()
        {
            SetupHUD(playerHUD1, _field.YourA, false);
            SetupHUD(playerHUD2, _field.YourB, false);
            SetupHUD(enemyHUD1,  _field.OppA,  true);
            SetupHUD(enemyHUD2,  _field.OppB,  true);
        }

        private void SetupHUD(EntityHUD hud, PokemonSlot slot, bool isOpponent)
        {
            if (hud == null) return;
            
            // Hide HUD if pokemon is missing or fainted
            if (slot == null || slot.IsFainted)
            {
                hud.gameObject.SetActive(false);
                return;
            }

            hud.gameObject.SetActive(true);
            string displayName = slot.SpeciesName;
            hud.SetupEntity(hud.entityId, displayName, slot.CurrentHp, slot.MaxHp);
            hud.SetLevel(slot.Level);
            hud.SetTypes(slot.Type1, slot.Type2);
            hud.SetStatus(slot.Status);
            hud.SetTeraType(slot.TerType, slot.IsTerastallized);
        }

        private void UpdateSprites()
        {
            var loader = FindObjectOfType<BattleSpriteLoader>();
            if (loader == null) return;

            if (_field.YourA != null && !_field.YourA.IsFainted) loader.LoadSpriteForSlot("Player_Lead_Slot", "Player1_HUD", _field.YourA.SpeciesName, true);
            else loader.ClearBattleSprite("Player_Lead_Slot");

            if (_field.YourB != null && !_field.YourB.IsFainted) loader.LoadSpriteForSlot("Player_Sub2_Slot", "Player2_HUD", _field.YourB.SpeciesName, true);
            else loader.ClearBattleSprite("Player_Sub2_Slot");

            if (_field.OppA != null && !_field.OppA.IsFainted) loader.LoadSpriteForSlot("Enemy_Sub2_Slot", "Enemy1_HUD", _field.OppA.SpeciesName, false);
            else loader.ClearBattleSprite("Enemy_Sub2_Slot");

            if (_field.OppB != null && !_field.OppB.IsFainted) loader.LoadSpriteForSlot("Enemy_Lead_Slot", "Enemy2_HUD", _field.OppB.SpeciesName, false);
            else loader.ClearBattleSprite("Enemy_Lead_Slot");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Move Selection -> Target Selection -> Submit
        // ═══════════════════════════════════════════════════════════════════════

        private void PopulateMoves(int activeSlot)
        {
            if (skillPanel == null) skillPanel = FindObjectOfType<BattleSkillPanel>();
            if (skillPanel == null) return;

            var pkmn = (activeSlot == 0) ? _field.YourA : _field.YourB;
            if (pkmn == null || pkmn.Moves == null) return;

            for (int i = 0; i < 4; i++)
            {
                if (i < pkmn.Moves.Count) {
                    var m = pkmn.Moves[i];
                    skillPanel.SetMove(i, m);
                } else {
                    skillPanel.SetMove(i, null);
                }
            }
        }

        // Player chon 1 chieu thuc -> hien target selection
        private void OnMoveSelected(int moveIndex)
        {
            _pendingMoveSlot = moveIndex;
            var pkmn = (_currentSrcSlot == 0) ? _field.YourA : _field.YourB;
            if (pkmn == null || pkmn.Moves == null || moveIndex >= pkmn.Moves.Count) return;

            var move = pkmn.Moves[moveIndex];
            Debug.Log($"[Battle] Move {move.Name} (ID: {move.MoveId}, TargetType: {move.TargetType}) selected for slot {_currentSrcSlot}");

            // 0: SingleOpponent, 2: SingleAlly
            bool needsManualTarget = move.TargetType == 0 || move.TargetType == 2;

            if (needsManualTarget && skillPanel != null)
            {
                Debug.Log($"[Battle] Showing targets for move {move.Name}");
                BattleEvents.OnPrintDialog?.Invoke($"Select target for {move.Name}...", false);
                
                string oppA = (_field.OppA != null && !_field.OppA.IsFainted) ? _field.OppA.SpeciesName : "---";
                string oppB = (_field.OppB != null && !_field.OppB.IsFainted) ? _field.OppB.SpeciesName : "---";
                string myA  = (_field.YourA != null && !_field.YourA.IsFainted) ? _field.YourA.SpeciesName : "---";
                string myB  = (_field.YourB != null && !_field.YourB.IsFainted) ? _field.YourB.SpeciesName : "---";

                // Swap labels to match visual layout: Button 0 (Right/Slot B), Button 1 (Left/Slot A)
                skillPanel.SetTargetLabels(oppB, oppA, myA, myB);
                uiManager?.SwitchPanel(BattlePanelType.Skill);
            }
            else
            {
                Debug.Log($"[Battle] Auto-submitting move {move.Name} with default target.");
                // For spread/field/self moves, targetSlot doesn't matter much on client,
                // but we send 0 and ensure OnTargetSelected doesn't swap it if it's an auto-call.
                OnTargetSelectedInternal(0, isAuto: true);
            }
        }

        // Player chon muc tieu -> gui len server
        private void OnTargetSelected(int targetBtnIndex)
        {
            OnTargetSelectedInternal(targetBtnIndex, isAuto: false);
        }

        private void OnTargetSelectedInternal(int targetBtnIndex, bool isAuto)
        {
            if (_pendingMoveSlot < 0) return;

            int finalTarget = targetBtnIndex;
            if (!isAuto)
            {
                // Swap back indices to match server slots: 0 (Left/OppA), 1 (Right/OppB)
                if (targetBtnIndex == 0) finalTarget = 1;      // Click Right Button -> Target OppB (1)
                else if (targetBtnIndex == 1) finalTarget = 0; // Click Left Button -> Target OppA (0)
            }

            Debug.Log($"[Battle] Target {finalTarget} selected (isAuto: {isAuto}) for move {_pendingMoveSlot}");

            bool useTera = (skillPanel != null && skillPanel.IsTeraActive);
            SubmitAction("Move", _pendingMoveSlot, null, finalTarget, useTera);
            
            _pendingMoveSlot = -1;
            if (skillPanel != null) skillPanel.ResetTeraToggle();

            // Optimistic transition to next slot
            MoveToNextActionSlot();
        }

        private void MoveToNextActionSlot()
        {
            // If we just finished slot 0 and slot 1 is available, move to slot 1
            if (_currentSrcSlot == 0 && _field.YourB != null && !_field.YourB.IsFainted)
            {
                ShowCommandPanel(1);
            }
            else
            {
                // No more slots to command
                uiManager?.SwitchPanel(BattlePanelType.None);
                BattleEvents.OnPrintDialog?.Invoke("Waiting for opponent...", false);
            }
        }

        private void OnVoluntarySwitchRequested(int srcSlot)
        {
            _currentSrcSlot = srcSlot;
            _voluntarySwitchSrcSlot = srcSlot;
            var data = new PartyPanelData {
                Pokemon = GetCurrentPartyData(),
                AvailableIdxs = GetAvailableSwitchIndices(srcSlot),
                IsForcedSwitch = false
            };
            BattleEvents.OnPartyPanelOpen?.Invoke(data);
            uiManager?.SwitchPanel(BattlePanelType.Party);
        }

        private int[] GetAvailableSwitchIndices(int srcSlot)
        {
            var party = GetCurrentPartyData();
            var list = new List<int>();
            HashSet<int> occupied = new HashSet<int>();

            // 1. Nhung con dang tren san
            if (_field.YourA != null && !_field.YourA.IsFainted) occupied.Add(GetPartyIndexBySpeciesId(_field.YourA.SpeciesId));
            if (_field.YourB != null && !_field.YourB.IsFainted) occupied.Add(GetPartyIndexBySpeciesId(_field.YourB.SpeciesId));

            // 2. Nhung con da duoc chon de doi vao o slot kia
            foreach (var kvp in _pendingSwitches)
            {
                if (kvp.Key != srcSlot) occupied.Add(kvp.Value);
            }

            for (int i = 0; i < party.Length; i++)
            {
                if (!party[i].IsFainted && !occupied.Contains(i))
                    list.Add(i);
            }
            return list.ToArray();
        }

        private int GetPartyIndexBySpeciesId(string speciesId)
        {
            return _myFullTeam.FindIndex(p => p.SpeciesId == speciesId);
        }

        private void OnPartySlotChosen(int partyIndex)
        {
            // Check if this was a forced switch or voluntary
            // For now, we can check if the party panel data was set as forced
            // A better way is to track state in BNC
            
            // If it's a forced switch, we need to know the slot
            // I'll assume we store it in _pendingForcedSlot
            if (_isForcedSwitchPending)
            {
                SendForcedSwitch(_pendingForcedSlot, partyIndex);
                _isForcedSwitchPending = false;
            }
            else
            {
                _pendingSwitches[_currentSrcSlot] = partyIndex;
                SubmitAction("Switch", null, partyIndex, 0, false);
                
                // Optimistic transition to next slot
                MoveToNextActionSlot();
            }
        }

        private async void SendForcedSwitch(int slot, int partyIndex)
        {
            try {
                var hub = SignalRClient.Instance.Battle;
                if (hub != null && hub.State == HubConnectionState.Connected) {
                    await hub.InvokeAsync("SubmitForcedSwitch", _battleId, slot, partyIndex);
                }
            } catch (Exception ex) {
                Debug.LogError("[Battle] SubmitForcedSwitch error: " + ex.Message);
            }
        }

        private async void SubmitAction(string actionType, int? moveSlot, int? switchIndex, int targetSlot, bool useTera)
        {
            try {
                var hub = SignalRClient.Instance.Battle;
                if (hub != null && hub.State == HubConnectionState.Connected) {
                    await hub.InvokeAsync("SubmitBattleAction", _battleId, _currentSrcSlot, actionType, moveSlot, switchIndex, targetSlot, useTera);
                }
            } catch (Exception ex) {
                Debug.LogError("[Battle] Submit error: " + ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UI Flow
        // ═══════════════════════════════════════════════════════════════════════

        private void ShowCommandPanel(int slot)
        {
            var p = (slot == 0) ? _field.YourA : _field.YourB;
            if (p == null || p.IsFainted) {
                if (slot == 0) { _currentSrcSlot = 1; ShowCommandPanel(1); }
                return;
            }
            _isMyTurn = true;
            _currentSrcSlot = slot;
            if (commandPanel != null) commandPanel.CurrentSrcSlot = slot;
            PopulateMoves(slot);
            BattleEvents.OnPrintDialog?.Invoke($"What will {p.SpeciesName} do?", false);
            uiManager?.SwitchPanel(BattlePanelType.Command);
        }

        private async void SendTeamOrder(int[] order)
        {
            Debug.Log("[Battle] Sending Team Order: " + string.Join(",", order));
            try {
                var hub = SignalRClient.Instance.Battle;
                if (hub != null && hub.State == HubConnectionState.Connected)
                    await hub.InvokeAsync("SubmitTeamOrder", _battleId, order.ToList());
            } catch (Exception ex) {
                Debug.LogError("[Battle] Team order error: " + ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Turn Resolution
        // ═══════════════════════════════════════════════════════════════════════

        private IEnumerator ResolveTurn(TurnDto r)
        {
            _isProcessingTurn = true;
            _pendingSwitches.Clear();
            uiManager?.SwitchPanel(BattlePanelType.None);
            if (r.TypedEvents != null) {
                foreach (var ev in r.TypedEvents) {
                    // Trigger Visual Effects
                    ProcessEventVisuals(ev);

                    string msg = ev.Message ?? "...";
                    string owner = ev.PlayerId;
                    string myId = SignalRClient.Instance.PlayerId;

                    // If owner is missing, try to guess from PokemonName
                    if (string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(ev.PokemonName))
                    {
                        string pName = ev.PokemonName.ToLower();
                        if (_field != null) {
                            bool isOppA = _field.OppA != null && _field.OppA.SpeciesName.ToLower() == pName;
                            bool isOppB = _field.OppB != null && _field.OppB.SpeciesName.ToLower() == pName;
                            if (isOppA || isOppB) owner = "OPPONENT_GUESSED";
                            else owner = myId;
                        }
                    }

                    Debug.Log($"<color=magenta>[Battle] Processing Msg: {msg} | Owner: {owner} | MyID: {myId}</color>");

                    bool isMe = !string.IsNullOrEmpty(owner) && owner.Equals(myId, System.StringComparison.OrdinalIgnoreCase);
                    bool isOpp = !string.IsNullOrEmpty(owner) && !isMe;

                    // 1. Replace [TRAINER] placeholder or raw IDs
                    string trainerName = isMe ? "You" : "Opponent";
                    msg = msg.Replace("[TRAINER]", trainerName);
                    
                    if (!string.IsNullOrEmpty(owner) && owner.Length > 10) // Only replace if it looks like a UUID
                        msg = System.Text.RegularExpressions.Regex.Replace(msg, System.Text.RegularExpressions.Regex.Escape(owner), trainerName, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    // 2. Prefix Pokemon names to distinguish ownership
                    string pfx = isOpp ? "Opponent " : "Your ";
                    
                    if (!string.IsNullOrEmpty(ev.PokemonName)) 
                        msg = System.Text.RegularExpressions.Regex.Replace(msg, @"\b" + System.Text.RegularExpressions.Regex.Escape(ev.PokemonName) + @"\b", pfx + ev.PokemonName, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    if (!string.IsNullOrEmpty(ev.WithdrawnName)) 
                        msg = System.Text.RegularExpressions.Regex.Replace(msg, @"\b" + System.Text.RegularExpressions.Regex.Escape(ev.WithdrawnName) + @"\b", pfx + ev.WithdrawnName, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        
                    if (!string.IsNullOrEmpty(ev.SentOutName)) 
                        msg = System.Text.RegularExpressions.Regex.Replace(msg, @"\b" + System.Text.RegularExpressions.Regex.Escape(ev.SentOutName) + @"\b", pfx + ev.SentOutName, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (!string.IsNullOrEmpty(ev.TargetName))
                    {
                        string targetPfx = isOpp ? "Your " : "Opponent ";
                        msg = System.Text.RegularExpressions.Regex.Replace(msg, @"\b" + System.Text.RegularExpressions.Regex.Escape(ev.TargetName) + @"\b", targetPfx + ev.TargetName, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }

                    Debug.Log($"<color=cyan>[Battle] Final Msg: {msg}</color>");

                    BattleEvents.OnPrintDialog?.Invoke(msg, false);
                    yield return new WaitForSeconds(1.2f);
                }
            }
            if (r.State == "Ended") {
                // Kết quả thắng/thua + điều hướng do sự kiện "BattleEnded" (server tính per-player) xử lý.
                // Không tự quyết ở đây để tránh double-fire và so id sai khi 2 client chung PlayerPrefs.
                Debug.Log("[Battle] Turn ket thuc tran - cho su kien BattleEnded quyet dinh ket qua.");
            } else {
                // Yeu cau server gui lai trang thai moi nhat
                var hub = SignalRClient.Instance.Battle;
                if (hub != null && hub.State == HubConnectionState.Connected)
                    yield return hub.InvokeAsync("RequestCurrentState", _battleId);
            }
            _isProcessingTurn = false;
        }

        private void ProcessEventVisuals(EventDto ev)
        {
            var eff = BattleEffectManager.Instance;
            var audio = AudioManager.Instance;
            if (eff == null) return;

            // 1. Move Animation
            if (!string.IsNullOrEmpty(ev.MoveName))
            {
                string targetSlot = GetSlotNameByPokemonName(ev.TargetName);
                if (!string.IsNullOrEmpty(targetSlot))
                    eff.PlayMoveEffect(ev.MoveName, targetSlot);
            }

            // 2. Hit / Damage Effect
            if (ev.Damage > 0 || ev.EventType == "Damage")
            {
                string targetSlot = GetSlotNameByPokemonName(ev.TargetName ?? ev.PokemonName);
                if (!string.IsNullOrEmpty(targetSlot))
                {
                    eff.PlayHitEffect(targetSlot);
                    BattleEvents.OnHealthChanged?.Invoke(targetSlot, ev.HpAfter, 100);

                    bool isCrit = !string.IsNullOrEmpty(ev.Message) &&
                                  ev.Message.IndexOf("critical", StringComparison.OrdinalIgnoreCase) >= 0;
                    audio?.PlaySFX(isCrit ? sfxCrit : sfxHit);

                    var slot = GetSlotByName(targetSlot);
                    if (slot != null && slot.MaxHp > 0 && ev.HpAfter > 0 &&
                        (float)ev.HpAfter / slot.MaxHp < 0.2f)
                        audio?.PlaySFX(sfxHpLow);
                }
            }

            // 3. Status Infliction
            if (ev.EventType == "StatusInflictedEvent")
            {
                string targetSlot = GetSlotNameByPokemonName(ev.PokemonName);
                if (!string.IsNullOrEmpty(targetSlot))
                {
                    Color statusColor = Color.white;
                    string status = ev.Status ?? "";
                    if (status.IndexOf("Burn", StringComparison.OrdinalIgnoreCase) >= 0)
                    { statusColor = Color.red;     audio?.PlaySFX(sfxStatusBurn); }
                    else if (status.IndexOf("Paralysis", StringComparison.OrdinalIgnoreCase) >= 0)
                    { statusColor = Color.yellow;  audio?.PlaySFX(sfxStatusPara); }
                    else if (status.IndexOf("Poison", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             status.IndexOf("Toxic",  StringComparison.OrdinalIgnoreCase) >= 0)
                    { statusColor = Color.magenta; audio?.PlaySFX(sfxStatusBurn); }
                    else if (status.IndexOf("Sleep",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                             status.IndexOf("Freeze", StringComparison.OrdinalIgnoreCase) >= 0)
                    { statusColor = Color.cyan;    audio?.PlaySFX(sfxStatusSleep); }

                    eff.PlayStatusFlash(targetSlot, statusColor);
                }
            }

            // 4. Stat Changes
            if (ev.EventType == "StatChangeEvent")
            {
                string targetSlot = GetSlotNameByPokemonName(ev.PokemonName);
                if (!string.IsNullOrEmpty(targetSlot))
                {
                    Color statColor = ev.Stages > 0 ? Color.green : Color.blue;
                    eff.PlayStatusFlash(targetSlot, statColor);
                    audio?.PlaySFX(ev.Stages > 0 ? sfxStatUp : sfxStatDown);
                }
            }

            // 5. Fainting
            if (ev.EventType == "PokemonFaintEvent")
            {
                audio?.PlaySFX(sfxFaint);
                string slotName = GetSlotNameByPokemonName(ev.PokemonName);
                if (slotName == "Player_Lead_Slot" && _field.YourA != null) _field.YourA.IsFainted = true;
                else if (slotName == "Player_Sub2_Slot" && _field.YourB != null) _field.YourB.IsFainted = true;
                else if (slotName == "Enemy_Lead_Slot"  && _field.OppA != null) _field.OppA.IsFainted = true;
                else if (slotName == "Enemy_Sub2_Slot"  && _field.OppB != null) _field.OppB.IsFainted = true;

                UpdateHUDs();
                UpdateSprites();
            }

            // 6. Tera
            if (ev.EventType == "TerastallizeEvent")
                audio?.PlaySFX(sfxTera);

            // 7. Weather
            if (ev.EventType == "WeatherChangeEvent" || ev.EventType == "WeatherSetEvent")
            {
                string msg = ev.Message ?? "";
                if      (msg.IndexOf("rain",      StringComparison.OrdinalIgnoreCase) >= 0) audio?.PlaySFX(sfxWeatherRain);
                else if (msg.IndexOf("sun",       StringComparison.OrdinalIgnoreCase) >= 0 ||
                         msg.IndexOf("harsh",     StringComparison.OrdinalIgnoreCase) >= 0) audio?.PlaySFX(sfxWeatherSun);
                else if (msg.IndexOf("sandstorm", StringComparison.OrdinalIgnoreCase) >= 0) audio?.PlaySFX(sfxWeatherSand);
                else if (msg.IndexOf("snow",      StringComparison.OrdinalIgnoreCase) >= 0 ||
                         msg.IndexOf("hail",      StringComparison.OrdinalIgnoreCase) >= 0) audio?.PlaySFX(sfxWeatherSnow);
            }
        }

        private PokemonSlot GetSlotByName(string slotName)
        {
            if (_field == null) return null;
            return slotName switch
            {
                "Player_Lead_Slot" => _field.YourA,
                "Player_Sub2_Slot" => _field.YourB,
                "Enemy_Lead_Slot"  => _field.OppA,
                "Enemy_Sub2_Slot"  => _field.OppB,
                _ => null
            };
        }

        private string GetSlotNameByPokemonName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            name = name.ToLower();
            if (_field.YourA != null && _field.YourA.SpeciesName.ToLower() == name) return "Player_Lead_Slot";
            if (_field.YourB != null && _field.YourB.SpeciesName.ToLower() == name) return "Player_Sub2_Slot";
            if (_field.OppA  != null && _field.OppA.SpeciesName.ToLower()  == name) return "Enemy_Lead_Slot";
            if (_field.OppB  != null && _field.OppB.SpeciesName.ToLower()  == name) return "Enemy_Sub2_Slot";
            return null;
        }

        private void Enqueue(Action action) { lock (_mainThreadQueue) _mainThreadQueue.Enqueue(action); }
        private T J<T>(object raw) { try { return JsonConvert.DeserializeObject<T>(raw.ToString()); } catch { return default; } }
    }
}
