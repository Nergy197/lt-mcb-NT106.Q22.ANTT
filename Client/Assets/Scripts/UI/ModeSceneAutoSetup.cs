using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using PokemonArena.UI;

namespace PokemonMMO.UI
{
    /// <summary>
    /// Tự động dựng UI cho màn chọn chế độ (scene có ModeMenu, tức 0_BattleScene)
    /// mà KHÔNG cần chỉnh sửa file .unity hay kéo-thả trong Inspector.
    ///
    /// Chạy sau mỗi lần load scene: nếu scene có ModeMenu nhưng thiếu
    /// MatchmakingUI / PrivateRoomPanel thì dựng chúng bằng code và nối tham chiếu.
    /// Nếu panel đã tồn tại (được đặt sẵn trong scene) thì bỏ qua — an toàn idempotent.
    /// </summary>
    public static class ModeSceneAutoSetup
    {
        static bool _hooked;

        // ── Màu sắc ─────────────────────────────────────────────────────────
        static readonly Color Overlay   = new(0f, 0f, 0f, 0.80f);
        static readonly Color CardBg     = new(0.03f, 0.04f, 0.10f, 0.98f);
        static readonly Color Teal       = new(0.24f, 0.78f, 0.83f, 1f);
        static readonly Color InkWhite   = new(0.91f, 0.93f, 1f, 1f);
        static readonly Color BotYellow  = new(0.96f, 0.77f, 0.26f, 1f);
        static readonly Color RedBg      = new(0.65f, 0.10f, 0.10f, 0.95f);
        static readonly Color GreenBg    = new(0.15f, 0.65f, 0.30f, 1f);
        static readonly Color BlueBg     = new(0.15f, 0.45f, 0.85f, 1f);
        static readonly Color GrayBg     = new(0.30f, 0.30f, 0.35f, 1f);
        static readonly Color InputBg    = new(0.12f, 0.12f, 0.18f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (_hooked) return;
            _hooked = true;
            SceneManager.sceneLoaded += (_, __) => TrySetup();
            TrySetup(); // scene đầu tiên (nếu vào thẳng màn chọn mode)
        }

        static void TrySetup()
        {
            // Chỉ tác động vào scene chọn chế độ (nơi có ModeMenu)
            var modeMenu = Object.FindFirstObjectByType<ModeMenu>(FindObjectsInactive.Include);
            if (modeMenu == null) return;

            var canvas = EnsureCanvas();

            if (Object.FindFirstObjectByType<MatchmakingUI>(FindObjectsInactive.Include) == null)
                BuildMatchmakingUI(canvas.transform);

            if (Object.FindFirstObjectByType<PrivateRoomPanel>(FindObjectsInactive.Include) == null)
                BuildPrivateRoomPanel(canvas.transform);
            // ModeMenu tự resolve privateRoomPanel khi bấm nút Private.
        }

        // ════════════════════════════════════════════════════════════════════
        // MatchmakingUI (Ranked / Casual): host luôn active, panel con ẩn.
        // ════════════════════════════════════════════════════════════════════
        static void BuildMatchmakingUI(Transform canvas)
        {
            // Host: giữ component MatchmakingUI, luôn active để nhận sự kiện tìm trận.
            var host = NewUI("MatchmakingUIHost", canvas);
            Stretch(host);
            host.SetActive(false); // tắt trong lúc dựng để Awake/OnEnable chạy sau Bind

            var mm = host.AddComponent<MatchmakingUI>();

            // Panel overlay (ẩn cho đến khi bắt đầu tìm trận)
            var panel = NewUI("MatchmakingPanel", host.transform);
            Stretch(panel);
            AddImage(panel, Overlay);
            panel.SetActive(false);

            // Card trung tâm
            var card = NewUI("Card", panel.transform);
            Rect(card, C, C, C, Vector2.zero, new Vector2(500, 340));
            AddImage(card, CardBg);
            var border = card.AddComponent<Outline>();
            border.effectColor = Teal; border.effectDistance = new Vector2(2, -2);

            var title = AddLabel(NewUI("Title", card.transform), "TÌM TRẬN", 22, Teal);
            Rect(title.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(-40, 40));
            title.fontStyle = FontStyles.Bold;

            var timer = AddLabel(NewUI("SearchTimerLabel", card.transform), "Đang tìm: 00:00", 22, Teal);
            Rect(timer.gameObject, C, C, C, new Vector2(0, 30), new Vector2(440, 40));
            timer.fontStyle = FontStyles.Bold;

            var bot = AddLabel(NewUI("BotCountdownLabel", card.transform), "Bot dự phòng trong: 20s", 15, BotYellow);
            Rect(bot.gameObject, C, C, C, new Vector2(0, -8), new Vector2(440, 30));
            bot.gameObject.SetActive(false);

            var cancelGO = NewUI("CancelButton", card.transform);
            Rect(cancelGO, C, C, C, new Vector2(0, -110), new Vector2(240, 52));
            var cancelImg = AddImage(cancelGO, RedBg);
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            var cancelTxt = AddLabel(NewUI("Text", cancelGO.transform), "✕  HUỶ TÌM TRẬN", 16, InkWhite);
            Stretch(cancelTxt.gameObject);
            cancelTxt.fontStyle = FontStyles.Bold;

            mm.Bind(panel, timer, bot, cancelBtn);
            host.SetActive(true); // kích hoạt → Awake + OnEnable chạy với field đã gán
        }

