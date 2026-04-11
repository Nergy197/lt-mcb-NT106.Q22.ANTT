using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PokemonMMO.UI;

namespace PokemonMMO.Editor
{
    /// <summary>
    /// Một-click setup Start Menu scene với sprites thực tế.
    ///
    /// Kiến trúc: mỗi view là một panel độc lập, căn giữa màn hình, tự co giãn
    /// theo nội dung (ContentSizeFitter). Chỉ một panel hiện tại một lúc.
    ///
    /// Sprites dùng:
    ///   Assets/Sprites/Login menu background.png  → nền toàn màn hình
    ///   Assets/Sprites/menu.png                   → background của từng panel
    ///   Assets/Sprites/login or sign in bar.png   → background input field
    ///   Assets/Sprites/sign in button.png         → nút hành động chính (Login)
    ///   Assets/Sprites/create account button.png  → nút hành động chính (Sign Up)
    ///
    /// Menu: GameObject ▶ UI ▶ Start Menu ▶ Setup Start Menu Scene
    /// </summary>
    public static class StartMenuSetupTool
    {
        // ── Sprite paths ─────────────────────────────────────────────────────
        private const string SP_BG         = "Assets/Sprites/Login menu background.png";
        private const string SP_CARD       = "Assets/Sprites/menu.png";
        private const string SP_BAR        = "Assets/Sprites/login or sign in bar.png";
        private const string SP_BTN_LOGIN  = "Assets/Sprites/sign in button.png";
        private const string SP_BTN_CREATE = "Assets/Sprites/create account button.png";

        // ── Panel width (chiều cao tự động qua ContentSizeFitter) ─────────────
        private const float PANEL_W = 560f;

