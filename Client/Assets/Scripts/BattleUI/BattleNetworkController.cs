using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using Game.Battle.UI;
using Game.Network;

namespace Game.Battle.Logic
{
    public class BattleNetworkController : MonoBehaviour
    {
        [Header("Liên kết UI - Phe Ta")]
        public EntityHUD playerHUD1;
        public EntityHUD playerHUD2;

        [Header("Liên kết UI - Phe Địch")]
        public EntityHUD enemyHUD1;
        public EntityHUD enemyHUD2;

        public BattleSkillPanel skillPanel;

        private string _battleId;
        private string _myPlayerId;
        private BattleSessionDto _currentBattle;

        // Quản lý việc chọn chiêu cho con nào
        private int _currentSourceIndexToChoose = 0; 

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

        private async void Start()
        {
            _battleId = MatchmakingManager.CurrentBattleId;
            _myPlayerId = PlayerPrefs.GetString("player_id", "");

            if (string.IsNullOrEmpty(_battleId))
            {
                Debug.LogWarning("[Battle] Không có BattleID, vui lòng chạy từ Menu để có dữ liệu thật.");
                return;
            }

            await InitializeBattle();
        }

        private async System.Threading.Tasks.Task InitializeBattle()
        {
            // Đợi kết nối SignalR sẵn sàng
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
                Debug.Log($"[Battle] Đã tham gia trận đấu 2v2: {_battleId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Battle] Lỗi khi JoinBattle: {ex.Message}");
            }
        }

        private void OnBattleSync(BattleSessionDto battle)
        {
            _currentBattle = battle;
            UpdateUIFromBattle(battle);
        }

        private void UpdateUIFromBattle(BattleSessionDto battle)
        {
            bool isP1 = battle.Player1Id == _myPlayerId;
            var myTeam = isP1 ? battle.Team1 : battle.Team2;
            var oppTeam = isP1 ? battle.Team2 : battle.Team1;

            // Slot A Phe Ta
            int myIdxA = isP1 ? battle.ActiveIndex1 : battle.ActiveIndex2;
            UpdateEntitySlot(myTeam, myIdxA, playerHUD1, "Player_Lead_Slot", "Player1_HUD", true);

            // Slot B Phe Ta
            int myIdxB = isP1 ? battle.ActiveIndex1b : battle.ActiveIndex2b;
            UpdateEntitySlot(myTeam, myIdxB, playerHUD2, "Player_Sub2_Slot", "Player2_HUD", true);

            // Slot A Phe Địch
            int oppIdxA = isP1 ? battle.ActiveIndex2 : battle.ActiveIndex1;
            UpdateEntitySlot(oppTeam, oppIdxA, enemyHUD1, "Enemy1_Slot", "Enemy1_HUD", false);

            // Slot B Phe Địch
            int oppIdxB = isP1 ? battle.ActiveIndex2b : battle.ActiveIndex1b;
            UpdateEntitySlot(oppTeam, oppIdxB, enemyHUD2, "Enemy2_Slot", "Enemy2_HUD", false);

            // Tự động cập nhật Moveset cho con đang chọn chiêu
            UpdateSkillPanel(myTeam, _currentSourceIndexToChoose == 0 ? myIdxA : myIdxB);
        }

        private void UpdateEntitySlot(List<PokemonSnapshotDto> team, int idx, EntityHUD hud, string slotName, string spriteHudName, bool isPlayer)
        {
            if (idx >= 0 && idx < team.Count)
            {
                var p = team[idx];
                if (hud != null)
                {
                    hud.gameObject.SetActive(true);
                    hud.SetupEntity(isPlayer ? "Player" : "Enemy", p.Nickname, p.CurrentHp, p.MaxHp);
                }
                LoadSprite(p.SpeciesId, slotName, spriteHudName, isPlayer);
            }
            else if (hud != null)
            {
                hud.gameObject.SetActive(false);
            }
        }

        private void UpdateSkillPanel(List<PokemonSnapshotDto> myTeam, int activeIdx)
        {
            if (activeIdx < 0 || activeIdx >= myTeam.Count || skillPanel == null) return;
            var p = myTeam[activeIdx];
            if (p.Moves == null) return;

            for (int i = 0; i < p.Moves.Count && i < skillPanel.skillButtons.Length; i++)
            {
                var btnText = skillPanel.skillButtons[i].GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (btnText != null) btnText.text = p.Moves[i].MoveName;
            }
        }

        private void LoadSprite(int speciesId, string slotName, string hudName, bool isPlayer)
        {
            BattleSpriteLoader loader = GetComponent<BattleSpriteLoader>();
            if (loader == null) loader = gameObject.AddComponent<BattleSpriteLoader>();
            loader.LoadSpriteForSlot(slotName, hudName, speciesId, isPlayer);
        }

        private void OnSkillSelectedFromUI(int moveSlot)
        {
            // Trong đấu đôi, tạm thời mình cho mặc định đánh vào Slot 0 của đối thủ 
            // Bạn có thể mở rộng UI để cho phép chọn mục tiêu sau
            SubmitAction(moveSlot, _currentSourceIndexToChoose, 0);

            // Sau khi con 0 chọn xong, nếu con 1 còn sống thì cho con 1 chọn
            if (_currentSourceIndexToChoose == 0)
            {
                _currentSourceIndexToChoose = 1;
                UpdateUIFromBattle(_currentBattle);
                BattleEvents.OnPlayerTurnStart?.Invoke(); // Mở lại bảng chiêu cho con thứ 2
            }
            else
            {
                // Cả 2 đã chọn xong
                _currentSourceIndexToChoose = 0;
                BattleEvents.OnPrintDialog?.Invoke("Đang chờ đối phương...", false);
            }
        }

        private async void SubmitAction(int moveSlot, int sourceIndex, int targetSlot)
        {
            if (SignalRClient.Instance != null && !string.IsNullOrEmpty(_battleId))
            {
                await SignalRClient.Instance.Battle.InvokeAsync("ChooseMove", _battleId, moveSlot, sourceIndex, targetSlot);
            }
        }

        private void OnTurnResolved(TurnResultDto result)
        {
            _currentSourceIndexToChoose = 0; // Reset lượt chọn
            StartCoroutine(HandleTurnResolution(result));
        }

        private IEnumerator HandleTurnResolution(TurnResultDto result)
        {
            foreach (var msg in result.Log)
            {
                BattleEvents.OnPrintDialog?.Invoke(msg, true);
                yield return new WaitForSeconds(2.0f);
            }

            if (result.UpdatedBattle != null)
            {
                _currentBattle = result.UpdatedBattle;
                UpdateUIFromBattle(_currentBattle);
            }

            BattleEvents.OnPlayerTurnStart?.Invoke();
        }
    }

    // --- DTOs ---
    [Serializable]
    public class BattleSessionDto
    {
        public string BattleId;
        public string Player1Id;
        public string Player2Id;
        public List<PokemonSnapshotDto> Team1;
        public List<PokemonSnapshotDto> Team2;
        public int ActiveIndex1;
        public int ActiveIndex1b;
        public int ActiveIndex2;
        public int ActiveIndex2b;
        public int TurnNumber;
    }

    [Serializable]
    public class PokemonSnapshotDto
    {
        public string InstanceId;
        public int SpeciesId;
        public string Nickname;
        public int CurrentHp;
        public int MaxHp;
        public List<MoveDto> Moves;
    }

    [Serializable]
    public class MoveDto
    {
        public int MoveId;
        public string MoveName;
    }

    [Serializable]
    public class TurnResultDto
    {
        public List<string> Log;
        public BattleSessionDto UpdatedBattle;
    }
}
