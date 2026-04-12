using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PokemonMMO.Battle;

namespace PokemonMMO.UI.Battle
{
    public class PBS_PartyPanel : MonoBehaviour
    {
        public PBS_PartyButton[] partyButtons;
        public Button backButton;

        public delegate void OnPokemonSelectedHandler(int partyIndex);
        public event OnPokemonSelectedHandler OnPokemonSelected;

        public delegate void OnBackHandler();
        public event OnBackHandler OnBack;

        private void Awake()
        {
            if (backButton != null)
            {
                backButton.onClick.AddListener(() => OnBack?.Invoke());
            }
        }

        public void Show(List<PartyPokemonSnapshot> partyData, Dictionary<string, Sprite> iconCache = null)
        {
            gameObject.SetActive(true);

            for (int i = 0; i < partyButtons.Length; i++)
            {
                if (i < partyData.Count)
                {
                    int index = i;
                    var data = partyData[i];
                    
                    Sprite icon = null;
                    if (iconCache != null && iconCache.TryGetValue(data.SpeciesId, out var cachedIcon))
                    {
                        icon = cachedIcon;
                    }

                    partyButtons[i].gameObject.SetActive(true);
                    partyButtons[i].Setup(data, icon, () => HandlePokemonClick(index));
                }
                else
                {
                    partyButtons[i].SetEmpty();
                }
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void HandlePokemonClick(int index)
        {
            OnPokemonSelected?.Invoke(index);
        }
    }
}
