using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Network;

namespace PokemonMMO.UI
{
    public class PrivateRoomPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panel;

        [Header("Tạo phòng")]
        [SerializeField] private GameObject createView;
        [SerializeField] private TMP_Text codeDisplay;
        [SerializeField] private Button createRoomBtn;

        [Header("Nhập mã")]
        [SerializeField] private GameObject joinView;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button joinRoomBtn;

        [Header("Chuyển tab")]
        [SerializeField] private Button switchToJoinBtn;
        [SerializeField] private Button switchToCreateBtn;

        [Header("Chung")]
        [SerializeField] private Button cancelBtn;
        [SerializeField] private TMP_Text statusLabel;

        private void Awake()
        {
            if (codeInput != null)
            {
                codeInput.contentType      = TMP_InputField.ContentType.IntegerNumber;
                codeInput.characterLimit   = 6;
            }

            createRoomBtn?.onClick.AddListener(OnCreateRoom);
            joinRoomBtn?.onClick.AddListener(OnJoinRoom);
            switchToJoinBtn?.onClick.AddListener(ShowJoinView);
            switchToCreateBtn?.onClick.AddListener(ShowCreateView);
            cancelBtn?.onClick.AddListener(Hide);
        }

        private void OnEnable()
        {
            MatchmakingManager.OnPrivateRoomCreated += HandleRoomCreated;
            MatchmakingManager.OnServerError        += HandleError;
        }

        private void OnDisable()
        {
            MatchmakingManager.OnPrivateRoomCreated -= HandleRoomCreated;
            MatchmakingManager.OnServerError        -= HandleError;
        }

        public void Show()
        {
            panel.SetActive(true);
            ShowCreateView();
            SetStatus("");
        }

        public void Hide()
        {
            panel.SetActive(false);
        }

        private void ShowCreateView()
        {
            createView.SetActive(true);
            joinView.SetActive(false);
            if (codeDisplay != null) codeDisplay.text = "---";
        }

        private void ShowJoinView()
        {
            createView.SetActive(false);
            joinView.SetActive(true);
            if (codeInput != null) codeInput.text = "";
        }

        private void OnCreateRoom()
        {
            SetStatus("Đang tạo phòng...");
            MatchmakingManager.Instance?.CreatePrivateRoom();
        }

        private void OnJoinRoom()
        {
            var code = codeInput?.text?.Trim();
            if (string.IsNullOrEmpty(code))
            {
                SetStatus("Vui lòng nhập mã phòng.");
                return;
            }
            SetStatus("Đang tham gia...");
            MatchmakingManager.Instance?.JoinPrivateRoom(code);
        }

        private void HandleRoomCreated(string code)
        {
            if (codeDisplay != null) codeDisplay.text = code;
            SetStatus("Chờ đối thủ nhập mã...");
        }

        private void HandleError(string msg)
        {
            SetStatus($"Lỗi: {msg}");
        }

        private void SetStatus(string msg)
        {
            if (statusLabel != null) statusLabel.text = msg;
        }
    }
}
