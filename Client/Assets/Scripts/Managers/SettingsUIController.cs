using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PokemonMMO.UI
{
    public class SettingsUIController : MonoBehaviour
    {
        [Header("References (Optional, auto-find if empty)")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private LogoutManager logoutManager;
        [SerializeField] private AudioSettingsManager audioSettingsManager;

        [Header("Runtime UI")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject confirmLogoutPanel;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TextMeshProUGUI volumeValueText;

        [Header("Buttons (optional — auto-find by name if empty)")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button logoutButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;

        private Button settingsButton;
        private Font uiFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapMenuSettingsUI()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != "Menu scene")
            {
                return;
            }

            if (FindFirstObjectByType<SettingsUIController>() != null)
            {
                return;
            }

            var host = new GameObject("SettingsUIRuntime");
            host.AddComponent<AudioSettingsManager>();
            host.AddComponent<SettingsUIController>();
        }

        private void Awake()
        {
            if (targetCanvas == null)
            {
                targetCanvas = FindFirstObjectByType<Canvas>();
            }

            if (logoutManager == null)
            {
                logoutManager = FindFirstObjectByType<LogoutManager>();
            }

            if (audioSettingsManager == null)
            {
                audioSettingsManager = FindFirstObjectByType<AudioSettingsManager>();
            }

            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            BuildRuntimeUIIfNeeded();
            DisableChildRaycastsOnButtons();
            WireEvents();
            RefreshVolumeVisual();
            CloseConfirmLogout();
            CloseSettings();
        }

        private void Start()
        {
            DisableChildRaycastsOnButtons();
            WireEvents();
            RefreshVolumeVisual();
        }

        public void OpenSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
            }
        }

        public void CloseSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        public void OpenConfirmLogout()
        {
            if (confirmLogoutPanel != null)
            {
                confirmLogoutPanel.SetActive(true);
            }
        }

        public void CloseConfirmLogout()
        {
            if (confirmLogoutPanel != null)
            {
                confirmLogoutPanel.SetActive(false);
            }
        }

        public void ConfirmLogout()
        {
            CloseConfirmLogout();
            CloseSettings();

            if (logoutManager == null)
            {
                logoutManager = FindFirstObjectByType<LogoutManager>();
            }

            if (logoutManager != null)
            {
                logoutManager.OnLogoutClicked();
                return;
            }

            Debug.LogWarning("[Settings] LogoutManager not found, cannot logout.");
        }

        public void OnVolumeChanged(float value)
        {
            if (audioSettingsManager == null)
            {
                audioSettingsManager = FindFirstObjectByType<AudioSettingsManager>();
            }

            audioSettingsManager?.SetMasterVolume(value);
            UpdateVolumeLabel(value);
        }

        private void RefreshVolumeVisual()
        {
            float current = volumeSlider != null
                ? volumeSlider.value
                : audioSettingsManager != null
                    ? audioSettingsManager.CurrentVolume
                    : AudioListener.volume;

            if (audioSettingsManager != null)
            {
                current = audioSettingsManager.CurrentVolume;
            }

            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(current);
            }

            UpdateVolumeLabel(current);
        }

        private void UpdateVolumeLabel(float normalizedValue)
        {
            if (volumeValueText == null)
            {
                return;
            }

            volumeValueText.text = $"Âm lượng: {Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * 100f)}%";
        }

        private void WireEvents()
        {
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OpenSettings);
            }

            closeButton = ResolveButton(closeButton, settingsPanel, "CloseButton");
            logoutButton = ResolveButton(logoutButton, settingsPanel, "LogoutButton");
            cancelButton = ResolveButton(cancelButton, confirmLogoutPanel, "CancelButton");
            confirmButton = ResolveButton(confirmButton, confirmLogoutPanel, "ConfirmButton");

            WireButton(closeButton, CloseSettings, "CloseButton");
            WireButton(logoutButton, OpenConfirmLogout, "LogoutButton");
            WireButton(cancelButton, CloseConfirmLogout, "CancelButton");
            WireButton(confirmButton, ConfirmLogout, "ConfirmButton");

            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }
            else
            {
                Debug.LogWarning("[Settings] Volume Slider chưa được gán trong SettingsUIController.");
            }
        }

        private static void WireButton(Button button, UnityEngine.Events.UnityAction action, string debugName)
        {
            if (button == null)
            {
                Debug.LogWarning($"[Settings] Không tìm thấy nút '{debugName}'. Gán trong Inspector hoặc đặt đúng tên object.");
                return;
            }

            // Chỉ gỡ listener code cũ, không xóa UnityEvent đã gán trong scene.
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void DisableChildRaycastsOnButtons()
        {
            DisableChildRaycasts(closeButton);
            DisableChildRaycasts(logoutButton);
            DisableChildRaycasts(cancelButton);
            DisableChildRaycasts(confirmButton);
        }

        private static void DisableChildRaycasts(Button button)
        {
            if (button == null)
            {
                return;
            }

            foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.gameObject == button.gameObject)
                {
                    continue;
                }

                graphic.raycastTarget = false;
            }
        }

        private static Button ResolveButton(Button assigned, GameObject root, string childName)
        {
            if (assigned != null)
            {
                return assigned;
            }

            return FindButtonRecursive(root, childName);
        }

        private void BuildRuntimeUIIfNeeded()
        {
            if (targetCanvas == null)
            {
                Debug.LogWarning("[Settings] Canvas not found, cannot build runtime UI.");
                return;
            }

            if (settingsButton == null)
            {
                settingsButton = CreateButton(targetCanvas.transform, "SettingsButton", "SET");
                var rt = settingsButton.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-24f, -24f);
                rt.sizeDelta = new Vector2(60f, 60f);
            }

            if (settingsPanel == null)
            {
                settingsPanel = CreatePanel(targetCanvas.transform, "SettingsPanel", new Vector2(460f, 300f));

                var title = CreateText(settingsPanel.transform, "Title", "Settings", 28, TextAnchor.UpperCenter);
                var titleRt = title.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.offsetMin = new Vector2(16f, -56f);
                titleRt.offsetMax = new Vector2(-16f, -16f);

                volumeValueText = CreateTmpText(settingsPanel.transform, "VolumeLabel", "Volume: 100%", 20, TextAlignmentOptions.MidlineLeft);
                var labelRt = volumeValueText.GetComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0f, 1f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.offsetMin = new Vector2(24f, -120f);
                labelRt.offsetMax = new Vector2(-24f, -80f);

                volumeSlider = CreateSlider(settingsPanel.transform, "VolumeSlider");
                var sliderRt = volumeSlider.GetComponent<RectTransform>();
                sliderRt.anchorMin = new Vector2(0f, 1f);
                sliderRt.anchorMax = new Vector2(1f, 1f);
                sliderRt.offsetMin = new Vector2(24f, -170f);
                sliderRt.offsetMax = new Vector2(-24f, -130f);

                var closeButton = CreateButton(settingsPanel.transform, "CloseButton", "Close");
                var closeRt = closeButton.GetComponent<RectTransform>();
                closeRt.anchorMin = new Vector2(0f, 0f);
                closeRt.anchorMax = new Vector2(0f, 0f);
                closeRt.anchoredPosition = new Vector2(90f, 32f);
                closeRt.sizeDelta = new Vector2(140f, 48f);

                var logoutButton = CreateButton(settingsPanel.transform, "LogoutButton", "Logout");
                var logoutRt = logoutButton.GetComponent<RectTransform>();
                logoutRt.anchorMin = new Vector2(1f, 0f);
                logoutRt.anchorMax = new Vector2(1f, 0f);
                logoutRt.anchoredPosition = new Vector2(-90f, 32f);
                logoutRt.sizeDelta = new Vector2(140f, 48f);
            }

            if (confirmLogoutPanel == null)
            {
                confirmLogoutPanel = CreatePanel(targetCanvas.transform, "ConfirmLogoutPanel", new Vector2(360f, 190f));

                var prompt = CreateText(confirmLogoutPanel.transform, "PromptText", "Are you sure you want to logout?", 20, TextAnchor.MiddleCenter);
                var promptRt = prompt.GetComponent<RectTransform>();
                promptRt.anchorMin = new Vector2(0f, 1f);
                promptRt.anchorMax = new Vector2(1f, 1f);
                promptRt.offsetMin = new Vector2(16f, -90f);
                promptRt.offsetMax = new Vector2(-16f, -16f);

                var cancelButton = CreateButton(confirmLogoutPanel.transform, "CancelButton", "Cancel");
                var cancelRt = cancelButton.GetComponent<RectTransform>();
                cancelRt.anchorMin = new Vector2(0f, 0f);
                cancelRt.anchorMax = new Vector2(0f, 0f);
                cancelRt.anchoredPosition = new Vector2(90f, 30f);
                cancelRt.sizeDelta = new Vector2(120f, 44f);

                var confirmButton = CreateButton(confirmLogoutPanel.transform, "ConfirmButton", "Confirm");
                var confirmRt = confirmButton.GetComponent<RectTransform>();
                confirmRt.anchorMin = new Vector2(1f, 0f);
                confirmRt.anchorMax = new Vector2(1f, 0f);
                confirmRt.anchoredPosition = new Vector2(-90f, 30f);
                confirmRt.sizeDelta = new Vector2(120f, 44f);
            }
        }

        private GameObject CreatePanel(Transform parent, string panelName, Vector2 size)
        {
            var panelObj = new GameObject(panelName, typeof(Image));
            panelObj.transform.SetParent(parent, false);

            var rt = panelObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            var image = panelObj.GetComponent<Image>();
            image.color = new Color(0.09f, 0.1f, 0.16f, 0.92f);

            return panelObj;
        }

        private Button CreateButton(Transform parent, string buttonName, string buttonText)
        {
            var buttonObj = new GameObject(buttonName, typeof(Image), typeof(Button));
            buttonObj.transform.SetParent(parent, false);

            var image = buttonObj.GetComponent<Image>();
            image.color = new Color(0.2f, 0.24f, 0.35f, 0.95f);

            var label = CreateText(buttonObj.transform, "Label", buttonText, 20, TextAnchor.MiddleCenter);
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            return buttonObj.GetComponent<Button>();
        }

        private Slider CreateSlider(Transform parent, string sliderName)
        {
            var sliderObj = new GameObject(sliderName);
            sliderObj.transform.SetParent(parent, false);
            var sliderRt = sliderObj.AddComponent<RectTransform>();
            sliderRt.sizeDelta = new Vector2(320f, 32f);

            var background = new GameObject("Background", typeof(Image));
            background.transform.SetParent(sliderObj.transform, false);
            var bgRt = background.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.25f);
            bgRt.anchorMax = new Vector2(1f, 0.75f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(sliderObj.transform, false);
            var fillAreaRt = fillArea.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.offsetMin = new Vector2(10f, 0f);
            fillAreaRt.offsetMax = new Vector2(-10f, 0f);

            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            fill.GetComponent<Image>().color = new Color(0.2f, 0.7f, 0.35f, 1f);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var handle = new GameObject("Handle", typeof(Image));
            handle.transform.SetParent(sliderObj.transform, false);
            handle.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.92f, 1f);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(22f, 32f);

            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        private Text CreateText(Transform parent, string textName, string content, int fontSize, TextAnchor alignment)
        {
            var textObj = new GameObject(textName, typeof(Text));
            textObj.transform.SetParent(parent, false);

            var text = textObj.GetComponent<Text>();
            text.font = uiFont;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;

            return text;
        }

        private TextMeshProUGUI CreateTmpText(Transform parent, string textName, string content, int fontSize, TextAlignmentOptions alignment)
        {
            var textObj = new GameObject(textName, typeof(TextMeshProUGUI));
            textObj.transform.SetParent(parent, false);

            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;

            return text;
        }

        private static Button FindButtonRecursive(GameObject root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name == childName)
                {
                    return button;
                }
            }

            return null;
        }
    }
}
