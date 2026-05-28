using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace PokemonMMO.Audio
{
    /// <summary>
    /// Singleton DontDestroyOnLoad — quản lý BGM crossfade và SFX pool (8 source).
    /// Khởi tạo ở scene đầu tiên (Start menu). Tự phát BGM theo scene qua SceneBGMConfig.
    /// Battle BGM được điều khiển thủ công bởi BattleNetworkController.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mixer")]
        [SerializeField] private AudioMixer mainMixer;
        [SerializeField] private AudioMixerGroup bgmGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private string bgmVolumeParam = "BGMVolume";
        [SerializeField] private string sfxVolumeParam = "SFXVolume";

        [Header("Scene BGM")]
        [SerializeField] private SceneBGMConfig bgmConfig;

        [Header("SFX Default")]
        public AudioClip DefaultClick;

        private AudioSource _bgmSource;
        private AudioSource[] _sfxPool;
        private int _sfxIndex;
        private Coroutine _bgmFadeCoroutine;

        private const int SfxPoolSize = 8;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop        = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume      = 1f;
            if (bgmGroup != null) _bgmSource.outputAudioMixerGroup = bgmGroup;

            _sfxPool = new AudioSource[SfxPoolSize];
            for (int i = 0; i < SfxPoolSize; i++)
            {
                _sfxPool[i] = gameObject.AddComponent<AudioSource>();
                _sfxPool[i].playOnAwake = false;
                _sfxPool[i].loop        = false;
                if (sfxGroup != null) _sfxPool[i].outputAudioMixerGroup = sfxGroup;
            }

            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        private void OnSceneChanged(Scene _, Scene newScene)
        {
            if (bgmConfig == null) return;
            var clip = bgmConfig.GetClipForScene(newScene.name);
            if (clip != null) PlayBGM(clip);
        }

        // ── BGM ──────────────────────────────────────────────────────────────────

        public void PlayBGM(AudioClip clip, float fadeDuration = 0.5f, bool loop = true)
        {
            if (clip == null) return;
            if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = StartCoroutine(FadeBGM(clip, fadeDuration, loop));
        }

        public void StopBGM(float fadeDuration = 0.3f)
        {
            if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = StartCoroutine(FadeOutBGM(fadeDuration));
        }

        private IEnumerator FadeBGM(AudioClip clip, float duration, bool loop)
        {
            float halfDur = duration * 0.5f;

            if (_bgmSource.isPlaying)
            {
                float startVol = _bgmSource.volume;
                for (float t = 0; t < halfDur; t += Time.deltaTime)
                {
                    _bgmSource.volume = Mathf.Lerp(startVol, 0f, t / halfDur);
                    yield return null;
                }
                _bgmSource.Stop();
            }

            _bgmSource.clip   = clip;
            _bgmSource.loop   = loop;
            _bgmSource.volume = 0f;
            _bgmSource.Play();

            for (float t = 0; t < halfDur; t += Time.deltaTime)
            {
                _bgmSource.volume = Mathf.Lerp(0f, 1f, t / halfDur);
                yield return null;
            }
            _bgmSource.volume = 1f;
        }

        private IEnumerator FadeOutBGM(float duration)
        {
            float startVol = _bgmSource.volume;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                _bgmSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
                yield return null;
            }
            _bgmSource.Stop();
            _bgmSource.volume = 1f;
        }

        // ── SFX ──────────────────────────────────────────────────────────────────

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null) return;
            var src = _sfxPool[_sfxIndex % SfxPoolSize];
            _sfxIndex++;
            src.clip = clip;
            src.Play();
        }

        // ── Volume API (cho AudioSettingsManager) ──────────────────────────────

        public void SetBGMVolume(float normalized)
        {
            if (mainMixer == null) return;
            float db = normalized < 0.0001f ? -80f : Mathf.Clamp(20f * Mathf.Log10(normalized), -80f, 0f);
            mainMixer.SetFloat(bgmVolumeParam, db);
        }

        public void SetSFXVolume(float normalized)
        {
            if (mainMixer == null) return;
            float db = normalized < 0.0001f ? -80f : Mathf.Clamp(20f * Mathf.Log10(normalized), -80f, 0f);
            mainMixer.SetFloat(sfxVolumeParam, db);
        }
    }
}
