using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Network;

namespace PokemonMMO.UI
{
    public class MatchmakingUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject matchmakingPanel;

        [Header("Labels")]
        [SerializeField] private TMP_Text searchTimerLabel;   // "Đang tìm: 00:23"
        [SerializeField] private TMP_Text botCountdownLabel;  // "Bot dự phòng trong: 12s"

        [Header("Cancel")]
        [SerializeField] private Button cancelButton;

        private bool  _isSearching;
        private float _elapsedSeconds;
        private int   _botSecondsLeft = -1;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            cancelButton?.onClick.AddListener(OnCancelClicked);
        }

        private void OnEnable()
        {
            MatchmakingManager.OnSearchStarted   += HandleSearchStarted;
            MatchmakingManager.OnCountdownTick   += HandleCountdownTick;
            MatchmakingManager.OnSearchCancelled += HandleSearchCancelled;
        }

        private void OnDisable()
        {
            MatchmakingManager.OnSearchStarted   -= HandleSearchStarted;
            MatchmakingManager.OnCountdownTick   -= HandleCountdownTick;
            MatchmakingManager.OnSearchCancelled -= HandleSearchCancelled;
        }

        private void Update()
        {
            if (!_isSearching) return;

            _elapsedSeconds += Time.deltaTime;
            int mins = (int)_elapsedSeconds / 60;
            int secs = (int)_elapsedSeconds % 60;

            if (searchTimerLabel != null)
                searchTimerLabel.text = $"Đang tìm: {mins:00}:{secs:00}";
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private void HandleSearchStarted()
        {
            _elapsedSeconds = 0f;
            _botSecondsLeft = -1;
            _isSearching    = true;

            if (matchmakingPanel != null) matchmakingPanel.SetActive(true);
            if (cancelButton != null)     cancelButton.interactable = true;
            if (searchTimerLabel != null) searchTimerLabel.text     = "Đang tìm: 00:00";
            if (botCountdownLabel != null) botCountdownLabel.gameObject.SetActive(false);
        }

        private void HandleCountdownTick(int secondsLeft)
        {
            _botSecondsLeft = secondsLeft;

            if (matchmakingPanel != null && !matchmakingPanel.activeSelf)
                matchmakingPanel.SetActive(true);

            if (botCountdownLabel != null)
            {
                botCountdownLabel.gameObject.SetActive(true);
                botCountdownLabel.text = secondsLeft > 0
                    ? $"Bot dự phòng trong: {secondsLeft}s"
                    : "Không tìm thấy đối thủ — đang ghép với Bot...";
            }
        }

        private void HandleSearchCancelled()
        {
            _isSearching = false;
            HidePanel();
        }

        // ── Cancel button ─────────────────────────────────────────────────────

        private void OnCancelClicked()
        {
            if (cancelButton != null) cancelButton.interactable = false;
            MatchmakingManager.Instance?.CancelSearching();
        }

        // ── Public ────────────────────────────────────────────────────────────

        public void HidePanel()
        {
            _isSearching = false;
            if (matchmakingPanel != null) matchmakingPanel.SetActive(false);
        }
    }
}
