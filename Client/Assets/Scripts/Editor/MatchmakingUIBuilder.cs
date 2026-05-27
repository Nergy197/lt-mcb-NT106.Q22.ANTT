#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PokemonMMO.UI;

namespace PokemonMMO.EditorTools
{
    public static class MatchmakingUIBuilder
    {
        // ── Colour palette ────────────────────────────────────────────────────
        static readonly Color Overlay     = new Color(0.00f, 0.00f, 0.00f, 0.78f);
        static readonly Color CardBg      = new Color(0.027f, 0.035f, 0.102f, 0.97f);
        static readonly Color CardBorder  = new Color(0.243f, 0.784f, 0.831f, 1f);   // teal
        static readonly Color InkWhite    = new Color(0.910f, 0.925f, 1.000f, 1f);
        static readonly Color InkDim      = new Color(0.541f, 0.572f, 0.780f, 1f);
        static readonly Color TimerColor  = new Color(0.243f, 0.784f, 0.831f, 1f);
        static readonly Color BotColor    = new Color(0.957f, 0.773f, 0.263f, 1f);   // yellow-warning
        static readonly Color CancelBg    = new Color(0.65f, 0.10f, 0.10f, 0.92f);
        static readonly Color CancelHover = new Color(0.85f, 0.15f, 0.15f, 1.00f);
        static readonly Color SpinnerColor = new Color(0.243f, 0.784f, 0.831f, 0.85f);

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("Pokemon/Matchmaking UI Builder")]
        public static void Build()
        {
            Canvas canvas = FindOrCreateCanvas();
            Sprite bg9    = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            Sprite knob   = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // ── Xoá panel cũ nếu đã có ───────────────────────────────────────
            var existing = canvas.transform.Find("MatchmakingPanel");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                Debug.Log("[MatchmakingUI] Xoá panel cũ, tạo lại mới.");
            }

            // ════════════════════════════════════════════════════════════════
            // ROOT: Full-screen overlay (bắt đầu ẩn)
            // ════════════════════════════════════════════════════════════════
            GameObject root = UIObj("MatchmakingPanel", canvas.transform);
            Undo.RegisterCreatedObjectUndo(root, "Create MatchmakingPanel");
            Stretch(root);

            Image overlayImg = root.AddComponent<Image>();
            overlayImg.color = Overlay;
            overlayImg.raycastTarget = true;
            root.SetActive(false);  // ẩn mặc định

