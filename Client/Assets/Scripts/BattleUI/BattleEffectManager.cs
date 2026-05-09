using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle.UI
{
    public class BattleEffectManager : MonoBehaviour
    {
        public static BattleEffectManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// Play a move animation at the target slot's position.
        /// </summary>
        public void PlayMoveEffect(string moveName, string targetSlotName)
        {
            GameObject targetObj = GameObject.Find(targetSlotName);
            if (targetObj == null) return;

            string effectPath = MapMoveToEffectPath(moveName);
            if (string.IsNullOrEmpty(effectPath)) return;

            StartCoroutine(PlaySpriteAnimationRoutine(effectPath, targetObj.transform.position));
        }

        public void PlayStatusFlash(string targetSlotName, Color flashColor)
        {
            GameObject targetObj = GameObject.Find(targetSlotName);
            if (targetObj == null) return;
            if (targetObj.TryGetComponent<SpriteRenderer>(out var sr))
                StartCoroutine(FlashRoutine(sr, flashColor));
        }

        private IEnumerator FlashRoutine(SpriteRenderer sr, Color flashColor)
        {
            Color originalColor = sr.color;
            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                sr.color = Color.Lerp(flashColor, originalColor, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            sr.color = originalColor;
        }

        /// <summary>
        /// Shake the target sprite when hit.
        /// </summary>
        public void PlayHitEffect(string targetSlotName)
        {
            GameObject targetObj = GameObject.Find(targetSlotName);
            if (targetObj == null) return;

            if (targetObj.TryGetComponent<SpriteRenderer>(out var sr))
            {
                StartCoroutine(ShakeRoutine(sr));
            }
        }

        private string MapMoveToEffectPath(string moveName)
        {
            moveName = moveName.ToLower();
            
            // Basic mapping
            if (moveName.Contains("thunder") || moveName.Contains("bolt") || moveName.Contains("shock"))
                return "MoveEffects/017-Thunder01";
            if (moveName.Contains("fire") || moveName.Contains("ember") || moveName.Contains("flame") || moveName.Contains("blast"))
                return "MoveEffects/015-Fire01";
            if (moveName.Contains("water") || moveName.Contains("surf") || moveName.Contains("hydro") || moveName.Contains("bubble"))
                return "MoveEffects/018-Water01";
            if (moveName.Contains("ice") || moveName.Contains("freeze") || moveName.Contains("blizzard") || moveName.Contains("beam"))
                return "MoveEffects/016-Ice01";
            if (moveName.Contains("earth") || moveName.Contains("quake") || moveName.Contains("ground") || moveName.Contains("mud"))
                return "MoveEffects/Earth1";
            if (moveName.Contains("air") || moveName.Contains("slash") || moveName.Contains("wing") || moveName.Contains("fly") || moveName.Contains("wind"))
                return "MoveEffects/PRAS- Air Slash";
            if (moveName.Contains("explosion") || moveName.Contains("self-destruct") || moveName.Contains("boom"))
                return "MoveEffects/030-Explosion01";
            if (moveName.Contains("psy") || moveName.Contains("mind") || moveName.Contains("zen"))
                return "MoveEffects/022-Darkness01"; // Fallback to Darkness for Psychic if no specific one
            
            // Default to generic attack
            return "MoveEffects/003-Attack01";
        }

        private IEnumerator PlaySpriteAnimationRoutine(string path, Vector3 position)
        {
            Sprite[] frames = Resources.LoadAll<Sprite>(path);
            if (frames == null || frames.Length == 0) yield break;

            GameObject animObj = new GameObject("Effect_" + path);
            animObj.transform.position = position + new Vector3(0, 0.5f, -1f); // Offset slightly in front
            animObj.transform.localScale = Vector3.one * 2f; // Scale up for visibility

            SpriteRenderer sr = animObj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 100; // Ensure it's on top

            float frameTime = 0.05f; // 20 FPS
            foreach (var frame in frames)
            {
                sr.sprite = frame;
                yield return new WaitForSeconds(frameTime);
            }

            Destroy(animObj);
        }

        private IEnumerator ShakeRoutine(SpriteRenderer sr)
        {
            Vector3 originalPos = sr.transform.localPosition;
            Color originalColor = sr.color;
            
            float elapsed = 0f;
            float duration = 0.4f;
            float magnitude = 0.15f;

            // Flash red
            sr.color = Color.red;

            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;

                sr.transform.localPosition = originalPos + new Vector3(x, y, 0);
                
                elapsed += Time.deltaTime;
                sr.color = Color.Lerp(Color.red, originalColor, elapsed / duration);
                yield return null;
            }

            sr.transform.localPosition = originalPos;
            sr.color = originalColor;
        }
    }
}
