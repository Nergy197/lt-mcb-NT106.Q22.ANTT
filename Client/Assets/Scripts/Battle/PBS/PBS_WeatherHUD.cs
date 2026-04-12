using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.UI.Battle
{
    public class PBS_WeatherHUD : MonoBehaviour
    {
        [Header("UI Components")]
        public Image weatherIcon;
        public TextMeshProUGUI weatherNameText;
        public TextMeshProUGUI turnsText;

        [Header("Weather Sprites Maps")]
        public Sprite sunSprite;
        public Sprite rainSprite;
        public Sprite sandstormSprite;
        public Sprite hailSprite;

        public void UpdateWeather(string weatherCondition, int turnsLeft)
        {
            if (string.IsNullOrEmpty(weatherCondition) || weatherCondition.ToUpper() == "NONE")
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            
            weatherNameText.text = weatherCondition.ToUpper();
            
            if (turnsLeft > 0)
            {
                turnsText.text = $"{turnsLeft} turns left";
            }
            else if (turnsLeft == -1) // Ví dụ: -1 là thời tiết vĩnh viễn (như từ Ability)
            {
                 turnsText.text = "";
            }
            else 
            {
                turnsText.text = "";
            }

            // Gán logic icon
            switch (weatherCondition.ToUpper())
            {
                case "SUN":
                case "SUNNY":
                    weatherIcon.sprite = sunSprite;
                    break;
                case "RAIN":
                case "RAINY":
                    weatherIcon.sprite = rainSprite;
                    break;
                case "SANDSTORM":
                    weatherIcon.sprite = sandstormSprite;
                    break;
                case "HAIL":
                case "SNOW": // Gen 9 đổi hail thành snow
                    weatherIcon.sprite = hailSprite;
                    break;
                default:
                    weatherIcon.sprite = null;
                    break;
            }
            
            if (weatherIcon.sprite == null)
            {
                weatherIcon.gameObject.SetActive(false);
            }
            else
            {
                weatherIcon.gameObject.SetActive(true);
            }
        }
    }
}
