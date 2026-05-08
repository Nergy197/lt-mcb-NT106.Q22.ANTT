using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Game.Battle.UI;

namespace Game.Battle.Demo
{
    /// <summary>
    /// Keyboard/gamepad navigation cho toàn bộ Battle UI.
    ///
    /// Keybindings:
    ///   W/A/S/D hoặc Arrow  → di chuyển con trỏ
    ///   Enter / Space / Z   → xác nhận
    ///   Escape / X          → quay lại / huỷ
    ///   Q                   → toggle Tera (chỉ khi đang ở SkillPanel)
    ///
    /// Gắn script này vào bất kỳ active GameObject nào trong scene
    /// (ví dụ: cùng GameObject với BattleDemoSimulator).
    /// </summary>
    public class BattleInputController : MonoBehaviour
    {
        [Header("Màu highlight (button đang được chọn)")]
        public Color selectedColor = new Color(1f, 0.85f, 0.15f);

        // ── Panel refs ──────────────────────────────────────────────────────

        BattleCommandPanel _cmd;
        BattleSkillPanel   _skill;
        BattlePartyPanel   _party;

        // ── Navigation state ────────────────────────────────────────────────

        BattlePanelType _activePanel = BattlePanelType.None;
        Button[]        _btns;
        int[][]         _grid;   // [row][col] = index in _btns, -1 = empty cell
        int             _row, _col;

        readonly Dictionary<Button, ColorBlock> _savedColors = new();

        // ── Unity Lifecycle ─────────────────────────────────────────────────

        void Awake()
        {
            _cmd   = FindObjectOfType<BattleCommandPanel>(true);
            _skill = FindObjectOfType<BattleSkillPanel>(true);
            _party = FindObjectOfType<BattlePartyPanel>(true);
        }

        void OnEnable()  => BattleEvents.OnPanelChanged += SetupForPanel;
        void OnDisable()
        {
            BattleEvents.OnPanelChanged -= SetupForPanel;
            ClearHighlight();
        }

        // ── Panel setup ─────────────────────────────────────────────────────

        void SetupForPanel(BattlePanelType type)
        {
            ClearHighlight();
            _activePanel = type;
            _row = 0; _col = 0;
            _btns = null; _grid = null;

            switch (type)
            {
                case BattlePanelType.Command:
                    // ┌──────┬──────┐
                    // │FIGHT │ PKMN │
                    // ├──────┼──────┤
                    // │ INFO │  –   │
                    // └──────┴──────┘
                    var cmdList = new List<Button>();
                    if (_cmd != null)
                    {
                        if (_cmd.fightButton   != null) cmdList.Add(_cmd.fightButton);
                        if (_cmd.pokemonButton != null) cmdList.Add(_cmd.pokemonButton);
                        if (_cmd.infoButton    != null) cmdList.Add(_cmd.infoButton);
                    }
                    _btns = cmdList.ToArray();
                    _grid = new[] { new[] { 0, 1 }, new[] { 2, -1 } };
                    break;

                case BattlePanelType.Skill:
                    // ┌────────┬────────┐
                    // │ Move 0 │ Move 1 │
                    // ├────────┼────────┤
                    // │ Move 2 │ Move 3 │
                    // └────────┴────────┘
                    _btns = _skill?.skillButtons ?? new Button[0];
                    _grid = new[] { new[] { 0, 1 }, new[] { 2, 3 } };
                    break;

                case BattlePanelType.Party:
                    // danh sách dọc: Slot 0, 1, 2, 3
                    _btns = _party?.partyButtons ?? new Button[0];
                    _grid = null; // linear vertical
                    break;

                default:
                    return; // Dialog, TeamPreview, Result, None → không điều hướng bằng phím
            }

            // Tìm ô đầu tiên hợp lệ
            FindFirstValid();
            Highlight();
        }

        // ── Update ──────────────────────────────────────────────────────────

        void Update()
        {
            if (_btns == null || _btns.Length == 0) return;

            bool up    = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
            bool down  = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
            bool left  = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
            bool right = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
            bool ok    = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z);
            bool back  = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X);
            bool tera  = Input.GetKeyDown(KeyCode.Q);

