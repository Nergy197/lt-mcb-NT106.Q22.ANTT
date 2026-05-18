using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using PokemonMMO.UI;

namespace PokemonMMO.Pokedex
{
    public class NationalPokedexPanel : MonoBehaviour
    {
        [Header("Left Panel")]
        public TMP_Text pokemonNameText;
        public Image    pokemonSpriteImage;
        public TMP_Text totalCountText;

        [Header("Right Panel")]
        public Transform  listContent;
        public GameObject itemPrefab;
        public ScrollRect scrollRect;

        [Header("Back Button")]
        public GameObject pokedexMenuPanel;

        [Header("Settings")]
        public string serverBaseUrl = "https://lt-mcb-nt106q22antt-production-cc69.up.railway.app";

        private List<PokedexEntry> _entries  = new();
        private List<PokedexItemUI> _items   = new();
        private int      _selectedIndex      = -1;
        private Coroutine _spriteCoroutine;
        private bool     _loaded             = false;

        private void OnEnable()
        {
            if (_loaded) { SelectItem(0); return; }
            StartCoroutine(LoadData());
        }

        // ── Load JSON ────────────────────────────────────────────────────────

        private IEnumerator LoadData()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "pokedex_final.json");
            string json = File.ReadAllText(path);
            yield return null;
            _entries = JsonConvert.DeserializeObject<List<PokedexEntry>>(json);
            _entries = _entries.FindAll(e => e.id < 10000);
            _entries.Sort((a, b) => a.id.CompareTo(b.id));

            totalCountText.text = $"Tổng số Pokémon: {_entries.Count}";

            PopulateList();
            _loaded = true;
            SelectItem(0);
        }

        // ── Populate list ────────────────────────────────────────────────────

        private void PopulateList()
        {
            foreach (Transform child in listContent)
                Destroy(child.gameObject);
            _items.Clear();

            foreach (var entry in _entries)
            {
                var obj = Instantiate(itemPrefab, listContent);
                var ui  = obj.GetComponent<PokedexItemUI>();
                if (ui == null) { Debug.LogError("[Pokedex] PokedexItemUI không có trên prefab!"); continue; }
                ui.SetData(entry.id, entry.DisplayName, OnItemClicked);
                _items.Add(ui);
            }
        }

        // ── Keyboard navigation ──────────────────────────────────────────────

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.DownArrow)) Navigate(1);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) Navigate(-1);
            else if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape)) OnBackClicked();
        }

        private void Navigate(int dir)
        {
            int next = Mathf.Clamp(_selectedIndex + dir, 0, _items.Count - 1);
            SelectItem(next);
            ScrollToItem(next);
        }

        private void OnItemClicked(int id)
        {
            int idx = _entries.FindIndex(e => e.id == id);
            if (idx >= 0) SelectItem(idx);
        }

        // ── Select ───────────────────────────────────────────────────────────

        private void SelectItem(int index)
        {
            if (_items.Count == 0 || index < 0 || index >= _items.Count) return;

            if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
                _items[_selectedIndex].SetSelected(false);

            _selectedIndex = index;
            _items[index].SetSelected(true);

            var entry = _entries[index];
            pokemonNameText.text = entry.DisplayName;

            if (_spriteCoroutine != null) StopCoroutine(_spriteCoroutine);
            _spriteCoroutine = StartCoroutine(LoadSprite(entry));
        }

        // ── Load sprite: thử server trước, fallback sang sprite_url trong JSON ─

        private IEnumerator LoadSprite(PokedexEntry entry)
        {
            pokemonSpriteImage.sprite = null;
            pokemonSpriteImage.color  = Color.clear;

            string serverUrl = $"{serverBaseUrl}/data/pokemon/front/{entry.id}.png";
            yield return TryLoadUrl(serverUrl);
            if (pokemonSpriteImage.sprite != null) yield break;

            if (!string.IsNullOrEmpty(entry.sprite_url))
                yield return TryLoadUrl(entry.sprite_url);
        }

        private IEnumerator TryLoadUrl(string url)
        {
            Debug.Log($"[Pokedex] Load ảnh: {url}");
            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Pokedex] Tải thành công: {url}");
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(req.downloadHandler.data);
                pokemonSpriteImage.sprite         = Sprite.Create(
                    tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                pokemonSpriteImage.preserveAspect = true;
                pokemonSpriteImage.color          = Color.white;
            }
            else
            {
                Debug.LogWarning($"[Pokedex] Tải thất bại: {url} | {req.error}");
            }
        }

        // ── Scroll ───────────────────────────────────────────────────────────

        private void ScrollToItem(int index)
        {
            if (scrollRect == null || _items.Count <= 1) return;
            float t = (float)index / (_items.Count - 1);
            scrollRect.verticalNormalizedPosition = 1f - t;
        }

        // ── Back button ──────────────────────────────────────────────────────

        public void OnBackClicked()
        {
            var ctrl = FindFirstObjectByType<PokedexSceneController>();
            if (ctrl != null)
                ctrl.CloseNationalPokedex();
            else
            {
                // Fallback nếu chưa dùng controller
                gameObject.SetActive(false);
                if (pokedexMenuPanel != null) pokedexMenuPanel.SetActive(true);
            }
        }
    }
}
