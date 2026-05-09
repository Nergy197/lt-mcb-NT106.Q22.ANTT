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
        private FieldSnapshot _field;
        private bool _isMyTurn = false;
        private int _currentSrcSlot = 0;     // slot dang chon hanh dong (0=A, 1=B)
        private int _pendingMoveSlot = -1;    // move index dang cho chon muc tieu
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
        }

        private void Start()
        {
            _battleId = MatchmakingManager.CurrentBattleId;
            if (string.IsNullOrEmpty(_battleId)) {
                if (demoAutoMatch) StartCoroutine(StartDemoAutoMatchRoutine());
            } else {
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
            if (string.IsNullOrEmpty(_battleId)) yield break;
            var hub = SignalRClient.Instance.Battle;
            while (hub.State != HubConnectionState.Connected) yield return new WaitForSeconds(0.5f);

            SetupHubHandlers(hub);
            Debug.Log("[Battle] Joining Battle Hub: " + _battleId);

            yield return new WaitForSeconds(0.8f);
            yield return hub.InvokeAsync("JoinBattle", _battleId);
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
            }));
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

        private void ReturnToMenu() => UnityEngine.SceneManagement.SceneManager.LoadScene("Menu scene");
        private void Enqueue(Action action) { lock (_mainThreadQueue) _mainThreadQueue.Enqueue(action); }
        private T J<T>(object raw) { try { return JsonConvert.DeserializeObject<T>(raw.ToString()); } catch { return default; } }
    }
}
