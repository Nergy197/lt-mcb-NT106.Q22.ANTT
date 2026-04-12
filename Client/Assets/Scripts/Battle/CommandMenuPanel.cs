using UnityEngine;
using UnityEngine.UI;

public class CommandMenuPanel : MonoBehaviour
{
    [Header("Buttons")]
    public Button fightButton;    // Nút Fight
    public Button pokemonButton; // Nút Pokemon

    [Header("Manager References")]
    public BattleUIManager uiManager;

    private void Start()
    {
        // Gán sự kiện cho 2 nút chính
        if (fightButton != null)
            fightButton.onClick.AddListener(() => uiManager.ShowFightMenu());

        if (pokemonButton != null)
            pokemonButton.onClick.AddListener(() => OnPokemonButtonClicked());
    }

    private void OnPokemonButtonClicked()
    {
        Debug.Log("Mở bảng chọn Pokémon...");
        if (uiManager != null)
        {
            uiManager.ShowPokemonMenu();
        }
    }
}