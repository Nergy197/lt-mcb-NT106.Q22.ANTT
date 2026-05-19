using System;
using System.Collections;
using Microsoft.AspNetCore.SignalR.Client;
using UnityEngine;

namespace Game.Chat
{
    /// <summary>
    /// Bridges SignalRClient.Chat to the Game.Chat event system.
    /// Does NOT create its own hub connection — reuses the shared chat hub.
    /// </summary>
    public class ChatHubClient : MonoBehaviour
    {
        public static ChatHubClient Instance { get; private set; }

        public event Action<ChatHistoryPayload> OnChatHistory;
        public event Action<ChatMessageData>    OnWorldMessage;
        public event Action<ChatMessageData>    OnDirectMessage;
        public event Action<string>             OnError;
        public event Action                     OnConnected;

        public bool IsReady => _isListening;

        private bool _isListening = false;
        private HubConnection _registeredHub = null;
        private IDisposable _historyToken;
        private IDisposable _worldToken;
        private IDisposable _directToken;
        private IDisposable _errorToken;
        // Giữ ref để gỡ Reconnected handler khi đổi connection
        private Func<string, System.Threading.Tasks.Task> _reconnectedHandler;

        private void Awake()
        {
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else { Destroy(gameObject); }
        }

        private void Start() => StartCoroutine(KeepCheckingConnection());

        private void OnDestroy()
        {
            if (_registeredHub != null && _reconnectedHandler != null)
                _registeredHub.Reconnected -= _reconnectedHandler;
            _historyToken?.Dispose();
            _worldToken?.Dispose();
            _directToken?.Dispose();
            _errorToken?.Dispose();
        }

        IEnumerator KeepCheckingConnection()
        {
            while (true)
            {
                var chat = Game.Network.SignalRClient.Instance?.Chat;
                if (chat != null && chat.State == HubConnectionState.Connected
                    && (chat != _registeredHub || !_isListening))
                {
                    _isListening = false;
                    SetupHandlers(chat);
                    _registeredHub = chat;
                }
                yield return new WaitForSeconds(2f);
            }
        }

        private void SetupHandlers(HubConnection chat)
        {
            // Gỡ handler cũ nếu đổi connection object
            if (_registeredHub != null && _reconnectedHandler != null)
                _registeredHub.Reconnected -= _reconnectedHandler;

            _historyToken?.Dispose();
            _worldToken?.Dispose();
            _directToken?.Dispose();
            _errorToken?.Dispose();

            _historyToken = chat.On<ChatHistoryPayload>("ChatHistory", payload =>
                Dispatch(() => OnChatHistory?.Invoke(payload)));

            _worldToken = chat.On<ChatMessageData>("WorldMessage", msg =>
                Dispatch(() => OnWorldMessage?.Invoke(msg)));

            _directToken = chat.On<ChatMessageData>("DirectMessage", msg =>
                Dispatch(() => OnDirectMessage?.Invoke(msg)));

            _errorToken = chat.On<string>("Error", err =>
                Dispatch(() => OnError?.Invoke(err)));

            // Khi WithAutomaticReconnect() kết nối lại thành công → reload history
            _reconnectedHandler = _ => {
                Debug.Log("[ChatHubClient] Auto-reconnect thành công, reload history.");
                Dispatch(() => OnConnected?.Invoke());
                return System.Threading.Tasks.Task.CompletedTask;
            };
            chat.Reconnected += _reconnectedHandler;

            _isListening = true;
            Debug.Log("[ChatHubClient] Handlers đã đăng ký lên SignalRClient.Chat.");
            Dispatch(() => OnConnected?.Invoke());
        }

        public void LoadWorldHistory()
        {
            var chat = Game.Network.SignalRClient.Instance?.Chat;
            if (chat?.State == HubConnectionState.Connected)
                chat.InvokeAsync("LoadWorldHistory")
                    .ContinueWith(t => { if (t.IsFaulted) LogSendError(t); });
        }

        public void LoadDirectHistory(string otherPlayerId)
        {
            var chat = Game.Network.SignalRClient.Instance?.Chat;
            if (chat?.State == HubConnectionState.Connected)
                chat.InvokeAsync("LoadDirectHistory", otherPlayerId)
                    .ContinueWith(t => { if (t.IsFaulted) LogSendError(t); });
        }

        public bool SendWorldMessage(string content)
        {
            var chat = Game.Network.SignalRClient.Instance?.Chat;
            if (chat == null || chat.State != HubConnectionState.Connected)
            {
                Dispatch(() => OnError?.Invoke("Mất kết nối — tin nhắn chưa được gửi."));
                return false;
            }
            chat.InvokeAsync("SendWorldMessage", content)
                .ContinueWith(t => {
                    if (t.IsFaulted)
                        Dispatch(() => OnError?.Invoke("Gửi tin thất bại — vui lòng thử lại."));
                });
            return true;
        }

        public bool SendDirectMessage(string receiverId, string content)
        {
            var chat = Game.Network.SignalRClient.Instance?.Chat;
            if (chat == null || chat.State != HubConnectionState.Connected)
            {
                Dispatch(() => OnError?.Invoke("Mất kết nối — tin nhắn chưa được gửi."));
                return false;
            }
            chat.InvokeAsync("SendDirectMessage", receiverId, content)
                .ContinueWith(t => {
                    if (t.IsFaulted)
                        Dispatch(() => OnError?.Invoke("Gửi tin thất bại — vui lòng thử lại."));
                });
            return true;
        }

        private void LogSendError(System.Threading.Tasks.Task t) =>
            Debug.LogWarning("[ChatHubClient] InvokeAsync failed: " + t.Exception?.GetBaseException().Message);

        private void Dispatch(Action action) =>
            UnityMainThreadDispatcher.Instance()?.Enqueue(action);
    }
}
