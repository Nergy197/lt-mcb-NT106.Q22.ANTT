using UnityEngine;
using TMPro; 
using UnityEngine.UI;
using Game.Network;
using Microsoft.AspNetCore.SignalR.Client;
using System;

public enum ChatType { World, Private }

// Định nghĩa cấu trúc tin nhắn linh hoạt để khớp với mọi kiểu đặt tên từ Server (PascalCase/camelCase)
[Serializable]
public class ChatMessageDto
{
    public string SenderId;
    public string senderId;
    
    public string SenderName;
    public string senderName;
    
    public string Content;
    public string content;
    
    public string Channel;
    public string ReceiverId;

    public string GetContent() => !string.IsNullOrEmpty(Content) ? Content : content;
    public string GetSenderName() => !string.IsNullOrEmpty(SenderName) ? SenderName : senderName;
    public string GetSenderId() => !string.IsNullOrEmpty(SenderId) ? SenderId : senderId;
}

public class ChatManager : MonoBehaviour
{
    [Header("Cấu hình Prefabs")]
    public GameObject myMessagePrefab;    
    public GameObject friendMessagePrefab; 
    public Transform contentContainer;    

    [Header("Cấu hình Input")]
    public TMP_InputField inputField;     
    public ScrollRect scrollRect;         

    [Header("Trạng thái Chat")]
    public ChatType currentChatType = ChatType.World;
    public string currentReceiverId;      
    public Sprite currentFriendAvatar;    

    async void Start()
    {
        if (SignalRClient.Instance != null)
        {
            Debug.Log("[Chat] Đang khởi động kết nối SignalR...");
            await SignalRClient.Instance.ConnectAsync();
        }

        if (SignalRClient.Instance != null && SignalRClient.Instance.Chat != null)
        {
            Debug.Log("[Chat] Đã đăng ký lắng nghe sự kiện WorldMessage và DirectMessage");

            SignalRClient.Instance.Chat.On<ChatMessageDto>("WorldMessage", (dto) => {
                string msg = dto.GetContent();
                string name = dto.GetSenderName();
                Debug.Log($"[Chat] Nhận tin nhắn World từ {name}: {msg}");
                
                if (currentChatType == ChatType.World)
                    ReceiveMessage(msg, name, false);
            });

            SignalRClient.Instance.Chat.On<ChatMessageDto>("DirectMessage", (dto) => {
                string msg = dto.GetContent();
                string name = dto.GetSenderName();
                string sId = dto.GetSenderId();
                
                Debug.Log($"[Chat] Nhận tin nhắn DM từ {name}: {msg}");
                
                string myId = PlayerPrefs.GetString("player_id", "");
                bool isMe = sId == myId;
                
                if (isMe || currentReceiverId == sId)
                {
                    ReceiveMessage(msg, name, isMe);
                }
            });
        }
        else
        {
            Debug.LogError("[Chat] Không tìm thấy SignalRClient hoặc Chat Hub!");
        }
    }

    public async void SendMessageFromInput()
    {
        string messageText = inputField.text;
        if (string.IsNullOrWhiteSpace(messageText)) return;

        if (SignalRClient.Instance == null || SignalRClient.Instance.Chat.State != HubConnectionState.Connected)
        {
            Debug.LogError("Chưa kết nối Chat Server!");
            return;
        }

        try {
            if (currentChatType == ChatType.World)
            {
                await SignalRClient.Instance.Chat.InvokeAsync("SendWorldMessage", messageText);
            }
            else
            {
                if (string.IsNullOrEmpty(currentReceiverId)) return;
                await SignalRClient.Instance.Chat.InvokeAsync("SendDirectMessage", currentReceiverId, messageText);
            }
            
            inputField.text = "";
            inputField.ActivateInputField();
        }
        catch (Exception ex) {
            Debug.LogError("Lỗi gửi tin nhắn: " + ex.Message);
        }
    }

    public void ReceiveMessage(string messageText, string senderName, bool isMe)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (contentContainer == null) {
                Debug.LogError("Chưa kéo Content Container vào ChatManager!");
                return;
            }

            GameObject prefab = isMe ? myMessagePrefab : friendMessagePrefab;
            if (prefab == null) {
                Debug.LogError("Chưa kéo Message Prefab vào ChatManager!");
                return;
            }

            GameObject newMsg = Instantiate(prefab, contentContainer);
            Debug.Log($"[Chat] Đã khởi tạo tin nhắn: {messageText}");

            TMP_Text textComponent = newMsg.GetComponentInChildren<TMP_Text>();
            if (textComponent != null) textComponent.text = messageText;

            if (!isMe)
            {
                Image avatarImage = newMsg.transform.Find("Avatar")?.GetComponent<Image>();
                if (avatarImage == null) avatarImage = newMsg.GetComponentInChildren<Image>();

                if (avatarImage != null && currentFriendAvatar != null)
                    avatarImage.sprite = currentFriendAvatar;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer.GetComponent<RectTransform>());
            scrollRect.verticalNormalizedPosition = 0f;
        });
    }

    public void SetActiveChatFriend(string playerId, string playerName, Sprite avatar)
    {
        currentChatType = ChatType.Private;
        currentReceiverId = playerId;
        currentFriendAvatar = avatar;
        Debug.Log($"Bắt đầu chat riêng với: {playerName}");
        
        foreach (Transform child in contentContainer) Destroy(child.gameObject);
        SignalRClient.Instance.Chat.InvokeAsync("LoadDirectHistory", playerId);
    }
}