        // ════════════════════════════════════════════════════════════════════
        // PrivateRoomPanel (Private): root ẩn, Show() bởi ModeMenu.
        // ════════════════════════════════════════════════════════════════════
        static void BuildPrivateRoomPanel(Transform canvas)
        {
            var root = NewUI("PrivateRoomPanel", canvas);
            Stretch(root);
            AddImage(root, Overlay);
            root.SetActive(false); // Awake bị hoãn tới khi Show()

            var comp = root.AddComponent<PrivateRoomPanel>();

            var card = NewUI("Card", root.transform);
            Rect(card, C, C, C, Vector2.zero, new Vector2(500, 420));
            AddImage(card, CardBg);

            // ── CreateView ──────────────────────────────────────────────────
            var createView = NewUI("CreateView", card.transform);
            Stretch(createView);

            var titleC = AddLabel(NewUI("Title", createView.transform), "TẠO PHÒNG PRIVATE", 24, InkWhite);
            RectAnchors(titleC.gameObject, new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.95f));

            var codeDisplay = AddLabel(NewUI("CodeDisplay", createView.transform), "---", 42, new Color(0.9f, 0.85f, 0.2f));
            RectAnchors(codeDisplay.gameObject, new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.74f));
            codeDisplay.fontStyle = FontStyles.Bold;

            var createRoomBtn   = MakeButton("CreateRoomBtn", createView.transform, "TẠO PHÒNG", GreenBg,
                                             new Vector2(0.15f, 0.26f), new Vector2(0.85f, 0.44f));
            var switchToJoinBtn = MakeButton("SwitchToJoinBtn", createView.transform, "Nhập mã thay thế", GrayBg,
                                             new Vector2(0.20f, 0.08f), new Vector2(0.80f, 0.23f));

            // ── JoinView ────────────────────────────────────────────────────
            var joinView = NewUI("JoinView", card.transform);
            Stretch(joinView);
            joinView.SetActive(false);

            var titleJ = AddLabel(NewUI("Title", joinView.transform), "NHẬP MÃ PHÒNG", 24, InkWhite);
            RectAnchors(titleJ.gameObject, new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.95f));

            var codeInput = MakeInputField("CodeInput", joinView.transform,
                                           new Vector2(0.12f, 0.52f), new Vector2(0.88f, 0.74f));
            var joinRoomBtn      = MakeButton("JoinRoomBtn", joinView.transform, "THAM GIA", BlueBg,
                                              new Vector2(0.15f, 0.26f), new Vector2(0.85f, 0.44f));
            var switchToCreateBtn= MakeButton("SwitchToCreateBtn", joinView.transform, "Tạo phòng thay thế", GrayBg,
                                              new Vector2(0.20f, 0.08f), new Vector2(0.80f, 0.23f));

            // ── Cancel + status ─────────────────────────────────────────────
            var cancelBtn = MakeButton("CancelBtn", card.transform, "✕ HUỶ", RedBg,
                                       new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.12f));
            var statusLabel = AddLabel(NewUI("StatusLabel", card.transform), "", 16, new Color(0.8f, 0.8f, 0.8f));
            RectAnchors(statusLabel.gameObject, new Vector2(0.05f, 0.13f), new Vector2(0.95f, 0.24f));

            comp.Bind(root, createView, joinView, codeDisplay,
                      createRoomBtn.GetComponent<Button>(), switchToJoinBtn.GetComponent<Button>(),
                      codeInput.GetComponent<TMP_InputField>(), joinRoomBtn.GetComponent<Button>(),
                      switchToCreateBtn.GetComponent<Button>(), cancelBtn.GetComponent<Button>(), statusLabel);
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════
        static readonly Vector2 C = new(0.5f, 0.5f);

        static Canvas EnsureCanvas()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas != null)
            {
                EnsureEventSystem();
                return canvas;
            }

            var go = new GameObject("Canvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void Stretch(GameObject go)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void Rect(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        static void RectAnchors(GameObject go, Vector2 aMin, Vector2 aMax)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static Image AddImage(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static TextMeshProUGUI AddLabel(GameObject go, string text, int size, Color color)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        static GameObject MakeButton(string name, Transform parent, string label, Color color,
                                     Vector2 aMin, Vector2 aMax)
        {
            var go = NewUI(name, parent);
            RectAnchors(go, aMin, aMax);
            var img = AddImage(go, color);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var txt = AddLabel(NewUI("Text", go.transform), label, 20, InkWhite);
            Stretch(txt.gameObject);
            return go;
        }

        static GameObject MakeInputField(string name, Transform parent, Vector2 aMin, Vector2 aMax)
        {
            var go = NewUI(name, parent);
            RectAnchors(go, aMin, aMax);
            AddImage(go, InputBg);
            var field = go.AddComponent<TMP_InputField>();

            var area = NewUI("Text Area", go.transform);
            var areaRT = (RectTransform)area.transform;
            areaRT.anchorMin = Vector2.zero; areaRT.anchorMax = Vector2.one;
            areaRT.offsetMin = new Vector2(8, 4); areaRT.offsetMax = new Vector2(-8, -4);
            area.AddComponent<RectMask2D>();

            var text = AddLabel(NewUI("Text", area.transform), "", 28, InkWhite);
            Stretch(text.gameObject);

            var ph = AddLabel(NewUI("Placeholder", area.transform), "Nhập mã 6 số...", 22, new Color(0.5f, 0.5f, 0.5f));
            Stretch(ph.gameObject);

            field.textViewport   = areaRT;
            field.textComponent  = text;
            field.placeholder    = ph;
            field.contentType    = TMP_InputField.ContentType.IntegerNumber;
            field.characterLimit = 6;
            field.pointSize      = 28;
            return go;
        }
    }
}
