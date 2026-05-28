using System;
using UnityEngine;

namespace PokemonMMO.Audio
{
    /// <summary>
    /// ScriptableObject ánh xạ tên scene → AudioClip BGM.
    /// Tạo asset: Assets/Resources/SceneBGMConfig.asset (Create → Pokemon → Scene BGM Config).
    /// Battle scene KHÔNG dùng config này — BGM được điều khiển thủ công trong BattleNetworkController.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneBGMConfig", menuName = "Pokemon/Scene BGM Config")]
    public class SceneBGMConfig : ScriptableObject
    {
        [Serializable]
        public class SceneBGMEntry
        {
            public string sceneName;
            public AudioClip clip;
        }

        public SceneBGMEntry[] entries;

        public AudioClip GetClipForScene(string sceneName)
        {
            if (entries == null) return null;
            foreach (var e in entries)
                if (string.Equals(e.sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                    return e.clip;
            return null;
        }
    }
}
