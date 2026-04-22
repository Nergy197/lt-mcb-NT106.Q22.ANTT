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

    public void SetData(string name, Sprite avatar, bool isOnline)
    {
        nameText.text = name;
        pokemonIcon.sprite = avatar;

        if (statusDot != null)
        {
            // Xanh lá = Online, Xám = Offline
            statusDot.color = isOnline ? Color.green : Color.gray;
        }
    }
}