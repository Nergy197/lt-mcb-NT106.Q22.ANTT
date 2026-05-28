using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Game.Battle.UI;

namespace Game.Battle.Logic
{
    /// <summary>
    /// Overlay toàn màn hình che battle scene trong khi chờ kết nối server.
    /// Tự ẩn khi nhận TeamPreviewReady hoặc BattleRunning (OnBattleConnected).
    /// Timeout 15s → hiện lỗi → về menu.
    /// Gắn vào một Image phủ full-screen trong battle scene.
    /// </summary>
    public class BattleLoadingOverlay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;
        private const float TimeoutSeconds = 15f;
        private bool _hidden;

        private void Awake()
        {
            gameObject.SetActive(true);
        }

        private void OnEnable()
        {
            _hidden = false;
            BattleEvents.OnTeamPreviewStart += OnTeamPreview;
            BattleEvents.OnBattleConnected  += Hide;
            StartCoroutine(TimeoutRoutine());
        }

        private void OnDisable()
        {
            BattleEvents.OnTeamPreviewStart -= OnTeamPreview;
            BattleEvents.OnBattleConnected  -= Hide;
        }

        private void OnTeamPreview(PreviewTeamData _) => Hide();

        public void Hide()
        {
            if (_hidden) return;
            _hidden = true;
            gameObject.SetActive(false);
        }

        private IEnumerator TimeoutRoutine()
        {
            yield return new WaitForSeconds(TimeoutSeconds);
            if (_hidden) yield break;
            if (statusText != null)
                statusText.text = "Không thể kết nối tới trận đấu. Đang trở về menu...";
            yield return new WaitForSeconds(3f);
            SceneManager.LoadScene("Menu scene");
        }
    }
}
