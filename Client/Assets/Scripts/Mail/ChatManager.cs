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

    [Header("Panels")]
    public GameObject privateChatPanel; // kéo PrivateChat_Panel vào đây — ẩn khi chưa chọn bạn

    [Header("Trạng thái Chat")]
    public ChatType currentChatType = ChatType.Private;
    public string currentReceiverId;
    public Sprite currentFriendAvatar;

    private string MyPlayerId => PlayerPrefs.GetString("player_id", "");
    private bool _subscribed = false;
    public bool keepSubscribed = false;

    void OnEnable()  => Subscribe();
    void OnDisable() { if (!keepSubscribed) Unsubscribe(); }
    void Start()
    {
        _ = UnityMainThreadDispatcher.Instance();
        if (privateChatPanel != null) privateChatPanel.SetActive(false);
        if (!_subscribed) Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        if (ChatHubClient.Instance == null) { StartCoroutine(SubscribeWhenReady()); return; }

        // ChatManager chỉ xử lý DM — world chat do WorldChatPanel lo
        ChatHubClient.Instance.OnDirectMessage += HandleDirectMsg;
        ChatHubClient.Instance.OnChatHistory   += HandleChatHistory;
        ChatHubClient.Instance.OnError         += HandleChatError;
        ChatHubClient.Instance.OnConnected     += HandleConnected;
        _subscribed = true;
        Debug.Log("[ChatManager] Đã subscribe.");

        if (ChatHubClient.Instance.IsReady) HandleConnected();
    }

    private void Unsubscribe()
    {
        if (!_subscribed || ChatHubClient.Instance == null) return;
        ChatHubClient.Instance.OnDirectMessage -= HandleDirectMsg;
        ChatHubClient.Instance.OnChatHistory   -= HandleChatHistory;
        ChatHubClient.Instance.OnError         -= HandleChatError;
        ChatHubClient.Instance.OnConnected     -= HandleConnected;
        _subscribed = false;
    }

    IEnumerator SubscribeWhenReady()
    {
        while (ChatHubClient.Instance == null)
            yield return new WaitForSeconds(0.5f);
        Subscribe();
    }

    // ── Handlers ────────────────────────────────────────────────────────

    private void HandleConnected()
    {
        // Chỉ load DM history nếu đang chat với ai đó
        if (!string.IsNullOrEmpty(currentReceiverId))
            ChatHubClient.Instance?.LoadDirectHistory(currentReceiverId);
    }

    private void HandleDirectMsg(ChatMessageData msg)
    {
        string sId = msg.SenderId ?? "";
        if (sId == MyPlayerId) return;

        if (sId == currentReceiverId)
        {
            RenderMessage(msg.Content ?? "", msg.SenderName ?? "", false);
            StartCoroutine(ScrollToBottomNextFrame());
        }
        else if (string.IsNullOrEmpty(currentReceiverId))
        {
            currentChatType     = ChatType.Private;
            currentReceiverId   = sId;
            currentFriendAvatar = null;
            while (contentContainer.childCount > 0)
                UnityEngine.Object.DestroyImmediate(contentContainer.GetChild(0).gameObject);
            ChatHubClient.Instance?.LoadDirectHistory(sId);
        }
    }

    private void HandleChatHistory(ChatHistoryPayload payload)
    {
        string ch      = payload?.Channel      ?? "";
        string otherId = payload?.OtherPlayerId ?? "";
        var rawMsgs    = payload?.Messages      ?? new List<ChatMessageData>();

        // ChatManager chỉ xử lý DM history
        if (ch != "dm" || currentChatType != ChatType.Private || otherId != currentReceiverId)
        {
            Debug.Log($"[ChatHistory] ChatManager bỏ qua: ch={ch} other={otherId}");
            return;
        }

        var msgs = rawMsgs.ToList();
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (contentContainer == null) return;
            while (contentContainer.childCount > 0)
                UnityEngine.Object.DestroyImmediate(contentContainer.GetChild(0).gameObject);
            foreach (var dto in msgs)
                RenderMessage(dto.Content ?? "", dto.SenderName ?? "", dto.SenderId == MyPlayerId);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer.GetComponent<RectTransform>());
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
        });
    }

    private void HandleChatError(string error)
    {
        Debug.LogWarning($"[Chat] Lỗi: {error}");
        RenderMessage($"[Lỗi] {error}", "System", false);
        StartCoroutine(ScrollToBottomNextFrame());
    }

    // ── Gửi tin ─────────────────────────────────────────────────────────

    public void SendMessageFromInput()
    {
        string text = inputField.text;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (ChatHubClient.Instance == null) return;

        // Gửi trước — nếu không có kết nối sẽ bắn OnError và return false, không render
        if (!ChatHubClient.Instance.SendDirectMessage(currentReceiverId, text)) return;

        inputField.text = "";
        inputField.ActivateInputField();

        RenderMessage(text, "Tôi", true);
        StartCoroutine(ScrollToBottomNextFrame());
    }

    // ── Render ───────────────────────────────────────────────────────────

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

    // ── Điều hướng ───────────────────────────────────────────────────────

    public void SetActiveChatFriend(string playerId, string playerName, Sprite avatar)
    {
        currentFriendAvatar = avatar;

        // Hiện panel khi lần đầu chọn bạn
        if (privateChatPanel != null) privateChatPanel.SetActive(true);

        // Cùng người → không xóa/reload để tránh mất tin nhắn cục bộ
        if (playerId == currentReceiverId) return;

        currentChatType   = ChatType.Private;
        currentReceiverId = playerId;

        while (contentContainer.childCount > 0)
            UnityEngine.Object.DestroyImmediate(contentContainer.GetChild(0).gameObject);

        Debug.Log($"[ChatManager] → '{playerName}'");
        ChatHubClient.Instance?.LoadDirectHistory(playerId);
    }

    public void SetWorldChat()
    {
        currentChatType   = ChatType.World;
        currentReceiverId = "";
        FriendItemUI.ClearSelection();
        while (contentContainer.childCount > 0)
            UnityEngine.Object.DestroyImmediate(contentContainer.GetChild(0).gameObject);
        // Không gọi LoadWorldHistory — WorldChatPanel tự lo
    }
}
