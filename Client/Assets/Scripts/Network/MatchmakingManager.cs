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

                hub.On<BattleStartedDto>("MatchFound", OnMatchFound);
                hub.On<string>("SearchStarted", (msg) => Debug.Log($"[Matchmaking] {msg}"));
                
                // Tự động Join Lobby ngay khi vào Menu
                await hub.InvokeAsync("JoinLobby");
            }
        }

        public async void StartSearching()
        {
            if (SignalRClient.Instance != null)
            {
                await SignalRClient.Instance.Matchmaking.InvokeAsync("FindMatch");
            }
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
    }

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