        // ── Colours ──────────────────────────────────────────────────────────
        private static readonly Color White      = Color.white;
        private static readonly Color Overlay    = new Color(0f,    0f,    0f,    0.42f);
        private static readonly Color CardFallbk = new Color(0.07f, 0.07f, 0.15f, 0.95f);
        private static readonly Color InputFallbk= new Color(0.18f, 0.18f, 0.28f, 1f);
        private static readonly Color PlaceholdrC= new Color(0.60f, 0.60f, 0.78f, 1f);
        private static readonly Color LinkColor  = new Color(0.65f, 0.85f, 1.00f, 1f);
        private static readonly Color OrangeBtn  = new Color(0.85f, 0.45f, 0.10f, 1f);
        private static readonly Color BlueFallbk = new Color(0.20f, 0.55f, 1.00f, 1f);
        private static readonly Color GreenFallbk= new Color(0.15f, 0.75f, 0.50f, 1f);

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("GameObject/UI/Start Menu/Setup Start Menu Scene", false, 8)]
        public static void Setup()
        {
            Sprite spBg    = Load(SP_BG,         "background");
            Sprite spCard  = Load(SP_CARD,        "menu card");
            Sprite spBar   = Load(SP_BAR,         "input bar");
            Sprite spLogin = Load(SP_BTN_LOGIN,   "sign-in button");
            Sprite spCreate= Load(SP_BTN_CREATE,  "create-account button");

            // ── Canvas ────────────────────────────────────────────────────────
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var cgo = new GameObject("Canvas");
                canvas  = cgo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var sc  = cgo.AddComponent<CanvasScaler>();
                sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920, 1080);
                sc.matchWidthOrHeight  = 0.5f;
                cgo.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(cgo, "Setup Start Menu");
            }

            // ── EventSystem ───────────────────────────────────────────────────
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(es, "Setup Start Menu");
            }

            // =================================================================
            // BACKGROUND (nền toàn màn hình)
            // =================================================================
            var bgRoot = Elem("Background", canvas.transform);
            Stretch(bgRoot.GetComponent<RectTransform>());
            var bgImg  = bgRoot.AddComponent<Image>();
            if (spBg != null) { bgImg.sprite = spBg; bgImg.type = Image.Type.Simple; }
            else bgImg.color = new Color(0.05f, 0.05f, 0.20f, 1f);
            bgImg.raycastTarget = false;

            var ov = Elem("Overlay", bgRoot.transform);
            Stretch(ov.GetComponent<RectTransform>());
            ov.AddComponent<Image>().color = Overlay;
            ov.GetComponent<Image>().raycastTarget = false;

            Undo.RegisterCreatedObjectUndo(bgRoot, "Setup Start Menu");

            // =================================================================
            // HELPER: tạo panel trung tâm (menu.png, auto-height, VLG+CSF)
            // =================================================================
            // Mỗi view được tạo bằng MakePanel() bên dưới.

            // =================================================================
            // PANEL 1: MAIN MENU  (title + 2 sprite buttons lớn)
            // =================================================================
            var mainPanel = MakePanel("MainMenuPanel", canvas.transform, spCard,
                                      padTop: 48, padBottom: 48, padLR: 52, spacing: 20f);

            MakeTitle(mainPanel.transform, "POKEMON MMO");
            Spacer(mainPanel.transform, 16f);
            Button mainLoginBtn  = PrimaryBtn("LoginBtn",      spLogin,  BlueFallbk,  mainPanel.transform, 72f);
            Button mainCreateBtn = PrimaryBtn("CreateBtn",     spCreate, GreenFallbk, mainPanel.transform, 72f);

            // =================================================================
            // PANEL 2: LOGIN
            // header row: [← Trở về]  [Đăng Nhập]
            // body: username, password, feedback, sign-in-button
            // footer: "Quên mật khẩu?"  "Chưa có tài khoản? Tạo tài khoản"
            // =================================================================
            var loginPanel = MakePanel("LoginPanel", canvas.transform, spCard,
                                       padTop: 20, padBottom: 40, padLR: 48, spacing: 14f);
            loginPanel.SetActive(false);

            Button loginBackBtn = HeaderRow("Đăng Nhập", loginPanel.transform);
            InputField loginUser = BarInput("UsernameInput", "Tên đăng nhập", spBar, loginPanel.transform, 56f);
            InputField loginPass = BarInput("PasswordInput", "Mật khẩu",      spBar, loginPanel.transform, 56f, isPassword: true);
            Text       loginFB   = FbLabel("FeedbackText", loginPanel.transform);
            Button     loginBtn  = PrimaryBtn("SignInBtn", spLogin, BlueFallbk, loginPanel.transform, 68f);
            Button     forgotBtn = TextLink("ForgotLink",   "Quên mật khẩu?",                    loginPanel.transform);
            Button     toSignUp  = TextLink("ToSignUpLink", "Chưa có tài khoản? Tạo tài khoản", loginPanel.transform);

            // =================================================================
            // PANEL 3: SIGN UP
            // header row: [← Trở về]  [Tạo Tài Khoản]
            // body: username, email, password, feedback, create-account-button
            // footer: "Đã có tài khoản? Đăng nhập"
            // =================================================================
            var signUpPanel = MakePanel("SignUpPanel", canvas.transform, spCard,
                                        padTop: 20, padBottom: 40, padLR: 48, spacing: 14f);
            signUpPanel.SetActive(false);

            Button suBackBtn = HeaderRow("Tạo Tài Khoản", signUpPanel.transform);
            InputField suUser  = BarInput("UsernameInput", "Tên đăng nhập", spBar, signUpPanel.transform, 56f);
            InputField suEmail = BarInput("EmailInput",    "Email",         spBar, signUpPanel.transform, 56f);
            InputField suPass  = BarInput("PasswordInput", "Mật khẩu",     spBar, signUpPanel.transform, 56f, isPassword: true);
            Text       suFB    = FbLabel("FeedbackText", signUpPanel.transform);
            Button     suBtn   = PrimaryBtn("CreateBtn", spCreate, GreenFallbk, signUpPanel.transform, 68f);
            Button     toLogin = TextLink("ToLoginLink", "Đã có tài khoản? Đăng nhập", signUpPanel.transform);

            // =================================================================
            // PANEL 4: FORGOT PASSWORD
            // header row: [← Trở về]  [Quên Mật Khẩu]
            // body: instruction, email, feedback, send button
            // =================================================================
            var forgotPanel = MakePanel("ForgotPanel", canvas.transform, spCard,
                                         padTop: 20, padBottom: 44, padLR: 48, spacing: 16f);
            forgotPanel.SetActive(false);

            Button forgotBackBtn = HeaderRow("Quên Mật Khẩu", forgotPanel.transform);
            SmallNote("Nhập email để nhận mã đặt lại mật khẩu", forgotPanel.transform);
            InputField forgotEmail = BarInput("EmailInput", "Địa chỉ email", spBar, forgotPanel.transform, 56f);
            Text       forgotFB   = FbLabel("FeedbackText", forgotPanel.transform);
            Button     forgotSend = PlainBtn("SendBtn", "GỬI YÊU CẦU", OrangeBtn, forgotPanel.transform, 64f);

            // =================================================================
            // PANEL 5: RESET PASSWORD
            // header row: [← Trở về]  [Đặt Lại Mật Khẩu]
            // body: instruction, token, new password, feedback, reset button
            // =================================================================
            var resetPanel = MakePanel("ResetPanel", canvas.transform, spCard,
                                        padTop: 20, padBottom: 44, padLR: 48, spacing: 14f);
            resetPanel.SetActive(false);

            Button resetBackBtn = HeaderRow("Đặt Lại Mật Khẩu", resetPanel.transform);
            SmallNote("Dán mã token bạn nhận được vào ô bên dưới", resetPanel.transform);
            InputField resetToken   = BarInput("TokenInput",    "Reset token",   spBar, resetPanel.transform, 56f);
            InputField resetNewPass = BarInput("NewPassInput",  "Mật khẩu mới", spBar, resetPanel.transform, 56f, isPassword: true);
            Text       resetFB      = FbLabel("FeedbackText", resetPanel.transform);
            Button     resetSubmit  = PlainBtn("ResetBtn", "ĐẶT LẠI MẬT KHẨU", OrangeBtn, resetPanel.transform, 64f);

            // =================================================================
            // AUTH UI MANAGER  (gắn lên Canvas root để không bị ẩn cùng panel)
            // =================================================================
            var authGO = new GameObject("AuthUIManager");
            authGO.transform.SetParent(canvas.transform, false);
            authGO.AddComponent<RectTransform>();

            var auth = authGO.AddComponent<AuthUIManager>();
            auth.serverUrl             = "http://localhost:2567";
            auth.gameSceneName         = "Menu scene";
            auth.mainMenuView          = mainPanel;
            auth.loginView             = loginPanel;
            auth.signUpView            = signUpPanel;
            auth.forgotPasswordView    = forgotPanel;
            auth.resetPasswordView     = resetPanel;
            auth.loginUsernameInput    = loginUser;
            auth.loginPasswordInput    = loginPass;
            auth.loginFeedback         = loginFB;
            auth.signUpUsernameInput   = suUser;
            auth.signUpEmailInput      = suEmail;
            auth.signUpPasswordInput   = suPass;
            auth.signUpFeedback        = suFB;
            auth.forgotEmailInput      = forgotEmail;
            auth.forgotFeedback        = forgotFB;
            auth.resetTokenInput       = resetToken;
            auth.resetNewPasswordInput = resetNewPass;
            auth.resetFeedback         = resetFB;

            Undo.RegisterCreatedObjectUndo(authGO, "Setup Start Menu");

            // ── Sự kiện Main Menu ─────────────────────────────────────────────
            Wire(mainLoginBtn.onClick,  auth.ShowLoginView);
            Wire(mainCreateBtn.onClick, auth.ShowSignUpView);

            // ── Sự kiện Login Panel ───────────────────────────────────────────
            Wire(loginBackBtn.onClick, auth.ShowMainMenuView);
            Wire(loginBtn.onClick,     auth.OnLoginSubmit);
            Wire(forgotBtn.onClick,    auth.ShowForgotPasswordView);
            Wire(toSignUp.onClick,     auth.ShowSignUpView);

            // ── Sự kiện Sign Up Panel ─────────────────────────────────────────
            Wire(suBackBtn.onClick, auth.ShowMainMenuView);
            Wire(suBtn.onClick,     auth.OnSignUpSubmit);
            Wire(toLogin.onClick,   auth.ShowLoginView);

            // ── Sự kiện Forgot Panel ──────────────────────────────────────────
            Wire(forgotBackBtn.onClick, auth.ShowLoginView);
            Wire(forgotSend.onClick,    auth.OnForgotPasswordSubmit);

            // ── Sự kiện Reset Panel ───────────────────────────────────────────
            Wire(resetBackBtn.onClick, auth.ShowLoginView);
            Wire(resetSubmit.onClick,  auth.OnResetPasswordSubmit);

            // ── Kết thúc ──────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = mainPanel;

            Debug.Log("[StartMenuSetup] Hoàn tất!\n" +
                $"  bg:     {(spBg    != null ? "OK" : "MISSING – " + SP_BG)}\n" +
                $"  card:   {(spCard  != null ? "OK" : "MISSING – " + SP_CARD)}\n" +
                $"  bar:    {(spBar   != null ? "OK" : "MISSING – " + SP_BAR)}\n" +
                $"  login:  {(spLogin != null ? "OK" : "MISSING – " + SP_BTN_LOGIN)}\n" +
                $"  create: {(spCreate!= null ? "OK" : "MISSING – " + SP_BTN_CREATE)}\n" +
                "  → Ctrl+S để lưu scene");
        }

        [MenuItem("GameObject/UI/Start Menu/Setup Start Menu Scene", true)]
        static bool Validate() => !EditorApplication.isPlaying;

        // =====================================================================
        // Panel factory
        // =====================================================================

        /// <summary>
        /// Tạo panel căn giữa màn hình, chiều cao tự động theo nội dung.
        /// Dùng VLG + ContentSizeFitter (Vertical = PreferredSize).
        /// </summary>
        static GameObject MakePanel(string name, Transform parent, Sprite cardSprite,
                                    int padTop, int padBottom, int padLR, float spacing)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt         = go.AddComponent<RectTransform>();
            rt.anchorMin   = new Vector2(0.5f, 0.5f);
            rt.anchorMax   = new Vector2(0.5f, 0.5f);
            rt.pivot       = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta   = new Vector2(PANEL_W, 0f); // cao tự động

            // Background sprite — Simple+stretch: mỗi panel tự scale theo chiều cao riêng
            var img        = go.AddComponent<Image>();
            if (cardSprite != null)
            {
                img.sprite          = cardSprite;
                img.type            = Image.Type.Simple;
                img.preserveAspect  = false;   // kéo dãn theo kích thước panel
                img.color           = White;
            }
            else img.color = CardFallbk;

            // Layout
            var vl                      = go.AddComponent<VerticalLayoutGroup>();
            vl.padding                  = new RectOffset(padLR, padLR, padTop, padBottom);
            vl.spacing                  = spacing;
            vl.childAlignment           = TextAnchor.UpperCenter;
            vl.childForceExpandHeight   = false;
            vl.childForceExpandWidth    = true;
            vl.childControlHeight       = false;
            vl.childControlWidth        = true;

            // Auto-height
            var csf                     = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit             = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit           = ContentSizeFitter.FitMode.Unconstrained;

            Undo.RegisterCreatedObjectUndo(go, "Setup Start Menu");
            return go;
        }

        // =====================================================================
        // UI element builders
        // =====================================================================

        /// <summary>
        /// Header row: nút back bên trái, heading căn giữa. Gắn vào VLG panel.
        /// Trả về nút back để wire sự kiện.
        /// </summary>
        static Button HeaderRow(string heading, Transform parent)
        {
            const float H = 50f;
            var row = new GameObject("HeaderRow");
            row.transform.SetParent(parent, false);
            var rowRT       = row.AddComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0, H);
            var rowLE       = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = H;

            // ── Back button (trái) ───────────────────────────────────────────
            var backGO = new GameObject("BackButton");
            backGO.transform.SetParent(row.transform, false);
            var backRT         = backGO.AddComponent<RectTransform>();
            backRT.anchorMin   = new Vector2(0f, 0f);
            backRT.anchorMax   = new Vector2(0f, 1f);
            backRT.pivot       = new Vector2(0f, 0.5f);
            backRT.offsetMin   = new Vector2(0f,  0f);
            backRT.offsetMax   = new Vector2(100f, 0f);

            var backImg        = backGO.AddComponent<Image>(); backImg.color = Color.clear;
            var backBtn        = backGO.AddComponent<Button>(); backBtn.targetGraphic = backImg;
            var bCb            = backBtn.colors;
            bCb.normalColor    = bCb.selectedColor = Color.clear;
            bCb.highlightedColor = new Color(1,1,1,0.08f);
            bCb.pressedColor     = new Color(1,1,1,0.18f);
            backBtn.colors     = bCb;

            var bLabel         = new GameObject("Label");
            bLabel.transform.SetParent(backGO.transform, false);
            var bLabelRT       = bLabel.AddComponent<RectTransform>();
            bLabelRT.anchorMin = Vector2.zero; bLabelRT.anchorMax = Vector2.one;
            bLabelRT.offsetMin = bLabelRT.offsetMax = Vector2.zero;
            var bText          = bLabel.AddComponent<Text>();
            bText.text         = "← Trở về";
            bText.font         = Font();
            bText.fontSize     = 17; bText.fontStyle = FontStyle.Bold;
            bText.alignment    = TextAnchor.MiddleLeft;
            bText.color        = LinkColor;

            // ── Heading (giữa, phủ full width nhưng text căn giữa) ───────────
            var hGO            = new GameObject("Heading");
            hGO.transform.SetParent(row.transform, false);
            var hRT            = hGO.AddComponent<RectTransform>();
            hRT.anchorMin      = Vector2.zero; hRT.anchorMax = Vector2.one;
            hRT.offsetMin      = hRT.offsetMax = Vector2.zero;
            var hText          = hGO.AddComponent<Text>();
            hText.text         = heading;
            hText.font         = Font();
            hText.fontSize     = 26; hText.fontStyle = FontStyle.Bold;
            hText.alignment    = TextAnchor.MiddleCenter;
            hText.color        = White;
            hText.raycastTarget = false;

            return backBtn;
        }

        /// <summary>Title POKEMON MMO + shadow + subtitle, dùng cho MainMenu.</summary>
        static void MakeTitle(Transform parent, string text)
        {
            // TitleWrapper — chiều cao cố định, khai báo cho ContentSizeFitter
            const float TH = 110f, SH = 30f;
            var wrap = new GameObject("TitleWrapper");
            wrap.transform.SetParent(parent, false);
            wrap.AddComponent<RectTransform>().sizeDelta = new Vector2(0, TH);
            var wLE = wrap.AddComponent<LayoutElement>(); wLE.preferredHeight = TH;

            var shadow = ChildText("Shadow", wrap.transform, text, 56, FontStyle.Bold,
                                   TextAnchor.MiddleCenter, new Color(0, 0, 0, 0.45f));
            shadow.GetComponent<RectTransform>().anchoredPosition = new Vector2(3, -3);
            shadow.GetComponent<Text>().raycastTarget = false;

            ChildText("Title", wrap.transform, text, 56, FontStyle.Bold,
                      TextAnchor.MiddleCenter, White).GetComponent<Text>().raycastTarget = false;

            // SubtitleWrapper
            var subWrap = new GameObject("SubtitleWrapper");
            subWrap.transform.SetParent(parent, false);
            subWrap.AddComponent<RectTransform>().sizeDelta = new Vector2(0, SH);
            var sLE = subWrap.AddComponent<LayoutElement>(); sLE.preferredHeight = SH;

            ChildText("Subtitle", subWrap.transform, "Online Adventure", 20,
                      FontStyle.Italic, TextAnchor.MiddleCenter,
                      new Color(0.75f, 0.90f, 1f, 0.85f)).GetComponent<Text>().raycastTarget = false;
        }

        /// <summary>Input field với sprite bar làm nền, tự khai báo preferred height.</summary>
        static InputField BarInput(string name, string placeholder, Sprite barSprite,
                                   Transform parent, float h = 56f, bool isPassword = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, h);
            // LayoutElement đảm bảo CSF đọc đúng preferred height
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = h;

            var bg = go.AddComponent<Image>();
            if (barSprite != null) { bg.sprite = barSprite; bg.type = Image.Type.Sliced; bg.color = White; }
            else bg.color = InputFallbk;

            var field = go.AddComponent<InputField>();

            var tGO = new GameObject("Text"); tGO.transform.SetParent(go.transform, false);
            var tRT = tGO.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(14, 3); tRT.offsetMax = new Vector2(-14, -3);
            var tComp = tGO.AddComponent<Text>();
            tComp.color = White; tComp.fontSize = 19; tComp.font = Font();

            var pGO = new GameObject("Placeholder"); pGO.transform.SetParent(go.transform, false);
            var pRT = pGO.AddComponent<RectTransform>();
            pRT.anchorMin = Vector2.zero; pRT.anchorMax = Vector2.one;
            pRT.offsetMin = new Vector2(14, 3); pRT.offsetMax = new Vector2(-14, -3);
            var pText = pGO.AddComponent<Text>();
            pText.text = placeholder; pText.fontSize = 19;
            pText.fontStyle = FontStyle.Italic; pText.color = PlaceholdrC; pText.font = Font();

            field.textComponent = tComp; field.placeholder = pText;
            if (isPassword) field.contentType = InputField.ContentType.Password;
            return field;
        }

        /// <summary>Nút sprite lớn (hành động chính của màn hình).</summary>
        static Button PrimaryBtn(string name, Sprite sp, Color fallback,
                                 Transform parent, float h = 68f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, h);
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = h;

            var img = go.AddComponent<Image>();
            if (sp != null) { img.sprite = sp; img.type = Image.Type.Simple; img.color = White; }
            else
            {
                img.color = fallback;
                AddCentredText(go.transform, name.Contains("Create") ? "TẠO TÀI KHOẢN" : "ĐĂNG NHẬP",
                               22, FontStyle.Bold, White);
            }

            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor      = White;
            cb.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            cb.pressedColor     = new Color(0.70f, 0.70f, 0.70f, 1f);
            cb.selectedColor    = White;
            btn.colors = cb;
            return btn;
        }

        /// <summary>Nút màu đơn + text (dùng cho Send / Reset submit).</summary>
        static Button PlainBtn(string name, string label, Color bg,
                               Transform parent, float h = 60f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, h);
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = h;

            var img = go.AddComponent<Image>(); img.color = bg;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            var cb  = btn.colors;
            cb.normalColor      = bg;
            cb.highlightedColor = new Color(Mathf.Min(bg.r+.12f,1f), Mathf.Min(bg.g+.12f,1f), Mathf.Min(bg.b+.12f,1f));
            cb.pressedColor     = new Color(bg.r*.75f, bg.g*.75f, bg.b*.75f);
            btn.colors = cb;
            AddCentredText(go.transform, label, 20, FontStyle.Bold, White);
            return btn;
        }

        /// <summary>Link text nhỏ, trong suốt, không chiếm nhiều không gian.</summary>
        static Button TextLink(string name, string label, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 30f);
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 30f;

            var img = go.AddComponent<Image>(); img.color = Color.clear;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            var cb  = btn.colors;
            cb.normalColor = cb.selectedColor = Color.clear;
            cb.highlightedColor = new Color(1,1,1,.06f);
            cb.pressedColor     = new Color(1,1,1,.14f);
            btn.colors = cb;
            AddCentredText(go.transform, label, 15, FontStyle.Normal, LinkColor);
            return btn;
        }

        /// <summary>Feedback text (lỗi/thành công) — ẩn khi text rỗng nhờ color.</summary>
        static Text FbLabel(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 26f);
            go.AddComponent<LayoutElement>().preferredHeight = 26f;
            var t = go.AddComponent<Text>();
            t.font = Font(); t.fontSize = 14;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 0.35f, 0.35f);
            t.text = "";
            return t;
        }

        /// <summary>Note text nhỏ nghiêng (instruction).</summary>
        static void SmallNote(string text, Transform parent)
        {
            var go = new GameObject("Note");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 36f);
            go.AddComponent<LayoutElement>().preferredHeight = 36f;
            var t = go.AddComponent<Text>();
            t.text = text; t.font = Font();
            t.fontSize = 16; t.fontStyle = FontStyle.Italic;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.72f, 0.88f, 1f, 0.85f);
            t.raycastTarget = false;
        }

        /// <summary>Spacer element trong VLG.</summary>
        static void Spacer(Transform parent, float h)
        {
            var go = new GameObject("Spacer");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, h);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = h; le.flexibleHeight = 0;
        }

        // ── Micro-helpers ─────────────────────────────────────────────────────

        static void Wire(UnityEngine.Events.UnityEvent ev, UnityEngine.Events.UnityAction action)
            => UnityEditor.Events.UnityEventTools.AddPersistentListener(ev, action);

        static GameObject Elem(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        static Font Font() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Sprite Load(string path, string label)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null) Debug.LogWarning($"[StartMenuSetup] '{label}' không tìm thấy: {path}");
            return sp;
        }

        static GameObject ChildText(string name, Transform parent, string text,
                                    int size, FontStyle style, TextAnchor align, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = text; t.font = Font();
            t.fontSize = size; t.fontStyle = style;
            t.alignment = align; t.color = color;
            return go;
        }

        static void AddCentredText(Transform parent, string text, int size,
                                   FontStyle style, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = text; t.font = Font();
            t.fontSize = size; t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter; t.color = color;
        }
    }
}
