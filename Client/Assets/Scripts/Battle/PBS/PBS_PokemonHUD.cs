using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.UI.Battle
{
    public class PBS_PokemonHUD : MonoBehaviour
    {
        [Header("Text Fields")]
        public Text nameTxt;
        public Text lvlTxt;
        public Text hpTxt;

        [Header("HP Bar")]
        public Image hpBar;
        public Color hpHigh = Color.green;
        public Color hpMed  = Color.yellow;
        public Color hpLow  = Color.red;
        public float hpDrainAnimationDuration = 0.3f;

        [Header("Status & Stats")]
        public GameObject statusBadge;
        public Text statusText;
        public GameObject statFlashObj;
        public Text statFlashText;

        private int _maxHp;
        private int _currentHp;

        public void Initialize(string pokemonName, int maxHp, int currentHp, int level, string status)
        {
            nameTxt.text = pokemonName;
            if (lvlTxt != null) lvlTxt.text = $"Lv.{level}";
            _maxHp = maxHp;
            _currentHp = currentHp;

            UpdateHUDInstant();
            UpdateStatus(status);

            if (statFlashObj != null) statFlashObj.SetActive(false);
        }

        public void UpdateHP(int currentHp, int maxHp)
        {
            _maxHp = maxHp;
            StartCoroutine(AnimateHPDrain(_currentHp, currentHp));
            _currentHp = currentHp;
        }

        public void UpdateStatus(string statusName)
        {
            if (string.IsNullOrEmpty(statusName) || statusName.ToUpper() == "NONE")
            {
                if (statusBadge != null) statusBadge.SetActive(false);
            }
            else
            {
                if (statusBadge != null) statusBadge.SetActive(true);
                if (statusText != null) statusText.text = statusName.ToUpper();
            }
        }

        public IEnumerator FlashStatChange(string statName, int stages)
        {
            if (statFlashObj == null || statFlashText == null) yield break;

            string sign = stages > 0 ? "+" : "";
            statFlashText.text = $"{sign}{stages} {statName}";
            
            statFlashObj.SetActive(true);
            yield return new WaitForSeconds(0.8f);
            statFlashObj.SetActive(false);
        }

        private void UpdateHUDInstant()
        {
            float ratio = _maxHp > 0 ? (float)_currentHp / _maxHp : 0;
            if (hpBar != null)
            {
                hpBar.fillAmount = ratio;
                hpBar.color = GetHPColor(ratio);
            }

            if (hpTxt != null)
            {
                hpTxt.text = $"{_currentHp}/{_maxHp}";
            }
        }

        private IEnumerator AnimateHPDrain(int fromHp, int toHp)
        {
            float elapsedTime = 0f;
            while (elapsedTime < hpDrainAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / hpDrainAnimationDuration);
                
                int animatedHp = Mathf.RoundToInt(Mathf.Lerp(fromHp, toHp, t));
                float ratio = _maxHp > 0 ? (float)animatedHp / _maxHp : 0;

                if (hpBar != null)
                {
                    hpBar.fillAmount = ratio;
                    hpBar.color = GetHPColor(ratio);
                }

                if (hpTxt != null)
                {
                    hpTxt.text = $"{animatedHp}/{_maxHp}";
                }

                yield return null;
            }

            // Ensure exact target at the end
            _currentHp = toHp;
            UpdateHUDInstant();
        }

        private Color GetHPColor(float ratio)
        {
            if (ratio > 0.5f) return hpHigh;
            if (ratio > 0.2f) return hpMed; 
            // VGC usually considers < 20% or 25% as red, setting to 0.2 for now
            return hpLow;
        }
    }
}
