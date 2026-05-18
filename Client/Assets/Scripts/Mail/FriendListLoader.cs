using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking; // ThÆ° viá»‡n Ä‘á»ƒ gá»i API (HTTP)
using Newtonsoft.Json;       // ThÆ° viá»‡n Ä‘á»ƒ dá»‹ch JSON (Anh cáº§n cÃ i cÃ¡i nÃ y)

public class FriendListLoader : MonoBehaviour
{
    [Header("Cáº¥u hÃ¬nh UI")]
    public GameObject friendPrefab;
    public Transform container;
    public Sprite[] pokemonAvatarPool;

    // Cache avatar theo playerId â€” tá»“n táº¡i suá»‘t session, xÃ³a khi logout
    private static readonly Dictionary<string, int>    _sessionAvatarCache  = new();
    private static readonly Dictionary<string, Sprite> _sessionSpriteCache  = new();

    /// <summary>Tra cá»©u Sprite avatar Ä‘Ã£ cáº¥p phÃ¡t cho player trong session hiá»‡n táº¡i.</summary>
    public static Sprite GetPlayerAvatar(string playerId) =>
        _sessionSpriteCache.TryGetValue(playerId, out var s) ? s : null;

    [Header("Cáº¥u hÃ¬nh Backend")]
    // ÄÃ£ sá»­a láº¡i PORT 2567 vÃ  http (khÃ´ng cÃ³ chá»¯ s) cho khá»›p vá»›i Server
    public string apiUrl = "https://lt-mcb-nt106q22antt-production-cc69.up.railway.app/api/friends";

    void OnEnable()
    {
        // Má»—i láº§n Tab Ä‘Æ°á»£c báº­t lÃªn lÃ  Ä‘i gá»i Backend láº¥y danh sÃ¡ch ngay
        StartCoroutine(FetchFriendListFromServer());
    }

    IEnumerator FetchFriendListFromServer()
    {
        // 1. Láº¥y Token anh Ä‘Ã£ lÆ°u lÃºc Login (Sá»­a auth_token thÃ nh jwt_token cho khá»›p vá»›i AuthUIManager)
        string token = PlayerPrefs.GetString("jwt_token", "");

        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            // 2. Gáº¯n "Tháº» bÃ i" xÃ¡c thá»±c vÃ o Header
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 10; // Giá»›i háº¡n 10 giÃ¢y, trÃ¡nh treo vÄ©nh viá»…n

            Debug.Log("Äang káº¿t ná»‘i Backend láº¥y danh sÃ¡ch...");
            yield return request.SendWebRequest();

            Debug.Log($"ÄÃ£ káº¿t thÃºc request! Káº¿t quáº£: {request.result}");

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 3. Dá»¯ liá»‡u thÃ´ tá»« Server (Chuá»—i JSON)
                string rawJson = request.downloadHandler.text;
                Debug.Log("Dá»¯ liá»‡u Server tráº£ vá»: " + rawJson);

                try
                {
                    // 4. Äá»• dá»¯ liá»‡u vÃ o "KhuÃ´n" (FriendData)
                    List<FriendData> friends = JsonConvert.DeserializeObject<List<FriendData>>(rawJson);
                    
                    // Hiá»‡n Log bÃ¡o sá»‘ lÆ°á»£ng báº¡n bÃ¨ theo yÃªu cáº§u cá»§a anh
                    Debug.Log($"NgÆ°á»i chÆ¡i nÃ y Ä‘ang cÃ³ tá»•ng cá»™ng {friends.Count} báº¡n bÃ¨!");
                    
                    // 5. Hiá»ƒn thá»‹ lÃªn mÃ n hÃ¬nh
                    PopulateUI(friends);
                    Debug.Log("Hiá»ƒn thá»‹ giao diá»‡n thÃ nh cÃ´ng!");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("Lá»—i khi Ä‘á»c JSON (cÃ³ thá»ƒ do thÆ° viá»‡n Newtonsoft): " + ex.Message);
                }
            }
            else
            {
                // In ra lá»—i chÃ­nh xÃ¡c tá»« Unity vÃ  mÃ£ pháº£n há»“i (VD: 401 Unauthorized, 404 Not Found, hay lá»—i SSL)
                Debug.LogError($"Lá»—i láº¥y báº¡n bÃ¨: {request.error} | MÃ£ HTTP: {request.responseCode}");
                if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    Debug.LogError("Chi tiáº¿t tá»« Server: " + request.downloadHandler.text);
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
        // XÃ³a sáº¡ch máº¥y cÃ¡i Ä‘á»“ cÅ©
        foreach (Transform child in container) { Destroy(child.gameObject); }

        foreach (var data in friends)
        {
            GameObject obj = Instantiate(friendPrefab, container);
            FriendItemUI itemUI = obj.GetComponent<FriendItemUI>();

            if (itemUI != null)
            {
                // Láº§n Ä‘áº§u gáº·p player nÃ y trong session â†’ random vÃ  lÆ°u cache
                // Láº§n sau â†’ dÃ¹ng láº¡i index Ä‘Ã£ lÆ°u, avatar khÃ´ng Ä‘á»•i
                if (!_sessionAvatarCache.TryGetValue(data.playerId, out int avatarIndex))
                {
                    avatarIndex = Random.Range(0, pokemonAvatarPool.Length);
                    _sessionAvatarCache[data.playerId] = avatarIndex;
                }

                Sprite avatar = pokemonAvatarPool[avatarIndex];
                _sessionSpriteCache[data.playerId] = avatar;
                itemUI.SetData(data.playerId, data.playerName, avatar, data.isOnline);
            }
        }
    }
}
