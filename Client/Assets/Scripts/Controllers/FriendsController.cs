using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class FriendsController : MonoBehaviour
{
    public GameObject friendsPopup;
    public Image friendsButtonBottom;

    [Header("Tab Settings")]
    public GameObject friendsTabPanel;
    public GameObject addFriendTabPanel;
    public RectTransform friendsTabButton;
    public RectTransform addFriendTabButton;

    [Header("Add Friend Controls")]
    public TMP_InputField addFriendNameInput;
    public Button addFriendSubmitButton;
    public TMP_Text addFriendSubmitButtonText;
    public string addFriendButtonDefaultText = "Th\xeam b\u1EA1n";
    public string addFriendButtonAddedText = "\u0110\xE3 th\xEAm";
    public GameObject addFriendDefaultVisual;
    public GameObject addFriendAddedVisual;
    public Image addFriendSubmitButtonImage;
    public Sprite addFriendDefaultSprite;
    public Sprite addFriendAddedSprite;
    public string friendsApiUrl = "https://lt-mcb-nt106q22antt-production-cc69.up.railway.app/api/friends/request";

    [Header("Incoming Requests")]
    public FriendRequestListLoader friendRequestListLoader;

    [Header("Error Popup")]
    public GameObject errorPopup;
    public TMP_Text errorPopupMessageText;

    private static readonly Color InactiveColor = Color.white;
    private static readonly Color ActiveColor = new(0.7f, 0.7f, 0.7f, 1f);
    private static readonly Regex PlayerNameRegex = new("^[a-zA-Z0-9_]{2,20}$", RegexOptions.Compiled);

    private bool isSubmittingAddFriend;

    private void OnEnable()
    {
        if (addFriendNameInput != null)
            addFriendNameInput.onValueChanged.AddListener(OnAddFriendNameChanged);
    }

    private void OnDisable()
    {
        if (addFriendNameInput != null)
            addFriendNameInput.onValueChanged.RemoveListener(OnAddFriendNameChanged);
    }

    public void ToggleFriendsPopup()
    {
        if (friendsPopup == null) return;

        if (friendsPopup.activeSelf)
        {
            CloseAll();
        }
        else
        {
            OpenFriendsTab();
        }
    }

    private void CloseAll()
    {
        if (friendsPopup != null) friendsPopup.SetActive(false);
        if (friendsButtonBottom != null) friendsButtonBottom.color = InactiveColor;
    }

    public void ShowFriendsTab()
    {
        OpenFriendsTab();
    }

    public void ShowAddTab()
    {
        OpenAddTab();
    }

    public void SubmitAddFriend()
    {
        if (isSubmittingAddFriend)
            return;

        string playerName = addFriendNameInput == null ? string.Empty : addFriendNameInput.text.Trim();
        if (!IsValidPlayerName(playerName))
        {
            ShowErrorPopup("T\xEAn ng\u01B0\u1EDDi ch\u01A1i kh\xF4ng h\u1EE3p l\u1EC7. Ch\u1EC9 d\xF9ng 2-20 k\xFD t\u1EF1: ch\u1EEF, s\u1ED1 ho\u1EB7c _.");
            return;
        }

        StartCoroutine(SendAddFriendRequest(playerName));
    }

    public void CloseErrorPopup()
    {
        if (errorPopup != null)
            errorPopup.SetActive(false);
    }

    private void OpenFriendsTab()
    {
        if (friendsPopup != null) friendsPopup.SetActive(true);
        EnsureTabButtonsClickable();
        SetActiveTab(friendsTabPanel, addFriendTabPanel);
        if (friendsButtonBottom != null) friendsButtonBottom.color = ActiveColor;
    }

    private void OpenAddTab()
    {
        if (friendsPopup != null) friendsPopup.SetActive(true);
        EnsureTabButtonsClickable();
        SetActiveTab(addFriendTabPanel, friendsTabPanel);
        SetAddFriendButtonState(isAdded: false);
        friendRequestListLoader?.Refresh();
        if (friendsButtonBottom != null) friendsButtonBottom.color = ActiveColor;
    }

    private static void SetActiveTab(GameObject activeTab, GameObject inactiveTab)
    {
        if (activeTab == null)
        {
            Debug.LogWarning("FriendsController: active tab panel is not assigned.");
            return;
        }

        activeTab.SetActive(true);
        if (inactiveTab != null) inactiveTab.SetActive(false);
    }

    private void EnsureTabButtonsClickable()
    {
        if (friendsTabButton != null)
            friendsTabButton.gameObject.SetActive(true);

        if (addFriendTabButton != null)
            addFriendTabButton.gameObject.SetActive(true);

        // Keep tab buttons at the top of draw/raycast stack.
        if (friendsTabButton != null)
            friendsTabButton.SetAsLastSibling();

        if (addFriendTabButton != null)
            addFriendTabButton.SetAsLastSibling();
    }

    private IEnumerator SendAddFriendRequest(string playerName)
    {
        isSubmittingAddFriend = true;
        SetAddFriendButtonInteractable(false);

        string token = PlayerPrefs.GetString("jwt_token", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            isSubmittingAddFriend = false;
            SetAddFriendButtonInteractable(true);
            ShowErrorPopup("B\u1EA1n ch\u01B0a \u0111\u0103ng nh\u1EADp. Vui l\xF2ng \u0111\u0103ng nh\u1EADp l\u1EA1i.");
            yield break;
        }

        FriendRequestPayload payload = new() { playerName = playerName };
        string body = JsonUtility.ToJson(payload);

        using UnityWebRequest request = new(friendsApiUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + token);
        request.timeout = 10;

        yield return request.SendWebRequest();

        bool success = request.result == UnityWebRequest.Result.Success &&
                       request.responseCode >= 200 &&
                       request.responseCode < 300;

        if (success)
        {
            SetAddFriendButtonState(isAdded: true);
        }
        else
        {
            SetAddFriendButtonInteractable(true);
            ShowErrorPopup(ParseServerMessage(request.downloadHandler?.text) ?? "Kh\xF4ng th\u1EC3 th\xEAm b\u1EA1n. Vui l\xF2ng th\u1EED l\u1EA1i.");
        }

        isSubmittingAddFriend = false;
    }

    private void SetAddFriendButtonState(bool isAdded)
    {
        if (addFriendSubmitButtonText != null)
            addFriendSubmitButtonText.text = isAdded ? addFriendButtonAddedText : addFriendButtonDefaultText;

        if (addFriendDefaultVisual != null)
            addFriendDefaultVisual.SetActive(!isAdded);

        if (addFriendAddedVisual != null)
            addFriendAddedVisual.SetActive(isAdded);

        if (addFriendSubmitButtonImage != null)
        {
            if (isAdded && addFriendAddedSprite != null)
                addFriendSubmitButtonImage.sprite = addFriendAddedSprite;
            else if (!isAdded && addFriendDefaultSprite != null)
                addFriendSubmitButtonImage.sprite = addFriendDefaultSprite;
        }

        if (addFriendSubmitButton != null)
            addFriendSubmitButton.interactable = !isAdded;
    }

    private void SetAddFriendButtonInteractable(bool isInteractable)
    {
        if (addFriendSubmitButton != null)
            addFriendSubmitButton.interactable = isInteractable;
    }

    private void OnAddFriendNameChanged(string _)
    {
        if (!isSubmittingAddFriend)
            SetAddFriendButtonState(isAdded: false);
    }

    private void ShowErrorPopup(string message)
    {
        if (errorPopupMessageText != null)
            errorPopupMessageText.text = message;

        if (errorPopup != null)
            errorPopup.SetActive(true);
    }

    private static bool IsValidPlayerName(string playerName)
    {
        return !string.IsNullOrWhiteSpace(playerName) && PlayerNameRegex.IsMatch(playerName);
    }

    private static string ParseServerMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            ErrorResponse response = JsonUtility.FromJson<ErrorResponse>(json);
            return string.IsNullOrWhiteSpace(response?.message) ? null : response.message;
        }
        catch
        {
            return null;
        }
    }

    [System.Serializable]
    private class FriendRequestPayload
    {
        public string playerName;
    }

    [System.Serializable]
    private class ErrorResponse
    {
        public string message;
    }
}