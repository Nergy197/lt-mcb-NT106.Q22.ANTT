using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PokemonMMO.Audio;

namespace PokemonMMO.UI
{
    /// <summary>
    /// Quản lý UI Recruit Scene.
    /// - Recruit_UI_Panel: chứa banner + button "Meet a New Lineup of Pokémon!"
    /// - Pokemon_List_Bar: chứa 10 ô (slot) hiển thị icon Pokémon sau khi roll.
    /// 
    /// Cách gắn trong Unity:
    ///   1. Tạo một Empty GameObject "RecruitManager" trong RecuitScene.
    ///   2. Gắn script này vào RecruitManager.
    ///   3. Kéo thả các GameObject tương ứng vào Inspector:
    ///      - recruitPanel  → Recruit_UI_Panel
    ///      - pokemonListBar → Pokemon_List_Bar
    ///      - recruitButton → Button "Meet a New Lineup of Pokémon!"
    ///      - slots[0..9]   → 10 Image con bên trong Pokemon_List_Bar (ô trắng)
    /// </summary>
    public class RecruitManager : MonoBehaviour
    {
        // ── Server ────────────────────────────────────────────────────────
        [Header("Server")]
        public string serverUrl      = "https://lt-mcb-nt106q22antt-production-cc69.up.railway.app";
        public string menuSceneName  = "Menu scene";

        // ── UI References ─────────────────────────────────────────────────
        [Header("UI Panels")]
        [Tooltip("Panel chứa banner và button (ẩn sau khi bấm)")]
        public GameObject recruitPanel;

        [Tooltip("Panel chứa dãy 10 ô trắng (hiện ra sau khi bấm)")]
        public GameObject pokemonListBar;

        [Header("Button")]
        [Tooltip("Button 'Meet a New Lineup of Pokémon!'")]
        public Button recruitButton;

        [Header("Slots - Kéo 10 Image (ô trắng) vào đây theo thứ tự")]
        [Tooltip("10 ô Image bên trong Pokemon_List_Bar. Ô trắng giữ nguyên, icon sẽ xuất hiện ở giữa.")]
        public Image[] slots = new Image[10];

        // ── Internal ──────────────────────────────────────────────────────
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private readonly Queue<Action> _mainThread = new Queue<Action>();
        private bool _isRolling = false;

        // Lưu các child icon đã tạo để xoá khi roll lại
        private readonly List<GameObject> _iconObjects = new List<GameObject>();

        // ── Navigation State ──────────────────────────────────────────────
        private RecruitResultDto[] _currentResults;
        private int _selectedIndex = -1;
        private bool _confirmButtonsWired = false;
        private readonly Dictionary<string, Sprite> _frontSpriteCache = new Dictionary<string, Sprite>();
        private Coroutine _currentDetailLoadCoroutine;

        // ── New UI References for Navigation ──────────────────────────────
        [Header("Detail View")]
        [Tooltip("Image lớn hiển thị Pokemon đang được chọn")]
        public Image detailImage;
        [Tooltip("Màu viền khi ô được chọn")]
        public Color selectedColor = new Color(1f, 1f, 0.5f);
        [Tooltip("Màu viền bình thường")]
        public Color normalColor = Color.white;

        [Header("Options Popup")]
        [Tooltip("Kéo GameObject Options_Popup vào đây")]
        public GameObject optionsPopup;

        [Header("SFX")]
        [SerializeField] private AudioClip sfxRecruitRoll;
        [SerializeField] private AudioClip sfxRecruitPop;
        [SerializeField] private AudioClip sfxRecruitConfirm;


        // ── Unity Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (recruitPanel != null)   recruitPanel.SetActive(true);
            if (pokemonListBar != null) pokemonListBar.SetActive(false);
            if (detailImage != null)    detailImage.gameObject.SetActive(false);

            if (recruitButton != null)
                recruitButton.image.raycastTarget = false;

            // Khôi phục kết quả cũ nếu chưa quá 24 giờ
            var saved = LoadSavedResults();
            if (saved != null)
            {
                _currentResults = saved;
                StartCoroutine(RestoreIcons());
            }
        }

