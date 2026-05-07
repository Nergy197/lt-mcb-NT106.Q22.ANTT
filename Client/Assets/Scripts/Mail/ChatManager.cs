using UnityEngine;
using TMPro; 
using UnityEngine.UI;
using Game.Network;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum ChatType { World, Private }

[Serializable]
public class ChatMessageDto
{
    public string Id { get; set; }
    public string SenderId { get; set; }
    public string senderId { get; set; }
    public string SenderName { get; set; }
    public string senderName { get; set; }
    public string Content { get; set; }
    public string content { get; set; }
    public string ReceiverId { get; set; }
    public string receiverId { get; set; }
    public string Channel { get; set; }

    public string GetContent() => !string.IsNullOrEmpty(Content) ? Content : content;
    public string GetSenderName() => !string.IsNullOrEmpty(SenderName) ? SenderName : senderName;
    public string GetSenderId() => !string.IsNullOrEmpty(SenderId) ? SenderId : senderId;
    public string GetReceiverId() => !string.IsNullOrEmpty(ReceiverId) ? ReceiverId : receiverId;
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

    private string MyPlayerId => PlayerPrefs.GetString("player_id", "");
    private bool isListening = false;

    void Start()
    {
        Debug.Log("<color=green>[ChatManager]</color> Script đã Start.");
        StartCoroutine(KeepCheckingConnection());
    }

    System.Collections.IEnumerator KeepCheckingConnection()
    {
        while (true)
        {
            if (SignalRClient.Instance?.Chat != null && !isListening)
            {
                SetupHandlers();
            }
            yield return new WaitForSeconds(2f);
        }
    }

    private void SetupHandlers()
    {
        if (SignalRClient.Instance?.Chat == null) return;

        var chat = SignalRClient.Instance.Chat;
        chat.Remove("WorldMessage");
        chat.Remove("DirectMessage");
        chat.Remove("ReceiveHistoryMessage");

        chat.On<ChatMessageDto>("WorldMessage", (dto) => {
            if (currentChatType == ChatType.World && dto.GetSenderId() != MyPlayerId)
                ReceiveMessage(dto.GetContent(), dto.GetSenderName(), false);
        });

        chat.On<ChatMessageDto>("DirectMessage", (dto) => {
            string sId = dto.GetSenderId();
            if (currentChatType == ChatType.Private && (sId == currentReceiverId || dto.GetReceiverId() == currentReceiverId))
                ReceiveMessage(dto.GetContent(), dto.GetSenderName(), sId == MyPlayerId);
        });

        chat.On<ChatMessageDto>("ReceiveHistoryMessage", (dto) => {
            ReceiveMessage(dto.GetContent(), dto.GetSenderName(), dto.GetSenderId() == MyPlayerId);
        });

        isListening = true;
        Debug.Log("<color=cyan>[ChatManager]</color> Đã kết nối bộ lắng nghe tin nhắn.");
    }

    public async void SendMessageFromInput()
    {
        string text = inputField.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        ReceiveMessage(text, "Tôi", true);
        
        try {
            if (SignalRClient.Instance?.Chat?.State == HubConnectionState.Connected)
            {
                if (currentChatType == ChatType.World)
                    await SignalRClient.Instance.Chat.InvokeAsync("SendWorldMessage", text);
                else
                    await SignalRClient.Instance.Chat.InvokeAsync("SendDirectMessage", currentReceiverId, text);
            }
        } catch (Exception ex) {
            Debug.LogError("Lỗi gửi tin: " + ex.Message);
        }

        inputField.text = "";
        inputField.ActivateInputField();
    }

    public void ReceiveMessage(string message, string sender, bool isMe)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (contentContainer == null) return;
            GameObject prefab = isMe ? myMessagePrefab : friendMessagePrefab;
            if (prefab == null) return;

            GameObject msgObj = Instantiate(prefab, contentContainer);
            msgObj.GetComponentInChildren<TMP_Text>().text = message;

            if (!isMe && currentFriendAvatar != null)
            {
                Image img = msgObj.transform.Find("Avatar")?.GetComponent<Image>() ?? msgObj.GetComponentInChildren<Image>();
                if (img != null) img.sprite = currentFriendAvatar;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer.GetComponent<RectTransform>());
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
        });
    }

    public void SetActiveChatFriend(string playerId, string playerName, Sprite avatar)
    {
        Debug.Log($"<color=orange>[UI-SWITCH]</color> Chat với: {playerName}");
        
        currentChatType = ChatType.Private;
        currentReceiverId = playerId;
        currentFriendAvatar = avatar;

        foreach (Transform child in contentContainer) Destroy(child.gameObject);
        
        if (SignalRClient.Instance?.Chat?.State == HubConnectionState.Connected)
        {
            SignalRClient.Instance.Chat.InvokeAsync("LoadDirectHistory", playerId);
        }
    }

    public void SetWorldChat()
    {
        currentChatType = ChatType.World;
        currentReceiverId = "";
        foreach (Transform child in contentContainer) Destroy(child.gameObject);
        
        if (SignalRClient.Instance?.Chat?.State == HubConnectionState.Connected)
            SignalRClient.Instance.Chat.InvokeAsync("LoadWorldHistory");
    }
}