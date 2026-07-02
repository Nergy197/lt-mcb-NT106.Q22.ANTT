using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PokemonMMO.Box
{
    public class PartyPanelUI : MonoBehaviour
    {
        [Header("Party Slots (6 ô, đúng thứ tự slot 0-5)")]
        public PartySlotUI[] slots = new PartySlotUI[6];

        [Header("Exit Button")]
        public Button exitButton;
        public string exitSceneName = "Menu scene";

        [Header("Settings")]
        public string serverBaseUrl = "https://pokemon-mmo-server-123-gkaqfbejgycbcwfb.southeastasia-01.azurewebsites.net";

        // Bắn ra khi người chơi click chuột vào 1 ô party (0-5)
        public event System.Action<int> OnSlotClicked;

        private static readonly Dictionary<int, Sprite> _spriteCache = new();

        private void Start()
        {
            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                int idx = i;
                var btn = slots[i].GetComponent<Button>();
                if (btn == null) btn = slots[i].gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() => OnSlotClicked?.Invoke(idx));
            }
        }

        private void OnEnable() => StartCoroutine(LoadParty());

        public void Refresh() => StartCoroutine(LoadParty());

        public RectTransform GetSlotRect(int index)
        {
            if (index < 0 || index >= slots.Length || slots[index] == null) return null;
            return slots[index].GetComponent<RectTransform>();
        }

        private IEnumerator LoadParty()
        {
            string token = PlayerPrefs.GetString("jwt_token", "");
            using var req = UnityWebRequest.Get($"{serverBaseUrl}/api/box/party");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();

            // Clear sau khi có data để tránh flash trắng trong lúc chờ HTTP
            foreach (var s in slots) s?.SetEmpty();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Party] Load thất bại: {req.error}");
                yield break;
            }

            var info = JsonConvert.DeserializeObject<PartyInfoData>(req.downloadHandler.text);
            if (info?.Slots == null) yield break;

            foreach (var slot in info.Slots)
            {
                if (slot.Slot < 0 || slot.Slot >= slots.Length) continue;
                slots[slot.Slot]?.SetTrialBadge(slot.IsTrial);
                StartCoroutine(LoadSlotSprite(slot.Slot, slot.SpeciesId, slot.PokemonId, slot.IconUrl ?? ""));
            }
        }

        private IEnumerator LoadSlotSprite(int slotIndex, int speciesId, string pokemonId, string iconUrl = "")
        {
            if (_spriteCache.TryGetValue(speciesId, out var cached))
            {
                slots[slotIndex].SetPokemon(pokemonId, cached);
                yield break;
            }

            string primaryUrl = !string.IsNullOrEmpty(iconUrl)
                ? $"{serverBaseUrl}{iconUrl}"
                : $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/versions/generation-viii/icons/{speciesId}.png";

            using var req = UnityWebRequest.Get(primaryUrl);
            yield return req.SendWebRequest();

            Sprite sprite = null;
            if (req.result == UnityWebRequest.Result.Success)
            {
                sprite = MakeSprite(req.downloadHandler.data);
            }
            else
            {
                string fallback = $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/versions/generation-viii/icons/{speciesId}.png";
                if (fallback != primaryUrl)
                {
                    using var req2 = UnityWebRequest.Get(fallback);
                    yield return req2.SendWebRequest();
                    if (req2.result == UnityWebRequest.Result.Success)
                        sprite = MakeSprite(req2.downloadHandler.data);
                }
            }

            if (sprite != null)
            {
                _spriteCache[speciesId] = sprite;
                slots[slotIndex].SetPokemon(pokemonId, sprite);
            }
        }

        private static Sprite MakeSprite(byte[] data)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(data);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        public void OnExitClicked() => SceneManager.LoadScene(exitSceneName);
    }
}
