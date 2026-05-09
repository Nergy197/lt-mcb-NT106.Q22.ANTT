using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Chat
{
    /// <summary>
    /// Gắn vào GameObject WorldChat_Panel trong scene.
    /// </summary>
    public class WorldChatPanel : MonoBehaviour
    {
        [Header("UI - kéo từ scene vào")]
        public ScrollRect     chatScroll;
        public Transform      messageContainer;
        public TMP_InputField inputField;
        public Button         sendButton;

        [Header("Prefab tin nhắn")]
        public GameObject myMessagePrefab;
        public GameObject friendMessagePrefab;

        [Header("Giới hạn tin nhắn hiển thị")]
        public int maxMessages = 100;

        private string MyPlayerId => PlayerPrefs.GetString("player_id", "");
        private bool _subscribed = false;

        private void Start()
        {
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendClick);
        }

        private void OnEnable()
        {
            StartCoroutine(SubscribeAndLoadHistory());
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        // ── Subscription ─────────────────────────────────────────────────────

        private IEnumerator SubscribeAndLoadHistory()
        {
            // Chờ tối đa 6s cho ChatHubClient sẵn sàng
            float elapsed = 0f;
            while ((ChatHubClient.Instance == null || !ChatHubClient.Instance.IsReady) && elapsed < 6f)
            {
                elapsed += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            if (ChatHubClient.Instance == null) yield break;

            if (!_subscribed)
            {
                ChatHubClient.Instance.OnChatHistory  += HandleChatHistory;
                ChatHubClient.Instance.OnWorldMessage += HandleWorldMessage;
                _subscribed = true;
            }

            ChatHubClient.Instance.LoadWorldHistory();
        }

        private void Unsubscribe()
        {
            if (!_subscribed || ChatHubClient.Instance == null) return;
            ChatHubClient.Instance.OnChatHistory  -= HandleChatHistory;
            ChatHubClient.Instance.OnWorldMessage -= HandleWorldMessage;
            _subscribed = false;
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        private void HandleChatHistory(ChatHistoryPayload payload)
        {
            if (payload?.Channel != "world") return;

            ClearMessages();
            foreach (var msg in payload.Messages)
                AppendMessage(msg);

            StartCoroutine(ScrollToBottomNextFrame());
        }

        private void HandleWorldMessage(ChatMessageData msg)
        {
            AppendMessage(msg);
            TrimExcessMessages();
            StartCoroutine(ScrollToBottomNextFrame());
        }

        // ── Gửi tin ──────────────────────────────────────────────────────────

        private void OnSendClick()
        {
            string text = inputField?.text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            ChatHubClient.Instance?.SendWorldMessage(text);
            inputField.text = "";
            inputField.ActivateInputField();
        }

        // ── Render ───────────────────────────────────────────────────────────

        private void AppendMessage(ChatMessageData msg)
        {
            if (messageContainer == null) return;
            bool isMe = msg.SenderId == MyPlayerId;
            GameObject prefab = isMe ? myMessagePrefab : friendMessagePrefab;
            if (prefab == null) return;

            var obj = Instantiate(prefab, messageContainer);

            // Thử dùng MessageItemUI nếu prefab có gắn script đó
            var ui = obj.GetComponent<MessageItemUI>();
            if (ui != null) { ui.SetData(msg.SenderName, msg.Content, msg.CreatedAt, isMe); return; }

            // Layout thủ công: MessageBubble + SenderName_Text + Avatar
            var bubble = obj.transform.Find("MessageBubble");
            var contentText = bubble != null
                ? bubble.GetComponentInChildren<TMPro.TMP_Text>()
                : obj.GetComponentInChildren<TMPro.TMP_Text>();
            if (contentText != null) contentText.text = msg.Content ?? "";

            if (isMe) return;

            var nameLabel = obj.transform.Find("SenderName_Text")?.GetComponent<TMPro.TMP_Text>();
            if (nameLabel != null) nameLabel.text = msg.SenderName ?? "";

            var avatarImg = obj.transform.Find("Avatar")?.GetComponent<Image>();
            if (avatarImg != null)
            {
                Sprite cached = FriendListLoader.GetPlayerAvatar(msg.SenderId);
                if (cached != null) avatarImg.sprite = cached;
            }
        }

        private void TrimExcessMessages()
        {
            while (messageContainer != null && messageContainer.childCount > maxMessages)
                DestroyImmediate(messageContainer.GetChild(0).gameObject);
        }

        private void ClearMessages()
        {
            if (messageContainer == null) return;
            while (messageContainer.childCount > 0)
                DestroyImmediate(messageContainer.GetChild(0).gameObject);
        }

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
