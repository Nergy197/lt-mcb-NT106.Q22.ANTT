using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Network;

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
        public TextMeshProUGUI vpDeltaText;      // "+200 VP" / "+80 VP"
        public TextMeshProUGUI rankDeltaText;    // "+84 RP" / "-77 RP" (null nếu không phải ranked)

        [Header("Buttons")]
        public Button returnButton;

        [Header("Colors")]
        public Color winColor  = new Color(0.2f, 0.8f, 0.2f);
        public Color loseColor = new Color(0.9f, 0.3f, 0.3f);

        // Thời gian tối đa chờ reward events trước khi tự hiện
        private const float RewardWaitTimeout = 3f;

        private bool _iWon;
        private int? _vpDelta;
        private int? _rankDelta;

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
            _vpDelta = null;
            _rankDelta = null;
            StartCoroutine(ShowResultRoutine());
        }

        private void OnVPReceived(int total, int delta)    => _vpDelta   = delta;
        private void OnRankReceived(int total, int delta)  => _rankDelta = delta;

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
            if (resultText != null)
            {
                resultText.text  = _iWon ? "CHIEN THANG!" : "THAT BAI...";
                resultText.color = _iWon ? winColor : loseColor;
            }

            if (vpDeltaText != null)
            {
                if (_vpDelta.HasValue)
                {
                    string sign = _vpDelta.Value >= 0 ? "+" : "";
                    vpDeltaText.text  = $"{sign}{_vpDelta.Value} VP";
                    vpDeltaText.color = _vpDelta.Value >= 0 ? winColor : loseColor;
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
                    string sign = _rankDelta.Value >= 0 ? "+" : "";
                    rankDeltaText.text  = $"{sign}{_rankDelta.Value} RP";
                    rankDeltaText.color = _rankDelta.Value >= 0 ? winColor : loseColor;
                }
                else
                {
                    rankDeltaText.text = "";
                }
            }
        }

        private void OnReturnClicked()
        {
            Game.Network.MatchmakingManager.ResetBattleId();
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu scene");
        }
    }
}
