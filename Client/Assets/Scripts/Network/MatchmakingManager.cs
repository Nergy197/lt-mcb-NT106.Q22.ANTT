using UnityEngine;
using UnityEngine.SceneManagement;
using Microsoft.AspNetCore.SignalR.Client;
using System;

namespace Game.Network
{
    public class MatchmakingManager : MonoBehaviour
    {
        public static string CurrentBattleId { get; private set; }

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

        private void OnMatchFound(BattleStartedDto data)
        {
            Debug.Log($"[Matchmaking] Tìm thấy trận! BattleId: {data.BattleId}");
            CurrentBattleId = data.BattleId;
            
            // Chuyển scene sang Battle scene
            SceneManager.LoadScene("Battle scene");
        }
    }

    [Serializable]
    public class BattleStartedDto
    {
        public string BattleId;
        public string Player1Id;
        public string Player2Id;
        public int TurnNumber;
        public string State;
    }
}
