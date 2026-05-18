using UnityEngine;
using UnityEngine.UI;

public class MailManager : MonoBehaviour
{
    public GameObject mailPopup;
    public GameObject worldPopup;
    public Image mailButtonBottom;

    private ChatManager _chat;

    private void Awake()
    {
        _chat = FindFirstObjectByType<ChatManager>(FindObjectsInactive.Include);
    }

    public void ToggleMail()
    {
        if (mailPopup.activeSelf || worldPopup.activeSelf)
            CloseAll();
        else
            OpenFriendTab();
    }

    private void CloseAll()
    {
        mailPopup.SetActive(false);
        worldPopup.SetActive(false);
        if (_chat != null) _chat.keepSubscribed = false;
        mailButtonBottom.color = Color.white;
    }

    public void OpenWorldTab()
    {
        mailPopup.SetActive(false);
        worldPopup.SetActive(true);
        mailButtonBottom.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        // Giữ ChatManager subscribe để không miss DM trong khi đang xem World Chat
        if (_chat != null) _chat.keepSubscribed = true;
    }

    public void OpenFriendTab()
    {
        worldPopup.SetActive(false);
        mailPopup.SetActive(true);
        mailButtonBottom.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        if (_chat != null) _chat.keepSubscribed = false;
    }
}
