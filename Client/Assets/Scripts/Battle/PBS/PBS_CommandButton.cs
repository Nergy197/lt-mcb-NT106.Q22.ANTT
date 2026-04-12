using UnityEngine;
using UnityEngine.UI;

namespace PokemonMMO.UI.Battle
{
    public class PBS_CommandButton : MonoBehaviour
    {
        public Text labelText;

        public void SetLabel(string text)
        {
            if (labelText != null) labelText.text = text;
        }
    }
}
