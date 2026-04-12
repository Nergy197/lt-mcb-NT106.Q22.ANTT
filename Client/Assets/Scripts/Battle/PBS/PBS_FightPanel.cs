using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PokemonMMO.Battle;

namespace PokemonMMO.UI.Battle
{
    public class PBS_FightPanel : MonoBehaviour
    {
        [Header("Move Buttons")]
        public PBS_MoveButton[] moveButtons; // Mảng 4 nút

        [Header("Special Feature")]
        public Button specialButton; // Mega/Dynamax
        
        [Header("Move Info Details")]
        public TextMeshProUGUI moveInfoText;

        public delegate void OnMoveSelectedHandler(int moveSlot);
        public event OnMoveSelectedHandler OnMoveSelected;

        private List<MoveDisplayInfo> _currentMoves;

        public void Show(List<MoveDisplayInfo> moves, bool hasMega, bool hasDynamax)
        {
            _currentMoves = moves;
            gameObject.SetActive(true);
            SetMoves(moves);
            SetSpecialButton(hasMega, hasDynamax);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetMoves(List<MoveDisplayInfo> moves)
        {
            for (int i = 0; i < moveButtons.Length; i++)
            {
                if (i < moves.Count)
                {
                    int slot = i; // capture loop variable
                    MoveDisplayInfo m = moves[i];
                    moveButtons[i].Setup(
                        m.Name, 
                        m.Type, 
                        m.CurrentPp, 
                        m.MaxPp, 
                        () => HandleMoveClick(slot)
                    );
                }
                else
                {
                    moveButtons[i].SetEmpty();
                }
            }
        }

        public void SetSpecialButton(bool hasMega, bool hasDynamax)
        {
            if (specialButton != null)
            {
                specialButton.gameObject.SetActive(hasMega || hasDynamax);
                // Có thể đổi text/icon dựa trên loại cơ chế
            }
        }

        public void HighlightMove(int index)
        {
            if (_currentMoves == null || index < 0 || index >= _currentMoves.Count)
            {
                if (moveInfoText != null) moveInfoText.text = "";
                return;
            }

            var m = _currentMoves[index];
            if (moveInfoText != null)
            {
                string power = m.Power > 0 ? m.Power.ToString() : "-";
                string acc = m.Accuracy > 0 ? $"{m.Accuracy}%" : "-";
                string cat = string.IsNullOrEmpty(m.Category) ? "Status" : m.Category;
                
                moveInfoText.text = $"{m.Type.ToUpper()} | {cat.ToUpper()} | PWR: {power} | ACC: {acc}";
            }
        }

        private void HandleMoveClick(int slot)
        {
            HighlightMove(slot); // Update UI text
            OnMoveSelected?.Invoke(slot);
        }
    }
}
