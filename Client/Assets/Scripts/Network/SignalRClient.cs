using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR.Client;
using UnityEngine;

namespace Game.Network
{
    public class SignalRClient : MonoBehaviour
    {
        public static SignalRClient Instance { get; private set; }
        public string PlayerId => PlayerPrefs.GetString("player_id", "");

        [Header("Cáº¥u hÃ¬nh Server")]
        public string serverUrl = "https://lt-mcb-nt106q22antt-production-cc69.up.railway.app";

        private HubConnection _matchmakingHub;
        private HubConnection _battleHub;
        private HubConnection _chatHub;

        public HubConnection Matchmaking => _matchmakingHub;
        public HubConnection Battle => _battleHub;
        public HubConnection Chat => _chatHub;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent == null) DontDestroyOnLoad(gameObject);
                
                // KHÃ”NG khá»Ÿi táº¡o Hub á»Ÿ Ä‘Ã¢y ná»¯a, Ä‘á»£i Ä‘áº¿n khi ConnectAsync má»›i khá»Ÿi táº¡o Ä‘á»ƒ cÃ³ Token
            }
            else if (Instance != this)
            {
                Destroy(this);
            }
        }

        public async Task ConnectAsync()
        {
            // LuÃ´n láº¥y token má»›i nháº¥t tá»« PlayerPrefs
            string token = PlayerPrefs.GetString("jwt_token", "");
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[Network] KhÃ´ng tÃ¬m tháº¥y JWT Token. Äang cháº¡y á»Ÿ cháº¿ Ä‘á»™ Guest.");
            }

            // Chá»‰ khá»Ÿi táº¡o náº¿u chÆ°a cÃ³ hoáº·c Ä‘Ã£ bá»‹ há»§y
            if (_matchmakingHub == null) _matchmakingHub = CreateConnection("/hubs/matchmaking", token);
            if (_battleHub == null)      _battleHub = CreateConnection("/hubs/battle", token);
            if (_chatHub == null)        _chatHub = CreateConnection("/hubs/chat", token);

            try
            {
                List<Task> connectTasks = new List<Task>();
                if (_matchmakingHub.State == HubConnectionState.Disconnected) connectTasks.Add(_matchmakingHub.StartAsync());
                if (_battleHub.State == HubConnectionState.Disconnected)      connectTasks.Add(_battleHub.StartAsync());
                if (_chatHub.State == HubConnectionState.Disconnected)        connectTasks.Add(_chatHub.StartAsync());

                if (connectTasks.Count > 0)
                {
                    await Task.WhenAll(connectTasks);
                    Debug.Log("[Network] ÄÃ£ káº¿t ná»‘i/tÃ¡i káº¿t ná»‘i SignalR thÃ nh cÃ´ng.");
                }
                else
                {
                    Debug.Log("[Network] SignalR Ä‘Ã£ á»Ÿ tráº¡ng thÃ¡i káº¿t ná»‘i.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Network] Lá»—i káº¿t ná»‘i SignalR: {ex.Message}");
            }
        }

        private HubConnection CreateConnection(string hubPath, string token)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl(serverUrl + hubPath, options =>
                {
                    // Quan trá»ng: GÃ¡n token vÃ o Header cho má»—i láº§n káº¿t ná»‘i
                    options.AccessTokenProvider = () => Task.FromResult(token);
                })
                .WithAutomaticReconnect()
                .Build();

            if (hubPath == "/hubs/battle")
            {
                connection.On<VPChangedPayload>("VPChanged", payload =>
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        global::VPManager.Instance?.ApplyServerBalance(payload.Vp);
                        Debug.Log($"[VP] Server updated balance: {payload.Vp} ({payload.Delta:+#;-#;0}, {payload.Reason})");
                    });
                });
            }

            return connection;
        }

        public async Task DisconnectAsync()
        {
            if (_matchmakingHub != null) await _matchmakingHub.StopAsync();
            if (_battleHub != null) await _battleHub.StopAsync();
            if (_chatHub != null) await _chatHub.StopAsync();
        }

        [Serializable]
        public class VPChangedPayload
        {
            public int Vp { get; set; }
            public int Delta { get; set; }
            public string Reason { get; set; }
        }
    }
}

