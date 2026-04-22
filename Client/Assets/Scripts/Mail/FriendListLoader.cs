using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking; // Thư viện để gọi API (HTTP)
using Newtonsoft.Json;       // Thư viện để dịch JSON (Anh cần cài cái này)

public class FriendListLoader : MonoBehaviour
{
    [Header("Cấu hình UI")]
    public GameObject friendPrefab;
    public Transform container;
    public Sprite[] pokemonAvatarPool;

    [Header("Cấu hình Backend")]
    // Đã sửa lại PORT 2567 và http (không có chữ s) cho khớp với Server
    public string apiUrl = "http://localhost:2567/api/friends";

    void OnEnable()
    {
        // Mỗi lần Tab được bật lên là đi gọi Backend lấy danh sách ngay
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

    void PopulateUI(List<FriendData> friends)
    {
        // Xóa sạch mấy cái đồ cũ
        foreach (Transform child in container) { Destroy(child.gameObject); }

        foreach (var data in friends)
        {
            GameObject obj = Instantiate(friendPrefab, container);
            FriendItemUI itemUI = obj.GetComponent<FriendItemUI>();

            if (itemUI != null)
            {
                // Bốc đại 1 tấm ảnh Pokemon cho sinh động
                int randomIndex = Random.Range(0, pokemonAvatarPool.Length);

                // Đổ tên từ Backend (data.playerName) và trạng thái online vào ô UI
                itemUI.SetData(data.playerName, pokemonAvatarPool[randomIndex], data.isOnline);
            }
        }
    }
}