using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using Game.Battle.UI;
using Game.Network;
using System.Linq;

namespace Game.Battle.Logic
{
    public class BattleNetworkController : MonoBehaviour
    {
        [Header("Lien ket UI - Phe Ta")]
        public EntityHUD playerHUD1;
        public EntityHUD playerHUD2;

        [Header("Lien ket UI - Phe Dich")]
        public EntityHUD enemyHUD1;
        public EntityHUD enemyHUD2;

        public BattleSkillPanel skillPanel;

        private string _battleId;
        private string _myPlayerId;
        private BattleSessionDto _currentBattle;

        // Quan ly trang thai chon
        private int _currentSourceIndexToChoose = 0; // 0: con Lead, 1: con Sub
        private int _selectedMoveSlot = -1;
        private bool _isSelectingTarget = false;
        private bool _isWaitingForTurn = false;

        // Thread-safe queue
        private readonly Queue<Action> _mainThreadActions = new Queue<Action>();
        private readonly object _lockObj = new object();

        private void OnEnable()
        {
            BattleEvents.OnPlayerUseSkill += OnSkillSelectedFromUI;
        }

        private void OnDisable()
        {
            BattleEvents.OnPlayerUseSkill -= OnSkillSelectedFromUI;
            if (SignalRClient.Instance != null && SignalRClient.Instance.Battle != null)
            {
                SignalRClient.Instance.Battle.Remove("BattleSync");
                SignalRClient.Instance.Battle.Remove("TurnResolved");
            }
        }

        private void Update()
        {
            lock (_lockObj)
            {
                while (_mainThreadActions.Count > 0)
                {
                    var action = _mainThreadActions.Dequeue();
                    action?.Invoke();
                }
            }
        }

        private void EnqueueMainThread(Action action)
        {
            lock (_lockObj)
            {
                _mainThreadActions.Enqueue(action);
            }
        }

        private async void Start()
        {
            _battleId = MatchmakingManager.CurrentBattleId;
            _myPlayerId = PlayerPrefs.GetString("player_id", "");

            if (string.IsNullOrEmpty(_battleId)) return;

            await InitializeBattle();
        }

        private async System.Threading.Tasks.Task InitializeBattle()
        {
            while (SignalRClient.Instance == null || SignalRClient.Instance.Battle.State != HubConnectionState.Connected)
            {
                await System.Threading.Tasks.Task.Yield();
            }

            var battleHub = SignalRClient.Instance.Battle;
            battleHub.On<BattleSessionDto>("BattleSync", OnBattleSync);
            battleHub.On<TurnResultDto>("TurnResolved", OnTurnResolved);

            try
            {
                await battleHub.InvokeAsync("JoinBattle", _battleId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Battle] JoinBattle error: {ex.Message}");
            }
        }

        private void OnBattleSync(BattleSessionDto battle)
        {
            if (battle == null) return;
            _currentBattle = battle;
            EnqueueMainThread(() =>
            {
                UpdateUIFromBattle(battle);
                StartCoroutine(BattleIntro());
            });
        }

        private void OnTurnResolved(TurnResultDto result)
        {
            EnqueueMainThread(() =>
            {
                _currentSourceIndexToChoose = 0;
                _isWaitingForTurn = false;
                _isSelectingTarget = false;
                StartCoroutine(HandleTurnResolution(result));
            });
        }

        private IEnumerator BattleIntro()
        {
            yield return new WaitForSeconds(0.5f);
            if (_currentBattle == null) yield break;

            bool isP1 = _currentBattle.Player1Id == _myPlayerId;
            string oppName = _currentBattle.Player2Id == "BOT_PLAYER" ? "Champion Bot" : "Doi thu";
            BattleEvents.OnPrintDialog?.Invoke($"{oppName} muon thach dau!", true);
            yield return new WaitForSeconds(1.5f);

            StartPlayerTurnSequence();
        }

        private void StartPlayerTurnSequence()
        {
            _currentSourceIndexToChoose = 0;
            StartPlayerTurnPhase();
        }

