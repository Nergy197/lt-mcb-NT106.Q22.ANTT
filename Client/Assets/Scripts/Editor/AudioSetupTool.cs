#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Menu: Pokemon → Audio Setup
/// Tự động hoá toàn bộ việc setup AudioManager, SceneBGMConfig, và wire audio clips.
/// </summary>
public static class AudioSetupTool
{
    // ── Paths ───────────────────────────────────────────────────────────────
    private const string AudioBGMPath  = "Assets/Audio/BGM";
    private const string AudioSFXPath  = "Assets/Audio/SFX";
    private const string ScenesPath    = "Assets/Scenes";
    private const string ResourcesPath = "Assets/Resources";
    private const string ConfigAssetPath = "Assets/Resources/SceneBGMConfig.asset";
    private const string MixerPath     = "Assets/Audio/MainAudioMixer.mixer";

    // ── Scene → BGM clip name mapping ───────────────────────────────────────
    private static readonly (string scene, string clip)[] SceneBGMMap =
    {
        ("Start menu",   "bgm_title"),
        ("Menu scene",   "bgm_lobby"),
        ("RecuitScene",  "bgm_recruit"),
        ("BoxScene",     "bgm_box"),
        ("PokedexScene", "bgm_pokedex"),
    };

    // ── BattleNetworkController: field → clip name ───────────────────────────
    private static readonly Dictionary<string, string> BncMap = new()
    {
        { "bgmPreview",       "bgm_preview"        },
        { "bgmBattle",        "bgm_battle"          },
        { "bgmWin",           "bgm_win"             },
        { "bgmLose",          "bgm_lose"            },
        { "sfxHit",           "sfx_battle_hit"      },
        { "sfxCrit",          "sfx_battle_crit"     },
        { "sfxFaint",         "sfx_battle_faint"    },
        { "sfxHpLow",         "sfx_battle_hp_low"   },
        { "sfxStatusBurn",    "sfx_status_burn"     },
        { "sfxStatusPara",    "sfx_status_para"     },
        { "sfxStatusSleep",   "sfx_status_sleep"    },
        { "sfxStatUp",        "sfx_stat_up"         },
        { "sfxStatDown",      "sfx_stat_down"       },
        { "sfxWeatherRain",   "sfx_weather_rain"    },
        { "sfxWeatherSun",    "sfx_weather_sun"     },
        { "sfxWeatherSand",   "sfx_weather_sand"    },
        { "sfxWeatherSnow",   "sfx_weather_snow"    },
        { "sfxTera",          "sfx_tera"            },
    };

    // ── Other components: field → clip name ─────────────────────────────────
    private static readonly Dictionary<string, string> TeamPreviewMap = new()
    {
        { "sfxClick",   "sfx_ui_click"   },
        { "sfxConfirm", "sfx_ui_confirm" },
    };

    private static readonly Dictionary<string, string> ResultPanelMap = new()
    {
        { "sfxVictory",  "sfx_victory"   },
        { "sfxDefeat",   "sfx_defeat"    },
        { "sfxCoinTick", "sfx_coin_tick" },
    };

    private static readonly Dictionary<string, string> RecruitMap = new()
    {
        { "sfxRecruitRoll",    "sfx_recruit_roll"    },
        { "sfxRecruitPop",     "sfx_recruit_pop"     },
        { "sfxRecruitConfirm", "sfx_recruit_confirm" },
    };

    private static readonly Dictionary<string, string> MatchmakingMgrMap = new()
    {
        { "sfxMatchFound", "sfx_match_found" },
    };

    private static readonly Dictionary<string, string> MatchmakingUIMap = new()
    {
        { "sfxTick", "sfx_tick" },
    };

    private static readonly Dictionary<string, string> AudioManagerMap = new()
    {
        { "DefaultClick", "sfx_ui_click" },
    };

    // ════════════════════════════════════════════════════════════════════════
    // MENU ITEMS
    // ════════════════════════════════════════════════════════════════════════

