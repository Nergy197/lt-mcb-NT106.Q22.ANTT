using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Game.Battle.UI;

public class BattleUIHelper : EditorWindow
{
    // ═══════════════════════════════════════════════════════════════════════
    // Result Panel
    // ═══════════════════════════════════════════════════════════════════════

    [MenuItem("Tools/Pokemon MMO/Create Battle Result Panel")]
    public static void CreateBattleResultPanel()
    {
        // 1. Tìm parent canvas — ưu tiên BATTLE CANVAS, fallback Canvas bất kỳ
        GameObject canvas = GameObject.Find("BATTLE CANVAS") ?? GameObject.Find("Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error",
                "Không tìm thấy 'BATTLE CANVAS' trong scene.\nHãy mở Battle scene trước.", "OK");
            return;
        }

        if (GameObject.Find("ResultPanel") != null)
        {
            EditorUtility.DisplayDialog("Đã tồn tại",
                "'ResultPanel' đã có trong scene rồi.", "OK");
            return;
        }

        // ── Root panel ───────────────────────────────────────────────────────
        GameObject root = new GameObject("ResultPanel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        Image rootImg = root.GetComponent<Image>();
        rootImg.color = new Color(0f, 0f, 0f, 0.85f);   // overlay tối
        root.SetActive(false);                            // ẩn mặc định

        // ── Content box (giữa màn hình) ──────────────────────────────────────
        GameObject box = new GameObject("ContentBox", typeof(RectTransform));
        box.transform.SetParent(root.transform, false);

        RectTransform boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot     = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(480, 340);
        boxRt.anchoredPosition = Vector2.zero;

        // Nền hộp
        box.AddComponent<CanvasRenderer>();
        Image boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // ── Result text (CHIEN THANG / THAT BAI) ────────────────────────────
        TextMeshProUGUI resultTmp = MakeTMP(box.transform, "ResultText",
            "CHIEN THANG!", 52, TextAlignmentOptions.Center,
            new Vector2(0f, 80f), new Vector2(440, 80));

        // ── VP delta ─────────────────────────────────────────────────────────
        TextMeshProUGUI vpTmp = MakeTMP(box.transform, "VPDeltaText",
            "+200 VP", 36, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(440, 56));

        // ── Rank delta ───────────────────────────────────────────────────────
        TextMeshProUGUI rankTmp = MakeTMP(box.transform, "RankDeltaText",
            "+84 RP", 36, TextAlignmentOptions.Center,
            new Vector2(0f, -56f), new Vector2(440, 56));

        // ── Return button ────────────────────────────────────────────────────
        GameObject btnGo = new GameObject("ReturnBtn",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(box.transform, false);

        RectTransform btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.pivot     = new Vector2(0.5f, 0.5f);
        btnRt.sizeDelta = new Vector2(220, 56);
        btnRt.anchoredPosition = new Vector2(0f, -136f);

        btnGo.GetComponent<Image>().color = new Color(0.18f, 0.38f, 0.18f, 1f);

        MakeTMP(btnGo.transform, "Text",
            "VE MENU", 28, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.zero, stretch: true);

        // ── Gắn BattleResultPanel component ─────────────────────────────────
        var panel = root.AddComponent<BattleResultPanel>();
        panel.PanelType    = BattlePanelType.Result;
        panel.resultText   = resultTmp;
        panel.vpDeltaText  = vpTmp;
        panel.rankDeltaText = rankTmp;
        panel.returnButton  = btnGo.GetComponent<Button>();

        // ── Đăng ký vào BattleUIManager.panels ──────────────────────────────
        var uiManager = Object.FindFirstObjectByType<BattleUIManager>();
        if (uiManager != null)
        {
            if (uiManager.panels == null) uiManager.panels = new System.Collections.Generic.List<BasePanel>();
            if (!uiManager.panels.Contains(panel))
                uiManager.panels.Add(panel);
            EditorUtility.SetDirty(uiManager);
        }

        // ── Undo & chọn ─────────────────────────────────────────────────────
        Undo.RegisterCreatedObjectUndo(root, "Create Battle Result Panel");
        EditorUtility.SetDirty(root);
        Selection.activeGameObject = root;

        string uiMsg = uiManager != null
            ? "Đã thêm vào BattleUIManager.panels tự động."
            : "Chưa tìm thấy BattleUIManager — hãy kéo ResultPanel vào danh sách panels thủ công.";

        EditorUtility.DisplayDialog("Thành công",
            $"Đã tạo ResultPanel trong '{canvas.name}'.\n\n{uiMsg}\n\n" +
            "Các text/button đã được wire sẵn.", "OK");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TextMeshProUGUI MakeTMP(
        Transform parent, string name, string text, int fontSize,
        TextAlignmentOptions align, Vector2 anchoredPos, Vector2 sizeDelta,
        bool stretch = false)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = sizeDelta;
        }

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = align;
        tmp.color     = Color.white;
        return tmp;
    }

