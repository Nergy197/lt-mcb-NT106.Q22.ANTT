using UnityEngine;
using UnityEngine.UI; // Cần cái này để điều khiển Image

public class FriendsController : MonoBehaviour
{
    public GameObject friendsPopup;

    [Header("Tab Settings")]
    public Image backgroundDisplay; // Kéo cái Background_Display vào đây
    public Sprite spriteBanBe;      // Kéo ảnh "Tab Bạn bè đang sáng" vào đây
    public Sprite spriteThemBan;    // Kéo ảnh "Tab Thêm bạn đang sáng" vào đây

    public void ToggleFriendsPopup()
    {
        friendsPopup.SetActive(!friendsPopup.activeSelf);
        // Mỗi lần mở lên thì mặc định hiện tab Bạn Bè trước
        if (friendsPopup.activeSelf) ShowFriendsTab();
    }

    // Hàm gọi khi nhấn nút Tab_BanBe
    public void ShowFriendsTab()
    {
        backgroundDisplay.sprite = spriteBanBe;
        Debug.Log("Đã đổi sang ảnh Bạn bè");
    }

    // Hàm gọi khi nhấn nút Tab_ThemBan
    public void ShowAddTab()
    {
        backgroundDisplay.sprite = spriteThemBan;
        Debug.Log("Đã đổi sang ảnh Thêm bạn");
    }
}