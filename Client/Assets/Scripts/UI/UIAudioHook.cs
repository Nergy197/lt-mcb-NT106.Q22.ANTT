using UnityEngine;
using UnityEngine.UI;
using PokemonMMO.Audio;

namespace PokemonMMO.UI
{
    /// <summary>
    /// Gắn vào bất kỳ Button nào để tự động phát SFX khi bấm.
    /// Để trống clip → dùng AudioManager.DefaultClick.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UIAudioHook : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(PlaySound);
        }

        private void PlaySound()
        {
            var mgr = AudioManager.Instance;
            if (mgr == null) return;
            mgr.PlaySFX(clip != null ? clip : mgr.DefaultClick);
        }
    }
}
