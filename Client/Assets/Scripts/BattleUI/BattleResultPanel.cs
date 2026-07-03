using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Network;
using PokemonMMO.Audio;

namespace Game.Battle.UI
{
    /// <summary>
    /// Màn hình kết quả sau trận: thắng/thua, VP nhận được, RankPoints thay đổi.
    /// Tự động hiện khi BattleEvents.OnBattleResult được fire.
    /// Gắn vào GameObject có PanelType = Result trong scene.
    /// </summary>
    public class BattleResultPanel : BasePanel
    {
        [Header("Result Text")]
        public TextMeshProUGUI resultText;       // "CHIẾN THẮNG!" / "THẤT BẠI..."
        public TextMeshProUGUI vpDeltaText;      // "+200 VP • Tổng: 1200"
        public TextMeshProUGUI rankDeltaText;    // "+84 RP • Tổng: 840" (rỗng nếu không phải ranked)
        public TextMeshProUGUI hintText;         // "Nhấn vào màn hình để tiếp tục" (optional)

        [Header("Buttons")]
        public Button returnButton;

        [Header("Colors")]
        public Color winColor  = new Color(0.2f, 0.8f, 0.2f);
        public Color loseColor = new Color(0.9f, 0.3f, 0.3f);
        public Color drawColor = new Color(0.8f, 0.8f, 0.3f);

        [Header("SFX")]
        [SerializeField] private AudioClip sfxVictory;
        [SerializeField] private AudioClip sfxDefeat;
        [SerializeField] private AudioClip sfxCoinTick;

        // Thời gian tối đa chờ reward events trước khi tự hiện
        private const float RewardWaitTimeout = 3f;

        private bool _iWon;
        private bool _isDraw;
        private int? _vpDelta;
        private int? _rankDelta;
        private int _vpTotal;
        private int _rankTotal;
        private bool _readyToClose;   // chỉ cho phép click tắt panel sau khi đã hiện xong kết quả

        private void Awake()
        {
            PanelType = BattlePanelType.Result;
            returnButton?.onClick.AddListener(OnReturnClicked);
        }

        private void OnEnable()
        {
            BattleEvents.OnBattleResult    += OnBattleResult;
            SignalRClient.OnVPRewardReceived   += OnVPReceived;
            SignalRClient.OnRankRewardReceived += OnRankReceived;

            // Panel bị inactive lúc trận kết thúc nên KHÔNG nhận được OnBattleResult
            // (chưa subscribe khi event phát). BattleUIManager bật panel lên để phản ứng
            // với chính event đó → giờ OnEnable chạy, ta đọc lại kết quả đã cache.
            if (BattleEvents.HasPendingResult)
            {
                BattleEvents.HasPendingResult = false;
                OnBattleResult(BattleEvents.PendingResultIWon, BattleEvents.PendingResultWinnerId);
            }
        }

        private void OnDisable()
        {
            BattleEvents.OnBattleResult    -= OnBattleResult;
            SignalRClient.OnVPRewardReceived   -= OnVPReceived;
            SignalRClient.OnRankRewardReceived -= OnRankReceived;
        }

        private void OnBattleResult(bool iWon, string winnerId)
        {
            _iWon = iWon;
            _isDraw = string.IsNullOrEmpty(winnerId);
            _vpDelta = null;
            _rankDelta = null;
            _readyToClose = false;
            if (hintText != null) hintText.text = "";

            // Đặt tiêu đề thắng/thua NGAY (không đợi phần thưởng) để tránh nháy:
            // panel vừa bật lên đang hiển thị text placeholder "CHIEN THANG!" của scene.
            ApplyResultTitle();
            // Ẩn số VP/RP placeholder trong lúc chờ event thưởng để không hiện nhầm.
            if (vpDeltaText   != null) vpDeltaText.text   = "";
            if (rankDeltaText != null) rankDeltaText.text = "";

            StartCoroutine(ShowResultRoutine());
        }

