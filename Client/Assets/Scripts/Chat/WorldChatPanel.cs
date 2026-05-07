using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Chat
{
    /// <summary>
    /// Gắn vào GameObject WorldPopup trong scene.
    /// </summary>
    public class WorldChatPanel : MonoBehaviour
    {
        [Header("UI - kéo từ scene vào")]
        public ScrollRect     chatScroll;
        public Transform      messageContainer;
        public TMP_InputField inputField;
        public Button         sendButton;

        [Header("Prefab tin nhắn")]
        public GameObject messagePrefab;

        private string _myPlayerId;

        private void Start()
        {
            _myPlayerId = PlayerPrefs.GetString("player_id", "");

            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendClick);

            if (ChatHubClient.Instance != null)
            {
                ChatHubClient.Instance.OnChatHistory += HandleChatHistory;
                ChatHubClient.Instance.OnWorldMessage += HandleWorldMessage;
            }
        }

        private void OnEnable()
        {
            ChatHubClient.Instance?.LoadWorldHistory();
        }

        private void OnDestroy()
        {
            if (ChatHubClient.Instance != null)
            {
                ChatHubClient.Instance.OnChatHistory  -= HandleChatHistory;
                ChatHubClient.Instance.OnWorldMessage -= HandleWorldMessage;
            }
        }

        private void HandleChatHistory(ChatHistoryPayload payload)
        {
            if (payload.Channel != "world") return;

            ClearMessages();
            foreach (var msg in payload.Messages)
                AppendMessage(msg);

            ScrollToBottom();
        }

        private void HandleWorldMessage(ChatMessageData msg)
        {
            AppendMessage(msg);
            ScrollToBottom();
        }

        private void OnSendClick()
        {
            string text = inputField?.text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            ChatHubClient.Instance?.SendWorldMessage(text);
            inputField.text = "";
        }

        private void AppendMessage(ChatMessageData msg)
        {
            if (messagePrefab == null || messageContainer == null) return;

            var obj = Instantiate(messagePrefab, messageContainer);
            var ui  = obj.GetComponent<MessageItemUI>();
            ui?.SetData(msg.SenderName, msg.Content, msg.CreatedAt, msg.SenderId == _myPlayerId);
        }

        private void ClearMessages()
        {
            if (messageContainer == null) return;
            foreach (Transform child in messageContainer)
                Destroy(child.gameObject);
        }

        private void ScrollToBottom()
        {
            if (chatScroll == null) return;
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f;
        }
    }
}
