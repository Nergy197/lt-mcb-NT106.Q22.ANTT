using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PokemonMMO.UI;

namespace PokemonMMO.Editor
{
    /// <summary>
    /// Single tool that builds the complete Auth flow in one click:
    ///   Main Menu (animated BG + title + Login/SignUp buttons)
    ///     └─ Auth Panel (Login form  ↔  Sign Up form)
    ///
    /// Menu: GameObject ▶ UI ▶ Auth ▶ Generate Full Auth UI
    /// </summary>
    public static class AuthFullUIGenerator
    {
        private static readonly (Color color, float alpha, float size, Vector2 anchor)[] OrbDefs =
        {
            (new Color(0.4f, 0.2f, 0.8f), 0.35f, 350f, new Vector2(0.15f, 0.75f)),
            (new Color(0.1f, 0.5f, 0.9f), 0.30f, 280f, new Vector2(0.80f, 0.60f)),
            (new Color(0.8f, 0.2f, 0.5f), 0.25f, 200f, new Vector2(0.50f, 0.20f)),
            (new Color(0.2f, 0.8f, 0.7f), 0.20f, 160f, new Vector2(0.25f, 0.35f)),
            (new Color(0.9f, 0.6f, 0.1f), 0.18f, 120f, new Vector2(0.75f, 0.85f)),
        };

        [MenuItem("GameObject/UI/Auth/Generate Full Auth UI", false, 9)]
        public static void Generate()
        {
            // ── Canvas ────────────────────────────────────────────────────────
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject cgo = new GameObject("Canvas");
                canvas = cgo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = cgo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution  = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight   = 0.5f;
                cgo.AddComponent<GraphicRaycaster>();
            }

            // ── EventSystem ───────────────────────────────────────────────────
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            // =================================================================
            // PART 1 – MAIN MENU PANEL
            // =================================================================
            GameObject menuPanel = Elem("MainMenuPanel", canvas.transform);
            Stretch(menuPanel.GetComponent<RectTransform>());

            // Animated background
            GameObject bgGO = Elem("AnimatedBackground", menuPanel.transform);
            Stretch(bgGO.GetComponent<RectTransform>());
            Image bgImage = bgGO.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.25f, 1f);

            AnimatedBackground animBG = bgGO.AddComponent<AnimatedBackground>();
            animBG.backgroundImage = bgImage;

            // Orbs
            GameObject orbContainer = Elem("Orbs", bgGO.transform);
            Stretch(orbContainer.GetComponent<RectTransform>());
            foreach (var d in OrbDefs)
                animBG.orbs.Add(Orb(orbContainer.transform, d.color, d.alpha, d.size, d.anchor));

            // Content layer
            GameObject content = Elem("Content", menuPanel.transform);
            Stretch(content.GetComponent<RectTransform>());

            // Title area (top 55 % of screen)
            GameObject titleArea = Elem("TitleArea", content.transform);
            AnchorRect(titleArea.GetComponent<RectTransform>(),
                new Vector2(0f, 0.45f), new Vector2(1f, 1f));

            TitleText("TitleShadow", titleArea.transform, "POKEMON MMO",
                new Color(0f, 0f, 0f, 0.4f), 80, new Vector2(4f, -4f));
            TitleText("TitleText", titleArea.transform, "POKEMON MMO",
                Color.white, 80, Vector2.zero);
            SubtitleText("SubtitleText", titleArea.transform,
                "Online Adventure", new Color(0.8f, 0.9f, 1f, 0.85f));

