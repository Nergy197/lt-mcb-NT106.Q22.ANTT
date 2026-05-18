using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PokemonMMO.Box
{
    public class PokemonBoxPanel : MonoBehaviour
    {
        [Header("Box Header")]
        public Image    boxWallpaper;
        public TMP_Text boxNameText;

        [Header("Grid (6 cột × 5 hàng = 30 slot)")]
        public Transform  gridContainer;
        public GameObject slotPrefab;

        [Header("Cursor")]
        public RectTransform cursorArrow;

        [Header("Box Wallpapers (32 sprite)")]
        public Sprite[] boxWallpapers;

        [Header("Party Panel")]
        public PartyPanelUI partyPanel;

        [Header("Context Menu")]
        public BoxContextMenuUI contextMenu;

        [Header("Exit Button")]
        public RectTransform exitButtonRect;

        [Header("Settings")]
        public string serverBaseUrl = "https://lt-mcb-nt106q22antt-production-cc69.up.railway.app";
        public string menuSceneName = "Menu scene";

        // ── Constants ─────────────────────────────────────────────────────────
        private const int Cols      = 6;
        private const int Rows      = 5;
        private const int PartyCols = 2;
        private const int PartyRows = 3;

        // ── Box state ─────────────────────────────────────────────────────────
        private BoxSlotUI[] _slots;
        private int  _currentBox = 0;
        private int  _totalBoxes = 32;
        private bool _onHeader   = false;
        private int  _cursorCol  = 0;
        private int  _cursorRow  = 0;

        // ── Party focus state ─────────────────────────────────────────────────
        private bool _isFocusOnParty  = false;
        private bool _onExitButton    = false;
        private int  _partyCol = 0;
        private int  _partyRow = 0;

        private readonly Dictionary<int, Sprite> _spriteCache = new();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake() => BuildGrid();

        private void OnEnable()
        {
            _currentBox      = 0;
            _onHeader        = false;
            _cursorCol       = 0;
            _cursorRow       = 0;
            _isFocusOnParty  = false;
            _onExitButton    = false;
            StartCoroutine(LoadBox(_currentBox));
        }

        // ── Grid ──────────────────────────────────────────────────────────────

        private void BuildGrid()
        {
            if (_slots != null) return;
            _slots = new BoxSlotUI[Cols * Rows];
            foreach (Transform child in gridContainer) Destroy(child.gameObject);
            for (int i = 0; i < Cols * Rows; i++)
            {
                var obj = Instantiate(slotPrefab, gridContainer);
                _slots[i] = obj.GetComponent<BoxSlotUI>();
                _slots[i].SetEmpty();
            }
        }

        // ── Load box ──────────────────────────────────────────────────────────

        private IEnumerator LoadBox(int boxIndex)
        {
            string token = PlayerPrefs.GetString("jwt_token", "");
            using var req = UnityWebRequest.Get($"{serverBaseUrl}/api/box/{boxIndex}");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();

            foreach (var s in _slots) s.SetEmpty();

            BoxInfoData info = null;
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Box] Load thất bại: {req.error}");
            }
            else
            {
                info = JsonConvert.DeserializeObject<BoxInfoData>(req.downloadHandler.text);
                _totalBoxes = info.TotalBoxes;
                foreach (var slot in info.Slots)
                {
                    if (slot.Slot < 0 || slot.Slot >= _slots.Length) continue;
                    StartCoroutine(LoadSlotSprite(slot.Slot, slot.SpeciesId, slot.PokemonId, slot.IconUrl ?? ""));
                }
            }

            int wi = boxIndex % (boxWallpapers?.Length > 0 ? boxWallpapers.Length : 1);
            if (boxWallpaper != null && boxWallpapers?.Length > 0)
                boxWallpaper.sprite = boxWallpapers[wi];
            if (boxNameText != null)
                boxNameText.text = (info != null && !string.IsNullOrEmpty(info.BoxName))
                    ? info.BoxName
                    : $"Box {boxIndex + 1}";

            // Ép layout tính xong vị trí slot trước khi đặt cursor
            Canvas.ForceUpdateCanvases();
            UpdateCursor();
        }

        private IEnumerator LoadSlotSprite(int slotIndex, int speciesId, string pokemonId, string iconUrl = "")
        {
            if (_spriteCache.TryGetValue(speciesId, out var cached))
            {
                _slots[slotIndex].SetPokemon(speciesId, pokemonId, cached);
                yield break;
            }

            // Ưu tiên icon URL từ server (ảnh icon nhỏ), fallback PokeAPI icon
            string primaryUrl = !string.IsNullOrEmpty(iconUrl)
                ? $"{serverBaseUrl}{iconUrl}"
                : $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/versions/generation-viii/icons/{speciesId}.png";

            using var req = UnityWebRequest.Get(primaryUrl);
            yield return req.SendWebRequest();

            Sprite sprite = null;
            if (req.result == UnityWebRequest.Result.Success)
                sprite = MakeSprite(req.downloadHandler.data);
            else
            {
                // Fallback: PokeAPI icon gen-viii
                string fb = $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/versions/generation-viii/icons/{speciesId}.png";
                if (fb != primaryUrl)
                {
                    using var req2 = UnityWebRequest.Get(fb);
                    yield return req2.SendWebRequest();
                    if (req2.result == UnityWebRequest.Result.Success)
                        sprite = MakeSprite(req2.downloadHandler.data);
                }
            }

            if (sprite != null)
            {
                _spriteCache[speciesId] = sprite;
                _slots[slotIndex].SetPokemon(speciesId, pokemonId, sprite);
            }
        }

        private static Sprite MakeSprite(byte[] data)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(data);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        // ── Input ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (BoxContextMenuUI.IsOpen) return;

            if (_onExitButton)    HandleExitButtonInput();
            else if (_isFocusOnParty) HandlePartyInput();
            else                  HandleBoxInput();
        }

        private void HandleBoxInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)    || Input.GetKeyDown(KeyCode.W)) MoveUp();
            if (Input.GetKeyDown(KeyCode.DownArrow)  || Input.GetKeyDown(KeyCode.S)) MoveDown();
            if (Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A)) MoveLeft();
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveRight();
            if (Input.GetKeyDown(KeyCode.Return)     || Input.GetKeyDown(KeyCode.C)) ConfirmBoxSlot();
            if (Input.GetKeyDown(KeyCode.X)          || Input.GetKeyDown(KeyCode.Escape)) SceneManager.LoadScene(menuSceneName);
        }

        private void HandlePartyInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                if (_partyRow > 0) { _partyRow--; UpdateCursor(); }
            }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                if (_partyRow < PartyRows - 1) { _partyRow++; UpdateCursor(); }
                else { _isFocusOnParty = false; _onExitButton = true; UpdateCursor(); }
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                if (_partyCol > 0) { _partyCol--; UpdateCursor(); }
            }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                if (_partyCol < PartyCols - 1) { _partyCol++; UpdateCursor(); }
                else { _isFocusOnParty = false; UpdateCursor(); }
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.C)) ConfirmPartySlot();
            if (Input.GetKeyDown(KeyCode.X)      || Input.GetKeyDown(KeyCode.Escape)) SceneManager.LoadScene(menuSceneName);
        }

        private void HandleExitButtonInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                _onExitButton = false; _isFocusOnParty = true; _partyRow = PartyRows - 1;
                UpdateCursor();
            }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                _onExitButton = false; UpdateCursor();
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.C) ||
                Input.GetKeyDown(KeyCode.X)      || Input.GetKeyDown(KeyCode.Escape))
                SceneManager.LoadScene(menuSceneName);
        }

        // ── Confirm / Menu ────────────────────────────────────────────────────

        private void ConfirmBoxSlot()
        {
            if (_onHeader) return;
            int idx = _cursorRow * Cols + _cursorCol;
            if (idx >= _slots.Length || _slots[idx].IsEmpty) return;

            string pid = _slots[idx].PokemonId;
            contextMenu?.Show("Rút ra", "Huỷ", choice =>
            {
                if (choice == 0) StartCoroutine(WithdrawPokemon(pid));
            });
        }

        private void ConfirmPartySlot()
        {
            int idx = _partyRow * PartyCols + _partyCol;
            if (partyPanel == null || idx >= partyPanel.slots.Length) return;
            var slot = partyPanel.slots[idx];
            if (slot == null || slot.IsEmpty) return;

            string pid = slot.PokemonId;
            contextMenu?.Show("Đưa vào", "Huỷ", choice =>
            {
                if (choice == 0) StartCoroutine(DepositPokemon(pid));
            });
        }

        // ── API calls ─────────────────────────────────────────────────────────

        private IEnumerator WithdrawPokemon(string pokemonId)
        {
            yield return PostAction("withdraw", pokemonId);
            StartCoroutine(LoadBox(_currentBox));
            partyPanel?.Refresh();
        }

        private IEnumerator DepositPokemon(string pokemonId)
        {
            yield return PostAction("deposit", pokemonId);
            StartCoroutine(LoadBox(_currentBox));
            partyPanel?.Refresh();
        }

        private IEnumerator PostAction(string action, string pokemonId)
        {
            string token = PlayerPrefs.GetString("jwt_token", "");
            string body  = JsonConvert.SerializeObject(new { PokemonId = pokemonId });
            var bytes    = System.Text.Encoding.UTF8.GetBytes(body);

            using var req = new UnityWebRequest($"{serverBaseUrl}/api/box/{action}", "POST");
            req.uploadHandler   = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"[Box] {action} thất bại: {req.downloadHandler.text}");
        }

        // ── Box navigation ────────────────────────────────────────────────────

        private void MoveUp()
        {
            if (_onHeader) return;
            if (_cursorRow == 0) { _onHeader = true; UpdateCursor(); return; }
            _cursorRow--;
            UpdateCursor();
        }

        private void MoveDown()
        {
            if (_onHeader) { _onHeader = false; _cursorRow = 0; UpdateCursor(); return; }
            if (_cursorRow < Rows - 1) { _cursorRow++; UpdateCursor(); }
        }

        private void MoveLeft()
        {
            if (_onHeader) { PrevBox(); return; }
            if (_cursorCol > 0) { _cursorCol--; UpdateCursor(); }
            else { _isFocusOnParty = true; _partyCol = 1; _partyRow = 0; UpdateCursor(); }
        }

        private void MoveRight()
        {
            if (_onHeader) { NextBox(); return; }
            if (_cursorCol < Cols - 1) { _cursorCol++; UpdateCursor(); }
        }

        private void PrevBox()
        {
            _currentBox = (_currentBox - 1 + _totalBoxes) % _totalBoxes;
            StartCoroutine(LoadBox(_currentBox));
        }

        private void NextBox()
        {
            _currentBox = (_currentBox + 1) % _totalBoxes;
            StartCoroutine(LoadBox(_currentBox));
        }

        // ── Cursor ────────────────────────────────────────────────────────────

        private void UpdateCursor()
        {
            if (cursorArrow == null) return;
            var corners = new Vector3[4];

            if (_onExitButton)
            {
                if (exitButtonRect != null)
                {
                    exitButtonRect.GetWorldCorners(corners);
                    cursorArrow.position = new Vector3((corners[1].x + corners[2].x) * 0.5f, corners[1].y, 0);
                }
                return;
            }

            if (_isFocusOnParty && partyPanel != null)
            {
                int pi = _partyRow * PartyCols + _partyCol;
                var rt = partyPanel.GetSlotRect(pi);
                if (rt != null)
                {
                    rt.GetWorldCorners(corners);
                    cursorArrow.position = new Vector3((corners[1].x + corners[2].x) * 0.5f, corners[1].y, 0);
                }
                return;
            }

            if (_onHeader)
            {
                if (boxNameText != null)
                {
                    boxNameText.ForceMeshUpdate();
                    var b  = boxNameText.textBounds;
                    var rt = boxNameText.GetComponent<RectTransform>();
                    cursorArrow.position = rt.TransformPoint(new Vector3(b.max.x, b.max.y, 0));
                }
                return;
            }

            int slotIndex = _cursorRow * Cols + _cursorCol;
            if (slotIndex < _slots.Length)
            {
                _slots[slotIndex].GetComponent<RectTransform>().GetWorldCorners(corners);
                // top-center của slot
                cursorArrow.position = new Vector3(
                    (corners[1].x + corners[2].x) * 0.5f,
                    corners[1].y,
                    0);
            }
        }
    }
}
