using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FriendItemUI : MonoBehaviour
{
    // Anh kéo cái Image Avatar và Text Name vào 2 ô này trong Inspector của Prefab
    public Image pokemonIcon;
    public TextMeshProUGUI nameText;

    // Chấm tròn hiển thị trạng thái Online/Offline (kéo UI Image vào đây)
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
            // Xanh lá = Online, Xám = Offline
            statusDot.color = isOnline ? Color.green : Color.gray;
        }
    }

    // Gắn hàm này vào Button của FriendPrefab
    public void OnClickFriend()
    {
        ChatManager chat = FindFirstObjectByType<ChatManager>();
        if (chat != null)
        {
            chat.SetActiveChatFriend(myPlayerId, myPlayerName, myAvatar);
            
            // Mở bảng chat lên nếu nó đang ẩn (tùy cấu hình UI của bạn)
            // Ví dụ: chat.gameObject.SetActive(true);
        }
    }
}