using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI row for the Friends tab list (avatar, name, delete).
/// </summary>
public class FriendsListItemUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text statusText; // NEW: Trạng thái Online/Offline
    [SerializeField] private Button deleteButton;

    private string playerId;
    private string cachedName;
    private Sprite cachedAvatar;
    private Action<string> onDelete;

    private void Awake()
    {
        if (deleteButton != null)
            deleteButton.onClick.AddListener(HandleDeleteClicked);
    }

    private void OnDestroy()
    {
        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(HandleDeleteClicked);
    }

    public void SetData(string id, string name, Sprite avatar, bool isOnline = false, string lastSeenAt = null)
    {
        playerId      = id;
        cachedName    = name;
        cachedAvatar  = avatar;

        if (playerNameText != null)
            playerNameText.text = name;

        if (avatarImage != null && avatar != null)
            avatarImage.sprite = avatar;

        if (statusText != null)
        {
            if (isOnline)
            {
                statusText.text = "<color=#00FF00>Đang Online</color>";
            }
            else if (!string.IsNullOrEmpty(lastSeenAt) && DateTime.TryParse(lastSeenAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime lastSeenTime))
            {
                // Chuyển lastSeenTime về giờ địa phương nếu nó là UTC
                if (lastSeenTime.Kind == DateTimeKind.Utc)
                    lastSeenTime = lastSeenTime.ToLocalTime();
                
                TimeSpan diff = DateTime.Now - lastSeenTime;
                
                if (diff.TotalMinutes < 1)
                    statusText.text = "<color=#AAAAAA>Vừa mới truy cập</color>";
                else if (diff.TotalMinutes < 60)
                    statusText.text = $"<color=#AAAAAA>Hoạt động {(int)diff.TotalMinutes} phút trước</color>";
                else if (diff.TotalHours < 24)
                    statusText.text = $"<color=#AAAAAA>Hoạt động {(int)diff.TotalHours} giờ trước</color>";
                else
                    statusText.text = $"<color=#AAAAAA>Hoạt động {(int)diff.TotalDays} ngày trước</color>";
            }
            else
            {
                statusText.text = "<color=#AAAAAA>Offline</color>";
            }
        }
    }

    public void BindDelete(Action<string> deleteAction)
    {
        onDelete = deleteAction;
    }

    // Gọi từ Button OnClick trên toàn bộ row (không phải nút Delete)
    public void OnClickRow()
    {
        if (string.IsNullOrWhiteSpace(playerId)) return;

        ChatManager chat = FindFirstObjectByType<ChatManager>(FindObjectsInactive.Include);
        if (chat != null)
            chat.SetActiveChatFriend(playerId, cachedName, cachedAvatar);

        Game.Chat.DMChatPanel.Instance?.OpenChat(playerId, cachedName);
    }

    private void HandleDeleteClicked()
    {
        if (!string.IsNullOrWhiteSpace(playerId))
            onDelete?.Invoke(playerId);
    }
}
