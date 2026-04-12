using UnityEngine;
using UnityEngine.UI;
using PokemonMMO.Battle;

namespace PokemonMMO.UI.Battle
{
    public class PBS_PartyButton : MonoBehaviour
    {
        public Image pokemonIcon;
        public Text nameText;
        public Text levelText;
        public Text hpText;
        
        [Header("HP & Status")]
        public Image hpBar;
        public Image statusBadge;
        public Text statusText;
        public GameObject faintedOverlay;
        
        [Header("Interaction")]
        public Button buttonComponent;

        public void Setup(PartyPokemonSnapshot snapshot, Sprite iconSprite, UnityEngine.Events.UnityAction onClick)
        {
            if (pokemonIcon != null && iconSprite != null) pokemonIcon.sprite = iconSprite;
            
            string displayName = string.IsNullOrEmpty(snapshot.Nickname) ? $"Pokemon {snapshot.SpeciesId}" : snapshot.Nickname;
            if (nameText != null) nameText.text = displayName;
            if (levelText != null) levelText.text = $"Lv.{snapshot.Level}";
            if (hpText != null) hpText.text = $"{snapshot.CurrentHp}/{snapshot.MaxHp}";

            // HP Bar
            if (hpBar != null)
            {
                float ratio = snapshot.MaxHp > 0 ? (float)snapshot.CurrentHp / snapshot.MaxHp : 0;
                hpBar.fillAmount = ratio;
                hpBar.color = GetHPColor(ratio);
            }

            // Status Badge
            if (statusBadge != null && statusText != null)
            {
                if (string.IsNullOrEmpty(snapshot.StatusCondition) || snapshot.StatusCondition.ToUpper() == "NONE")
                {
                    statusBadge.gameObject.SetActive(false);
                }
                else
                {
                    statusBadge.gameObject.SetActive(true);
                    statusText.text = GetStatusShortName(snapshot.StatusCondition);
                }
            }

            // Fainted state
            if (faintedOverlay != null) faintedOverlay.SetActive(snapshot.IsFainted);

            // Button Action
            if (buttonComponent != null)
            {
                buttonComponent.onClick.RemoveAllListeners();
                
                // VGC luật: không thể đổi sang con đã chết
                if (!snapshot.IsFainted) 
                {
                    buttonComponent.interactable = true;
                    buttonComponent.onClick.AddListener(onClick);
                }
                else
                {
                    buttonComponent.interactable = false;
                }
            }
        }

        public void SetEmpty()
        {
            gameObject.SetActive(false);
        }

        private Color GetHPColor(float ratio)
        {
            if (ratio > 0.5f) return Color.green;
            if (ratio > 0.2f) return Color.yellow;
            return Color.red;
        }

        private string GetStatusShortName(string fullStatus)
        {
            switch (fullStatus.ToUpper())
            {
                case "BURN": return "BRN";
                case "PARALYSIS": return "PAR";
                case "POISON": return "PSN";
                case "TOXIC": return "TOX";
                case "SLEEP": return "SLP";
                case "FREEZE": return "FRZ";
                default: return fullStatus.ToUpper();
            }
        }
    }
}
