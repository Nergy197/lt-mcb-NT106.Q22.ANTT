using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.UI.Battle
{
    public class PBS_MoveButton : MonoBehaviour
    {
        public Text moveName;
        public Text ppText;
        public Text typeText;
        public Image background;
        public Button buttonComponent;

        public void Setup(string name, string type, int currentPp, int maxPp, UnityEngine.Events.UnityAction onClick)
        {
            if (moveName != null) moveName.text = name;
            
            if (ppText != null) 
            {
                ppText.text = $"{currentPp}/{maxPp}";
                ppText.color = currentPp == 0 ? Color.red : Color.white;
            }

            if (typeText != null) 
            {
                typeText.text = string.IsNullOrEmpty(type) ? "NORMAL" : type.ToUpper();
                typeText.color = PBS_TypeColors.GetTypeColor(type);
            }

            if (background != null)
            {
                // Thay đổi màu nền nhẹ dựa trên type
                Color bgColor = PBS_TypeColors.GetTypeColor(type);
                bgColor.a = 0.8f; // Hơi trong suốt
                background.color = bgColor;
            }

            if (buttonComponent != null)
            {
                buttonComponent.onClick.RemoveAllListeners();
                if (currentPp > 0)
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
            if (moveName != null) moveName.text = "-";
            if (ppText != null) ppText.text = "";
            if (typeText != null) typeText.text = "";
            if (background != null) background.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            if (buttonComponent != null) 
            {
                buttonComponent.onClick.RemoveAllListeners();
                buttonComponent.interactable = false;
            }
        }
    }
}
