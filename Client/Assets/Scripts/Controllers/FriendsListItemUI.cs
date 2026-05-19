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

    public void SetData(string id, string playerName, Sprite avatar)
    {
        playerId = id;

        if (playerNameText != null)
            playerNameText.text = playerName;

        if (avatarImage != null && avatar != null)
            avatarImage.sprite = avatar;
    }

    public void BindDelete(Action<string> deleteAction)
    {
        onDelete = deleteAction;
    }

    private void HandleDeleteClicked()
    {
        if (!string.IsNullOrWhiteSpace(playerId))
            onDelete?.Invoke(playerId);
    }
}
