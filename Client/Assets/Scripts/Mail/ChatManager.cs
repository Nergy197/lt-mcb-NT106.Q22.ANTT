using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Chat;

public enum ChatType { World, Private }

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
    private bool _subscribed = false;

    void OnEnable()  => Subscribe();
    void OnDisable() => Unsubscribe();
    void Start()
    {
        // Pre-create dispatcher trên main thread trước khi SignalR callback cần dùng
        _ = UnityMainThreadDispatcher.Instance();
        if (!_subscribed) Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        if (ChatHubClient.Instance == null) { StartCoroutine(SubscribeWhenReady()); return; }

        ChatHubClient.Instance.OnWorldMessage  += HandleWorldMsg;
        ChatHubClient.Instance.OnDirectMessage += HandleDirectMsg;
        ChatHubClient.Instance.OnChatHistory   += HandleChatHistory;
        ChatHubClient.Instance.OnError         += HandleChatError;
        _subscribed = true;
        Debug.Log("[ChatManager] Đã subscribe ChatHubClient events.");
    }

    private void Unsubscribe()
    {
        if (!_subscribed || ChatHubClient.Instance == null) return;
        ChatHubClient.Instance.OnWorldMessage  -= HandleWorldMsg;
        ChatHubClient.Instance.OnDirectMessage -= HandleDirectMsg;
        ChatHubClient.Instance.OnChatHistory   -= HandleChatHistory;
        ChatHubClient.Instance.OnError         -= HandleChatError;
        _subscribed = false;
    }

    IEnumerator SubscribeWhenReady()
    {
        while (ChatHubClient.Instance == null)
            yield return new WaitForSeconds(0.5f);
        Subscribe();
    }

    // ── Handlers ────────────────────────────────────────────────────────

    private void HandleWorldMsg(ChatMessageData msg)
    {
        if (currentChatType != ChatType.World || msg.SenderId == MyPlayerId) return;
        RenderMessage(msg.Content ?? "", msg.SenderName ?? "", false);
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private void HandleDirectMsg(ChatMessageData msg)
    {
        string sId = msg.SenderId ?? "";
        if (sId == MyPlayerId) return;

        if (sId == currentReceiverId)
        {
            // Đang mở đúng cuộc chat với người này — hiện ngay
            RenderMessage(msg.Content ?? "", msg.SenderName ?? "", false);
            StartCoroutine(ScrollToBottomNextFrame());
        }
        else if (string.IsNullOrEmpty(currentReceiverId) || currentChatType == ChatType.World)
        {
            // Đang ở World Chat hoặc chưa mở chat nào — tự động chuyển sang DM
            currentChatType     = ChatType.Private;
            currentReceiverId   = sId;
            currentFriendAvatar = null;
            while (contentContainer.childCount > 0)
                UnityEngine.Object.DestroyImmediate(contentContainer.GetChild(0).gameObject);
            ChatHubClient.Instance?.LoadDirectHistory(sId);
        }
        // Nếu đang chat với người khác → bỏ qua (cần notification system)
    }

    private void HandleChatHistory(ChatHistoryPayload payload)
    {
        string ch      = payload?.Channel      ?? "";
        string otherId = payload?.OtherPlayerId ?? "";
        var rawMsgs    = payload?.Messages      ?? new List<ChatMessageData>();

        Debug.Log($"<color=yellow>[ChatHistory]</color> channel='{ch}' otherId='{otherId}' msgCount={rawMsgs.Count} | currentType={currentChatType} currentReceiverId='{currentReceiverId}'");

        bool isWorldHistory   = ch == "world" && currentChatType == ChatType.World;
        bool isPrivateHistory = ch == "dm"    && currentChatType == ChatType.Private
                                && otherId == currentReceiverId;

        if (!isWorldHistory && !isPrivateHistory)
        {
            Debug.Log($"<color=red>[ChatHistory]</color> Bỏ qua: isWorld={isWorldHistory} isPrivate={isPrivateHistory}");
            return;
        }

        var msgs = rawMsgs.ToList();
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (contentContainer == null) return;
            while (contentContainer.childCount > 0)
                UnityEngine.Object.DestroyImmediate(contentContainer.GetChild(0).gameObject);
            foreach (var dto in msgs)
                RenderMessage(dto.Content ?? "", dto.SenderName ?? "", dto.SenderId == MyPlayerId);
            Debug.Log($"<color=cyan>[ChatHistory]</color> Đã render {msgs.Count} tin nhắn.");
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer.GetComponent<RectTransform>());
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
        });
    }

    private void HandleChatError(string error)
    {
        Debug.LogWarning($"[Chat] Lỗi từ server: {error}");
        RenderMessage($"[Lỗi] {error}", "System", false);
        StartCoroutine(ScrollToBottomNextFrame());
    }

    // ── Gửi tin ─────────────────────────────────────────────────────────

    public void SendMessageFromInput()
    {
        string text = inputField.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        ReceiveMessage(text, "Tôi", true);

        if (currentChatType == ChatType.World)
            ChatHubClient.Instance?.SendWorldMessage(text);
        else
            ChatHubClient.Instance?.SendDirectMessage(currentReceiverId, text);

        inputField.text = "";
        inputField.ActivateInputField();
    }

    // ── Render ───────────────────────────────────────────────────────────

    public void ReceiveMessage(string message, string sender, bool isMe)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            RenderMessage(message, sender, isMe);
            StartCoroutine(ScrollToBottomNextFrame());
        });
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer.GetComponent<RectTransform>());
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    private void RenderMessage(string message, string sender, bool isMe)
    {
        if (contentContainer == null) return;
        GameObject prefab = isMe ? myMessagePrefab : friendMessagePrefab;
        if (prefab == null) return;

        GameObject msgObj = Instantiate(prefab, contentContainer);
        msgObj.GetComponentInChildren<TMP_Text>().text = message;

        if (!isMe && currentFriendAvatar != null)
        {
            Image img = msgObj.transform.Find("Avatar")?.GetComponent<Image>()
                        ?? msgObj.GetComponentInChildren<Image>();
            if (img != null) img.sprite = currentFriendAvatar;
        }
    }

    // ── Điều hướng tab ───────────────────────────────────────────────────

    public void SetActiveChatFriend(string playerId, string playerName, Sprite avatar)
    {
        currentChatType     = ChatType.Private;
        currentReceiverId   = playerId;
        currentFriendAvatar = avatar;

        // Không clear ngay — để HandleChatHistory clear khi data về, tránh flash trắng
        Debug.Log($"<color=orange>[UI-SWITCH]</color> → '{playerName}' | isListening={_subscribed}");
        ChatHubClient.Instance?.LoadDirectHistory(playerId);
    }

    public void SetWorldChat()
    {
        currentChatType   = ChatType.World;
        currentReceiverId = "";
        FriendItemUI.ClearSelection();
        while (contentContainer.childCount > 0)
            UnityEngine.Object.DestroyImmediate(contentContainer.GetChild(0).gameObject);

        ChatHubClient.Instance?.LoadWorldHistory();
    }
}