    [MenuItem("Tools/Pokemon MMO/Add Back Button to SkillPanel")]
    public static void AddBackButtonToSkillPanel()
    {
        GameObject skillPanel = GameObject.Find("SkillPanel");
        if (skillPanel == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find 'SkillPanel' in hierarchy!", "OK");
            return;
        }

        // 1. Create Button GameObject
        GameObject backBtnGo = new GameObject("BackBtn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        backBtnGo.transform.SetParent(skillPanel.transform, false);

        // 2. Setup RectTransform (Bottom Right or similar)
        RectTransform rt = backBtnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = new Vector2(-20, 20);
        rt.sizeDelta = new Vector2(120, 50);

        // 3. Setup Image
        Image img = backBtnGo.GetComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

        // 4. Add Text
        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(backBtnGo.transform, false);
        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "BACK";
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        // 5. Link to SkillPanel script if possible
        var script = skillPanel.GetComponent<Game.Battle.UI.BattleSkillPanel>();
        if (script != null)
        {
            script.backButton = backBtnGo.GetComponent<Button>();
            EditorUtility.SetDirty(script);
        }

        Selection.activeGameObject = backBtnGo;
        Undo.RegisterCreatedObjectUndo(backBtnGo, "Add Back Button");
        
        EditorUtility.DisplayDialog("Success", "Added Back button to SkillPanel! You can now move it to your preferred position.", "OK");
    }

    [MenuItem("Tools/Pokemon MMO/Add Surrender Button to CommandPanel")]
    public static void AddSurrenderButtonToCommandPanel()
    {
        GameObject cmdPanel = GameObject.Find("CommandPanel");
        if (cmdPanel == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find 'CommandPanel' in hierarchy!", "OK");
            return;
        }

        // 1. Create Button GameObject
        GameObject surrenderBtnGo = new GameObject("ForfeitBtn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        surrenderBtnGo.transform.SetParent(cmdPanel.transform, false);

        // 2. Setup RectTransform (Bottom of the list)
        RectTransform rt = surrenderBtnGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160, 50);
        rt.anchoredPosition = new Vector2(0, -180); // Giả định vị trí dưới nút Info

        // 3. Setup Image
        Image img = surrenderBtnGo.GetComponent<Image>();
        img.color = new Color(0.4f, 0.1f, 0.1f, 0.9f); // Màu đỏ tối cho đầu hàng

        // 4. Add Text
        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(surrenderBtnGo.transform, false);
        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "SURRENDER";
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        // 5. Link to CommandPanel script
        var script = cmdPanel.GetComponent<Game.Battle.UI.BattleCommandPanel>();
        if (script != null)
        {
            script.forfeitButton = surrenderBtnGo.GetComponent<Button>();
            EditorUtility.SetDirty(script);
        }

        Selection.activeGameObject = surrenderBtnGo;
        Undo.RegisterCreatedObjectUndo(surrenderBtnGo, "Add Surrender Button");
        
        EditorUtility.DisplayDialog("Success", "Added SURRENDER button to CommandPanel! You can now adjust its position.", "OK");
    }
}
