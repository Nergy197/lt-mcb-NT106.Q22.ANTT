using UnityEngine;
using UnityEngine.Audio;

namespace PokemonMMO.UI
{
    public class AudioSettingsManager : MonoBehaviour
    {
        public const string MasterVolumeKey = "master_volume";

        [Header("Audio Mixer (Optional)")]
        [SerializeField] private AudioMixer mainMixer;
        [SerializeField] private string masterVolumeParameter = "MasterVolume";
        [SerializeField] private float minDb = -80f;
        [SerializeField] private float maxDb = 0f;
        [SerializeField] private float defaultVolume = 1f;

        public float CurrentVolume { get; private set; } = 1f;

        private void Awake()
        {
            CurrentVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, defaultVolume));
            ApplyVolume(CurrentVolume, save: false);
        }

        public void SetMasterVolume(float normalizedValue)
        {
            ApplyVolume(Mathf.Clamp01(normalizedValue), save: true);
        }

        private void ApplyVolume(float normalizedValue, bool save)
        {
            CurrentVolume = normalizedValue;

            // If no mixer is wired yet, this still keeps the feature functional.
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

            if (!save)
            {
                return;
            }

            PlayerPrefs.SetFloat(MasterVolumeKey, normalizedValue);
            PlayerPrefs.Save();
        }
    }
}
