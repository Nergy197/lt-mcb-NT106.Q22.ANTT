using UnityEngine;
using UnityEngine.UI;

public class MailManager : MonoBehaviour
{
    public GameObject mailPopup;
    public GameObject worldPopup;
    public Image mailButtonBottom;
    public Button mailButton; // kéo MailButton vào đây

    private ChatManager _chat;

    private void Awake()
    {
        _chat = FindFirstObjectByType<ChatManager>(FindObjectsInactive.Include);
        // Tự tìm Button nếu chưa được kéo vào Inspector
        if (mailButton == null && mailButtonBottom != null)
            mailButton = mailButtonBottom.GetComponentInParent<Button>();
    }

    public void ToggleMail()
    {
        if (mailPopup.activeSelf || worldPopup.activeSelf)
            CloseAll();
        else
        {
            OpenFriendTab();
            BottomMenuManager.Instance?.NotifyOpen(mailButton);
        }
    }

    private void CloseAll()
    {
        mailPopup.SetActive(false);
        worldPopup.SetActive(false);
        if (_chat != null) _chat.keepSubscribed = false;
        mailButtonBottom.color = Color.white;
        BottomMenuManager.Instance?.NotifyClose();
    }

    public void OpenWorldTab()
    {
        if (_chat != null) _chat.keepSubscribed = true; // phải set TRƯỚC SetActive(false)
        mailPopup.SetActive(false);
        worldPopup.SetActive(true);
        mailButtonBottom.color = new Color(0.7f, 0.7f, 0.7f, 1f);
    }

    public void OpenFriendTab()
    {
        worldPopup.SetActive(false);
        mailPopup.SetActive(true);
        mailButtonBottom.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        if (_chat != null) _chat.keepSubscribed = false;
    }
}
