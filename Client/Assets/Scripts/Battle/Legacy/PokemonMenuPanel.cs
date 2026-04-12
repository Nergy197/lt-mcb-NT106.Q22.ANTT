using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý layout và tương tác của bảng chọn Pokemon trong battle.
/// Layout: 1 slot tròn (pokemon đầu tiên) + 5 slot chữ nhật (pokemon còn lại) + nút Cancel
/// </summary>
public class PokemonMenuPanel : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    //  Sprite References (kéo vào Inspector)
    // ─────────────────────────────────────────────────────────────────
    [Header("Panel Sprites – Normal")]
    public Sprite panelRound;       // panel_round.png      – slot 0
    public Sprite panelRect;        // panel_rect.png       – slot 1-5

    [Header("Panel Sprites – Selected")]
    public Sprite panelRoundSel;    // panel_round_sel.png
    public Sprite panelRectSel;     // panel_rect_sel.png

    [Header("Panel Sprites – Fainted")]
    public Sprite panelRoundFaint;      // panel_round_faint.png
    public Sprite panelRectFaint;       // panel_rect_faint.png
    public Sprite panelRoundFaintSel;   // panel_round_faint_sel.png
    public Sprite panelRectFaintSel;    // panel_rect_faint_sel.png

    [Header("Panel Sprites – Swap")]
    public Sprite panelRoundSwap;       // panel_round_swap.png
    public Sprite panelRectSwap;        // panel_rect_swap.png
    public Sprite panelRoundSwapSel;    // panel_round_swap_sel.png
    public Sprite panelRectSwapSel;     // panel_rect_swap_sel.png
    public Sprite panelRoundSwapSel2;   // panel_round_swap_sel2.png
    public Sprite panelRectSwapSel2;    // panel_rect_swap_sel2.png

    [Header("Panel Sprites – Empty")]
    public Sprite panelBlank;           // panel_blank.png

    [Header("HP Bar Sprites")]
    public Sprite overlayHpBack;        // overlay_hp_back.png
    public Sprite overlayHpBackFaint;   // overlay_hp_back_faint.png
    public Sprite overlayHpBackSwap;    // overlay_hp_back_swap.png
    public Sprite overlayHp;            // overlay_hp.png  (fill bar)
    public Sprite overlayLv;            // overlay_lv.png

    [Header("Icon Sprites")]
    public Sprite iconBall;             // icon_ball.png
    public Sprite iconBallSel;          // icon_ball_sel.png
    public Sprite iconItem;             // icon_item.png
    public Sprite iconMail;             // icon_mail.png
    public Sprite iconMega;             // icon_mega.png
    public Sprite iconZCrystal;         // icon_zcrystal.png

    [Header("Cancel Button Sprites")]
    public Sprite iconCancel;           // icon_cancel.png
    public Sprite iconCancelSel;        // icon_cancel_sel.png
    public Sprite iconCancelNarrow;     // icon_cancel_narrow.png
    public Sprite iconCancelNarrowSel;  // icon_cancel_narrow_sel.png

    [Header("Background")]
    public Sprite bgSprite;             // bg.png

    // ─────────────────────────────────────────────────────────────────
    //  UI References (kéo vào Inspector)
    // ─────────────────────────────────────────────────────────────────
    [Header("UI References")]
    public Image backgroundImage;

    [Header("Pokemon Slots (0 = round, 1-5 = rect)")]
    public PokemonSlotUI[] slots = new PokemonSlotUI[6];

    [Header("Cancel Button")]
    public Button cancelButton;
    public Image cancelButtonImage;

    [Header("Manager")]
    public BattleUIManager uiManager;

    // ─────────────────────────────────────────────────────────────────
    //  Runtime State
    // ─────────────────────────────────────────────────────────────────
    private int _selectedIndex = -1;
    private int _swapSourceIndex = -1;    // -1 = không đang swap
    private bool _isSwapMode = false;

    // ─────────────────────────────────────────────────────────────────
    //  Enums & Data Classes
    // ─────────────────────────────────────────────────────────────────
    public enum SlotState { Normal, Fainted, Swap }

    [System.Serializable]
    public class PokemonSlotUI
    {
        [Header("Core")]
        public GameObject slotObject;       // root GameObject của slot
        public Image panelImage;            // ảnh nền panel
        public Image pokemonIcon;           // icon pokemon (từ folder Pokemon)

        [Header("HP Bar")]
        public Image hpBackImage;           // overlay_hp_back / faint / swap
        public Image hpFillImage;           // overlay_hp (fill, dùng Image.fillAmount)
        public Image lvOverlayImage;        // overlay_lv

        [Header("Text")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI hpText;      // "HP: 40/40" hoặc "---"

        [Header("Item Icon")]
        public Image itemIconImage;         // icon_item / icon_mail / icon_mega / icon_zcrystal

        [Header("Ball Icon")]
        public Image ballIconImage;         // icon_ball / icon_ball_sel

        [Header("State")]
        [HideInInspector] public SlotState currentState = SlotState.Normal;
        [HideInInspector] public bool isEmpty = true;

        // HP animation
        [HideInInspector] public float currentHpRatio = 1f;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────
    private void Start()
    {
        if (backgroundImage != null && bgSprite != null)
            backgroundImage.sprite = bgSprite;

        for (int i = 0; i < slots.Length; i++)
        {
            int capturedIndex = i;
            if (slots[i]?.slotObject != null)
            {
                Button btn = slots[i].slotObject.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(() => OnSlotClicked(capturedIndex));
            }
        }

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        RefreshAllSlots();
    }

    private void OnEnable()
    {
        _selectedIndex = -1;
        _swapSourceIndex = -1;
        _isSwapMode = false;
        RefreshAllSlots();
        SetCancelButtonSelected(false);

        // THÊM ĐOẠN NÀY ĐỂ TEST UI
        SetupDummyData();
    }

    /// <summary>
    /// Hàm tạo dữ liệu giả để kiểm tra UI. 
    /// Sau này khi có kết nối Server thành công, hãy xóa dòng SetupDummyData() ở OnEnable đi.
    /// </summary>
    private void SetupDummyData()
    {
        // Slot 0 - Pokémon chính (Round slot) - Đang khỏe mạnh
        SetSlotData(0, null, "Pikachu", 25, 50, 50, SlotState.Normal);
        
        // Slot 1 - Pokémon phụ 1 (Rect slot) - HP thấp (màu vàng)
        SetSlotData(1, null, "Charmander", 18, 10, 40, SlotState.Normal);
        
        // Slot 2 - Pokémon phụ 2 - Bị ngất (màu xám / Fainted)
        SetSlotData(2, null, "Bulbasaur", 15, 0, 35, SlotState.Fainted);

        // Các slot còn lại để trống hoàn toàn (panel_blank)
        for (int i = 3; i < slots.Length; i++)
        {
            ClearSlot(i);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Public API – gán dữ liệu Pokemon vào slot
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Nạp dữ liệu pokemon vào một slot cụ thể.
    /// </summary>
    public void SetSlotData(int index, Sprite pokemonSprite, string pokemonName,
                            int level, int currentHp, int maxHp,
                            SlotState state = SlotState.Normal,
                            Sprite itemSprite = null)
    {
        if (index < 0 || index >= slots.Length) return;
        var slot = slots[index];
        if (slot == null) return;

        slot.isEmpty = false;
        slot.currentState = state;
        slot.currentHpRatio = maxHp > 0 ? (float)currentHp / maxHp : 0f;

        if (slot.pokemonIcon != null)
        {
            slot.pokemonIcon.sprite = pokemonSprite;
            slot.pokemonIcon.enabled = pokemonSprite != null;
        }
        if (slot.nameText != null)   slot.nameText.text = pokemonName;
        if (slot.levelText != null)  slot.levelText.text = $"Lv.{level}";
        if (slot.hpText != null)
            slot.hpText.text = state == SlotState.Fainted ? "---" : $"{currentHp}/{maxHp}";

        if (slot.itemIconImage != null)
        {
            slot.itemIconImage.sprite = itemSprite;
            slot.itemIconImage.enabled = itemSprite != null;
        }

        RefreshSlotVisual(index, isSelected: _selectedIndex == index);
    }

    /// <summary>
    /// Đánh dấu slot trống (panel_blank).
    /// </summary>
    public void ClearSlot(int index)
    {
        if (index < 0 || index >= slots.Length) return;
        var slot = slots[index];
        if (slot == null) return;

        slot.isEmpty = true;
        slot.currentState = SlotState.Normal;
        if (slot.panelImage != null) slot.panelImage.sprite = panelBlank;
        if (slot.pokemonIcon != null) slot.pokemonIcon.enabled = false;
        if (slot.nameText != null)   slot.nameText.text = string.Empty;
        if (slot.levelText != null)  slot.levelText.text = string.Empty;
        if (slot.hpText != null)     slot.hpText.text = string.Empty;
        if (slot.hpFillImage != null) slot.hpFillImage.fillAmount = 0f;
        if (slot.itemIconImage != null) slot.itemIconImage.enabled = false;
        if (slot.ballIconImage != null) slot.ballIconImage.enabled = false;
        if (slot.hpBackImage != null) slot.hpBackImage.enabled = false;
        if (slot.lvOverlayImage != null) slot.lvOverlayImage.enabled = false;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Click Handlers
    // ─────────────────────────────────────────────────────────────────
    private void OnSlotClicked(int index)
    {
        var slot = slots[index];
        if (slot == null || slot.isEmpty) return;

        if (_isSwapMode)
        {
            HandleSwapSelection(index);
            return;
        }

        if (_selectedIndex == index)
        {
            // Click lần 2 vào slot đang chọn → deselect
            _selectedIndex = -1;
        }
        else
        {
            _selectedIndex = index;
        }

        RefreshAllSlots();
    }

    private void HandleSwapSelection(int index)
    {
        if (index == _swapSourceIndex)
        {
            // Huỷ swap
            _isSwapMode = false;
            _swapSourceIndex = -1;
            _selectedIndex = -1;
        }
        else
        {
            // Hoàn tất swap (logic thực tế sẽ do BattleManager xử lý)
            Debug.Log($"[PokemonMenuPanel] Swap slot {_swapSourceIndex} ↔ slot {index}");
            _isSwapMode = false;
            _swapSourceIndex = -1;
            _selectedIndex = -1;
        }

        RefreshAllSlots();
    }

    /// <summary>
    /// Bắt đầu chế độ swap – gọi từ bên ngoài (vd: BattleManager).
    /// </summary>
    public void EnterSwapMode(int sourceIndex)
    {
        _isSwapMode = true;
        _swapSourceIndex = sourceIndex;
        _selectedIndex = -1;
        RefreshAllSlots();
    }

    private void OnCancelClicked()
    {
        _selectedIndex = -1;
        _isSwapMode = false;
        _swapSourceIndex = -1;
        if (uiManager != null)
            uiManager.ShowCommandMenu();
    }

    // ─────────────────────────────────────────────────────────────────
    //  Visual Refresh
    // ─────────────────────────────────────────────────────────────────
    private void RefreshAllSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            if (slots[i].isEmpty)
            {
                ClearSlot(i);
                continue;
            }

            bool isSelected = _selectedIndex == i;
            bool isSwapSrc  = _swapSourceIndex == i;
            bool isSwapTarget = _isSwapMode && !isSwapSrc;

            // Trong swap mode: source dùng Swap, target dùng SwapSel2
            if (_isSwapMode)
            {
                if (isSwapSrc)
                    RefreshSlotVisual(i, isSelected: true, forceSwap: true, isSwapSrc: true);
                else
                    RefreshSlotVisual(i, isSelected: false, forceSwap: true, isSwapSrc: false);
            }
            else
            {
                RefreshSlotVisual(i, isSelected: isSelected);
            }
        }

        // Cancel button
        SetCancelButtonSelected(_selectedIndex == -1 && !_isSwapMode);
    }

    private void RefreshSlotVisual(int index, bool isSelected,
                                   bool forceSwap = false, bool isSwapSrc = false)
    {
        var slot = slots[index];
        if (slot == null || slot.isEmpty) return;

        bool isRound = index == 0;

        // ── Chọn panel sprite ──
        Sprite panelSp;
        Sprite hpBackSp;

        if (forceSwap)
        {
            if (isSwapSrc)
            {
                panelSp  = isRound ? panelRoundSwapSel  : panelRectSwapSel;
                hpBackSp = overlayHpBackSwap;
            }
            else
            {
                panelSp  = isRound ? panelRoundSwapSel2 : panelRectSwapSel2;
                hpBackSp = overlayHpBackSwap;
            }
        }
        else
        {
            switch (slot.currentState)
            {
                case SlotState.Fainted:
                    panelSp  = isSelected
                        ? (isRound ? panelRoundFaintSel : panelRectFaintSel)
                        : (isRound ? panelRoundFaint    : panelRectFaint);
                    hpBackSp = overlayHpBackFaint;
                    break;

                case SlotState.Swap:
                    panelSp  = isSelected
                        ? (isRound ? panelRoundSwapSel  : panelRectSwapSel)
                        : (isRound ? panelRoundSwap     : panelRectSwap);
                    hpBackSp = overlayHpBackSwap;
                    break;

                default: // Normal
                    panelSp  = isSelected
                        ? (isRound ? panelRoundSel : panelRectSel)
                        : (isRound ? panelRound    : panelRect);
                    hpBackSp = overlayHpBack;
                    break;
            }
        }

        if (slot.panelImage  != null) slot.panelImage.sprite  = panelSp;
        if (slot.hpBackImage != null)
        {
            slot.hpBackImage.sprite  = hpBackSp;
            slot.hpBackImage.enabled = true;
        }

        // ── HP fill ──
        if (slot.hpFillImage != null)
        {
            slot.hpFillImage.sprite      = overlayHp;
            slot.hpFillImage.fillAmount  = slot.currentHpRatio;
            slot.hpFillImage.color       = GetHpColor(slot.currentHpRatio);
            slot.hpFillImage.enabled     = slot.currentState != SlotState.Fainted;
        }

        // ── Level overlay ──
        if (slot.lvOverlayImage != null)
        {
            slot.lvOverlayImage.sprite  = overlayLv;
            slot.lvOverlayImage.enabled = slot.currentState != SlotState.Fainted;
        }

        // ── Ball icon ──
        if (slot.ballIconImage != null)
        {
            slot.ballIconImage.sprite  = isSelected ? iconBallSel : iconBall;
            slot.ballIconImage.enabled = true;
        }
    }

    private static Color GetHpColor(float ratio)
    {
        if (ratio > 0.5f) return new Color(0.12f, 0.78f, 0.24f);   // xanh lá
        if (ratio > 0.2f) return new Color(0.95f, 0.78f, 0.08f);   // vàng
        return new Color(0.90f, 0.18f, 0.18f);                      // đỏ
    }

    private void SetCancelButtonSelected(bool selected)
    {
        if (cancelButtonImage == null) return;
        cancelButtonImage.sprite = selected ? iconCancelSel : iconCancel;
    }

    // ─────────────────────────────────────────────────────────────────
    //  HP Animation (gọi từ BattleManager sau khi nhận damage)
    // ─────────────────────────────────────────────────────────────────
    public void AnimateHp(int slotIndex, float targetRatio, float duration = 1f)
    {
        var slot = slots[slotIndex];
        if (slot?.hpFillImage == null) return;
        StartCoroutine(HpAnimCoroutine(slot, targetRatio, duration));
    }

    private IEnumerator HpAnimCoroutine(PokemonSlotUI slot, float targetRatio, float duration)
    {
        float startRatio = slot.currentHpRatio;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            slot.currentHpRatio = Mathf.Lerp(startRatio, targetRatio, t);

            if (slot.hpFillImage != null)
            {
                slot.hpFillImage.fillAmount = slot.currentHpRatio;
                slot.hpFillImage.color = GetHpColor(slot.currentHpRatio);
            }
            yield return null;
        }

        slot.currentHpRatio = targetRatio;
    }
}
