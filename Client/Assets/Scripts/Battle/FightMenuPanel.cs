using UnityEngine;
using UnityEngine.UI;

public class FightMenuPanel : MonoBehaviour
{
    [Header("Buttons")]
    public Button backButton; // Nút quay lại

    [Header("Manager References")]
    public BattleUIManager uiManager;

    private void Start()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
        }
    }

    private void OnBackButtonClicked()
    {
        // Bấm Back thì bảo sếp hiện lại menu chính
        if (uiManager != null)
        {
            uiManager.ShowCommandMenu();
        }
    }
}