    [MenuItem("Pokemon/Audio Setup/1. Tạo SceneBGMConfig Asset")]
    public static void CreateSceneBGMConfig()
    {
        var clips = LoadAllClips();

        if (!Directory.Exists(ResourcesPath))
            Directory.CreateDirectory(ResourcesPath);

        // Load existing or create new
        var config = AssetDatabase.LoadAssetAtPath<PokemonMMO.Audio.SceneBGMConfig>(ConfigAssetPath);
        bool isNew = config == null;
        if (isNew)
        {
            config = ScriptableObject.CreateInstance<PokemonMMO.Audio.SceneBGMConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
        }

        var so = new SerializedObject(config);
        var entries = so.FindProperty("entries");
        entries.arraySize = SceneBGMMap.Length;

        for (int i = 0; i < SceneBGMMap.Length; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("sceneName").stringValue = SceneBGMMap[i].scene;

            var clipProp = entry.FindPropertyRelative("clip");
            if (clipProp.objectReferenceValue == null)
            {
                if (clips.TryGetValue(SceneBGMMap[i].clip, out var clip))
                    clipProp.objectReferenceValue = clip;
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string action = isNew ? "Đã tạo" : "Đã cập nhật";
        Debug.Log($"[AudioSetup] {action} SceneBGMConfig tại {ConfigAssetPath}");
        EditorGUIUtility.PingObject(config);
    }

    // ────────────────────────────────────────────────────────────────────────

    [MenuItem("Pokemon/Audio Setup/2. Tạo AudioManager (Start menu scene)")]
    public static void CreateAudioManagerInStartMenu()
    {
        string scenePath = $"{ScenesPath}/Start menu.unity";
        if (!File.Exists(scenePath))
        {
            Debug.LogError($"[AudioSetup] Không tìm thấy scene: {scenePath}");
            return;
        }

        bool isCurrentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == scenePath;
        if (!isCurrentScene)
        {
            if (!EditorUtility.DisplayDialog("Mở Start menu scene?",
                "Tool cần mở 'Start menu.unity'. Scene hiện tại sẽ được save trước.\n\nTiếp tục?",
                "OK", "Huỷ")) return;

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        CreateAudioManagerInCurrentScene();
    }

    [MenuItem("Pokemon/Audio Setup/3. Tạo AudioManager (Scene hiện tại)")]
    public static void CreateAudioManagerInCurrentScene()
    {
        // Kiểm tra đã có chưa
        var existing = Object.FindObjectOfType<PokemonMMO.Audio.AudioManager>();
        if (existing != null)
        {
            Debug.Log($"[AudioSetup] AudioManager đã tồn tại: '{existing.gameObject.name}'");
            EditorGUIUtility.PingObject(existing.gameObject);
            return;
        }

        var go = new GameObject("AudioManager");
        Undo.RegisterCreatedObjectUndo(go, "Create AudioManager");

        var mgr = go.AddComponent<PokemonMMO.Audio.AudioManager>();
        var so  = new SerializedObject(mgr);

        // Gán AudioMixer
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer != null)
        {
            so.FindProperty("mainMixer").objectReferenceValue = mixer;

            // Gán mixer groups nếu đã tạo trong Editor
            AssignMixerGroup(so, mixer, "bgmGroup",  "BGM");
            AssignMixerGroup(so, mixer, "sfxGroup",  "SFX");
        }
        else
        {
            Debug.LogWarning($"[AudioSetup] Không tìm thấy AudioMixer tại {MixerPath}");
        }

        // Gán SceneBGMConfig
        var config = AssetDatabase.LoadAssetAtPath<PokemonMMO.Audio.SceneBGMConfig>(ConfigAssetPath);
        if (config != null)
            so.FindProperty("bgmConfig").objectReferenceValue = config;
        else
            Debug.LogWarning("[AudioSetup] SceneBGMConfig chưa tồn tại. Chạy bước 1 trước.");

        // DefaultClick
        var clips = LoadAllClips();
        TryAssign(so, "DefaultClick", clips, "sfx_ui_click");

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(go.scene);

        Debug.Log($"[AudioSetup] Đã tạo AudioManager trong scene '{go.scene.name}'");
        EditorGUIUtility.PingObject(go);
    }

    // ────────────────────────────────────────────────────────────────────────

    [MenuItem("Pokemon/Audio Setup/4. Wire Audio Clips (Scene hiện tại)")]
    public static void WireClipsCurrentScene()
    {
        var clips = LoadAllClips();
        int count = WireComponentsInCurrentScene(clips);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[AudioSetup] Wire hoàn tất. Đã gán {count} clip(s) trong scene hiện tại.");
    }

    [MenuItem("Pokemon/Audio Setup/5. Wire Audio Clips (Tất cả scenes) ⚠️")]
    public static void WireClipsAllScenes()
    {
        if (!EditorUtility.DisplayDialog("⚠️ Wire Tất cả Scenes",
            "Tool sẽ lần lượt mở từng scene, gán clips, và save.\n" +
            "Bất kỳ thay đổi chưa save trong scene hiện tại sẽ được hỏi trước.\n\n" +
            "Tiếp tục?", "OK", "Huỷ"))
            return;

        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        string[] sceneNames = { "Start menu", "Menu scene", "Battle scene", "0_BattleScene",
                                 "RecuitScene", "BoxScene", "PokedexScene" };

        var clips = LoadAllClips();
        int totalCount = 0;

        foreach (string sceneName in sceneNames)
        {
            string path = $"{ScenesPath}/{sceneName}.unity";
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[AudioSetup] Không tìm thấy: {path}");
                continue;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int count = WireComponentsInCurrentScene(clips);
            totalCount += count;

            if (count > 0)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[AudioSetup] {sceneName}: đã gán {count} clip(s) → saved.");
            }
            else
            {
                Debug.Log($"[AudioSetup] {sceneName}: không có clip nào cần gán.");
            }
        }

        Debug.Log($"[AudioSetup] ✅ Hoàn tất tất cả scenes. Tổng cộng {totalCount} clip(s) đã gán.");
    }

    // ────────────────────────────────────────────────────────────────────────

    [MenuItem("Pokemon/Audio Setup/6. Thêm UIAudioHook vào tất cả Button (Scene hiện tại)")]
    public static void AddUIAudioHooks()
    {
        var buttons = Object.FindObjectsOfType<UnityEngine.UI.Button>(true);
        int added   = 0;

        foreach (var btn in buttons)
        {
            if (btn.GetComponent<PokemonMMO.UI.UIAudioHook>() != null) continue;

            Undo.AddComponent<PokemonMMO.UI.UIAudioHook>(btn.gameObject);
            added++;
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[AudioSetup] Đã thêm UIAudioHook vào {added} Button(s) — dùng AudioManager.DefaultClick làm fallback.");
    }

    // ────────────────────────────────────────────────────────────────────────

    [MenuItem("Pokemon/Audio Setup/─────────────────────")]
    public static void Separator() { }

    [MenuItem("Pokemon/Audio Setup/─────────────────────", true)]
    public static bool SeparatorValidate() => false;

    [MenuItem("Pokemon/Audio Setup/RUN ALL (1→2→4→5→6→7→8)")]
    public static void RunAll()
    {
        if (!EditorUtility.DisplayDialog("Chạy Full Audio Setup?",
            "Thực hiện theo thứ tự:\n" +
            "1. Tạo SceneBGMConfig\n" +
            "2. Tạo AudioManager trong Start menu\n" +
            "3. Wire clips tất cả scenes\n" +
            "4. Thêm UIAudioHook (scene hiện tại)\n" +
            "5. Tạo Placeholder SFX\n" +
            "6. Wire Battle Scene BGM+SFX\n\n" +
            "⚠️ Scene hiện tại sẽ được lưu.\n\nTiếp tục?", "OK", "Huỷ"))
            return;

        CreateSceneBGMConfig();
        CreateAudioManagerInStartMenu();
        WireClipsAllScenes();
        AddUIAudioHooks();
        WireBattleSceneAudio(); // bao gồm CreatePlaceholderSFX bên trong
    }

    // ────────────────────────────────────────────────────────────────────────

    // Tên tất cả SFX cần có trong project
    private static readonly string[] AllSfxNames =
    {
        "sfx_battle_hit", "sfx_battle_crit", "sfx_battle_faint",
        "sfx_battle_hp_low", "sfx_status_burn", "sfx_status_para",
        "sfx_status_sleep", "sfx_stat_up", "sfx_stat_down",
        "sfx_weather_rain", "sfx_weather_sun", "sfx_weather_sand",
        "sfx_weather_snow", "sfx_tera", "sfx_ui_click", "sfx_ui_confirm",
        "sfx_victory", "sfx_defeat", "sfx_coin_tick",
        "sfx_recruit_roll", "sfx_recruit_pop", "sfx_recruit_confirm",
        "sfx_match_found", "sfx_tick",
    };

    [MenuItem("Pokemon/Audio Setup/7. Tạo Placeholder SFX (silent .wav)")]
    public static void CreatePlaceholderSFX()
    {
        if (!Directory.Exists(AudioSFXPath))
            Directory.CreateDirectory(AudioSFXPath);

        int created = 0;
        foreach (var sfxName in AllSfxNames)
        {
            string path = $"{AudioSFXPath}/{sfxName}.wav";
            if (File.Exists(path)) continue;
            File.WriteAllBytes(path, BuildSilentWav(0.1f, 44100));
            created++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[AudioSetup] Đã tạo {created} placeholder SFX (.wav silent) trong {AudioSFXPath}");
    }

    [MenuItem("Pokemon/Audio Setup/8. Wire Battle Scene BGM+SFX (force)")]
    public static void WireBattleSceneAudio()
    {
        // Tạo placeholder SFX nếu thiếu
        CreatePlaceholderSFX();

        string scenePath = $"{ScenesPath}/Battle scene.unity";
        if (!File.Exists(scenePath))
        {
            Debug.LogError($"[AudioSetup] Không tìm thấy: {scenePath}");
            return;
        }

        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var clips = LoadAllClips();
        int count = 0;

        // Force-assign tất cả field trong BattleNetworkController (kể cả đã set)
        foreach (var c in Object.FindObjectsOfType<Game.Battle.Logic.BattleNetworkController>(true))
        {
            var so = new SerializedObject(c);
            foreach (var (field, clipName) in BncMap)
            {
                var prop = so.FindProperty(field);
                if (prop == null) continue;
                if (clips.TryGetValue(clipName, out var clip))
                {
                    prop.objectReferenceValue = clip;
                    count++;
                }
                else
                    Debug.LogWarning($"[AudioSetup] Không tìm thấy clip '{clipName}'");
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(c);
        }

        // Wire các component khác trong battle scene
        foreach (var c in Object.FindObjectsOfType<Game.Battle.UI.BattleTeamPreviewPanel>(true))
            count += WireComponent(c, TeamPreviewMap, clips);
        foreach (var c in Object.FindObjectsOfType<Game.Battle.UI.BattleResultPanel>(true))
            count += WireComponent(c, ResultPanelMap, clips);

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[AudioSetup] Battle scene: wire {count} field(s) → đã save.");
    }

    // Tạo WAV PCM 16-bit mono silent
    private static byte[] BuildSilentWav(float durationSeconds, int sampleRate)
    {
        int sampleCount = (int)(sampleRate * durationSeconds);
        int dataSize    = sampleCount * 2; // 16-bit = 2 bytes/sample
        var buf         = new byte[44 + dataSize];
        int p           = 0;

        void Str(string s)  { foreach (char c in s) buf[p++] = (byte)c; }
        void I32(int v)     { buf[p++]=(byte)(v);buf[p++]=(byte)(v>>8);buf[p++]=(byte)(v>>16);buf[p++]=(byte)(v>>24); }
        void I16(short v)   { buf[p++]=(byte)(v);buf[p++]=(byte)(v>>8); }

        Str("RIFF"); I32(36 + dataSize); Str("WAVE");
        Str("fmt "); I32(16); I16(1); I16(1);
        I32(sampleRate); I32(sampleRate * 2); I16(2); I16(16);
        Str("data"); I32(dataSize);
        // phần còn lại đã là zeros (silence)
        return buf;
    }

    // ── Quick open scenes ──────────────────────────────────────────────────

    [MenuItem("Pokemon/Mở Scene/Start menu")]
    public static void OpenStartMenu() => OpenScene("Start menu");

    [MenuItem("Pokemon/Mở Scene/Menu scene")]
    public static void OpenMenuScene() => OpenScene("Menu scene");

    [MenuItem("Pokemon/Mở Scene/Battle scene")]
    public static void OpenBattleScene() => OpenScene("Battle scene");

    [MenuItem("Pokemon/Mở Scene/RecuitScene")]
    public static void OpenRecuitScene() => OpenScene("RecuitScene");

    [MenuItem("Pokemon/Mở Scene/BoxScene")]
    public static void OpenBoxScene() => OpenScene("BoxScene");

    [MenuItem("Pokemon/Mở Scene/PokedexScene")]
    public static void OpenPokedexScene() => OpenScene("PokedexScene");

    private static void OpenScene(string name)
    {
        string path = $"{ScenesPath}/{name}.unity";
        if (!File.Exists(path)) { Debug.LogError($"[AudioSetup] Không tìm thấy: {path}"); return; }
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    // ════════════════════════════════════════════════════════════════════════
    // CORE WIRING LOGIC
    // ════════════════════════════════════════════════════════════════════════

    private static int WireComponentsInCurrentScene(Dictionary<string, AudioClip> clips)
    {
        int count = 0;

        // AudioManager
        foreach (var c in Object.FindObjectsOfType<PokemonMMO.Audio.AudioManager>())
            count += WireComponent(c, AudioManagerMap, clips);

        // BattleNetworkController
        foreach (var c in Object.FindObjectsOfType<Game.Battle.Logic.BattleNetworkController>())
            count += WireComponent(c, BncMap, clips);

        // BattleTeamPreviewPanel
        foreach (var c in Object.FindObjectsOfType<Game.Battle.UI.BattleTeamPreviewPanel>())
            count += WireComponent(c, TeamPreviewMap, clips);

        // BattleResultPanel
        foreach (var c in Object.FindObjectsOfType<Game.Battle.UI.BattleResultPanel>())
            count += WireComponent(c, ResultPanelMap, clips);

        // RecruitManager
        foreach (var c in Object.FindObjectsOfType<PokemonMMO.UI.RecruitManager>())
            count += WireComponent(c, RecruitMap, clips);

        // MatchmakingManager
        foreach (var c in Object.FindObjectsOfType<Game.Network.MatchmakingManager>())
            count += WireComponent(c, MatchmakingMgrMap, clips);

        // MatchmakingUI
        foreach (var c in Object.FindObjectsOfType<PokemonMMO.UI.MatchmakingUI>())
            count += WireComponent(c, MatchmakingUIMap, clips);

        return count;
    }

    private static int WireComponent(Object component, Dictionary<string, string> fieldMap,
                                      Dictionary<string, AudioClip> clips)
    {
        var so    = new SerializedObject(component);
        int count = 0;

        foreach (var (field, clipName) in fieldMap)
            if (TryAssign(so, field, clips, clipName))
                count++;

        if (count > 0)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
        }
        return count;
    }

    // ════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private static Dictionary<string, AudioClip> LoadAllClips()
    {
        var dict  = new Dictionary<string, AudioClip>(System.StringComparer.OrdinalIgnoreCase);
        var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;

            string name = Path.GetFileNameWithoutExtension(path).ToLower();
            dict[name] = clip;
        }

        Debug.Log($"[AudioSetup] Tìm thấy {dict.Count} AudioClip trong Assets/Audio");
        return dict;
    }

    /// <summary>Trả về true nếu đã gán clip mới (skip nếu field đã có giá trị).</summary>
    private static bool TryAssign(SerializedObject so, string fieldName,
                                   Dictionary<string, AudioClip> clips, string clipName)
    {
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[AudioSetup] Không tìm thấy property '{fieldName}' trên {so.targetObject?.GetType().Name}");
            return false;
        }
        if (prop.objectReferenceValue != null) return false; // đã gán, skip

        if (!clips.TryGetValue(clipName, out var clip))
        {
            Debug.LogWarning($"[AudioSetup] Không tìm thấy clip '{clipName}.mp3/.wav' trong Assets/Audio");
            return false;
        }

        prop.objectReferenceValue = clip;
        return true;
    }

    private static void AssignMixerGroup(SerializedObject so, AudioMixer mixer, string fieldName, string groupName)
    {
        var prop = so.FindProperty(fieldName);
        if (prop == null || prop.objectReferenceValue != null) return;

        var groups = mixer.FindMatchingGroups(groupName);
        if (groups != null && groups.Length > 0)
            prop.objectReferenceValue = groups[0];
        else
            Debug.LogWarning($"[AudioSetup] Mixer group '{groupName}' chưa tồn tại. " +
                             $"Vào MainAudioMixer → thêm group '{groupName}' con của Master, " +
                             $"expose parameter '{groupName}Volume', rồi chạy lại tool này.");
    }
}
#endif
