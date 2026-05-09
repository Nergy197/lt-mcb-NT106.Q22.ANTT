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
    void Start()     { if (!_subscribed) Subscribe(); }

    private void Subscribe()
    {
        if (_subscribed) return;
        if (ChatHubClient.Instance == null) { StartCoroutine(SubscribeWhenReady()); return; }

        ChatHubClient.Instance.OnWorldMessage  += HandleWorldMsg;
        ChatHubClient.Instance.OnDirectMessage += HandleDirectMsg;
        ChatHubClient.Instance.OnChatHistory   += HandleChatHistory;
        _subscribed = true;
        Debug.Log("[ChatManager] Đã subscribe ChatHubClient events.");
    }

    private void Unsubscribe()
    {
        if (!_subscribed || ChatHubClient.Instance == null) return;
        ChatHubClient.Instance.OnWorldMessage  -= HandleWorldMsg;
        ChatHubClient.Instance.OnDirectMessage -= HandleDirectMsg;
        ChatHubClient.Instance.OnChatHistory   -= HandleChatHistory;
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
        if (currentChatType == ChatType.World && msg.SenderId != MyPlayerId)
            ReceiveMessage(msg.Content ?? "", msg.SenderName ?? "", false);
    }

    private void HandleDirectMsg(ChatMessageData msg)
    {
        string sId = msg.SenderId ?? "";
        if (sId == MyPlayerId) return;

        if (currentChatType == ChatType.Private && sId == currentReceiverId)
        {
            ReceiveMessage(msg.Content ?? "", msg.SenderName ?? "", false);
        }
        else if (string.IsNullOrEmpty(currentReceiverId))
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                currentChatType     = ChatType.Private;
                currentReceiverId   = sId;
                currentFriendAvatar = null;
                while (contentContainer.childCount > 0)
                    UnityEngine.Object.DestroyImmediate(contentContainer.GetChild(0).gameObject);
                ChatHubClient.Instance?.LoadDirectHistory(sId);
            });
        }
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

        while (contentContainer.childCount > 0)
            UnityEngine.Object.DestroyImmediate(contentContainer.GetChild(0).gameObject);

        Debug.Log($"<color=orange>[UI-SWITCH]</color> → '{playerName}' | isListening={_subscribed}");
        Debug.Log($"<color=orange>[UI-SWITCH]</color> Gọi LoadDirectHistory({playerId})");
        ChatHubClient.Instance?.LoadDirectHistory(playerId);
    }

    public void SetWorldChat()
    {
        currentChatType   = ChatType.World;
        currentReceiverId = "";
        while (contentContainer.childCount > 0)
            UnityEngine.Object.DestroyImmediate(contentContainer.GetChild(0).gameObject);

        ChatHubClient.Instance?.LoadWorldHistory();
    }
}
