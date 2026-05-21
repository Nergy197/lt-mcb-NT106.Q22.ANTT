using UnityEngine;
using UnityEngine.SceneManagement;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

namespace Game.Network
{
    public class MatchmakingManager : MonoBehaviour
    {
        public static MatchmakingManager Instance { get; private set; }
        public static string CurrentBattleId { get; set; }
        private bool _shouldLoadBattle = false;

        public static event Action<int> OnCountdownTick;

        public static void ResetBattleId() => CurrentBattleId = null;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

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

                hub.On<object>("MatchFound", OnMatchFound);
                hub.On<SearchStartedDto>("SearchStarted", OnSearchStarted);
                hub.On<SearchTickDto>("SearchTick", dto => {
                    OnCountdownTick?.Invoke(dto.secondsLeft);
                });
                hub.On<string>("Debug", msg => Debug.Log("[Server Debug] " + msg));
                hub.On<string>("Error", msg => Debug.LogError("[Server Error] " + msg));

                // Gọi lại JoinLobby mỗi khi hub tự reconnect (ConnectionId mới)
                hub.Reconnected += async _ =>
                {
                    Debug.Log("[Matchmaking] Reconnected — re-joining lobby...");
                    try { await hub.InvokeAsync("JoinLobby"); }
                    catch (Exception ex) { Debug.LogError("[Matchmaking] Re-JoinLobby failed: " + ex.Message); }
                };

                await hub.InvokeAsync("JoinLobby");
            }
        }

        public async void StartSearching()
        {
            var hub = SignalRClient.Instance?.Matchmaking;
            if (hub == null) return;

            try
            {
                // Đảm bảo đã join lobby (xử lý trường hợp reconnect thay đổi ConnectionId)
                await hub.InvokeAsync("JoinLobby");
                await hub.InvokeAsync("FindMatch");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Matchmaking] StartSearching error: " + ex.Message);
            }
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

        private void OnMatchFound(object rawData)
        {
            if (rawData == null)
            {
                Debug.LogError("[Matchmaking] Received NULL rawData in OnMatchFound!");
                return;
            }

            try {
                string json = rawData.ToString();
                Debug.Log("[Matchmaking] !!! RAW MATCH DATA !!!: " + json);
                
                // Use Newtonsoft.Json for robust deserialization (case-insensitive by default)
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<BattleStartedEventDto>(json);

                if (data == null || string.IsNullOrEmpty(data.battleId))
                {
                    Debug.LogError("[Matchmaking] Failed to deserialize MatchFound data or battleId is missing!");
                    return;
                }

                Debug.Log($"[Matchmaking] SUCCESS! BattleId: {data.battleId}, P1: {data.player1Id}, P2: {data.player2Id}");
                CurrentBattleId = data.battleId;
                _shouldLoadBattle = true;
            } catch (Exception ex) {
                Debug.LogError("[Matchmaking] MatchFound Parse Error: " + ex.Message);
            }
        }

        private void OnSearchStarted(SearchStartedDto dto)
        {
            Debug.Log($"[Matchmaking] Search Started. Bot in {dto.countdownSeconds}s");
            OnCountdownTick?.Invoke(dto.countdownSeconds);
        }
    }

    [Serializable]
    public class SearchStartedDto { public int countdownSeconds; }

    [Serializable]
    public class SearchTickDto { public int secondsLeft; }

    [Serializable]
    public class BattleStartedEventDto
    {
        public string battleId;
        public string player1Id;
        public string player2Id;
        public int turnNumber;
        public int turnTimeoutSeconds;
    }
}
