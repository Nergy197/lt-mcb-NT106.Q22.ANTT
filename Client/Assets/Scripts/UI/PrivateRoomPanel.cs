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

        // Đánh dấu host đã tạo phòng trên server (cần dọn khi hủy)
        private bool _roomCreated;

        /// <summary>
        /// Gán tham chiếu bằng code (dùng khi panel được dựng runtime thay vì
        /// kéo-thả trong Inspector). Gọi TRƯỚC khi panel được Show() lần đầu.
        /// </summary>
        public void Bind(GameObject panel, GameObject createView, GameObject joinView,
                         TMP_Text codeDisplay, Button createRoomBtn, Button switchToJoinBtn,
                         TMP_InputField codeInput, Button joinRoomBtn, Button switchToCreateBtn,
                         Button cancelBtn, TMP_Text statusLabel)
        {
            this.panel             = panel;
            this.createView        = createView;
            this.joinView          = joinView;
            this.codeDisplay       = codeDisplay;
            this.createRoomBtn     = createRoomBtn;
            this.switchToJoinBtn   = switchToJoinBtn;
            this.codeInput         = codeInput;
            this.joinRoomBtn       = joinRoomBtn;
            this.switchToCreateBtn = switchToCreateBtn;
            this.cancelBtn         = cancelBtn;
            this.statusLabel       = statusLabel;
        }

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
            cancelBtn?.onClick.AddListener(OnCancel);
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

        // Hủy: dọn phòng đã tạo trên server (nếu có) rồi đóng panel
        private void OnCancel()
        {
            if (_roomCreated)
            {
                MatchmakingManager.Instance?.CancelPrivateRoom();
                _roomCreated = false;
            }
            Hide();
        }

        private void ShowCreateView()
        {
            createView.SetActive(true);
            joinView.SetActive(false);
            if (codeDisplay != null) codeDisplay.text = "---";
            if (_roomCreated)
            {
                MatchmakingManager.Instance?.CancelPrivateRoom();
                _roomCreated = false;
            }
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
            _roomCreated = true;
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
