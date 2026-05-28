using UnityEngine;
using UnityEngine.Audio;
using PokemonMMO.Audio;

namespace PokemonMMO.UI
{
    public class AudioSettingsManager : MonoBehaviour
    {
        public const string MasterVolumeKey = "master_volume";
        public const string BGMVolumeKey    = "bgm_volume";
        public const string SFXVolumeKey    = "sfx_volume";

        [Header("Audio Mixer (Optional)")]
        [SerializeField] private AudioMixer mainMixer;
        [SerializeField] private string masterVolumeParameter = "MasterVolume";
        [SerializeField] private float minDb = -80f;
        [SerializeField] private float maxDb = 0f;
        [SerializeField] private float defaultVolume = 1f;

        public float CurrentVolume    { get; private set; } = 1f;
        public float CurrentBGMVolume { get; private set; } = 1f;
        public float CurrentSFXVolume { get; private set; } = 1f;

        private void Awake()
        {
            CurrentVolume    = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, defaultVolume));
            CurrentBGMVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BGMVolumeKey, defaultVolume));
            CurrentSFXVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SFXVolumeKey, defaultVolume));

            ApplyMasterVolume(CurrentVolume, save: false);
            ApplyBGMVolume(CurrentBGMVolume, save: false);
            ApplySFXVolume(CurrentSFXVolume, save: false);
        }

        public void SetMasterVolume(float normalizedValue)
        {
            ApplyMasterVolume(Mathf.Clamp01(normalizedValue), save: true);
        }

        public void SetBGMVolume(float normalizedValue)
        {
            ApplyBGMVolume(Mathf.Clamp01(normalizedValue), save: true);
        }

        public void SetSFXVolume(float normalizedValue)
        {
            ApplySFXVolume(Mathf.Clamp01(normalizedValue), save: true);
        }

        private void ApplyMasterVolume(float normalizedValue, bool save)
        {
            CurrentVolume = normalizedValue;

            if (mainMixer != null)
            {
                float clamped = Mathf.Max(normalizedValue, 0.0001f);
                float db = Mathf.Clamp(20f * Mathf.Log10(clamped), minDb, maxDb);
                mainMixer.SetFloat(masterVolumeParameter, db);
            }
            else
            {
                AudioListener.volume = normalizedValue;
            }

            if (!save) return;
            PlayerPrefs.SetFloat(MasterVolumeKey, normalizedValue);
            PlayerPrefs.Save();
        }

        private void ApplyBGMVolume(float normalizedValue, bool save)
        {
            CurrentBGMVolume = normalizedValue;
            AudioManager.Instance?.SetBGMVolume(normalizedValue);
            if (!save) return;
            PlayerPrefs.SetFloat(BGMVolumeKey, normalizedValue);
            PlayerPrefs.Save();
        }

        private void ApplySFXVolume(float normalizedValue, bool save)
        {
            CurrentSFXVolume = normalizedValue;
            AudioManager.Instance?.SetSFXVolume(normalizedValue);
            if (!save) return;
            PlayerPrefs.SetFloat(SFXVolumeKey, normalizedValue);
            PlayerPrefs.Save();
        }
    }
}
