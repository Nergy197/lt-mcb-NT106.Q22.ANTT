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

        [Header("Panels")]
        [Tooltip("Menu chính: 3 lựa chọn (Âm thanh / Điều khiển / Đăng xuất).")]
        [SerializeField] private GameObject settingsMainPanel;
        [SerializeField] private GameObject soundSettingsPanel;
        [SerializeField] private GameObject controlsSettingsPanel;
        [SerializeField] private GameObject confirmLogoutPanel;

        [Header("Audio UI")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TextMeshProUGUI volumeValueText;

        [Header("Buttons (optional — auto-find by name if empty)")]
        [SerializeField] private Button soundMenuButton;
        [SerializeField] private Button controlsMenuButton;
        [SerializeField] private Button logoutMenuButton;
        [SerializeField] private Button mainCloseButton;
        [SerializeField] private Button soundBackButton;
        [SerializeField] private Button controlsBackButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;

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

            DisableChildRaycastsOnButtons();
            WireEvents();
            RefreshVolumeVisual();
            HideAllSettingsPanels();
        }

        private void Start()
        {
            DisableChildRaycastsOnButtons();
            WireEvents();
            RefreshVolumeVisual();
        }

        /// <summary>Mở menu chính (3 lựa chọn) — gọi từ nút bánh răng.</summary>
        public void OpenSettings()
        {
            HideAllSettingsPanels();
            if (settingsMainPanel != null)
            {
                settingsMainPanel.SetActive(true);
            }
        }

        /// <summary>Đóng toàn bộ UI settings.</summary>
        public void CloseSettings()
        {
            HideAllSettingsPanels();
        }

        public void OpenSoundSettings()
        {
            if (settingsMainPanel != null)
            {
                settingsMainPanel.SetActive(false);
            }

            if (controlsSettingsPanel != null)
            {
                controlsSettingsPanel.SetActive(false);
            }

            if (soundSettingsPanel != null)
            {
                soundSettingsPanel.SetActive(true);
            }

            RefreshVolumeVisual();
        }

        public void OpenControlsSettings()
        {
            if (settingsMainPanel != null)
            {
                settingsMainPanel.SetActive(false);
            }

            if (soundSettingsPanel != null)
            {
                soundSettingsPanel.SetActive(false);
            }

            if (controlsSettingsPanel != null)
            {
                controlsSettingsPanel.SetActive(true);
            }
        }

        public void BackToMainMenu()
        {
            OpenSettings();
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
            float current = audioSettingsManager != null
                ? audioSettingsManager.CurrentVolume
                : volumeSlider != null
                    ? volumeSlider.value
                    : AudioListener.volume;

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

        private void HideAllSettingsPanels()
        {
            if (settingsMainPanel != null)
            {
                settingsMainPanel.SetActive(false);
            }

            if (soundSettingsPanel != null)
            {
                soundSettingsPanel.SetActive(false);
            }

            if (controlsSettingsPanel != null)
            {
                controlsSettingsPanel.SetActive(false);
            }

            if (confirmLogoutPanel != null)
            {
                confirmLogoutPanel.SetActive(false);
            }
        }

        private void WireEvents()
        {
            soundMenuButton = ResolveButton(soundMenuButton, settingsMainPanel, "SoundButton");
            controlsMenuButton = ResolveButton(controlsMenuButton, settingsMainPanel, "ControlsButton");
            logoutMenuButton = ResolveButton(logoutMenuButton, settingsMainPanel, "LogoutButton");
            mainCloseButton = ResolveButton(mainCloseButton, settingsMainPanel, "CloseButton");

            soundBackButton = ResolveButton(soundBackButton, soundSettingsPanel, "BackButton");
            controlsBackButton = ResolveButton(controlsBackButton, controlsSettingsPanel, "BackButton");

            cancelButton = ResolveButton(cancelButton, confirmLogoutPanel, "CancelButton");
            confirmButton = ResolveButton(confirmButton, confirmLogoutPanel, "ConfirmButton");

            WireButton(soundMenuButton, OpenSoundSettings, "SoundButton");
            WireButton(controlsMenuButton, OpenControlsSettings, "ControlsButton");
            WireButton(logoutMenuButton, OpenConfirmLogout, "LogoutButton");
            WireButton(mainCloseButton, CloseSettings, "CloseButton");

            WireButton(soundBackButton, BackToMainMenu, "SoundBackButton");
            WireButton(controlsBackButton, BackToMainMenu, "ControlsBackButton");

            WireButton(cancelButton, CloseConfirmLogout, "CancelButton");
            WireButton(confirmButton, ConfirmLogout, "ConfirmButton");

            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }
        }

        private static void WireButton(Button button, UnityEngine.Events.UnityAction action, string debugName)
        {
            if (button == null)
            {
                Debug.LogWarning($"[Settings] Không tìm thấy nút '{debugName}'.");
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void DisableChildRaycastsOnButtons()
        {
            DisableChildRaycasts(soundMenuButton);
            DisableChildRaycasts(controlsMenuButton);
            DisableChildRaycasts(logoutMenuButton);
            DisableChildRaycasts(mainCloseButton);
            DisableChildRaycasts(soundBackButton);
            DisableChildRaycasts(controlsBackButton);
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
