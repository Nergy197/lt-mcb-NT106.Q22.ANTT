using UnityEngine;
using TMPro; 
using UnityEngine.UI;
using Game.Network;
using Microsoft.AspNetCore.SignalR.Client;
using System;

public enum ChatType { World, Private }

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
            Debug.Log("<color=cyan>[Chat System]</color> Đang khởi động kết nối SignalR...");
            await SignalRClient.Instance.ConnectAsync();
            
            if (SignalRClient.Instance.Chat.State == HubConnectionState.Connected)
                Debug.Log("<color=green>[Chat System] KẾT NỐI THÀNH CÔNG!</color> Sẵn sàng gửi/nhận tin nhắn.");
            else
                Debug.LogWarning($"<color=yellow>[Chat System] Cảnh báo:</color> Kết nối không ở trạng thái Connected (Hiện tại: {SignalRClient.Instance.Chat.State})");
        }

        if (SignalRClient.Instance != null && SignalRClient.Instance.Chat != null)
        {
            SignalRClient.Instance.Chat.On<ChatMessageDto>("WorldMessage", (dto) => {
                string msg = dto.GetContent();
                string name = dto.GetSenderName();
                Debug.Log($"<color=white>[NHẬN - THẾ GIỚI]</color> <b>{name}</b>: {msg}");
                
                if (currentChatType == ChatType.World)
                    ReceiveMessage(msg, name, false);
            });

            SignalRClient.Instance.Chat.On<ChatMessageDto>("DirectMessage", (dto) => {
                string msg = dto.GetContent();
                string name = dto.GetSenderName();
                string sId = dto.GetSenderId();
                Debug.Log($"<color=magenta>[NHẬN - CHAT RIÊNG]</color> Từ <b>{name}</b>: {msg}");
                
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
            Debug.LogError("<color=red>[Chat System] LỖI:</color> Không tìm thấy SignalRClient hoặc Chat Hub trong Scene!");
        }
    }

    public async void SendMessageFromInput()
    {
        string messageText = inputField.text;
        if (string.IsNullOrWhiteSpace(messageText)) return;

        if (SignalRClient.Instance == null || SignalRClient.Instance.Chat.State != HubConnectionState.Connected)
        {
            Debug.LogError("<color=red>[Chat System] LỖI:</color> Chưa kết nối Server, không thể gửi tin!");
            return;
        }

        Debug.Log($"<color=blue>[GỬI TIN]</color> Đang gửi: {messageText} (Kênh: {currentChatType})");

        try {
            if (currentChatType == ChatType.World)
            {
                await SignalRClient.Instance.Chat.InvokeAsync("SendWorldMessage", messageText);
                Debug.Log("<color=green>[GỬI TIN] Thành công -> Thế giới</color>");
            }
            else
            {
                if (string.IsNullOrEmpty(currentReceiverId)) {
                    Debug.LogWarning("<color=orange>[GỬI TIN] Thất bại:</color> Chưa chọn người nhận (ReceiverId trống)");
                    return;
                }
                await SignalRClient.Instance.Chat.InvokeAsync("SendDirectMessage", currentReceiverId, messageText);
                Debug.Log($"<color=green>[GỬI TIN] Thành công -> Friend ID: {currentReceiverId}</color>");
            }
            
            inputField.text = "";
            inputField.ActivateInputField();
        }
        catch (Exception ex) {
            Debug.LogError($"<color=red>[GỬI TIN LỖI]</color> {ex.Message}");
        }
    }

    public void ReceiveMessage(string messageText, string senderName, bool isMe)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (contentContainer == null) return;

            GameObject prefab = isMe ? myMessagePrefab : friendMessagePrefab;
            if (prefab == null) return;

            GameObject newMsg = Instantiate(prefab, contentContainer);
            
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
            
            Debug.Log($"<color=cyan>[GIAO DIỆN]</color> Đã hiển thị tin nhắn của {senderName} lên màn hình.");
        });
    }

    public void SetActiveChatFriend(string playerId, string playerName, Sprite avatar)
    {
        currentChatType = ChatType.Private;
        currentReceiverId = playerId;
        currentFriendAvatar = avatar;
        Debug.Log($"<color=orange>[CHUYỂN TAB]</color> Bắt đầu chat riêng với: <b>{playerName}</b> (ID: {playerId})");
        
        foreach (Transform child in contentContainer) Destroy(child.gameObject);
        SignalRClient.Instance.Chat.InvokeAsync("LoadDirectHistory", playerId);
    }
}