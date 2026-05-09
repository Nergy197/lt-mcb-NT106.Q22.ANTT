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
                
                // KHÔNG khởi tạo Hub ở đây nữa, đợi đến khi ConnectAsync mới khởi tạo để có Token
            }
            else if (Instance != this)
            {
                Destroy(this);
            }
        }

        public async Task ConnectAsync()
        {
            // Luôn lấy token mới nhất từ PlayerPrefs
            string token = PlayerPrefs.GetString("jwt_token", "");
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[Network] Không tìm thấy JWT Token. Đang chạy ở chế độ Guest.");
            }

            // Khởi tạo Hub với Token hiện tại
            _matchmakingHub = CreateConnection("/hubs/matchmaking", token);
            _battleHub = CreateConnection("/hubs/battle", token);
            _chatHub = CreateConnection("/hubs/chat", token);

            try
            {
                // Dùng Task.WhenAll để kết nối nhanh hơn
                await Task.WhenAll(
                    _matchmakingHub.StartAsync(),
                    _battleHub.StartAsync(),
                    _chatHub.StartAsync()
                );

                Debug.Log("[Network] Đã kết nối SignalR thành công với Token: " + (string.IsNullOrEmpty(token) ? "NULL" : "OK"));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Network] Lỗi kết nối SignalR: {ex.Message}");
            }
        }

        private HubConnection CreateConnection(string hubPath, string token)
        {
            return new HubConnectionBuilder()
                .WithUrl(serverUrl + hubPath, options =>
                {
                    // Quan trọng: Gán token vào Header cho mỗi lần kết nối
                    options.AccessTokenProvider = () => Task.FromResult(token);
                })
                .WithAutomaticReconnect()
                .Build();
        }

        public async Task DisconnectAsync()
        {
            if (_matchmakingHub != null) await _matchmakingHub.StopAsync();
            if (_battleHub != null) await _battleHub.StopAsync();
            if (_chatHub != null) await _chatHub.StopAsync();
        }
    }
}
