using UnityEngine;
using UnityEngine.UI;
using PokemonMMO.UI;

public class RankController : MonoBehaviour
{
    public GameObject rankPopup;
    public Button rankButton; // kéo RANK bottom button vào đây

    [Header("Tab Panels")]
    public GameObject top100TabPanel;
    public GameObject friendsRankTabPanel;

    private GameObject _clickOutsideOverlay;

    private void Awake()
    {
        // Phòng trường hợp panel bị để active nhầm trong scene lúc save
        if (rankPopup != null) rankPopup.SetActive(false);
    }

    // 1. Hàm Toggle (Dùng cho nút Cúp vàng ở menu dưới)
    public void ToggleRankPopup()
    {
        if (rankPopup == null) return;

        if (rankPopup.activeSelf)
        {
            CloseAll();
        }
        else
        {
            OpenTop100Tab();
            BottomMenuManager.Instance?.NotifyOpen(rankButton);
        }
    }

    // 2. Hàm Đóng chuyên biệt (Dùng cho nút X)
    public void CloseRankPopup()
    {
        CloseAll();
    }

    public void ShowTop100()
    {
        OpenTop100Tab();
    }

    public void ShowFriendsRank()
    {
        OpenFriendsRankTab();
    }

    private void CloseAll()
    {
        if (rankPopup != null) rankPopup.SetActive(false);
        if (_clickOutsideOverlay != null) _clickOutsideOverlay.SetActive(false);
        BottomMenuManager.Instance?.NotifyClose();
    }

    private void OpenTop100Tab()
    {
        if (rankPopup != null) rankPopup.SetActive(true);
        ClickOutsideOverlay.Show(ref _clickOutsideOverlay, rankPopup, CloseAll);
        SetActiveTab(top100TabPanel, friendsRankTabPanel);
    }

    private void OpenFriendsRankTab()
    {
        if (rankPopup != null) rankPopup.SetActive(true);
        ClickOutsideOverlay.Show(ref _clickOutsideOverlay, rankPopup, CloseAll);
        SetActiveTab(friendsRankTabPanel, top100TabPanel);
    }

    private static void SetActiveTab(GameObject activeTab, GameObject inactiveTab)
    {
        if (activeTab == null)
        {
            Debug.LogWarning("RankController: active tab panel is not assigned.");
            return;
        }

        activeTab.SetActive(true);
        if (inactiveTab != null) inactiveTab.SetActive(false);
    }
}