        private void StartPlayerTurnPhase()
        {
            if (_currentBattle == null) return;
            _isSelectingTarget = false;
            _selectedMoveSlot = -1;

            bool isP1 = _currentBattle.Player1Id == _myPlayerId;
            var myTeam = isP1 ? _currentBattle.Team1 : _currentBattle.Team2;
            int activeIdx = _currentSourceIndexToChoose == 0 ? 
                (isP1 ? _currentBattle.ActiveIndex1 : _currentBattle.ActiveIndex2) :
                (isP1 ? _currentBattle.ActiveIndex1b : _currentBattle.ActiveIndex2b);

            if (myTeam != null && activeIdx >= 0 && activeIdx < myTeam.Count)
            {
                var p = myTeam[activeIdx];
                if (p.CurrentHp <= 0)
                {
                    if (_currentSourceIndexToChoose == 0)
                    {
                        _currentSourceIndexToChoose = 1;
                        StartPlayerTurnPhase();
                    }
                    else
                    {
                        FinishTurnSelection();
                    }
                    return;
                }

                BattleEvents.OnPrintDialog?.Invoke($">> Den luot {p.Nickname}! <<", true);
                UpdateSkillPanel(myTeam, activeIdx);
                BattleEvents.OnPlayerTurnStart?.Invoke();
            }
            else if (_currentSourceIndexToChoose == 0)
            {
                _currentSourceIndexToChoose = 1;
                StartPlayerTurnPhase();
            }
            else
            {
                FinishTurnSelection();
            }
        }

        private void OnSkillSelectedFromUI(int moveSlot)
        {
            if (_isWaitingForTurn || _currentBattle == null) return;

            if (!_isSelectingTarget)
            {
                // Chon chieu xong -> Chuyen sang chon muc tieu
                _selectedMoveSlot = moveSlot;
                _isSelectingTarget = true;
                
                bool isP1 = _currentBattle.Player1Id == _myPlayerId;
                var myTeam = isP1 ? _currentBattle.Team1 : _currentBattle.Team2;
                int activeIdx = _currentSourceIndexToChoose == 0 ? 
                    (isP1 ? _currentBattle.ActiveIndex1 : _currentBattle.ActiveIndex2) :
                    (isP1 ? _currentBattle.ActiveIndex1b : _currentBattle.ActiveIndex2b);
                
                string moveName = myTeam[activeIdx].Moves[moveSlot].MoveName;
                BattleEvents.OnPrintDialog?.Invoke($"{moveName}! Danh vao ai?", true);

                UpdateTargetSelectionButtons();
                BattleEvents.OnPlayerTurnStart?.Invoke(); // Mo lai de bam nut Target
            }
            else
            {
                // moveSlot gio la Target Slot (0 hoặc 1)
                SubmitAction(_selectedMoveSlot, _currentSourceIndexToChoose, moveSlot);
                
                if (_currentSourceIndexToChoose == 0)
                {
                    _currentSourceIndexToChoose = 1;
                    StartPlayerTurnPhase();
                }
                else
                {
                    FinishTurnSelection();
                }
            }
        }

        private void UpdateTargetSelectionButtons()
        {
            if (skillPanel == null || _currentBattle == null) return;
            bool isP1 = _currentBattle.Player1Id == _myPlayerId;
            var oppTeam = isP1 ? _currentBattle.Team2 : _currentBattle.Team1;
            int oppIdxA = isP1 ? _currentBattle.ActiveIndex2 : _currentBattle.ActiveIndex1;
            int oppIdxB = isP1 ? _currentBattle.ActiveIndex2b : _currentBattle.ActiveIndex1b;

            foreach (var btn in skillPanel.skillButtons)
            {
                var t = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (t != null) t.text = "";
            }

            if (oppTeam != null && oppIdxA >= 0 && oppIdxA < oppTeam.Count)
            {
                var t = skillPanel.skillButtons[0].GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (t != null) t.text = $"[1] {oppTeam[oppIdxA].Nickname}";
            }
            if (oppTeam != null && oppIdxB >= 0 && oppIdxB < oppTeam.Count && oppTeam[oppIdxB].CurrentHp > 0)
            {
                var t = skillPanel.skillButtons[1].GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (t != null) t.text = $"[2] {oppTeam[oppIdxB].Nickname}";
            }
        }

        private void FinishTurnSelection()
        {
            _isWaitingForTurn = true;
            _currentSourceIndexToChoose = 0;
            BattleEvents.OnPrintDialog?.Invoke("Dang cho doi phuong...", false);
        }

        private async void SubmitAction(int moveSlot, int sourceIndex, int targetSlot)
        {
            if (SignalRClient.Instance != null && !string.IsNullOrEmpty(_battleId))
            {
                await SignalRClient.Instance.Battle.InvokeAsync("ChooseMove", _battleId, moveSlot, sourceIndex, targetSlot);
            }
        }

