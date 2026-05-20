using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý tính exclusivity của các button bottom menu:
/// khi một panel đang mở, các button khác bị disable.
/// Gắn script này lên BottomMenu_Container (hoặc bất kỳ GameObject nào trong scene).
/// </summary>
public class BottomMenuManager : MonoBehaviour
{
    public static BottomMenuManager Instance { get; private set; }

    [Header("Bottom Menu Buttons")]
    public Button mailButton;
    public Button pokedexButton;
    public Button friendsButton;
    public Button rankButton;

    private Button[] AllButtons => new[] { mailButton, pokedexButton, friendsButton, rankButton };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    /// <summary>Gọi khi một panel vừa được mở. Disable tất cả button trừ button đang active.</summary>
    public void NotifyOpen(Button activeButton)
    {
        if (activeButton == null)
        {
            // Button chưa được assign trong Inspector — không disable gì để tránh kẹt
            Debug.LogWarning("[BottomMenuManager] NotifyOpen gọi với activeButton = null. Hãy kéo button vào Inspector.");
            return;
        }
        foreach (var btn in AllButtons)
            if (btn != null)
                btn.interactable = (btn == activeButton);
    }

    /// <summary>Gọi khi panel đã đóng hoàn toàn. Enable lại tất cả button.</summary>
    public void NotifyClose()
    {
        foreach (var btn in AllButtons)
            if (btn != null)
                btn.interactable = true;
    }
}