            // ════════════════════════════════════════════════════════════════
            // CARD: Hộp thông tin trung tâm (500 × 360)
            // ════════════════════════════════════════════════════════════════
            GameObject card = UIObj("Card", root.transform);
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                          new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 360));

            Image cardImg = card.AddComponent<Image>();
            cardImg.sprite = bg9;
            cardImg.type   = Image.Type.Sliced;
            cardImg.color  = CardBg;

            Outline cardBorder = card.AddComponent<Outline>();
            cardBorder.effectColor    = CardBorder;
            cardBorder.effectDistance = new Vector2(2, -2);

            // ── Header strip ──────────────────────────────────────────────
            GameObject header = UIObj("Header", card.transform);
            SetRect(header, new Vector2(0, 1), new Vector2(1, 1),
                            new Vector2(0.5f, 1), new Vector2(0, -0), new Vector2(0, 56));
            Image headerImg = header.AddComponent<Image>();
            headerImg.sprite = bg9;
            headerImg.type   = Image.Type.Sliced;
            headerImg.color  = new Color(CardBorder.r, CardBorder.g, CardBorder.b, 0.18f);

            GameObject titleObj = UIObj("TitleText", header.transform);
            Stretch(titleObj);
            var titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            titleTmp.text      = "TÌM TRẬN";
            titleTmp.color     = CardBorder;
            titleTmp.fontSize  = 22;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;

            // ── Spinner / icon area ───────────────────────────────────────
            GameObject spinnerArea = UIObj("SpinnerArea", card.transform);
            SetRect(spinnerArea, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                                 new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(90, 90));

            Image spinnerImg = spinnerArea.AddComponent<Image>();
            spinnerImg.sprite = knob;
            spinnerImg.color  = SpinnerColor;

            GameObject spinnerLabel = UIObj("SpinnerIcon", spinnerArea.transform);
            Stretch(spinnerLabel);
            var spinnerTmp = spinnerLabel.AddComponent<TextMeshProUGUI>();
            spinnerTmp.text      = "⚔";
            spinnerTmp.color     = new Color(0, 0, 0, 0.75f);
            spinnerTmp.fontSize  = 36;
            spinnerTmp.fontStyle = FontStyles.Bold;
            spinnerTmp.alignment = TextAlignmentOptions.Center;

            // ── Search timer label ("Đang tìm: 00:00") ────────────────────
            GameObject timerObj = UIObj("SearchTimerLabel", card.transform);
            SetRect(timerObj, new Vector2(0, 1), new Vector2(1, 1),
                              new Vector2(0.5f, 1), new Vector2(0, -178), new Vector2(-40, 40));
            var timerTmp = timerObj.AddComponent<TextMeshProUGUI>();
            timerTmp.text      = "Đang tìm: 00:00";
            timerTmp.color     = TimerColor;
            timerTmp.fontSize  = 22;
            timerTmp.fontStyle = FontStyles.Bold;
            timerTmp.alignment = TextAlignmentOptions.Center;

            // ── Bot countdown label ("Bot dự phòng trong: 20s") ──────────
            GameObject botObj = UIObj("BotCountdownLabel", card.transform);
            SetRect(botObj, new Vector2(0, 1), new Vector2(1, 1),
                            new Vector2(0.5f, 1), new Vector2(0, -222), new Vector2(-40, 30));
            var botTmp = botObj.AddComponent<TextMeshProUGUI>();
            botTmp.text      = "Bot dự phòng trong: 20s";
            botTmp.color     = BotColor;
            botTmp.fontSize  = 14;
            botTmp.fontStyle = FontStyles.Normal;
            botTmp.alignment = TextAlignmentOptions.Center;
            botObj.SetActive(false);  // ẩn cho đến khi server gửi tick

            // ── Divider line ──────────────────────────────────────────────
            GameObject divider = UIObj("Divider", card.transform);
            SetRect(divider, new Vector2(0.1f, 1), new Vector2(0.9f, 1),
                             new Vector2(0.5f, 1), new Vector2(0, -264), new Vector2(0, 1));
            Image divImg = divider.AddComponent<Image>();
            divImg.color = new Color(CardBorder.r, CardBorder.g, CardBorder.b, 0.3f);

            // ── Cancel button ─────────────────────────────────────────────
            GameObject cancelBtn = UIObj("CancelButton", card.transform);
            SetRect(cancelBtn, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                               new Vector2(0.5f, 1), new Vector2(0, -310), new Vector2(220, 52));

            Image cancelImg = cancelBtn.AddComponent<Image>();
            cancelImg.sprite = bg9;
            cancelImg.type   = Image.Type.Sliced;
            cancelImg.color  = CancelBg;

            Button cancelButton = cancelBtn.AddComponent<Button>();
            ColorBlock cb = cancelButton.colors;
            cb.normalColor      = CancelBg;
            cb.highlightedColor = CancelHover;
            cb.pressedColor     = new Color(0.45f, 0.06f, 0.06f, 1f);
            cb.disabledColor    = new Color(0.35f, 0.20f, 0.20f, 0.6f);
            cancelButton.colors = cb;
            cancelButton.targetGraphic = cancelImg;

            Outline cancelBorder = cancelBtn.AddComponent<Outline>();
            cancelBorder.effectColor    = new Color(1f, 0.4f, 0.4f, 0.6f);
            cancelBorder.effectDistance = new Vector2(1, -1);

            GameObject cancelTxtObj = UIObj("Text", cancelBtn.transform);
            Stretch(cancelTxtObj);
            var cancelTmp = cancelTxtObj.AddComponent<TextMeshProUGUI>();
            cancelTmp.text      = "✕  HUỶ TÌM TRẬN";
            cancelTmp.color     = InkWhite;
            cancelTmp.fontSize  = 16;
            cancelTmp.fontStyle = FontStyles.Bold;
            cancelTmp.alignment = TextAlignmentOptions.Center;

            // ════════════════════════════════════════════════════════════════
            // WIRE UP MatchmakingUI component
            // ════════════════════════════════════════════════════════════════
            MatchmakingUI uiComp = root.AddComponent<MatchmakingUI>();

            // Dùng SerializedObject để gán SerializeField private
            var so = new SerializedObject(uiComp);
            so.FindProperty("matchmakingPanel").objectReferenceValue  = root;
            so.FindProperty("searchTimerLabel").objectReferenceValue  = timerTmp;
            so.FindProperty("botCountdownLabel").objectReferenceValue = botTmp;
            so.FindProperty("cancelButton").objectReferenceValue      = cancelButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            EditorUtility.SetDirty(root);

            Debug.Log("<color=cyan>[MatchmakingUI] Panel đã được tạo! Gán vào scene Menu của bạn.</color>");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static Canvas FindOrCreateCanvas()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas != null) return canvas;

            GameObject cObj = new GameObject("Canvas");
            canvas = cObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = cObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            cObj.AddComponent<GraphicRaycaster>();

            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvas;
        }

        static GameObject UIObj(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.one;
            rt.offsetMin        = Vector2.zero;
            rt.offsetMax        = Vector2.zero;
            rt.pivot            = new Vector2(0.5f, 0.5f);
        }

        static void SetRect(GameObject go, Vector2 ancMin, Vector2 ancMax,
                            Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = ancMin;
            rt.anchorMax        = ancMax;
            rt.pivot            = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta        = size;
        }
    }
}
#endif
