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

    public void SetData(string id, string name, Sprite avatar)
    {
        playerId      = id;
        cachedName    = name;
        cachedAvatar  = avatar;

        if (playerNameText != null)
            playerNameText.text = name;

        if (avatarImage != null && avatar != null)
            avatarImage.sprite = avatar;
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
