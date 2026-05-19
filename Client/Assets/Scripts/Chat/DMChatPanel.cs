using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Chat
{
    public class DMChatPanel : MonoBehaviour
    {
        public static DMChatPanel Instance { get; private set; }

        [Header("UI - kéo từ scene vào")]
        public ScrollRect     chatScroll;
        public Transform      messageContainer;
        public TMP_InputField inputField;
        public Button         sendButton;
        public TextMeshProUGUI headerLabel;

        [Header("Prefab tin nhắn")]
        public GameObject messagePrefab;

        private string _currentFriendId;
        private string _myPlayerId;
        private bool   _subscribed = false;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _myPlayerId = PlayerPrefs.GetString("player_id", "");
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendClick);
        }

        private void OnEnable()
        {
            StartCoroutine(SubscribeWhenReady());
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        // ── Subscription ─────────────────────────────────────────────────────

        private IEnumerator SubscribeWhenReady()
        {
            float elapsed = 0f;
            while ((ChatHubClient.Instance == null || !ChatHubClient.Instance.IsReady) && elapsed < 10f)
            {
                elapsed += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }
            if (ChatHubClient.Instance == null) yield break;

            if (!_subscribed)
            {
                ChatHubClient.Instance.OnChatHistory   += HandleChatHistory;
                ChatHubClient.Instance.OnDirectMessage += HandleDirectMessage;
                ChatHubClient.Instance.OnError         += HandleError;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed || ChatHubClient.Instance == null) return;
            ChatHubClient.Instance.OnChatHistory   -= HandleChatHistory;
            ChatHubClient.Instance.OnDirectMessage -= HandleDirectMessage;
            ChatHubClient.Instance.OnError         -= HandleError;
            _subscribed = false;
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void OpenChat(string friendId, string friendName)
        {
            _currentFriendId = friendId;
            if (headerLabel != null) headerLabel.text = friendName;
            ClearMessages();
            ChatHubClient.Instance?.LoadDirectHistory(friendId);
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        private void HandleChatHistory(ChatHistoryPayload payload)
        {
            if (payload?.Channel != "dm") return;
            if (payload.OtherPlayerId != _currentFriendId) return;

            ClearMessages();
            foreach (var msg in payload.Messages)
                AppendMessage(msg);
            ScrollToBottom();
        }

        private void HandleDirectMessage(ChatMessageData msg)
        {
            // Bỏ qua tin nhắn của chính mình (đã render local khi send)
            if (msg.SenderId == _myPlayerId) return;
            // Chỉ hiện nếu đúng là từ người đang chat
            if (msg.SenderId != _currentFriendId) return;

            AppendMessage(msg);
            ScrollToBottom();
        }

        private void HandleError(string error)
        {
            Debug.LogWarning($"[DMChat] Lỗi: {error}");
        }

        // ── Gửi tin ──────────────────────────────────────────────────────────

        private void OnSendClick()
        {
            if (string.IsNullOrEmpty(_currentFriendId)) return;
            string text = inputField?.text?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (ChatHubClient.Instance == null) return;

            // Gửi trước — nếu không có kết nối sẽ bắn OnError và return false, không render
            if (!ChatHubClient.Instance.SendDirectMessage(_currentFriendId, text)) return;

            inputField.text = "";
            inputField.ActivateInputField();

            AppendMessage(new ChatMessageData {
                SenderId   = _myPlayerId,
                SenderName = "Tôi",
                Content    = text,
                CreatedAt  = DateTime.UtcNow
            });
            ScrollToBottom();
        }

        // ── Render ───────────────────────────────────────────────────────────

        private void AppendMessage(ChatMessageData msg)
        {
            if (messagePrefab == null || messageContainer == null) return;
            bool isMe = msg.SenderId == _myPlayerId;

            var obj = Instantiate(messagePrefab, messageContainer);
            var ui  = obj.GetComponent<MessageItemUI>();
            if (ui != null) { ui.SetData(msg.SenderName, msg.Content, msg.CreatedAt, isMe); return; }

            // Fallback nếu prefab không có MessageItemUI
            var bubble      = obj.transform.Find("MessageBubble");
            var contentText = bubble != null
                ? bubble.GetComponentInChildren<TMP_Text>()
                : obj.GetComponentInChildren<TMP_Text>();
            if (contentText != null) contentText.text = msg.Content ?? "";

            if (!isMe)
            {
                var nameLabel = obj.transform.Find("SenderName_Text")?.GetComponent<TMP_Text>();
                if (nameLabel != null) nameLabel.text = msg.SenderName ?? "";
            }
        }

        private void ClearMessages()
        {
            if (messageContainer == null) return;
            while (messageContainer.childCount > 0)
                DestroyImmediate(messageContainer.GetChild(0).gameObject);
        }

        private void ScrollToBottom() => StartCoroutine(ScrollToBottomNextFrame());

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (messageContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(messageContainer.GetComponent<RectTransform>());
            if (chatScroll != null)
                chatScroll.verticalNormalizedPosition = 0f;
        }
    }
}