        private IEnumerator HandleTurnResolution(TurnResultDto result)
        {
            if (result.Events != null)
            {
                foreach (var msg in result.Events)
                {
                    BattleEvents.OnPrintDialog?.Invoke(msg, true);
                    yield return new WaitForSeconds(1.5f);
                }
            }

            if (_currentBattle != null)
            {
                _currentBattle.ActiveIndex1 = result.ActiveIndex1;
                _currentBattle.ActiveIndex2 = result.ActiveIndex2;
                _currentBattle.TurnNumber = result.NextTurnNumber;
                // De don gian, BattleSync sau luot se tu cap nhat tiep
            }

            yield return new WaitForSeconds(0.5f);

            if (result.State == "Ended" || result.State == "2")
            {
                string msg = result.WinnerPlayerId == _myPlayerId ? "Ban thang!" : "Ban thua...";
                BattleEvents.OnPrintDialog?.Invoke(msg, true);
                yield return new WaitForSeconds(3f);
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu scene");
            }
            else
            {
                StartPlayerTurnSequence();
            }
        }

        private void UpdateUIFromBattle(BattleSessionDto battle)
        {
            if (battle == null) return;
            bool isP1 = battle.Player1Id == _myPlayerId;
            var myTeam = isP1 ? battle.Team1 : battle.Team2;
            var oppTeam = isP1 ? battle.Team2 : battle.Team1;

            UpdateSlot(myTeam, isP1 ? battle.ActiveIndex1 : battle.ActiveIndex2, playerHUD1, "Player_Lead_Slot", true);
            UpdateSlot(myTeam, isP1 ? battle.ActiveIndex1b : battle.ActiveIndex2b, playerHUD2, "Player_Sub2_Slot", true);
            UpdateSlot(oppTeam, isP1 ? battle.ActiveIndex2 : battle.ActiveIndex1, enemyHUD1, "Enemy_Lead_Slot", false);
            UpdateSlot(oppTeam, isP1 ? battle.ActiveIndex2b : battle.ActiveIndex1b, enemyHUD2, "Enemy_Sub2_Slot", false);
        }

        private void UpdateSlot(List<PokemonSnapshotDto> team, int idx, EntityHUD hud, string slot, bool p)
        {
            if (team != null && idx >= 0 && idx < team.Count)
            {
                var pkm = team[idx];
                if (hud != null)
                {
                    hud.gameObject.SetActive(true);
                    hud.SetupEntity(p ? "Player" : "Enemy", pkm.Nickname, pkm.CurrentHp, pkm.MaxHp);
                }
                var loader = GetComponent<BattleSpriteLoader>() ?? gameObject.AddComponent<BattleSpriteLoader>();
                loader.LoadSpriteForSlot(slot, "", pkm.SpeciesName, p);
            }
            else if (hud != null) hud.gameObject.SetActive(false);
        }

        private void UpdateSkillPanel(List<PokemonSnapshotDto> team, int idx)
        {
            if (team == null || idx < 0 || idx >= team.Count || skillPanel == null) return;
            var pkm = team[idx];
            for (int i = 0; i < pkm.Moves.Count && i < skillPanel.skillButtons.Length; i++)
            {
                var t = skillPanel.skillButtons[i].GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (t != null) t.text = pkm.Moves[i].MoveName;
            }
        }
    }

    [Serializable] public class BattleSessionDto {
        public string BattleId { get; set; }
        public string Player1Id { get; set; }
        public string Player2Id { get; set; }
        public List<PokemonSnapshotDto> Team1 { get; set; }
        public List<PokemonSnapshotDto> Team2 { get; set; }
        public int ActiveIndex1 { get; set; }
        public int ActiveIndex1b { get; set; }
        public int ActiveIndex2 { get; set; }
        public int ActiveIndex2b { get; set; }
        public int TurnNumber { get; set; }
    }
    [Serializable] public class PokemonSnapshotDto {
        public string SpeciesName { get; set; }
        public string Nickname { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public List<MoveDto> Moves { get; set; }
    }
    [Serializable] public class MoveDto {
        public string MoveName { get; set; }
    }
    [Serializable] public class TurnResultDto {
        public string State { get; set; }
        public string WinnerPlayerId { get; set; }
        public int ActiveIndex1 { get; set; }
        public int ActiveIndex2 { get; set; }
        public int NextTurnNumber { get; set; }
        public List<string> Events { get; set; }
    }
}
