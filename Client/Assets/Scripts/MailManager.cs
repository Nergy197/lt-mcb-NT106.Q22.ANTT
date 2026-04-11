using UnityEngine;
using UnityEngine.UI;

public class MailManager : MonoBehaviour
{
    public GameObject mailPopup;  // Tab Bạn bè
    public GameObject worldPopup; // Tab Thế giới
    public Image mailButtonBottom; // Icon Mail dưới cùng

    // 1. Nút Mail dưới cùng
    public void ToggleMail()
    {
        // Nếu một trong hai đang bật -> Đóng tất cả
        if (mailPopup.activeSelf || worldPopup.activeSelf)
        {
            CloseAll();
        }
        else
        {
            OpenFriendTab(); // Mặc định mở Friend
        }
    }

    private void CloseAll()
    {
        mailPopup.SetActive(false);
        worldPopup.SetActive(false);
        // Trả về màu trắng gốc (Full sáng)
        mailButtonBottom.color = Color.white;
    }

    // 2. Chuyển sang World Tab
    public void OpenWorldTab()
    {
        mailPopup.SetActive(false);
        worldPopup.SetActive(true);
        // Làm tối nút đi một chút để biết là đang mở (Gray)
        mailButtonBottom.color = new Color(0.7f, 0.7f, 0.7f, 1f);
    }

    // 3. Chuyển sang Friend Tab
    public void OpenFriendTab()
    {
        worldPopup.SetActive(false);
        mailPopup.SetActive(true);
        // Làm tối nút đi một chút
        mailButtonBottom.color = new Color(0.7f, 0.7f, 0.7f, 1f);
    }
}