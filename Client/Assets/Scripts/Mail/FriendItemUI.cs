using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FriendItemUI : MonoBehaviour
{
    public Image pokemonIcon;
    public TextMeshProUGUI nameText;
    public Image statusDot;

    private string myPlayerId;
    private string myPlayerName;
    private Sprite myAvatar;

    public void SetData(string id, string name, Sprite avatar, bool isOnline)
    {
        myPlayerId = id;
        myPlayerName = name;
        myAvatar = avatar;
        
        nameText.text = name;
        pokemonIcon.sprite = avatar;

        if (statusDot != null)
        {
            statusDot.color = isOnline ? Color.green : Color.gray;
        }
    }

    public void OnClickFriend()
    {
        Debug.Log($"<color=red>======= [CLICK] =======</color> Đã nhấn: {myPlayerName}");

        // Cách tìm ChatManager chắc chắn nhất: Tìm trong toàn bộ scene kể cả bị ẩn
        ChatManager chat = FindFirstObjectByType<ChatManager>(FindObjectsInactive.Include);
        
        if (chat != null)
        {
            chat.SetActiveChatFriend(myPlayerId, myPlayerName, myAvatar);
        }
        else
        {
            Debug.LogError("KHÔNG TÌM THẤY ChatManager TRONG SCENE! Hãy kiểm tra xem bạn đã kéo script ChatManager vào Object nào chưa.");
        }
    }
}