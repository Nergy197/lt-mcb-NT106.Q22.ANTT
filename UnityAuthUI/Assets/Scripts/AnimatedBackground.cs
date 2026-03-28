using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace PokemonMMO.UI
{
    /// <summary>
    /// Animates the main menu background with a pulsing color gradient
    /// and floating particle orbs for a dynamic feel.
    /// </summary>
    public class AnimatedBackground : MonoBehaviour
    {
        [Header("Gradient Animation")]
        public Image backgroundImage;
        public Color[] gradientColors = new Color[]
        {
            new Color(0.05f, 0.05f, 0.25f, 1f),
            new Color(0.10f, 0.02f, 0.30f, 1f),
            new Color(0.02f, 0.15f, 0.30f, 1f),
            new Color(0.05f, 0.20f, 0.20f, 1f),
        };
        public float colorCycleDuration = 6f;

        [Header("Floating Orbs")]
        public List<RectTransform> orbs = new List<RectTransform>();
        public float orbSpeed = 30f;
        public float orbAmplitude = 80f;

        private float _colorTimer;
        private int _colorIndex;
        private float[] _orbOffsets;

        private void Start()
        {
            _orbOffsets = new float[orbs.Count];
            for (int i = 0; i < orbs.Count; i++)
                _orbOffsets[i] = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            AnimateGradient();
            AnimateOrbs();
        }

        private void AnimateGradient()
        {
            _colorTimer += Time.deltaTime;
            float t = _colorTimer / colorCycleDuration;

            if (t >= 1f)
            {
                t = 0f;
                _colorTimer = 0f;
                _colorIndex = (_colorIndex + 1) % gradientColors.Length;
            }

            int nextIndex = (_colorIndex + 1) % gradientColors.Length;
            if (backgroundImage != null)
                backgroundImage.color = Color.Lerp(gradientColors[_colorIndex], gradientColors[nextIndex], t);
        }

        private void AnimateOrbs()
        {
            float time = Time.time;
            for (int i = 0; i < orbs.Count; i++)
            {
                if (orbs[i] == null) continue;
                float offset = _orbOffsets[i];
                float x = Mathf.Sin(time * orbSpeed * 0.01f + offset) * orbAmplitude;
                float y = Mathf.Cos(time * orbSpeed * 0.013f + offset * 1.3f) * orbAmplitude;
                orbs[i].anchoredPosition = orbs[i].anchoredPosition + new Vector2(x, y) * Time.deltaTime * 0.5f;

                // Keep orbs within a loose screen boundary by wrapping
                Vector2 pos = orbs[i].anchoredPosition;
                RectTransform parent = orbs[i].parent as RectTransform;
                if (parent != null)
                {
                    float hw = parent.rect.width * 0.5f + 100f;
                    float hh = parent.rect.height * 0.5f + 100f;
                    if (pos.x > hw) pos.x = -hw;
                    if (pos.x < -hw) pos.x = hw;
                    if (pos.y > hh) pos.y = -hh;
                    if (pos.y < -hh) pos.y = hh;
                }
                orbs[i].anchoredPosition = pos;
            }
        }
    }
}
