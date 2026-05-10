using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class BattleUIHelper : EditorWindow
{
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
