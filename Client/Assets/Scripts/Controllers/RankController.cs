using UnityEngine;
using UnityEngine.UI;

public class RankController : MonoBehaviour
{
    public GameObject rankPopup;

    [Header("Tab Sprites")]
    public Image backgroundDisplay;
    public Sprite spriteTop100;
    public Sprite spriteFriends;

    // 1. Hàm Toggle (Dùng cho nút Cúp vàng ở menu dưới)
    public void ToggleRankPopup()
    {
        rankPopup.SetActive(!rankPopup.activeSelf);
        if (rankPopup.activeSelf) ShowTop100();
    }

    // 2. Hàm Đóng chuyên biệt (Dùng cho nút X)
    public void CloseRankPopup()
    {
        rankPopup.SetActive(false);
    }

    public void ShowTop100()
    {
        backgroundDisplay.sprite = spriteTop100;
    }

    public void ShowFriendsRank()
    {
        backgroundDisplay.sprite = spriteFriends;
    }
}