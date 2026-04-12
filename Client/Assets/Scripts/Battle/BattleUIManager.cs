using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject commandMenu; 
    public GameObject fightMenu;   
    public GameObject pokemonMenu; // Thêm ô này để kéo thả bảng Pokemon

    private void Start()
    {
        ShowCommandMenu(); // Bắt đầu luôn hiện bảng 2 nút (Fight/Poke)
    }

    public void ShowCommandMenu()
    {
        commandMenu.SetActive(true);
        fightMenu.SetActive(false);
        pokemonMenu.SetActive(false);
    }

    public void ShowFightMenu()
    {
        commandMenu.SetActive(false);
        fightMenu.SetActive(true);
        pokemonMenu.SetActive(false);
    }

    public void ShowPokemonMenu()
    {
        commandMenu.SetActive(false);
        fightMenu.SetActive(false);
        pokemonMenu.SetActive(true);
    }
}