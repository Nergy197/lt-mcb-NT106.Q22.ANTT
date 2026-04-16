using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using UnityEngine;

namespace Game.Network
{
    public class SignalRClient : MonoBehaviour
    {
        public static SignalRClient Instance { get; private set; }

        [Header("Cấu hình Server")]
        public string serverUrl = "http://127.0.0.1:2567";

        private HubConnection _matchmakingHub;
        private HubConnection _battleHub;

        public HubConnection Matchmaking => _matchmakingHub;
        public HubConnection Battle => _battleHub;

        private string _token;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitHubs(); // Khởi tạo Hub ngay lập tức
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitHubs()
        {
            _matchmakingHub = CreateConnection("/hubs/matchmaking");
            _battleHub = CreateConnection("/hubs/battle");
        }

        public async Task ConnectAsync()
        {
            _token = PlayerPrefs.GetString("jwt_token", "");
            if (string.IsNullOrEmpty(_token))
            {
                Debug.LogError("[Network] Không tìm thấy JWT Token. Vui lòng login lại.");
                return;
            }

            // Cập nhật lại Connection với Token mới (vì Token thay đổi sau mỗi lần login)
            InitHubs();

            try
            {
                if (_matchmakingHub.State == HubConnectionState.Disconnected)
                    await _matchmakingHub.StartAsync();
                
                if (_battleHub.State == HubConnectionState.Disconnected)
                    await _battleHub.StartAsync();

                Debug.Log("[Network] Đã kết nối SignalR thành công.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Network] Lỗi kết nối SignalR: {ex.Message}");
            }
        }

        private HubConnection CreateConnection(string hubPath)
        {
            return new HubConnectionBuilder()
                .WithUrl(serverUrl + hubPath, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_token);
                })
                .WithAutomaticReconnect()
                .Build();
        }

        public async Task DisconnectAsync()
        {
            if (_matchmakingHub != null) await _matchmakingHub.StopAsync();
            if (_battleHub != null) await _battleHub.StopAsync();
        }
    }
}
