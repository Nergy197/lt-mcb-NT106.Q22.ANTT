using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class VPManager : MonoBehaviour
{
    public static VPManager Instance { get; private set; }

    [Header("Server")]
    [SerializeField] private string serverUrl = "https://pokemon-mmo-server-123-gkaqfbejgycbcwfb.southeastasia-01.azurewebsites.net";

    [SerializeField] private int defaultVP = 5000;
    public int CurrentVP { get; set; }

    public event Action<int> OnVPChanged;
    public event Action<bool> OnWelcomeCheckComplete;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentVP = defaultVP;
        OnVPChanged?.Invoke(CurrentVP);
    }

    private void Start()
    {
        RefreshFromServer();
    }

    public void RefreshFromServer()
    {
        StartCoroutine(FetchVP());
    }

    public void ApplyServerBalance(int vp)
    {
        CurrentVP = Mathf.Max(0, vp);
        OnVPChanged?.Invoke(CurrentVP);
    }

    private IEnumerator FetchVP()
    {
        string token = PlayerPrefs.GetString("jwt_token", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            Debug.LogWarning("[VP] Không tìm thấy JWT token, chưa thể lấy VP từ server.");
            yield break;
        }

        using (var req = UnityWebRequest.Get($"{serverUrl}/api/currency/vp"))
        {
            req.SetRequestHeader("Authorization", $"Bearer {token}");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[VP] Fetch failed: {req.responseCode} {req.error} - {req.downloadHandler.text}");
                yield break;
            }

            var response = JsonUtility.FromJson<VPBalanceResponse>(req.downloadHandler.text);
            ApplyServerBalance(response.Vp);
            OnWelcomeCheckComplete?.Invoke(response.WelcomeClaimed);
        }
    }

    public void AddVP(int amount)
    {
        StartCoroutine(DebugAddVP(amount));
    }

    public bool TrySpendVP(int cost)
    {
        if (cost < 0) return false;
        if (CurrentVP < cost) return false;

        StartCoroutine(DebugSpendVP(cost));
        return true;
    }

    private IEnumerator DebugAddVP(int amount)
    {
        yield return SendDebugVPRequest("add", amount);
    }

    private IEnumerator DebugSpendVP(int amount)
    {
        yield return SendDebugVPRequest("spend", amount);
    }

    private IEnumerator SendDebugVPRequest(string action, int amount)
    {
        string token = PlayerPrefs.GetString("jwt_token", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            Debug.LogWarning("[VP] Không tìm thấy JWT token, không thể debug thay đổi VP.");
            yield break;
        }

        string json = JsonUtility.ToJson(new DebugVPRequest { Amount = amount });
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (var req = new UnityWebRequest($"{serverUrl}/api/currency/vp/debug/{action}", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[VP] Debug {action} failed: {req.responseCode} {req.error} - {req.downloadHandler.text}");
                yield break;
            }

            var response = JsonUtility.FromJson<VPBalanceResponse>(req.downloadHandler.text);
            ApplyServerBalance(response.Vp);
        }
    }

    [Serializable]
    private class VPBalanceResponse
    {
        public int Vp;
        public bool WelcomeClaimed;
    }

    [Serializable]
    private class DebugVPRequest
    {
        public int Amount;
    }
}