using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Game.Battle.Logic
{
    public class BattleSpriteLoader : MonoBehaviour
    {
        [Header("Arena Slots")]
        public SpriteRenderer playerLeadSlot;
        public SpriteRenderer playerSub2Slot;
        public SpriteRenderer enemyLeadSlot;
        public SpriteRenderer enemySub2Slot;

        private string _serverUrl = "https://lt-mcb-nt106q22antt-production-cc69.up.railway.app/data/pokemon";

        // Cache sprite da tai de tranh tai lai
        private static readonly Dictionary<string, Sprite> _spriteCache = new();

        // ── API CHINH ────────────────────────────────────────────────────────

        /// <summary>
        /// Tai sprite chien dau (SpriteRenderer tren san) + icon mini (Image tren HUD).
        /// </summary>
        public void LoadSpriteForSlot(string slotName, string hudName, string pokemonName, bool isBackSprite)
        {
            StartCoroutine(DownloadSpriteCoroutine(slotName, hudName, pokemonName, isBackSprite));
        }

        /// <summary>
        /// Tai icon va gan truc tiep vao Image component — dung cho Party Panel, Team Preview.
        /// Khong can tim theo ten, khong bi loi khi object an.
        /// </summary>
        public void LoadIconDirect(string pokemonName, Image targetImage)
        {
            if (targetImage == null) return;
            string species = pokemonName.ToLower().Replace(" ", "-");

            // Kiem tra cache
            string cacheKey = $"icon_{species}";
            if (_spriteCache.TryGetValue(cacheKey, out var cached))
            {
                targetImage.sprite = cached;
                targetImage.color = Color.white;
                return;
            }
            StartCoroutine(DownloadIconDirect(species, targetImage, cacheKey));
        }

        // ── COROUTINES ───────────────────────────────────────────────────────

        private IEnumerator DownloadSpriteCoroutine(string slotName, string hudName, string pokemonName, bool isBackSprite)
        {
            if (string.IsNullOrEmpty(pokemonName)) yield break;
            string species = pokemonName.ToLower().Replace(" ", "-");

            // 1. TAI ANH CHIEN DAU
            if (!string.IsNullOrEmpty(slotName))
            {
                string folder = isBackSprite ? "back" : "front";
                string mainUrl = $"{_serverUrl}/{folder}/{species}.png";
                string cacheKey = $"{folder}_{species}";

                if (_spriteCache.TryGetValue(cacheKey, out var cached))
                {
                    ApplyBattleSprite(slotName, cached, isBackSprite);
                }
                else
                {
                    using (UnityWebRequest uwr = UnityWebRequest.Get(mainUrl))
                    {
                        yield return uwr.SendWebRequest();
                        if (uwr.result == UnityWebRequest.Result.Success)
                        {
                            var sp = MakeSprite(uwr.downloadHandler.data, new Vector2(0.5f, 0.2f));
                            if (sp != null)
                            {
                                _spriteCache[cacheKey] = sp;
                                ApplyBattleSprite(slotName, sp, isBackSprite);
                            }
                        }
                        else if (isBackSprite)
                        {
                            // FALLBACK: back/ khong co → dung front/
                            string fallbackUrl = $"{_serverUrl}/front/{species}.png";
                            string fallbackKey = $"front_{species}";
                            using (UnityWebRequest uwr2 = UnityWebRequest.Get(fallbackUrl))
                            {
                                yield return uwr2.SendWebRequest();
                                if (uwr2.result == UnityWebRequest.Result.Success)
                                {
                                    var sp2 = MakeSprite(uwr2.downloadHandler.data, new Vector2(0.5f, 0.2f));
                                    if (sp2 != null)
                                    {
                                        _spriteCache[fallbackKey] = sp2;
                                        ApplyBattleSprite(slotName, sp2, isBackSprite);
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning($"[SpriteLoader] Battle sprite FAIL (both back/ and front/): {species}");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[SpriteLoader] Battle sprite FAIL: {mainUrl} - {uwr.error}");
                        }
                    }
                }
            }

            // 2. TAI ICON MINI cho HUD
            if (!string.IsNullOrEmpty(hudName))
            {
                yield return StartCoroutine(DownloadAndApplyIcon(species, hudName));
            }
        }

        private IEnumerator DownloadAndApplyIcon(string species, string hudName)
        {
            string cacheKey = $"icon_{species}";

            // Check cache
            if (_spriteCache.TryGetValue(cacheKey, out var cached))
            {
                ApplyIconToHUD(hudName, cached);
                yield break;
            }

            // Try icons/ folder
            string iconUrl = $"{_serverUrl}/icons/{species}.png";
            using (UnityWebRequest uwr = UnityWebRequest.Get(iconUrl))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    var sp = MakeSprite(uwr.downloadHandler.data, Vector2.one * 0.5f);
                    if (sp != null)
                    {
                        _spriteCache[cacheKey] = sp;
                        ApplyIconToHUD(hudName, sp);
                        yield break;
                    }
                }
            }

            // Fallback: dung anh front/ lam icon
            string fallbackUrl = $"{_serverUrl}/front/{species}.png";
            using (UnityWebRequest uwr2 = UnityWebRequest.Get(fallbackUrl))
            {
                yield return uwr2.SendWebRequest();
                if (uwr2.result == UnityWebRequest.Result.Success)
                {
                    var sp = MakeSprite(uwr2.downloadHandler.data, Vector2.one * 0.5f);
                    if (sp != null)
                    {
                        _spriteCache[cacheKey] = sp;
                        ApplyIconToHUD(hudName, sp);
                    }
                }
                else
                {
                    Debug.LogWarning($"[SpriteLoader] Icon FAIL (both icons/ and front/): {species}");
                }
            }
        }

        private IEnumerator DownloadIconDirect(string species, Image target, string cacheKey)
        {
            // Try icons/
            string url = $"{_serverUrl}/icons/{species}.png";
            using (UnityWebRequest uwr = UnityWebRequest.Get(url))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    var sp = MakeSprite(uwr.downloadHandler.data, Vector2.one * 0.5f);
                    if (sp != null)
                    {
                        _spriteCache[cacheKey] = sp;
                        if (target != null) { target.sprite = sp; target.color = Color.white; }
                        yield break;
                    }
                }
            }

            // Fallback front/
            url = $"{_serverUrl}/front/{species}.png";
            using (UnityWebRequest uwr2 = UnityWebRequest.Get(url))
            {
                yield return uwr2.SendWebRequest();
                if (uwr2.result == UnityWebRequest.Result.Success)
                {
                    var sp = MakeSprite(uwr2.downloadHandler.data, Vector2.one * 0.5f);
                    if (sp != null)
                    {
                        _spriteCache[cacheKey] = sp;
                        if (target != null) { target.sprite = sp; target.color = Color.white; }
                    }
                }
            }
        }

        // ── HELPERS ──────────────────────────────────────────────────────────

        private static Sprite MakeSprite(byte[] bytes, Vector2 pivot)
        {
            Texture2D tex = new Texture2D(2, 2);
            if (!tex.LoadImage(bytes)) return null;
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, 100f);
        }

        private void ApplyBattleSprite(string slotName, Sprite sprite, bool isBackSprite)
        {
            GameObject slot = GameObject.Find(slotName);
            if (slot != null && slot.TryGetComponent<SpriteRenderer>(out var sr))
            {
                sr.sprite = sprite;
                sr.color = Color.white;
                // Mac dinh sprite front quay sang trai, sprite back quay sang phai.
                // Khong can flip neu muon 2 ben nhin nhau.
                sr.flipX = false; 
                var txtObj = slot.transform.Find("Label_Slot");
                if (txtObj) txtObj.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Xoa sprite chien dau khoi slot tren san (khi Pokemon rut lui).
        /// </summary>
        public void ClearBattleSprite(string slotName)
        {
            GameObject slot = GameObject.Find(slotName);
            if (slot != null && slot.TryGetComponent<SpriteRenderer>(out var sr))
            {
                sr.sprite = null;
                sr.color = new Color(1, 1, 1, 0);
            }
        }

        /// <summary>
        /// Gan icon vao HUD — dung FindInactive de tim ca object dang an.
        /// </summary>
        private void ApplyIconToHUD(string hudName, Sprite icon)
        {
            if (string.IsNullOrEmpty(hudName)) return;

            // Tim trong tat ca object, ke ca inactive
            Transform target = FindTransformIncludingInactive(hudName);
            if (target == null)
            {
                Debug.LogWarning($"[SpriteLoader] HUD not found (even inactive): {hudName}");
                return;
            }

            Transform iconTrans = target.Find("Avatar_Box/Icon") ?? target.Find("Icon");
            if (iconTrans != null && iconTrans.TryGetComponent<Image>(out var img))
            {
                img.sprite = icon;
                img.color = Color.white;
            }
        }

        /// <summary>
        /// Tim Transform theo ten, ke ca khi object bi SetActive(false).
        /// </summary>
        private static Transform FindTransformIncludingInactive(string name)
        {
            // Tim trong tat ca Canvas (root cua UI)
            var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (var canvas in canvases)
            {
                if (!canvas.gameObject.scene.isLoaded) continue;
                var result = FindChildRecursive(canvas.transform, name);
                if (result != null) return result;
            }
            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindChildRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