            if (up)    Navigate(0, -1);
            if (down)  Navigate(0, +1);
            if (left)  Navigate(-1, 0);
            if (right) Navigate(+1, 0);
            if (ok)    Confirm();
            if (back)  Cancel();
            if (tera && _activePanel == BattlePanelType.Skill) ToggleTera();
        }

        // ── Navigation ──────────────────────────────────────────────────────

        void Navigate(int dx, int dy)
        {
            if (_grid != null)
            {
                int rows = _grid.Length;
                int cols = _grid[0].Length;
                int r = _row, c = _col;

                for (int i = 0; i < rows * cols; i++)
                {
                    r = (r + dy + rows) % rows;
                    c = (c + dx + cols) % cols;
                    if (IsValidCell(r, c)) break;
                }

                if (IsValidCell(r, c) && (r != _row || c != _col))
                {
                    ClearHighlight();
                    _row = r; _col = c;
                    Highlight();
                }
            }
            else
            {
                // Linear (PartyPanel): chỉ up/down
                if (dy == 0) return;
                int n = _btns.Length, next = _row;
                for (int i = 0; i < n; i++)
                {
                    next = (next + dy + n) % n;
                    if (_btns[next] != null && _btns[next].interactable && _btns[next].gameObject.activeSelf) break;
                }
                if (next != _row)
                {
                    ClearHighlight();
                    _row = next;
                    Highlight();
                }
            }
        }

        void FindFirstValid()
        {
            if (_btns == null) return;
            if (_grid != null)
            {
                for (int r = 0; r < _grid.Length; r++)
                    for (int c = 0; c < _grid[r].Length; c++)
                        if (IsValidCell(r, c)) { _row = r; _col = c; return; }
            }
            else
            {
                for (int i = 0; i < _btns.Length; i++)
                    if (_btns[i] != null && _btns[i].interactable && _btns[i].gameObject.activeSelf) { _row = i; return; }
            }
        }

        bool IsValidCell(int r, int c)
        {
            if (_grid == null || r < 0 || r >= _grid.Length) return false;
            if (c < 0 || c >= _grid[r].Length) return false;
            int idx = _grid[r][c];
            if (idx < 0 || _btns == null || idx >= _btns.Length) return false;
            var btn = _btns[idx];
            return btn != null && btn.interactable && btn.gameObject.activeSelf;
        }

        // ── Actions ─────────────────────────────────────────────────────────

        void Confirm()
        {
            var btn = CurrentButton();
            if (btn != null && btn.isActiveAndEnabled && btn.interactable)
                btn.onClick.Invoke();
        }

        void Cancel()
        {
            if (_activePanel == BattlePanelType.Skill && _skill?.backButton != null)
                _skill.backButton.onClick.Invoke();
            else if (_activePanel == BattlePanelType.Party
                     && _party?.backButton != null
                     && _party.backButton.gameObject.activeSelf)
                _party.backButton.onClick.Invoke();
        }

        void ToggleTera()
        {
            if (_skill?.teraButton != null && _skill.teraButton.gameObject.activeSelf)
                _skill.teraButton.onClick.Invoke();
        }

        // ── Highlight ───────────────────────────────────────────────────────

        void Highlight()
        {
            var btn = CurrentButton();
            if (btn == null) return;
            if (!_savedColors.ContainsKey(btn))
                _savedColors[btn] = btn.colors;
            var cb = btn.colors;
            cb.normalColor      = selectedColor;
            cb.highlightedColor = selectedColor;
            btn.colors = cb;
        }

        void ClearHighlight()
        {
            foreach (var kv in _savedColors)
                if (kv.Key != null) kv.Key.colors = kv.Value;
            _savedColors.Clear();
        }

        Button CurrentButton()
        {
            if (_btns == null) return null;
            int idx = _grid != null ? _grid[_row][_col] : _row;
            if (idx < 0 || idx >= _btns.Length) return null;
            return _btns[idx];
        }
    }
}
