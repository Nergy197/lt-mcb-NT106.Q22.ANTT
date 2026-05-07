using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using UnityEngine;

namespace Game.Chat
{
    public class ChatHubClient : MonoBehaviour
    {
        public static ChatHubClient Instance { get; private set; }

        [Header("Server")]
        public string serverUrl = "http://127.0.0.1:2567";

        private HubConnection _hub;
        private readonly Queue<Action> _mainThread = new();

        public event Action<ChatHistoryPayload> OnChatHistory;
        public event Action<ChatMessageData>    OnWorldMessage;
        public event Action<ChatMessageData>    OnDirectMessage;
        public event Action<string>             OnError;

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
                return;
            }
        }

        private async void Start()
        {
            await ConnectAsync();
        }

        private void Update()
        {
            lock (_mainThread)
                while (_mainThread.Count > 0)
                    _mainThread.Dequeue()?.Invoke();
        }

        public async Task ConnectAsync()
        {
            string token = PlayerPrefs.GetString("jwt_token", "");
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[ChatHub] Chưa có JWT token, bỏ qua kết nối.");
                return;
            }

            _hub = new HubConnectionBuilder()
                .WithUrl(serverUrl + "/hubs/chat", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(token);
                })
                .WithAutomaticReconnect()
                .Build();

            _hub.Remove("ChatHistory");
            _hub.Remove("WorldMessage");
            _hub.Remove("DirectMessage");
            _hub.Remove("Error");

            _hub.On<ChatHistoryPayload>("ChatHistory", payload =>
                Dispatch(() => OnChatHistory?.Invoke(payload)));

            _hub.On<ChatMessageData>("WorldMessage", msg =>
                Dispatch(() => OnWorldMessage?.Invoke(msg)));

            _hub.On<ChatMessageData>("DirectMessage", msg =>
                Dispatch(() => OnDirectMessage?.Invoke(msg)));

            _hub.On<string>("Error", err =>
                Dispatch(() => OnError?.Invoke(err)));

            try
            {
                await _hub.StartAsync();
                Debug.Log("[ChatHub] Kết nối thành công.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatHub] Lỗi kết nối: {ex.Message}");
            }
        }

        public async void LoadWorldHistory()
        {
            if (_hub?.State == HubConnectionState.Connected)
                await _hub.InvokeAsync("LoadWorldHistory");
        }

        public async void LoadDirectHistory(string otherPlayerId)
        {
            if (_hub?.State == HubConnectionState.Connected)
                await _hub.InvokeAsync("LoadDirectHistory", otherPlayerId);
        }

        public async void SendWorldMessage(string content)
        {
            if (_hub?.State == HubConnectionState.Connected)
                await _hub.InvokeAsync("SendWorldMessage", content);
        }

        public async void SendDirectMessage(string receiverId, string content)
        {
            if (_hub?.State == HubConnectionState.Connected)
                await _hub.InvokeAsync("SendDirectMessage", receiverId, content);
        }

        private void Dispatch(Action action)
        {
            lock (_mainThread) _mainThread.Enqueue(action);
        }

        private async void OnDestroy()
        {
            if (_hub != null)
                await _hub.StopAsync();
        }
    }
}
