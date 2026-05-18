using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendRequestItemUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button declineButton;

    private string friendshipId;
    private Action<string> onAccept;
    private Action<string> onDecline;

    private void Awake()
    {
        if (acceptButton != null)
            acceptButton.onClick.AddListener(HandleAcceptClicked);

        if (declineButton != null)
            declineButton.onClick.AddListener(HandleDeclineClicked);
    }

    private void OnDestroy()
    {
        if (acceptButton != null)
            acceptButton.onClick.RemoveListener(HandleAcceptClicked);

        if (declineButton != null)
            declineButton.onClick.RemoveListener(HandleDeclineClicked);
    }

    public void SetData(string id, string playerName, Sprite avatar)
    {
        friendshipId = id;

        if (playerNameText != null)
            playerNameText.text = playerName;

        if (avatarImage != null && avatar != null)
            avatarImage.sprite = avatar;
    }

    public void BindActions(Action<string> acceptAction, Action<string> declineAction)
    {
        onAccept = acceptAction;
        onDecline = declineAction;
    }

    private void HandleAcceptClicked()
    {
        onAccept?.Invoke(friendshipId);
    }

    private void HandleDeclineClicked()
    {
        onDecline?.Invoke(friendshipId);
    }
}
