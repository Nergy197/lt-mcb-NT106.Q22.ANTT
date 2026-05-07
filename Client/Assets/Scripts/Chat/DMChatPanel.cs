using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Chat
{
    /// <summary>
    /// Gắn vào GameObject MailPopup trong scene.
    /// Kéo các thành phần UI vào Inspector theo hướng dẫn [Header].
    /// </summary>
    public class DMChatPanel : MonoBehaviour
    {
        public static DMChatPanel Instance { get; private set; }

        [Header("UI - kéo từ scene vào")]
        public ScrollRect   chatScroll;
        public Transform    messageContainer; // Content của ScrollRect
        public TMP_InputField inputField;
        public Button       sendButton;
        public TextMeshProUGUI headerLabel;   // Hiện tên bạn đang chat

        [Header("Prefab tin nhắn")]
        public GameObject messagePrefab; // Prefab có script MessageItemUI

        private string _currentFriendId;
        private string _myPlayerId;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _myPlayerId = PlayerPrefs.GetString("player_id", "");

            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendClick);

            if (ChatHubClient.Instance != null)
            {
                ChatHubClient.Instance.OnChatHistory   += HandleChatHistory;
                ChatHubClient.Instance.OnDirectMessage += HandleDirectMessage;
                ChatHubClient.Instance.OnError         += HandleError;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (ChatHubClient.Instance != null)
            {
                ChatHubClient.Instance.OnChatHistory   -= HandleChatHistory;
                ChatHubClient.Instance.OnDirectMessage -= HandleDirectMessage;
                ChatHubClient.Instance.OnError         -= HandleError;
            }
        }

        /// <summary>Gọi từ FriendItemUI khi người dùng click vào 1 bạn.</summary>
        public void OpenChat(string friendId, string friendName)
        {
            _currentFriendId = friendId;

            if (headerLabel != null)
                headerLabel.text = friendName;

            ClearMessages();
            ChatHubClient.Instance?.LoadDirectHistory(friendId);
        }

        private void HandleChatHistory(ChatHistoryPayload payload)
        {
            if (payload.Channel != "dm") return;
            if (payload.OtherPlayerId != _currentFriendId) return;

            ClearMessages();
            foreach (var msg in payload.Messages)
                AppendMessage(msg);

            ScrollToBottom();
        }

        private void HandleDirectMessage(ChatMessageData msg)
        {
            bool isFromFriend = msg.SenderId == _currentFriendId;
            bool isToFriend   = msg.ReceiverId == _currentFriendId && msg.SenderId == _myPlayerId;

            if (!isFromFriend && !isToFriend) return;

            AppendMessage(msg);
            ScrollToBottom();
        }

        private void HandleError(string error)
        {
            Debug.LogWarning($"[DMChat] Lỗi: {error}");
        }

        private void OnSendClick()
        {
            if (string.IsNullOrEmpty(_currentFriendId)) return;

            string text = inputField?.text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            ChatHubClient.Instance?.SendDirectMessage(_currentFriendId, text);
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
