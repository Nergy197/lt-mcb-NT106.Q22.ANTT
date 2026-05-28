#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Battle.Logic;

namespace Game.Battle.Editor
{
    public static class BattleLoadingOverlayBuilder
    {
        [MenuItem("Pokemon/Battle Loading Overlay Builder")]
        public static void Build()
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[BattleLoadingOverlayBuilder] Không tìm thấy Canvas trong scene.");
                return;
            }

            // Xóa overlay cũ nếu có
            var existing = canvas.transform.Find("BattleLoadingOverlay");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            // Root overlay — full-screen Image tối màu
            var root = new GameObject("BattleLoadingOverlay");
            Undo.RegisterCreatedObjectUndo(root, "Create Battle Loading Overlay");
            root.transform.SetParent(canvas.transform, false);

            var rootImg = root.AddComponent<Image>();
            rootImg.color = new Color(0f, 0f, 0f, 0.92f);
            rootImg.raycastTarget = true;

            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Card trung tâm
            var card = new GameObject("Card");
            card.transform.SetParent(root.transform, false);
            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.10f, 0.10f, 0.15f, 0.95f);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot     = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(460, 220);

            // Status text
            var textObj = new GameObject("StatusText");
            textObj.transform.SetParent(card.transform, false);
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "Đang kết nối trận đấu...";
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(20, 20);
            textRt.offsetMax = new Vector2(-20, -20);

            // Gắn BattleLoadingOverlay component
            var overlay = root.AddComponent<BattleLoadingOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("statusText").objectReferenceValue = tmp;
            so.ApplyModifiedProperties();

            root.transform.SetAsLastSibling();

            EditorUtility.SetDirty(root);
            Debug.Log("[BattleLoadingOverlayBuilder] Overlay tạo thành công.");
        }
    }
}
#endif
