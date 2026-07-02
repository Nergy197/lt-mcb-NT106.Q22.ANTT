using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public class FriendRequestListLoader : MonoBehaviour
{
    [Header("UI")]
    public GameObject requestItemPrefab;
    public Transform container;
    public GameObject emptyStateObject;

    [Header("Avatars")]
    public Sprite[] pokemonAvatarPool;

    [Header("API")]
    public string requestsApiUrl = "https://pokemon-mmo-server-123-gkaqfbejgycbcwfb.southeastasia-01.azurewebsites.net/api/friends/requests";
    public string respondApiUrl = "https://pokemon-mmo-server-123-gkaqfbejgycbcwfb.southeastasia-01.azurewebsites.net/api/friends/respond";

    private bool isResponding;

    private void OnEnable()
    {
        isResponding = false;
        Refresh();
    }

    private void OnDisable()
    {
        isResponding = false;
        StopAllCoroutines();
    }

    public void Refresh()
    {
        if (container == null || requestItemPrefab == null)
            return;

        StopAllCoroutines();
        StartCoroutine(FetchRequests());
    }

    private IEnumerator FetchRequests()
    {
        string token = PlayerPrefs.GetString("jwt_token", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            ClearList();
            SetEmptyStateVisible(true);
            yield break;
        }

        using UnityWebRequest request = UnityWebRequest.Get(requestsApiUrl);
        request.SetRequestHeader("Authorization", "Bearer " + token);
        request.timeout = 10;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Lỗi lấy lời mời kết bạn: {request.error} | HTTP {request.responseCode}");
            yield break;
        }

        try
        {
            List<FriendRequestData> requests =
                JsonConvert.DeserializeObject<List<FriendRequestData>>(request.downloadHandler.text);
            PopulateUI(requests ?? new List<FriendRequestData>());
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Lỗi parse JSON lời mời kết bạn: " + ex.Message);
        }
    }

    private void PopulateUI(List<FriendRequestData> requests)
    {
        ClearList();
        SetEmptyStateVisible(requests.Count == 0);

        foreach (FriendRequestData data in requests)
        {
            GameObject obj = Instantiate(requestItemPrefab, container);
            FriendRequestItemUI itemUI = obj.GetComponent<FriendRequestItemUI>();
            if (itemUI == null)
                continue;

            Sprite avatar = ResolveAvatar(data.requesterId);
            itemUI.SetData(data.friendshipId, data.requesterName, avatar);
            itemUI.BindActions(OnAccept, OnDecline);
        }
    }

    private Sprite ResolveAvatar(string requesterId) =>
        FriendListLoader.GetOrAssignAvatar(requesterId, pokemonAvatarPool);

    private void OnAccept(string friendshipId)
    {
        StartCoroutine(RespondToRequest(friendshipId, accept: true));
    }

    private void OnDecline(string friendshipId)
    {
        StartCoroutine(RespondToRequest(friendshipId, accept: false));
    }

    private IEnumerator RespondToRequest(string friendshipId, bool accept)
    {
        if (isResponding || string.IsNullOrWhiteSpace(friendshipId))
            yield break;

        isResponding = true;
        string token = PlayerPrefs.GetString("jwt_token", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            isResponding = false;
            yield break;
        }

        RespondPayload payload = new() { friendshipId = friendshipId, accept = accept };
        string body = JsonUtility.ToJson(payload);

        using UnityWebRequest request = new(respondApiUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + token);
        request.timeout = 10;

        yield return request.SendWebRequest();

        bool success = request.result == UnityWebRequest.Result.Success &&
                       request.responseCode >= 200 &&
                       request.responseCode < 300;

        if (!success)
        {
            Debug.LogError($"Lỗi phản hồi lời mời: {request.error} | HTTP {request.responseCode}");
            isResponding = false;
            // Refresh để restore lại item (không bị mất nếu thất bại)
            Refresh();
            yield break;
        }

        isResponding = false;
        Refresh();
    }

    private void ClearList()
    {
        if (container == null)
            return;

        foreach (Transform child in container)
            Destroy(child.gameObject);
    }

    private void SetEmptyStateVisible(bool visible)
    {
        if (emptyStateObject != null)
            emptyStateObject.SetActive(visible);
    }

    [System.Serializable]
    private class RespondPayload
    {
        public string friendshipId;
        public bool accept;
    }
}
