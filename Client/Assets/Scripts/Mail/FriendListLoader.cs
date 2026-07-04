using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class FriendListLoader : MonoBehaviour
{
    [Header("Cấu hình UI")]
    public GameObject friendPrefab;
    public Transform container;
    public Sprite[] pokemonAvatarPool;

    // Cache avatar theo playerId — tồn tại suốt session, xóa khi logout
    private static readonly Dictionary<string, int>    _sessionAvatarCache  = new();
    private static readonly Dictionary<string, Sprite> _sessionSpriteCache  = new();

    /// <summary>Tra cứu Sprite avatar đã cấp phát cho player trong session hiện tại.</summary>
    public static Sprite GetPlayerAvatar(string playerId) =>
        _sessionSpriteCache.TryGetValue(playerId, out var s) ? s : null;

    /// <summary>Lấy avatar đã cache, hoặc random mới từ pool và lưu lại.</summary>
    public static Sprite GetOrAssignAvatar(string playerId, Sprite[] pool)
    {
        if (string.IsNullOrWhiteSpace(playerId) || pool == null || pool.Length == 0)
            return null;

        if (_sessionSpriteCache.TryGetValue(playerId, out Sprite cached))
            return cached;

        if (!_sessionAvatarCache.TryGetValue(playerId, out int avatarIndex))
        {
            avatarIndex = Random.Range(0, pool.Length);
            _sessionAvatarCache[playerId] = avatarIndex;
        }

        Sprite avatar = pool[avatarIndex];
        _sessionSpriteCache[playerId] = avatar;
        return avatar;
    }

    [Header("Cấu hình Backend")]
    // Đã sửa lại PORT 2567 và http (không có chữ s) cho khớp với Server
    public string apiUrl = "https://pokemon-mmo-server-123-gkaqfbejgycbcwfb.southeastasia-01.azurewebsites.net/api/friends";

    private bool isRemovingFriend;

    void OnEnable()
    {
        isRemovingFriend = false;
        Refresh();
    }

    void OnDisable()
    {
        isRemovingFriend = false;
        StopAllCoroutines();
    }

    public void Refresh()
    {
        if (container == null || friendPrefab == null)
            return;

        StopAllCoroutines();
        StartCoroutine(FetchFriendListFromServer());
    }

    IEnumerator FetchFriendListFromServer()
    {
        // 1. Lấy Token anh đã lưu lúc Login (Sửa auth_token thành jwt_token cho khớp với AuthUIManager)
        string token = PlayerPrefs.GetString("jwt_token", "");

        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            // 2. Gắn "Thẻ bài" xác thực vào Header
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 10; // Giới hạn 10 giây, tránh treo vĩnh viễn

            Debug.Log("Đang kết nối Backend lấy danh sách...");
            yield return request.SendWebRequest();

            Debug.Log($"Đã kết thúc request! Kết quả: {request.result}");

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 3. Dữ liệu thô từ Server (Chuỗi JSON)
                string rawJson = request.downloadHandler.text;
                Debug.Log("Dữ liệu Server trả về: " + rawJson);

                try
                {
                    // 4. Đổ dữ liệu vào "Khuôn" (FriendData)
                    List<FriendData> friends = JsonConvert.DeserializeObject<List<FriendData>>(rawJson);
                    
                    // Hiện Log báo số lượng bạn bè theo yêu cầu của anh
                    Debug.Log($"Người chơi này đang có tổng cộng {friends.Count} bạn bè!");
                    
                    // 5. Hiển thị lên màn hình
                    PopulateUI(friends);
                    Debug.Log("Hiển thị giao diện thành công!");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("Lỗi khi đọc JSON (có thể do thư viện Newtonsoft): " + ex.Message);
                }
            }
            else
            {
                // In ra lỗi chính xác từ Unity và mã phản hồi (VD: 401 Unauthorized, 404 Not Found, hay lỗi SSL)
                Debug.LogError($"Lỗi lấy bạn bè: {request.error} | Mã HTTP: {request.responseCode}");
                if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    Debug.LogError("Chi tiết từ Server: " + request.downloadHandler.text);
                }
            }
        }
    }

    public static void ClearAvatarCache()
    {
        _sessionAvatarCache.Clear();
        _sessionSpriteCache.Clear();
    }

    void PopulateUI(List<FriendData> friends)
    {
        foreach (Transform child in container)
            Destroy(child.gameObject);

        if (pokemonAvatarPool == null || pokemonAvatarPool.Length == 0)
            Debug.LogWarning("[FriendListLoader] pokemonAvatarPool chưa được assign — hiển thị không có avatar. Hãy kéo sprites vào Inspector.");

        foreach (FriendData data in friends)
        {
            GameObject obj = Instantiate(friendPrefab, container);
            Sprite avatar = GetOrAssignAvatar(data.playerId, pokemonAvatarPool);

            FriendsListItemUI friendsItem = obj.GetComponent<FriendsListItemUI>();
            if (friendsItem != null)
            {
                friendsItem.SetData(data.playerId, data.playerName, avatar, data.isOnline, data.lastSeenAt);
                friendsItem.BindDelete(OnRemoveFriend);
                continue;
            }

            FriendItemUI legacyItem = obj.GetComponent<FriendItemUI>();
            if (legacyItem != null)
                legacyItem.SetData(data.playerId, data.playerName, avatar, data.isOnline, data.lastSeenAt);
        }

        StartCoroutine(RebuildLayoutNextFrame());
    }

    IEnumerator RebuildLayoutNextFrame()
    {
        yield return null; // chờ Destroy() hoàn tất và Instantiate ổn định
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(container.GetComponent<RectTransform>());
    }

    private void OnRemoveFriend(string friendPlayerId)
    {
        StartCoroutine(RemoveFriend(friendPlayerId));
    }

    private IEnumerator RemoveFriend(string friendPlayerId)
    {
        if (isRemovingFriend || string.IsNullOrWhiteSpace(friendPlayerId))
            yield break;

        isRemovingFriend = true;
        string token = PlayerPrefs.GetString("jwt_token", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            isRemovingFriend = false;
            yield break;
        }

        string url = $"{apiUrl.TrimEnd('/')}/{friendPlayerId}";
        using UnityWebRequest request = UnityWebRequest.Delete(url);
        request.SetRequestHeader("Authorization", "Bearer " + token);
        request.timeout = 10;

        yield return request.SendWebRequest();

        bool success = request.result == UnityWebRequest.Result.Success &&
                       request.responseCode >= 200 &&
                       request.responseCode < 300;

        if (!success)
            Debug.LogError($"Lỗi hủy kết bạn: {request.error} | HTTP {request.responseCode}");

        isRemovingFriend = false;
        Refresh();
    }
}