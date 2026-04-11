using UnityEngine;
using UnityEngine.UI;

public class MailManager : MonoBehaviour
{
    public GameObject mailPopup;  // Tab Bạn bè
    public GameObject worldPopup; // Tab Thế giới
    public Image mailButtonBottom; // Icon Mail dưới cùng

    // 1. Nút Mail dưới cùng (Bật mặc định là Friend Tab)
    public void ToggleMail()
    {
        if (mailPopup.activeSelf || worldPopup.activeSelf)
        {
            mailPopup.SetActive(false);
            worldPopup.SetActive(false);
            mailButtonBottom.color = new Color(0, 0, 0, 0f); // Sáng lại
        }
        else
        {
            OpenFriendTab();
        }
    }

    // 2. Chuyển sang World Tab
    public void OpenWorldTab()
    {
        mailPopup.SetActive(false);
        worldPopup.SetActive(true);
        mailButtonBottom.color = new Color(0, 0, 0, 0.5f); // Giữ mờ
    }

    // 3. Chuyển sang Friend Tab
    public void OpenFriendTab()
    {
        worldPopup.SetActive(false);
        mailPopup.SetActive(true);
        mailButtonBottom.color = new Color(0, 0, 0, 0.5f); // Giữ mờ
    }
}