        private void ApplyResultTitle()
        {
            AudioManager.Instance?.PlaySFX(_isDraw ? null : (_iWon ? sfxVictory : sfxDefeat));
            if (resultText != null)
            {
                resultText.text  = _isDraw ? "HOA!" : (_iWon ? "CHIEN THANG!" : "THAT BAI...");
                resultText.color = _isDraw ? drawColor : (_iWon ? winColor : loseColor);
            }
        }

        private void OnVPReceived(int total, int delta)    { _vpDelta   = delta; _vpTotal   = total; }
        private void OnRankReceived(int total, int delta)  { _rankDelta = delta; _rankTotal = total; }

        private void Update()
        {
            // Panel chỉ tắt khi người chơi click/chạm vào màn hình (sau khi đã hiện xong kết quả)
            if (!_readyToClose) return;

            bool clicked = Input.GetMouseButtonDown(0)
                           || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
            if (clicked)
            {
                _readyToClose = false;
                OnReturnClicked();
            }
        }

        private IEnumerator ShowResultRoutine()
        {
            // Đợi tối đa RewardWaitTimeout giây để nhận VP/Rank events từ server
            float elapsed = 0f;
            while (elapsed < RewardWaitTimeout)
            {
                elapsed += Time.deltaTime;
                // Dừng sớm nếu đã có cả 2 rewards (hoặc chỉ VP với casual/bot)
                if (_vpDelta.HasValue) break;
                yield return null;
            }

            UpdateUI();
        }

        private void UpdateUI()
        {
            // Tiêu đề thắng/thua đã đặt ngay trong OnBattleResult (ApplyResultTitle).
            // UpdateUI chỉ lo phần thưởng VP/RP và hint sau khi đã chờ event thưởng.
            if (vpDeltaText != null)
            {
                if (_vpDelta.HasValue)
                {
                    vpDeltaText.color = _vpDelta.Value >= 0 ? winColor : loseColor;
                    StartCoroutine(CountUpText(vpDeltaText, _vpDelta.Value, "VP", _vpTotal));
                }
                else
                {
                    vpDeltaText.text = "";
                }
            }

            if (rankDeltaText != null)
            {
                if (_rankDelta.HasValue)
                {
                    rankDeltaText.color = _rankDelta.Value >= 0 ? winColor : loseColor;
                    StartCoroutine(CountUpText(rankDeltaText, _rankDelta.Value, "RP", _rankTotal));
                }
                else
                {
                    rankDeltaText.text = "";
                }
            }

            // Hiện xong kết quả → cho phép click để tắt panel
            const string hint = "Nhấn vào màn hình để tiếp tục";
            if (hintText != null)
                hintText.text = hint;
            else if (resultText != null)
                resultText.text += $"\n<size=45%>{hint}</size>"; // fallback nếu chưa gắn ô hint riêng
            _readyToClose = true;
        }

        private IEnumerator CountUpText(TMPro.TextMeshProUGUI label, int target, string suffix, int total)
        {
            string sign  = target >= 0 ? "+" : "";
            string tail  = total > 0 ? $"  •  Tổng: {total}" : "";
            int abs      = Mathf.Abs(target);
            int steps    = Mathf.Min(abs, 20);
            if (steps == 0) { label.text = $"{sign}{target} {suffix}{tail}"; yield break; }

            for (int i = 1; i <= steps; i++)
            {
                int display = Mathf.RoundToInt(Mathf.Lerp(0, abs, (float)i / steps)) * (target < 0 ? -1 : 1);
                label.text = $"{sign}{display} {suffix}";
                AudioManager.Instance?.PlaySFX(sfxCoinTick);
                yield return new WaitForSeconds(0.05f);
            }
            label.text = $"{sign}{target} {suffix}{tail}";
        }

        private void OnReturnClicked()
        {
            Game.Network.MatchmakingManager.ResetBattleId();
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu scene");
        }
    }
}
