using UnityEngine;
using UnityEngine.SceneManagement;
using Microsoft.AspNetCore.SignalR.Client;
using System;

namespace Game.Network
{
    public class MatchmakingManager : MonoBehaviour
    {
        public static string CurrentBattleId { get; private set; }
        private bool _shouldLoadBattle = false;

        public static event Action<int> OnCountdownTick;

        /// <summary>Xoá battle ID cũ — gọi trước khi tạo trận mới để tránh BNC đọc nhầm.</summary>
        public static void ResetBattleId() => CurrentBattleId = null;

        private async void Start()
        {
            // Tự động kết nối nếu chưa kết nối
            if (SignalRClient.Instance != null)
            {
                await SignalRClient.Instance.ConnectAsync();
            }

            if (SignalRClient.Instance != null && SignalRClient.Instance.Matchmaking != null)
            {
                var hub = SignalRClient.Instance.Matchmaking;
                
                // Tránh đăng ký trùng lặp nếu quay lại scene cũ
                hub.Remove("MatchFound");
                hub.Remove("SearchStarted");
                hub.Remove("SearchTick");

                hub.On<BattleStartedDto>("MatchFound", OnMatchFound);
                hub.On<SearchStartedDto>("SearchStarted", OnSearchStarted);
                hub.On<SearchTickDto>("SearchTick", dto => {
                    Debug.Log($"[Matchmaking] Tick: {dto.SecondsLeft}s left");
                    OnCountdownTick?.Invoke(dto.SecondsLeft);
                });
                
                hub.On<string>("Debug", msg => Debug.Log(msg));
                hub.On<string>("Error", msg => Debug.LogError(msg));
                
                // Tự động Join Lobby ngay khi vào Menu
                await hub.InvokeAsync("JoinLobby");
            }
        }

        public async void StartSearching()
        {
            if (SignalRClient.Instance != null)
                await SignalRClient.Instance.Matchmaking.InvokeAsync("FindMatch");
        }

        /// <summary>
        /// Tạo bot battle ngay lập tức (không chờ 30s).
        /// Gọi từ nút "Demo / Fight Bot" trên UI menu.
        /// </summary>
        public async void FightBotNow()
        {
            if (SignalRClient.Instance != null)
                await SignalRClient.Instance.Matchmaking.InvokeAsync("FightBot");
        }

        private void Update()
        {
            if (_shouldLoadBattle)
            {
                _shouldLoadBattle = false;
                SceneManager.LoadScene("Battle scene");
            }
        }

        private void OnMatchFound(BattleStartedDto data)
        {
            Debug.Log($"[Matchmaking] Tim thay tran! BattleId: {data.BattleId}");
            CurrentBattleId = data.BattleId;
            
            // Đánh dấu cờ để Update() chuyển Scene trên Main Thread, tránh lỗi background thread của SignalR.
            _shouldLoadBattle = true;
        }

        private void OnSearchStarted(SearchStartedDto dto)
        {
            Debug.Log($"[Matchmaking] Tim tran... Bot fallback sau {dto.CountdownSeconds}s");
            OnCountdownTick?.Invoke(dto.CountdownSeconds);
        }
    }

    [Serializable]
    public class SearchStartedDto { public int CountdownSeconds; }

    [Serializable]
    public class SearchTickDto { public int SecondsLeft; }

    [Serializable]
    public class BattleStartedDto
    {
        public string BattleId { get; set; }
        public string Player1Id { get; set; }
        public string Player2Id { get; set; }
        public int TurnNumber { get; set; }
        public string State { get; set; }
    }
}
