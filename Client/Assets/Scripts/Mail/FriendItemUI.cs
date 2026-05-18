using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendItemUI : MonoBehaviour
{
    public Image pokemonIcon;
    public TextMeshProUGUI nameText;
    public Image statusDot;

    private string myPlayerId;
    private string myPlayerName;
    private Sprite myAvatar;

    private Image _bg;
    private Color _normalColor;
    private static readonly Color HighlightColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    private static FriendItemUI _selected;

    private void Awake()
    {
        _bg = GetComponent<Image>();
        if (_bg != null) _normalColor = _bg.color;
    }

    public void SetData(string id, string name, Sprite avatar, bool isOnline)
    {
        myPlayerId   = id;
        myPlayerName = name;
        myAvatar     = avatar;

        nameText.text      = name;
        pokemonIcon.sprite = avatar;

        if (statusDot != null)
            statusDot.color = isOnline ? Color.green : Color.gray;
    }

    public void OnClickFriend()
    {
        Debug.Log($"[FriendItemUI] Nhấn: {myPlayerName} ({myPlayerId})");

        Select();

        // ChatManager — xử lý DM với prefab đơn giản
        ChatManager chat = FindFirstObjectByType<ChatManager>(FindObjectsInactive.Include);
        if (chat != null)
            chat.SetActiveChatFriend(myPlayerId, myPlayerName, myAvatar);

        // DMChatPanel — xử lý DM với MessageItemUI (nếu có trong scene)
        Game.Chat.DMChatPanel.Instance?.OpenChat(myPlayerId, myPlayerName);
    }

    private void Select()
    {
        if (_selected != null && _selected != this)
            _selected.SetHighlight(false);
        _selected = this;
        SetHighlight(true);
    }

    private void SetHighlight(bool on)
    {
        if (_bg == null) return;
        _bg.color = on ? HighlightColor : _normalColor;
    }

    public static void ClearSelection()
    {
        if (_selected != null)
        {
            _selected.SetHighlight(false);
            _selected = null;
        }
    }

    private void OnDestroy()
    {
        if (_selected == this) _selected = null;
    }
}
