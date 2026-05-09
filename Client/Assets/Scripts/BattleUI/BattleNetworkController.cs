using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using Game.Battle.UI;
using Game.Network;

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
        private int _currentSrcSlot = 0;     // slot dang chon hanh dong (0=A, 1=B)
        private int _pendingMoveSlot = -1;    // move index dang cho chon muc tieu
        private bool _isForcedSwitchPending = false;
        private int _pendingForcedSlot = 0;
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

            var task = matchHub.InvokeAsync<string>("FightBot");
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsCompletedSuccessfully && !string.IsNullOrEmpty(task.Result)) {
                _battleId = task.Result;
                MatchmakingManager.CurrentBattleId = _battleId;
                StartCoroutine(ConnectRoutine());
            } else {
                Debug.LogError("[Battle] Server rejected FightBot.");
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

            yield return new WaitForSeconds(0.8f);
            
            bool joinFailed = false;
            try {
                hub.InvokeAsync("JoinBattle", _battleId);
            } catch (Exception ex) {
                Debug.LogError("[Battle] JoinBattle Invoke Error: " + ex.Message);
                joinFailed = true;
            }

            if (joinFailed)
            {
                yield return StartCoroutine(HandleBattleCrash("Lỗi kết nối trận đấu."));
            }
        }

        private IEnumerator HandleBattleCrash(string message)
        {
            Debug.LogError("[Battle] CRASH HANDLER: " + message);
            if (dialogPanel != null)
            {
                dialogPanel.EnqueueMessage(message, false);
                yield return new WaitForSeconds(3f);
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
                Debug.Log("[Battle] Event: TeamPreviewReady");
                var dto = J<TeamPreviewDto>(raw);
                _myFullTeam = dto.YourTeam;
                _myTeamCurrentHp = dto.YourTeam.Select(p => p.MaxHp).ToList(); // Start with full HP
                
                uiManager?.SwitchPanel(BattlePanelType.TeamPreview);
                var data = new PreviewTeamData {
                    MyTeam = dto.YourTeam.Select(p => new PreviewPokemon { Name = p.SpeciesName, Level = p.Level, SpeciesId = p.SpeciesId }).ToArray(),
                    OppTeam = dto.OpponentTeam.Select(p => new PreviewPokemon { Name = p.SpeciesName, Level = p.Level, SpeciesId = p.SpeciesId }).ToArray()
                };
                BattleEvents.OnTeamPreviewStart?.Invoke(data);
            }));

            hub.On<object>("BattleRunning", raw => Enqueue(() => {
                Debug.Log("[Battle] Event: BattleRunning");
                var dto = J<FieldDto>(raw);
                _field = FieldSnapshot.From(dto);
                if (dto.YourTeamHp != null) _myTeamCurrentHp = dto.YourTeamHp;
                
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
                // Slot A xong, tiep tuc Slot B (neu co)
                if (_currentSrcSlot == 0 && _field.YourB != null && !_field.YourB.IsFainted) {
                    _currentSrcSlot = 1;
                    PopulateMoves(1);
                    ShowCommandPanel(1);
                } else {
                    // Da gui du hanh dong, doi server xu ly
                    uiManager?.SwitchPanel(BattlePanelType.None);
                    BattleEvents.OnPrintDialog?.Invoke("Dang cho doi thu...", true);
                }
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
                
                _isForcedSwitchPending = true;
                _pendingForcedSlot = dto.Slot;
                
                // Open party panel in forced switch mode
                var data = new PartyPanelData {
                    Pokemon = GetCurrentPartyData(),
                    AvailableIdxs = dto.AvailableIndices.ToArray(),
                    IsForcedSwitch = true,
                    ForcedSlot = dto.Slot
                };
                BattleEvents.OnPartyPanelOpen?.Invoke(data);
                uiManager?.SwitchPanel(BattlePanelType.Party);
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
                string winner = dto.WinnerPlayerId;
                bool iWon = winner == SignalRClient.Instance.PlayerId;
                
                StartCoroutine(HandleBattleEnd(iWon, winner));
            }));
        }

        private IEnumerator HandleBattleEnd(bool iWon, string winnerId)
        {
            string msg = iWon ? "BAN DA CHIEN THANG!" : "BAN DA THAT BAI...";
            if (string.IsNullOrEmpty(winnerId)) msg = "TRAN DAU KET THUC HOA.";
            
            BattleEvents.OnPrintDialog?.Invoke(msg, false);
            yield return new WaitForSeconds(3f);
            ReturnToMenu();
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
            SetupHUD(playerHUD1, _field.YourA);
            SetupHUD(playerHUD2, _field.YourB);
            SetupHUD(enemyHUD1,  _field.OppA);
            SetupHUD(enemyHUD2,  _field.OppB);
        }

        private void SetupHUD(EntityHUD hud, PokemonSlot slot)
        {
            if (hud == null || slot == null) return;
            hud.SetupEntity(hud.entityId, slot.SpeciesName, slot.CurrentHp, slot.MaxHp);
            hud.SetLevel(slot.Level);
            hud.SetTypes(slot.Type1, slot.Type2);
            hud.SetStatus(slot.Status);
            hud.SetTeraType(slot.TerType, slot.IsTerastallized);
        }

        private void UpdateSprites()
        {
            var loader = FindObjectOfType<BattleSpriteLoader>();
            if (loader == null) return;
            if (_field.YourA != null) loader.LoadSpriteForSlot("Player_Lead_Slot", "Player1_HUD", _field.YourA.SpeciesName.ToLower(), true);
            if (_field.YourB != null) loader.LoadSpriteForSlot("Player_Sub2_Slot", "Player2_HUD", _field.YourB.SpeciesName.ToLower(), true);
            if (_field.OppA  != null) loader.LoadSpriteForSlot("Enemy_Lead_Slot",  "Enemy1_HUD",  _field.OppA.SpeciesName.ToLower(), false);
            if (_field.OppB  != null) loader.LoadSpriteForSlot("Enemy_Sub2_Slot",  "Enemy2_HUD",  _field.OppB.SpeciesName.ToLower(), false);
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
                    skillPanel.SetMove(i, m.Name ?? "???", m.Type ?? "normal", m.Category ?? "Physical", m.CurrentPp, m.MaxPp);
                } else {
                    skillPanel.SetMove(i, "---", "normal", "Physical", 0, 0);
                }
            }
        }

        // Player chon 1 chieu thuc -> hien target selection
        private void OnMoveSelected(int moveIndex)
        {
            _pendingMoveSlot = moveIndex;
            Debug.Log($"[Battle] Move {moveIndex} selected, showing targets");

            // Hien target selection: dung lai SkillPanel voi ten muc tieu
            if (skillPanel == null) return;

            string oppA = (_field.OppA != null && !_field.OppA.IsFainted) ? _field.OppA.SpeciesName : "---";
            string oppB = (_field.OppB != null && !_field.OppB.IsFainted) ? _field.OppB.SpeciesName : "---";
            string myA  = (_field.YourA != null && !_field.YourA.IsFainted) ? _field.YourA.SpeciesName : "---";
            string myB  = (_field.YourB != null && !_field.YourB.IsFainted) ? _field.YourB.SpeciesName : "---";

            skillPanel.SetTargetLabels(oppA, oppB, myA, myB);
            uiManager?.SwitchPanel(BattlePanelType.Skill);
        }

        // Player chon muc tieu -> gui len server
        private void OnTargetSelected(int targetSlot)
        {
            if (_pendingMoveSlot < 0) return;
            Debug.Log($"[Battle] Target {targetSlot} selected for move {_pendingMoveSlot}");

            bool useTera = (skillPanel != null && skillPanel.IsTeraActive);
            SubmitAction("Move", _pendingMoveSlot, null, targetSlot, useTera);
            _pendingMoveSlot = -1;
            if (skillPanel != null) skillPanel.ResetTeraToggle();
        }

        private void OnVoluntarySwitchRequested(int srcSlot)
        {
            _currentSrcSlot = srcSlot;
            var data = new PartyPanelData {
                Pokemon = GetCurrentPartyData(),
                AvailableIdxs = new int[] { 0, 1, 2, 3, 4, 5 }, // All non-active non-fainted
                IsForcedSwitch = false
            };
            BattleEvents.OnPartyPanelOpen?.Invoke(data);
            uiManager?.SwitchPanel(BattlePanelType.Party);
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
                SubmitAction("Switch", null, partyIndex, 0, false);
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
            uiManager?.SwitchPanel(BattlePanelType.None);
            if (r.TypedEvents != null) {
                foreach (var ev in r.TypedEvents) {
                    // Trigger Visual Effects
                    ProcessEventVisuals(ev);

                    BattleEvents.OnPrintDialog?.Invoke(ev.Message ?? "...", false);
                    yield return new WaitForSeconds(1.2f);
                }
            }
            if (r.State == "Ended") {
                bool won = false; // TODO: check WinnerPlayerId
                BattleEvents.OnBattleResult?.Invoke(won, r.WinnerPlayerId ?? "");
                yield return new WaitForSeconds(2f);
                ReturnToMenu();
            } else {
                // Yeu cau server gui lai trang thai moi nhat
                var hub = SignalRClient.Instance.Battle;
                if (hub != null && hub.State == HubConnectionState.Connected)
                    yield return hub.InvokeAsync("RequestCurrentState", _battleId);
            }
        }

        private void ProcessEventVisuals(EventDto ev)
        {
            var eff = BattleEffectManager.Instance;
            if (eff == null) return;

            // 1. Move Animation
            if (!string.IsNullOrEmpty(ev.MoveName))
            {
                string targetSlot = GetSlotNameByPokemonName(ev.TargetName);
                if (!string.IsNullOrEmpty(targetSlot))
                {
                    eff.PlayMoveEffect(ev.MoveName, targetSlot);
                }
            }

            // 2. Hit / Damage Effect (Shake)
            if (ev.Damage > 0 || ev.EventType == "Damage")
            {
                string targetSlot = GetSlotNameByPokemonName(ev.TargetName ?? ev.PokemonName);
                if (!string.IsNullOrEmpty(targetSlot))
                {
                    eff.PlayHitEffect(targetSlot);
                    
                    // Trigger health bar update immediately for visual sync
                    BattleEvents.OnHealthChanged?.Invoke(targetSlot, ev.HpAfter, 100); // MaxHP arbitrary here, HUD handles pct
                }
            }

            // 3. Status Infliction
            if (ev.EventType == "StatusInflictedEvent")
            {
                string targetSlot = GetSlotNameByPokemonName(ev.PokemonName);
                if (!string.IsNullOrEmpty(targetSlot))
                {
                    Color statusColor = Color.white;
                    if (ev.Status.Contains("Burn")) statusColor = Color.red;
                    else if (ev.Status.Contains("Poison") || ev.Status.Contains("Toxic")) statusColor = Color.magenta;
                    else if (ev.Status.Contains("Paralysis")) statusColor = Color.yellow;
                    else if (ev.Status.Contains("Freeze")) statusColor = Color.cyan;
                    else if (ev.Status.Contains("Sleep")) statusColor = Color.gray;

                    eff.PlayStatusFlash(targetSlot, statusColor);
                }
            }

            // 4. Stat Changes
            if (ev.EventType == "StatChangeEvent")
            {
                string targetSlot = GetSlotNameByPokemonName(ev.PokemonName);
                if (!string.IsNullOrEmpty(targetSlot))
                {
                    // Flash green for raise, blue for lower
                    Color statColor = ev.Stages > 0 ? Color.green : Color.blue;
                    eff.PlayStatusFlash(targetSlot, statColor);
                }
            }
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
