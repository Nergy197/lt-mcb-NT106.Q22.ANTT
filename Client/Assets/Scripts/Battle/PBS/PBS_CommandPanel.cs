using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.UI.Battle
{
    public class PBS_CommandPanel : MonoBehaviour
    {
        public Button fightButton;
        public Button pokemonButton;
        // public Button bagButton; // Chưa dùng tới trong đồ án
        // public Button runButton;

        public delegate void OnActionSelectedHandler(string actionType);
        public event OnActionSelectedHandler OnActionSelected;

        private void Awake()
        {
            if (fightButton != null)
                fightButton.onClick.AddListener(() => OnActionSelected?.Invoke("FIGHT"));
            
            if (pokemonButton != null)
                pokemonButton.onClick.AddListener(() => OnActionSelected?.Invoke("POKEMON"));
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