            // Button area (bottom)
            GameObject btnArea = Elem("ButtonArea", content.transform);
            AnchorRect(btnArea.GetComponent<RectTransform>(),
                new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.42f));
            var vl = btnArea.AddComponent<VerticalLayoutGroup>();
            vl.spacing            = 20f;
            vl.childAlignment     = TextAnchor.MiddleCenter;
            vl.childForceExpandHeight = false;
            vl.childForceExpandWidth  = true;
            vl.childControlHeight = false;
            vl.childControlWidth  = true;
            vl.padding            = new RectOffset(0, 0, 10, 10);

            Button menuLoginBtn  = MenuBtn("LoginButton",  "LOG IN",  btnArea.transform, new Color(0.20f, 0.55f, 1.00f));
            Button menuSignUpBtn = MenuBtn("SignUpButton", "SIGN UP", btnArea.transform, new Color(0.15f, 0.75f, 0.50f));

            // =================================================================
            // PART 2 – AUTH PANEL  (Login form + Sign Up form)
            // =================================================================
            GameObject authPanel = Elem("AuthPanel", canvas.transform);
            Stretch(authPanel.GetComponent<RectTransform>());
            authPanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            AuthUIManager uiMgr = authPanel.AddComponent<AuthUIManager>();

            // Back button (top-left of auth panel)
            Button backBtn = BackButton(authPanel.transform);

            // Login View
            GameObject loginView = CardPanel("LoginView", authPanel.transform);
            FormTitle("Sign In", loginView.transform);
            uiMgr.loginUsernameInput = InputBox("UsernameInput", "Username", loginView.transform, 1);
            uiMgr.loginPasswordInput = InputBox("PasswordInput", "Password", loginView.transform, 2, true);
            uiMgr.loginFeedback      = FeedbackText("FeedbackText", loginView.transform, 3);
            Button loginSubmitBtn    = FormBtn("LoginButton",        "SIGN IN",                       loginView.transform, 4);
            Button toSignUpBtn       = FormBtn("SwitchSignUpButton", "Don't have an account? Sign Up", loginView.transform, 5, true);

            // Sign Up View
            GameObject signUpView = CardPanel("SignUpView", authPanel.transform);
            FormTitle("Create Account", signUpView.transform);
            uiMgr.signUpUsernameInput = InputBox("UsernameInput", "Username", signUpView.transform, 1);
            uiMgr.signUpEmailInput    = InputBox("EmailInput",    "Email",    signUpView.transform, 2);
            uiMgr.signUpPasswordInput = InputBox("PasswordInput", "Password", signUpView.transform, 3, true);
            uiMgr.signUpFeedback      = FeedbackText("FeedbackText", signUpView.transform, 4);
            Button signUpSubmitBtn    = FormBtn("SignUpButton",       "CREATE ACCOUNT",                signUpView.transform, 5);
            Button toLoginBtn         = FormBtn("SwitchLoginButton",  "Already have an account? Sign In", signUpView.transform, 6, true);

            // Wire AuthUIManager views
            uiMgr.loginView  = loginView;
            uiMgr.signUpView = signUpView;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(toSignUpBtn.onClick,     uiMgr.ShowSignUpView);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(toLoginBtn.onClick,      uiMgr.ShowLoginView);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(loginSubmitBtn.onClick,  uiMgr.OnLoginSubmit);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(signUpSubmitBtn.onClick, uiMgr.OnSignUpSubmit);

            signUpView.SetActive(false);
            loginView.SetActive(true);
            authPanel.SetActive(false);

            // =================================================================
            // PART 3 – MAIN MENU MANAGER  (wires the two panels)
            // =================================================================
            MainMenuManager menuMgr = menuPanel.AddComponent<MainMenuManager>();
            menuMgr.mainMenuPanel = menuPanel;
            menuMgr.authPanel     = authPanel;
            menuMgr.authUIManager = uiMgr;

            UnityEditor.Events.UnityEventTools.AddPersistentListener(menuLoginBtn.onClick,  menuMgr.OnLoginClicked);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(menuSignUpBtn.onClick, menuMgr.OnSignUpClicked);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(backBtn.onClick,       menuMgr.ShowMainMenu);

            // Finish
            Undo.RegisterCreatedObjectUndo(menuPanel,  "Generate Full Auth UI");
            Undo.RegisterCreatedObjectUndo(authPanel,  "Generate Full Auth UI");
            Selection.activeGameObject = menuPanel;

            Debug.Log("[AuthFullUIGenerator] Full Auth UI generated! Server URL: " + uiMgr.serverUrl);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        static GameObject Elem(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void AnchorRect(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static RectTransform Orb(Transform parent, Color c, float a, float size, Vector2 anchor)
        {
            var go = Elem("Orb", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.color = new Color(c.r, c.g, c.b, a);
            img.raycastTarget = false;
            return rt;
        }

        static void TitleText(string name, Transform parent, string text, Color color, int size, Vector2 offset)
        {
            var go = Elem(name, parent);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt); rt.anchoredPosition = offset;
            var t = go.AddComponent<Text>();
            t.text = text; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.LowerCenter; t.color = color;
            t.raycastTarget = false;
        }

        static void SubtitleText(string name, Transform parent, string text, Color color)
        {
            var go = Elem(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0.42f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = text; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 28; t.fontStyle = FontStyle.Italic;
            t.alignment = TextAnchor.UpperCenter; t.color = color;
            t.raycastTarget = false;
        }

        static Button MenuBtn(string name, string label, Transform parent, Color bg)
        {
            var go = Elem(name, parent);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 70);
            var img = go.AddComponent<Image>(); img.color = bg;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor      = bg;
            cb.highlightedColor = new Color(bg.r + 0.1f, bg.g + 0.1f, bg.b + 0.1f, 1f);
            cb.pressedColor     = new Color(bg.r - 0.15f, bg.g - 0.15f, bg.b - 0.15f, 1f);
            btn.colors = cb;
            var textGO = Elem("Text", go.transform);
            Stretch(textGO.GetComponent<RectTransform>());
            var t = textGO.AddComponent<Text>();
            t.text = label; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 26; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
            return btn;
        }

        static GameObject CardPanel(string name, Transform parent)
        {
            var go = Elem(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(580, 660);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.18f, 0.97f);
            var vl = go.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(56, 56, 48, 48);
            vl.spacing = 20;
            vl.childForceExpandHeight = false; vl.childForceExpandWidth = true;
            vl.childControlHeight = false;     vl.childControlWidth = true;
            vl.childAlignment = TextAnchor.UpperCenter;
            return go;
        }

        static void FormTitle(string text, Transform parent)
        {
            var go = Elem("Title", parent);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 68);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 36; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
        }

        static InputField InputBox(string name, string placeholder, Transform parent, int siblingIndex, bool isPassword = false)
        {
            var go = Elem(name, parent);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 54);
            var bg = go.AddComponent<Image>(); bg.color = new Color(0.30f, 0.30f, 0.42f);

            var field = go.AddComponent<InputField>();

            var textGO = Elem("Text", go.transform);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(10, 2); textRT.offsetMax = new Vector2(-10, -2);
            var textComp = textGO.AddComponent<Text>();
            textComp.color = Color.white; textComp.fontSize = 20;
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var phGO = Elem("Placeholder", go.transform);
            var phRT = phGO.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(10, 2); phRT.offsetMax = new Vector2(-10, -2);
            var phText = phGO.AddComponent<Text>();
            phText.text = placeholder; phText.fontSize = 20; phText.fontStyle = FontStyle.Italic;
            phText.color = new Color(0.78f, 0.78f, 0.88f);
            phText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            field.textComponent = textComp; field.placeholder = phText;
            if (isPassword) field.contentType = InputField.ContentType.Password;
            go.transform.SetSiblingIndex(siblingIndex);
            return field;
        }

        static Text FeedbackText(string name, Transform parent, int siblingIndex)
        {
            var go = Elem(name, parent);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 30);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 16; t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 0.4f, 0.4f);
            t.text = "";
            go.transform.SetSiblingIndex(siblingIndex);
            return t;
        }

        static Button FormBtn(string name, string label, Transform parent, int siblingIndex, bool isLink = false)
        {
            var go = Elem(name, parent);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, isLink ? 34 : 62);
            var btn = go.AddComponent<Button>();
            if (!isLink)
            {
                var img = go.AddComponent<Image>();
                img.color = new Color(0.20f, 0.55f, 1.00f);
                btn.targetGraphic = img;
            }
            var tGO = Elem("Text", go.transform);
            Stretch(tGO.GetComponent<RectTransform>());
            var t = tGO.AddComponent<Text>();
            t.text = label; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = isLink ? 16 : 22; t.fontStyle = isLink ? FontStyle.Normal : FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = isLink ? new Color(0.75f, 0.90f, 1f) : Color.white;
            go.transform.SetSiblingIndex(siblingIndex);
            return btn;
        }

        static Button BackButton(Transform parent)
        {
            var go = Elem("BackButton", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -8f);   // sát mép trên
            rt.sizeDelta        = new Vector2(120f, 44f);
            // Nền hoàn toàn trong suốt — vẫn cần Image để Button có targetGraphic
            var img = go.AddComponent<Image>(); img.color = new Color(0f, 0f, 0f, 0f);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            // Tắt tint khi hover/press để không lộ nền
            var cb = btn.colors;
            cb.normalColor      = new Color(0, 0, 0, 0);
            cb.highlightedColor = new Color(1, 1, 1, 0.08f);
            cb.pressedColor     = new Color(1, 1, 1, 0.15f);
            cb.selectedColor    = new Color(0, 0, 0, 0);
            btn.colors = cb;
            var tGO = Elem("Text", go.transform);
            Stretch(tGO.GetComponent<RectTransform>());
            var t = tGO.AddComponent<Text>();
            t.text = "← Back"; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 18; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleLeft; t.color = Color.white;
            return btn;
        }
    }
}