        private void Update()
        {
            // Dispatch callbacks về Main Thread
            lock (_mainThread)
                while (_mainThread.Count > 0)
                    _mainThread.Dequeue()?.Invoke();

            // Màn hình banner
            if (recruitPanel != null && recruitPanel.activeSelf && !_isRolling)
            {
                if (Input.GetKeyDown(KeyCode.C))
                {
                    if (_currentResults != null && _currentResults.Length > 0)
                        ShowResultsFromBanner();
                    else
                        OnRecruitButtonClicked();
                }
                else if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape))
                    SceneManager.LoadScene(menuSceneName);
                return;
            }

            // Xử lý phím điều hướng nếu đã có kết quả
            if (_currentResults != null && _currentResults.Length > 0 && !_isRolling)
            {
                bool popupOpen = optionsPopup != null && optionsPopup.activeSelf;

                if (popupOpen)
                {
                    // X đóng popup
                    if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape))
                        optionsPopup.SetActive(false);
                }
                else
                {
                    if (Input.GetKeyDown(KeyCode.LeftArrow))
                        SelectSlot(Mathf.Max(0, _selectedIndex - 1));
                    else if (Input.GetKeyDown(KeyCode.RightArrow))
                        SelectSlot(Mathf.Min(_currentResults.Length - 1, _selectedIndex + 1));
                    else if (Input.GetKeyDown(KeyCode.C) && _selectedIndex >= 0)
                        ShowOptionsPopup();
                    else if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape))
                        GoBackToRecruitPanel();
                }
            }
        }

        // ── Button Handler ────────────────────────────────────────────────

        public void OnRecruitButtonClicked()
        {
            if (_isRolling) return;
            _isRolling = true;
            AudioManager.Instance?.PlaySFX(sfxRecruitRoll);

            // Ẩn panel banner, hiện list bar
            if (recruitPanel != null) recruitPanel.SetActive(false);
            if (pokemonListBar != null) pokemonListBar.SetActive(true);
            
            _currentResults = null;
            _selectedIndex = -1;
            if (detailImage != null) detailImage.gameObject.SetActive(false);
            
            if (_currentDetailLoadCoroutine != null)
            {
                StopCoroutine(_currentDetailLoadCoroutine);
                _currentDetailLoadCoroutine = null;
            }
            ClearCache();

            // Xoá icon cũ (nếu roll lại)
            ClearIcons();

            // Gọi API roll
            _ = RollRecruitAsync();
        }

        // ── API Call ──────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task RollRecruitAsync()
        {
            try
            {
                Debug.Log("[Recruit] Đang gọi API roll...");

                var response = await Http.GetAsync($"{serverUrl}/api/recruit/roll?count=10");
                var json = await response.Content.ReadAsStringAsync();

                Debug.Log($"[Recruit] Response: {json}");

                if (response.IsSuccessStatusCode)
                {
                    // Parse JSON array
                    var results = JsonHelper.FromJsonArray<RecruitResultDto>(json);

                    Dispatch(() =>
                    {
                        _currentResults = results;
                        SaveResults(results);
                        int pendingIcons = Mathf.Min(results.Length, slots.Length);
                        int iconsLoaded = 0;

                        for (int i = 0; i < results.Length && i < slots.Length; i++)
                        {
                            if (slots[i] != null && results[i] != null)
                            {
                                string iconUrl = $"{serverUrl}{results[i].IconUrl}";
                                int index = i; // capture loop variable cho coroutine
                                StartCoroutine(LoadIconIntoSlot(slots[index], iconUrl, results[index].Name, index, () => {
                                    iconsLoaded++;
                                    // Khi load xong tất cả icon, chọn ô đầu tiên
                                    if (iconsLoaded >= pendingIcons)
                                    {
                                        _isRolling = false;
                                        if (results.Length > 0)
                                            SelectSlot(0);
                                    }
                                }));
                            }
                            else
                            {
                                pendingIcons--;
                            }
                        }

                        if (pendingIcons == 0) _isRolling = false;
                    });
                }
                else
                {
                    Debug.LogError($"[Recruit] Lỗi API: {json}");
                    Dispatch(() => _isRolling = false);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Recruit] Exception: {ex.Message}");
                Dispatch(() => _isRolling = false);
            }
        }

        // ── Popup / Back ──────────────────────────────────────────────────

        private void ShowOptionsPopup()
        {
            if (optionsPopup == null) return;

            // Wire up confirm buttons lần đầu tiên qua OptionsPopupController.buttons[]
            // buttons[0] = Trial, buttons[1] = Permanent (theo thứ tự trong Inspector)
            if (!_confirmButtonsWired)
            {
                var ctrl = optionsPopup.GetComponent<PokemonMMO.UI.OptionsPopupController>();
                if (ctrl != null && ctrl.buttons != null)
                {
                    if (ctrl.buttons.Length > 0 && ctrl.buttons[0] != null)
                        ctrl.buttons[0].onClick.AddListener(() => StartCoroutine(ConfirmRecruitCoroutine("trial")));
                    if (ctrl.buttons.Length > 1 && ctrl.buttons[1] != null)
                        ctrl.buttons[1].onClick.AddListener(() => StartCoroutine(ConfirmRecruitCoroutine("permanent")));
                    _confirmButtonsWired = true;
                }
            }

            optionsPopup.SetActive(true);
        }

        public void CloseOptionsPopup()
        {
            if (optionsPopup != null)
                optionsPopup.SetActive(false);
        }

        // Quay về banner nhưng GIỮ NGUYÊN kết quả và icons
        private void GoBackToRecruitPanel()
        {
            if (detailImage != null)  detailImage.gameObject.SetActive(false);
            if (pokemonListBar != null) pokemonListBar.SetActive(false);
            if (recruitPanel != null)  recruitPanel.SetActive(true);
        }

        // Hiện lại danh sách kết quả cũ từ banner (không roll mới)
        private void ShowResultsFromBanner()
        {
            if (recruitPanel != null)  recruitPanel.SetActive(false);
            if (pokemonListBar != null) pokemonListBar.SetActive(true);
            // Khôi phục slot đang chọn và detail image
            int idx = _selectedIndex >= 0 ? _selectedIndex : 0;
            SelectSlot(idx);
        }

        // Reset hoàn toàn sau khi confirm recruit thành công
        private void ResetRecruitState()
        {
            ClearSavedResults();
            _currentResults = null;
            _selectedIndex  = -1;
            if (_currentDetailLoadCoroutine != null)
            {
                StopCoroutine(_currentDetailLoadCoroutine);
                _currentDetailLoadCoroutine = null;
            }
            ClearCache();
            ClearIcons();
            if (detailImage != null)   detailImage.gameObject.SetActive(false);
            if (pokemonListBar != null) pokemonListBar.SetActive(false);
            if (recruitPanel != null)  recruitPanel.SetActive(true);
        }

        // ── Navigation Logic ──────────────────────────────────────────────

        private void SelectSlot(int index)
        {
            if (_currentResults == null || index < 0 || index >= _currentResults.Length) return;

            // Đổi màu viền (nếu slot là Image trắng)
            if (_selectedIndex >= 0 && _selectedIndex < slots.Length && slots[_selectedIndex] != null)
            {
                slots[_selectedIndex].color = normalColor;
            }

            _selectedIndex = index;

            if (slots[_selectedIndex] != null)
            {
                slots[_selectedIndex].color = selectedColor;
            }

            // Cập nhật ảnh lớn
            if (detailImage != null)
            {
                detailImage.gameObject.SetActive(true);
                
                string frontUrl = $"{serverUrl}{_currentResults[_selectedIndex].SpriteUrl}";
                
                if (_currentDetailLoadCoroutine != null)
                {
                    StopCoroutine(_currentDetailLoadCoroutine);
                }
                
                if (_frontSpriteCache.TryGetValue(frontUrl, out Sprite cachedSprite))
                {
                    detailImage.sprite = cachedSprite;
                    detailImage.color = Color.white;
                }
                else
                {
                    detailImage.sprite = null;
                    detailImage.color = new Color(1, 1, 1, 0); // Hide until loaded
                    _currentDetailLoadCoroutine = StartCoroutine(LoadFrontSprite(frontUrl));
                }
            }
        }

        private IEnumerator LoadFrontSprite(string url)
        {
            using (var www = UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    texture.filterMode = FilterMode.Point;

                    if (texture.LoadImage(www.downloadHandler.data))
                    {
                        var sprite = Sprite.Create(
                            texture,
                            new Rect(0, 0, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f)
                        );

                        _frontSpriteCache[url] = sprite;
                        
                        if (detailImage != null)
                        {
                            detailImage.sprite = sprite;
                            detailImage.color = Color.white;
                            detailImage.preserveAspect = true;
                        }
                    }
                }
            }
        }

        // ── Icon Loader ───────────────────────────────────────────────────

        /// <summary>
        /// Tạo một child Image bên trong slot (ô trắng giữ nguyên),
        /// icon Pokémon sẽ hiển thị ở giữa ô.
        /// </summary>
        private IEnumerator LoadIconIntoSlot(Image slotImage, string url, string pokemonName, int slotIndex, Action onComplete)
        {
            using (var www = UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    texture.filterMode = FilterMode.Point; // Giữ nét pixel cho icon

                    if (texture.LoadImage(www.downloadHandler.data))
                    {
                        var sprite = Sprite.Create(
                            texture,
                            new Rect(0, 0, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f)
                        );

                        // Tạo child GameObject chứa Image icon bên trong slot
                        var iconObj = new GameObject($"Icon_{pokemonName}");
                        iconObj.transform.SetParent(slotImage.transform, false);
                        _iconObjects.Add(iconObj);

                        // Thêm Image component cho icon
                        var iconImage = iconObj.AddComponent<Image>();
                        iconImage.sprite = sprite;
                        iconImage.preserveAspect = false; // Tắt để icon fill đúng vị trí
                        iconImage.raycastTarget = false;

                        // Stretch anchors: fill toàn bộ ô trắng với padding nhỏ
                        var rt = iconObj.GetComponent<RectTransform>();
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        float padding = 3f; // padding pixels mỗi cạnh
                        rt.offsetMin = new Vector2(padding, padding);
                        rt.offsetMax = new Vector2(-padding, -padding);

                        Debug.Log($"[Recruit] ✅ {pokemonName} ({texture.width}x{texture.height})");
                        StartCoroutine(PlayPopSfxDelayed(slotIndex));
                    }
                    else
                    {
                        Debug.LogError($"[Recruit] Không decode được icon: {url}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Recruit] Không load được icon: {url} - {www.error}");
                    // Ô trắng giữ nguyên khi không load được icon
                }

                onComplete?.Invoke();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void ClearCache()
        {
            // Destroy cached sprites to free memory if needed, but for 10 it's fine just to clear the dict
            foreach (var sprite in _frontSpriteCache.Values)
            {
                if (sprite != null && sprite.texture != null)
                    Destroy(sprite.texture);
                if (sprite != null)
                    Destroy(sprite);
            }
            _frontSpriteCache.Clear();
        }

        /// <summary>
        /// Xoá tất cả icon cũ khỏi các slot (giữ nguyên ô trắng).
        /// </summary>
        private void ClearIcons()
        {
            foreach (var obj in _iconObjects)
            {
                if (obj != null)
                    Destroy(obj);
            }
            _iconObjects.Clear();
            
            // Reset màu các slot
            foreach(var slot in slots)
            {
                if (slot != null) slot.color = normalColor;
            }
        }

        private IEnumerator PlayPopSfxDelayed(int index)
        {
            yield return new WaitForSeconds(0.05f * index);
            AudioManager.Instance?.PlaySFX(sfxRecruitPop);
        }

        private void Dispatch(Action action)
        {
            lock (_mainThread) _mainThread.Enqueue(action);
        }

        // ── Persist Results ───────────────────────────────────────────────

        private const string SaveKey = "recruit_saved_results";

        private void SaveResults(RecruitResultDto[] results)
        {
            var wrapper = new ResultsWrapper
            {
                results   = results,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(wrapper));
            PlayerPrefs.Save();
        }

        private RecruitResultDto[] LoadSavedResults()
        {
            string json = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrEmpty(json)) return null;
            var wrapper = JsonUtility.FromJson<ResultsWrapper>(json);
            if (wrapper?.results == null || wrapper.results.Length == 0) return null;
            long age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - wrapper.timestamp;
            if (age > 24 * 3600) { ClearSavedResults(); return null; }
            return wrapper.results;
        }

        private void ClearSavedResults()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        private IEnumerator RestoreIcons()
        {
            ClearIcons();
            if (_currentResults == null) yield break;
            for (int i = 0; i < _currentResults.Length && i < slots.Length; i++)
            {
                if (slots[i] == null || _currentResults[i] == null) continue;
                string iconUrl = $"{serverUrl}{_currentResults[i].IconUrl}";
                int idx = i;
                StartCoroutine(LoadIconIntoSlot(slots[idx], iconUrl, _currentResults[idx].Name, idx, null));
                yield return null; // trải đều qua nhiều frame
            }
        }

        [Serializable]
        private class ResultsWrapper
        {
            public RecruitResultDto[] results;
            public long               timestamp;
        }

        // ── Confirm Recruit ───────────────────────────────────────────────

        private IEnumerator ConfirmRecruitCoroutine(string recruitType)
        {
            if (_selectedIndex < 0 || _currentResults == null) yield break;

            int speciesId = _currentResults[_selectedIndex].SpeciesId;
            CloseOptionsPopup();

            string token = PlayerPrefs.GetString("jwt_token", "");
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogError("[Recruit] Chưa đăng nhập, không thể confirm recruit.");
                yield break;
            }

            var payload = JsonUtility.ToJson(new RecruitConfirmDto { SpeciesId = speciesId, RecruitType = recruitType });
            var bytes   = System.Text.Encoding.UTF8.GetBytes(payload);

            using var req = new UnityEngine.Networking.UnityWebRequest($"{serverUrl}/api/recruit/confirm", "POST");
            req.uploadHandler   = new UnityEngine.Networking.UploadHandlerRaw(bytes);
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);

            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<RecruitConfirmResponseDto>(req.downloadHandler.text);
                Debug.Log($"[Recruit] ✅ {resp.Message} (Party: {resp.IsInParty})");
                AudioManager.Instance?.PlaySFX(sfxRecruitConfirm);
                ResetRecruitState(); // Reset hoàn toàn sau khi đã recruit
            }
            else
            {
                Debug.LogError($"[Recruit] ❌ Confirm thất bại: {req.downloadHandler.text}");
            }
        }

        // ── DTOs ──────────────────────────────────────────────────────────

        [Serializable]
        public class RecruitResultDto
        {
            public int    SpeciesId;
            public string Name;
            public string[] Types;
            public string IconUrl;
            public string SpriteUrl;
        }

        [Serializable]
        private class RecruitConfirmDto
        {
            public int    SpeciesId;
            public string RecruitType;
        }

        [Serializable]
        private class RecruitConfirmResponseDto
        {
            public bool   Success;
            public string Message;
            public bool   IsInParty;
            public string PokemonId;
        }
    }

    /// <summary>
    /// Helper class để parse JSON Array với JsonUtility (Unity không hỗ trợ trực tiếp).
    /// Trick: bọc array trong một wrapper object.
    /// </summary>
    public static class JsonHelper
    {
        public static T[] FromJsonArray<T>(string json)
        {
            // Bọc JSON array thành object: {"Items": [...]}
            string wrappedJson = "{\"Items\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrappedJson);
            return wrapper.Items;
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] Items;
        }
    }
}
