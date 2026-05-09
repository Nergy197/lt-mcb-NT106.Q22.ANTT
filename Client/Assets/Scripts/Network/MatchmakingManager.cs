using UnityEngine;
using UnityEngine.SceneManagement;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

namespace Game.Network
{
    public class MatchmakingManager : MonoBehaviour
    {
        public static string CurrentBattleId { get; set; }
        private bool _shouldLoadBattle = false;

        public static event Action<int> OnCountdownTick;

        public static void ResetBattleId() => CurrentBattleId = null;

        private async void Start()
        {
            if (SignalRClient.Instance != null)
            {
                await SignalRClient.Instance.ConnectAsync();
            }

            if (SignalRClient.Instance != null && SignalRClient.Instance.Matchmaking != null)
            {
                var hub = SignalRClient.Instance.Matchmaking;
                
                hub.Remove("MatchFound");
                hub.Remove("SearchStarted");
                hub.Remove("SearchTick");

                // Đăng ký nhận gói tin từ Server
                hub.On<BattleStartedDto>("MatchFound", OnMatchFound);
                hub.On<SearchStartedDto>("SearchStarted", OnSearchStarted);
                hub.On<SearchTickDto>("SearchTick", dto => {
                    OnCountdownTick?.Invoke(dto.SecondsLeft);
                });
                
                hub.On<string>("Debug", msg => Debug.Log("[Server Debug] " + msg));
                hub.On<string>("Error", msg => Debug.LogError("[Server Error] " + msg));
                
                await hub.InvokeAsync("JoinLobby");
            }
        }

        public async void StartSearching()
        {
            if (SignalRClient.Instance != null)
                await SignalRClient.Instance.Matchmaking.InvokeAsync("FindMatch");
        }

        public async void FightBotNow()
        {
            if (SignalRClient.Instance != null)
            {
                try {
                    Debug.Log("[Matchmaking] Requesting FightBot...");
                    // Server trả về BattleId trực tiếp khi gọi FightBot
                    string battleId = await SignalRClient.Instance.Matchmaking.InvokeAsync<string>("FightBot");
                    if (!string.IsNullOrEmpty(battleId))
                    {
                        Debug.Log("[Matchmaking] FightBot Created: " + battleId);
                        CurrentBattleId = battleId;
                        _shouldLoadBattle = true;
                    }
                } catch (Exception ex) {
                    Debug.LogError("[Matchmaking] FightBot Error: " + ex.Message);
                }
            }
        }

        private void Update()
        {
            if (_shouldLoadBattle)
            {
                _shouldLoadBattle = false;
                Debug.Log("[Matchmaking] LOADING BATTLE SCENE...");
                SceneManager.LoadScene("Battle scene");
            }
        }

        private void OnMatchFound(BattleStartedDto data)
        {
            Debug.Log($"[Matchmaking] MATCH FOUND! ID: {data.BattleId}");
            CurrentBattleId = data.BattleId;
            _shouldLoadBattle = true;
        }

        private void OnSearchStarted(SearchStartedDto dto)
        {
            Debug.Log($"[Matchmaking] Search Started. Bot in {dto.CountdownSeconds}s");
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
        public string BattleId;
        public string Player1Id;
        public string Player2Id;
    }
}
