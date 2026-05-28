#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PokemonMMO.UI;

/// <summary>
/// Menu: Pokemon → Private Room Panel Builder
/// Tự động tạo toàn bộ hierarchy PrivateRoomPanel trong scene hiện tại.
/// Chạy lại sẽ xoá panel cũ và tạo mới (có Undo).
/// </summary>
public static class PrivateRoomPanelBuilder
{
    // ── Màu sắc ────────────────────────────────────────────────────────────
    private static readonly Color OverlayBg    = new(0f,    0f,    0f,    0.85f);
    private static readonly Color CardBg       = new(0.08f, 0.08f, 0.12f, 0.97f);
    private static readonly Color BtnBlue      = new(0.15f, 0.45f, 0.85f, 1f);
    private static readonly Color BtnGreen     = new(0.15f, 0.65f, 0.30f, 1f);
    private static readonly Color BtnGray      = new(0.30f, 0.30f, 0.35f, 1f);
    private static readonly Color BtnRed       = new(0.75f, 0.20f, 0.20f, 1f);
    private static readonly Color InputBg      = new(0.12f, 0.12f, 0.18f, 1f);

    [MenuItem("Pokemon/Private Room Panel Builder")]
    public static void Build()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[PrivateRoomBuilder] Không tìm thấy Canvas trong scene.");
            return;
        }

        // Xoá panel cũ nếu có
        var existing = canvas.transform.Find("PrivateRoomPanel");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        // ── Root overlay ────────────────────────────────────────────────────
        var root = MakeImage("PrivateRoomPanel", canvas.transform, OverlayBg, fullscreen: true);
        Undo.RegisterCreatedObjectUndo(root, "Create PrivateRoomPanel");
        root.SetActive(false);

        // ── Card trung tâm ──────────────────────────────────────────────────
        var card = MakeImage("Card", root.transform, CardBg, fullscreen: false);
        SetSizeDelta(card, 500, 420);
        SetAnchorCenter(card);

        // ── CreateView ──────────────────────────────────────────────────────
        var createView = MakeContainer("CreateView", card.transform);
        SetFill(createView);

        MakeLabel("Title_Create", createView.transform, "TẠO PHÒNG PRIVATE", 26,
                  TextAlignmentOptions.Center, new Vector2(0.1f, 0.75f), new Vector2(0.9f, 0.95f));

        var codeDisplay = MakeLabel("CodeDisplay", createView.transform, "---", 42,
                                    TextAlignmentOptions.Center, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.72f));
        codeDisplay.GetComponent<TMP_Text>().color = new Color(0.9f, 0.85f, 0.2f);
        codeDisplay.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

        var createRoomBtn    = MakeButton("CreateRoomBtn",    createView.transform, "TẠO PHÒNG",         BtnGreen,
                                          new Vector2(0.15f, 0.22f), new Vector2(0.85f, 0.42f));
        var switchToJoinBtn  = MakeButton("SwitchToJoinBtn",  createView.transform, "Nhập mã thay thế",  BtnGray,
                                          new Vector2(0.20f, 0.04f), new Vector2(0.80f, 0.20f));

        // ── JoinView ────────────────────────────────────────────────────────
        var joinView = MakeContainer("JoinView", card.transform);
        SetFill(joinView);
        joinView.SetActive(false);

        MakeLabel("Title_Join", joinView.transform, "NHẬP MÃ PHÒNG", 26,
                  TextAlignmentOptions.Center, new Vector2(0.1f, 0.75f), new Vector2(0.9f, 0.95f));

        var codeInputGO      = MakeInputField("CodeInput", joinView.transform,
                                              new Vector2(0.10f, 0.50f), new Vector2(0.90f, 0.72f));
        var joinRoomBtn      = MakeButton("JoinRoomBtn",       joinView.transform, "THAM GIA",           BtnBlue,
                                          new Vector2(0.15f, 0.22f), new Vector2(0.85f, 0.42f));
        var switchToCreateBtn= MakeButton("SwitchToCreateBtn", joinView.transform, "Tạo phòng thay thế", BtnGray,
                                          new Vector2(0.20f, 0.04f), new Vector2(0.80f, 0.20f));

        // ── CancelBtn & StatusLabel (trên card, ngoài views) ────────────────
        var cancelBtn   = MakeButton("CancelBtn",    card.transform, "✕ HUỶ", BtnRed,
                                     new Vector2(0.35f, -0.12f), new Vector2(0.65f, -0.02f));
        // đặt cancel ngoài vùng card, anchor bên dưới
        var cancelRt = cancelBtn.GetComponent<RectTransform>();
        cancelRt.anchorMin  = new Vector2(0.35f, -0.13f);
        cancelRt.anchorMax  = new Vector2(0.65f, -0.02f);
        cancelRt.offsetMin  = Vector2.zero;
        cancelRt.offsetMax  = Vector2.zero;

        var statusLabel = MakeLabel("StatusLabel", card.transform, "", 18,
                                    TextAlignmentOptions.Center, new Vector2(0.05f, -0.02f), new Vector2(0.95f, 0.10f));
        statusLabel.GetComponent<TMP_Text>().color = new Color(0.8f, 0.8f, 0.8f);

        // ── Wire PrivateRoomPanel component ─────────────────────────────────
        var panel = root.AddComponent<PrivateRoomPanel>();
        var so    = new SerializedObject(panel);

        so.FindProperty("panel").objectReferenceValue           = root;
        so.FindProperty("createView").objectReferenceValue      = createView;
        so.FindProperty("joinView").objectReferenceValue        = joinView;
        so.FindProperty("codeDisplay").objectReferenceValue     = codeDisplay.GetComponent<TMP_Text>();
        so.FindProperty("createRoomBtn").objectReferenceValue   = createRoomBtn.GetComponent<Button>();
        so.FindProperty("switchToJoinBtn").objectReferenceValue = switchToJoinBtn.GetComponent<Button>();
        so.FindProperty("codeInput").objectReferenceValue       = codeInputGO.GetComponent<TMP_InputField>();
        so.FindProperty("joinRoomBtn").objectReferenceValue     = joinRoomBtn.GetComponent<Button>();
        so.FindProperty("switchToCreateBtn").objectReferenceValue = switchToCreateBtn.GetComponent<Button>();
        so.FindProperty("cancelBtn").objectReferenceValue       = cancelBtn.GetComponent<Button>();
        so.FindProperty("statusLabel").objectReferenceValue     = statusLabel.GetComponent<TMP_Text>();
        so.ApplyModifiedProperties();

        root.transform.SetAsLastSibling();
        EditorUtility.SetDirty(root);

        Debug.Log("[PrivateRoomBuilder] PrivateRoomPanel tạo thành công. " +
                  "Kéo component PrivateRoomPanel vào field 'privateRoomPanel' trong ModeMenu.");
        EditorGUIUtility.PingObject(root);
    }

    // ════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private static GameObject MakeContainer(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static GameObject MakeImage(string name, Transform parent, Color color, bool fullscreen)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        var rt  = go.GetComponent<RectTransform>();
        if (fullscreen)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        return go;
    }

    private static GameObject MakeLabel(string name, Transform parent, string text, int fontSize,
                                        TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text              = text;
        tmp.fontSize          = fontSize;
        tmp.alignment         = align;
        tmp.color             = Color.white;
        tmp.raycastTarget     = false;
        SetAnchors(go, anchorMin, anchorMax);
        return go;
    }

    private static GameObject MakeButton(string name, Transform parent, string label, Color color,
                                         Vector2 anchorMin, Vector2 anchorMax)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        go.AddComponent<Button>();
        SetAnchors(go, anchorMin, anchorMax);

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text          = label;
        tmp.fontSize      = 20;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = Color.white;
        tmp.raycastTarget = false;
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        return go;
    }

    private static GameObject MakeInputField(string name, Transform parent,
                                              Vector2 anchorMin, Vector2 anchorMax)
    {
        var go    = new GameObject(name);
        go.transform.SetParent(parent, false);
        var bg    = go.AddComponent<Image>();
        bg.color  = InputBg;
        var field = go.AddComponent<TMP_InputField>();
        SetAnchors(go, anchorMin, anchorMax);

        // Text area
        var textAreaGO = new GameObject("Text Area");
        textAreaGO.transform.SetParent(go.transform, false);
        var areaRT = textAreaGO.AddComponent<RectTransform>();
        areaRT.anchorMin = Vector2.zero;
        areaRT.anchorMax = Vector2.one;
        areaRT.offsetMin = new Vector2(6, 4);
        areaRT.offsetMax = new Vector2(-6, -4);
        textAreaGO.AddComponent<RectMask2D>();

        // Input text
        var inputTextGO = new GameObject("Text");
        inputTextGO.transform.SetParent(textAreaGO.transform, false);
        var inputTmp = inputTextGO.AddComponent<TextMeshProUGUI>();
        inputTmp.fontSize      = 28;
        inputTmp.alignment     = TextAlignmentOptions.Center;
        inputTmp.color         = Color.white;
        inputTmp.raycastTarget = false;
        var inputTxtRT = inputTextGO.GetComponent<RectTransform>();
        inputTxtRT.anchorMin = Vector2.zero;
        inputTxtRT.anchorMax = Vector2.one;
        inputTxtRT.offsetMin = Vector2.zero;
        inputTxtRT.offsetMax = Vector2.zero;

        // Placeholder
        var phGO   = new GameObject("Placeholder");
        phGO.transform.SetParent(textAreaGO.transform, false);
        var phTmp  = phGO.AddComponent<TextMeshProUGUI>();
        phTmp.text          = "Nhập mã 6 số...";
        phTmp.fontSize      = 22;
        phTmp.alignment     = TextAlignmentOptions.Center;
        phTmp.color         = new Color(0.5f, 0.5f, 0.5f);
        phTmp.raycastTarget = false;
        var phRT = phGO.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero;
        phRT.offsetMax = Vector2.zero;

        // Wire TMP_InputField
        field.textViewport       = areaRT;
        field.textComponent      = inputTmp;
        field.placeholder        = phTmp;
        field.contentType        = TMP_InputField.ContentType.IntegerNumber;
        field.characterLimit     = 6;
        field.pointSize          = 28;

        return go;
    }

    private static void SetAnchors(GameObject go, Vector2 min, Vector2 max)
    {
        var rt     = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetFill(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetAnchorCenter(GameObject go)
    {
        var rt     = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
    }

    private static void SetSizeDelta(GameObject go, float w, float h)
    {
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
    }
}
#endif
