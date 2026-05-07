using Game.Chat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendItemUI : MonoBehaviour
{
    public Image pokemonIcon;
    public TextMeshProUGUI nameText;
    public Image statusDot;

    private string _playerId;
    private string _playerName;

    public void SetData(string playerId, string name, Sprite avatar, bool isOnline)
    {
        _playerId   = playerId;
        _playerName = name;
        nameText.text      = name;
        pokemonIcon.sprite = avatar;

        if (statusDot != null)
            statusDot.color = isOnline ? Color.green : Color.gray;

        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        DMChatPanel.Instance?.OpenChat(_playerId, _playerName);
    }
}
