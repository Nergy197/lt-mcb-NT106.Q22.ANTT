using UnityEngine;
using UnityEngine.UI;

public class MenuBackButton : MonoBehaviour
{
    public Button backButton;           // Kéo nút Back vào đây
    public BattleUIManager uiManager;   // Kéo BattleUIManager vào đây

    private void Start()
    {
        if (backButton != null)
        {
            // Khi bấm nút này, bảo UI Manager hiện lại Command Menu
            backButton.onClick.AddListener(() => uiManager.ShowCommandMenu());
        }
    }
}