using UnityEngine;
using UnityEngine.UI;
using PokemonMMO.UI;

public class MailManager : MonoBehaviour
{
    public GameObject mailPopup;
    public GameObject worldPopup;
    public Image mailButtonBottom;
    public Button mailButton; // kéo MailButton vào đây

    private ChatManager _chat;
    private GameObject _clickOutsideOverlay;

    private void Awake()
    {
        _chat = FindFirstObjectByType<ChatManager>(FindObjectsInactive.Include);
        // Tự tìm Button nếu chưa được kéo vào Inspector
        if (mailButton == null && mailButtonBottom != null)
            mailButton = mailButtonBottom.GetComponentInParent<Button>();

        // Phòng trường hợp panel bị để active nhầm trong scene lúc save
        mailPopup.SetActive(false);
        worldPopup.SetActive(false);
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
        ClickOutsideOverlay.Hide(_clickOutsideOverlay);
        if (_chat != null) _chat.keepSubscribed = false;
        mailButtonBottom.color = Color.white;
        BottomMenuManager.Instance?.NotifyClose();
    }

    public void OpenWorldTab()
    {
        if (_chat != null) _chat.keepSubscribed = true; // phải set TRƯỚC SetActive(false)
        mailPopup.SetActive(false);
        worldPopup.SetActive(true);
        ClickOutsideOverlay.Show(ref _clickOutsideOverlay, worldPopup, CloseAll);
        mailButtonBottom.color = new Color(0.7f, 0.7f, 0.7f, 1f);
    }

    public void OpenFriendTab()
    {
        worldPopup.SetActive(false);
        mailPopup.SetActive(true);
        ClickOutsideOverlay.Show(ref _clickOutsideOverlay, mailPopup, CloseAll);
        mailButtonBottom.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        if (_chat != null) _chat.keepSubscribed = false;
    }
}
