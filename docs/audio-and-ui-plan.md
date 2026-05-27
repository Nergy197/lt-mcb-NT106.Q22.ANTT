# Kế hoạch: BGM / SFX + Battle Scene Optimization + Matchmaking UX

Cập nhật lần cuối: 2026-05-27

---

## Tổng quan tiến độ

| Phase | Nội dung | Trạng thái |
|---|---|---|
| 0 | Matchmaking timer + nút huỷ tìm trận | **DONE** |
| 0.5 | Casual mode + Private mode (nhập mã số) | **Code DONE — cần dựng UI Editor** |
| 1 | Battle scene loading overlay (ẩn placeholder) | Chưa làm |
| 2 | AudioManager foundation + Mixer groups | Chưa làm |
| 3 | BGM per scene | Chưa làm |
| 4 | Battle SFX | Chưa làm |
| 5 | UI SFX global | Chưa làm |

---

## Phase 0 — Matchmaking timer + nút huỷ ✅ DONE

### Vấn đề
- UI cũ chỉ hiển thị countdown bot fallback, không có elapsed timer và không có nút huỷ.

### Đã implement

**`MatchmakingManager.cs`**
- Thêm `static event Action OnSearchStarted` — fire khi server xác nhận `SearchStarted`
- Thêm `static event Action OnSearchCancelled` — fire sau khi gọi `CancelSearching()` (trong `finally`)
- Thêm `CancelSearching()` — gọi hub `"CancelMatchmaking"`, đảm bảo fire `OnSearchCancelled` kể cả khi hub lỗi
- Đổi tên handler nội bộ thành `HandleSearchStartedDto` để tránh collision tên với event

**`MatchmakingUI.cs`** (viết lại hoàn toàn)
- `searchTimerLabel` — đếm thời gian đang tìm, cập nhật mỗi frame `Update()`, format `MM:SS`
- `botCountdownLabel` — hiện khi server gửi `SearchTick`, hiển thị "Bot dự phòng trong: Xs", ẩn mặc định
- `cancelButton` — disable ngay khi bấm (tránh double-click), gọi `CancelSearching()`
- Panel tự hiện khi `OnSearchStarted`, tự ẩn khi `OnSearchCancelled`

**`ModeMenu.cs`**
- Track `_isSearching` flag — block keyboard nav và lock buttons khi đang tìm trận
- Subscribe `OnSearchCancelled` → unlock buttons, `_isSearching = false`

**`MatchmakingUIBuilder.cs`** (Editor tool)
- Menu: **Pokemon → Matchmaking UI Builder**
- Tự động tạo panel trong scene: overlay full-screen + card 500×360 + timer + countdown + cancel button
- Wire `MatchmakingUI` component qua `SerializedObject` (không cần kéo tay trong Inspector)
- Chạy lại tool sẽ xoá panel cũ và tạo mới (có Undo)

### Việc còn lại (Unity Editor)
- Mở **Menu scene** → chạy **Pokemon → Matchmaking UI Builder**
- Kiểm tra panel hiển thị đúng khi bấm Ranked

---

## Phase 0.5 — Casual mode + Private mode ✅ Code DONE

### Đã implement

**Server — `MatchmakingHub.cs`**
- `FindCasualMatch()` — queue riêng `CasualQueue`, bot fallback 20s, tạo battle với `BattleMode.Casual` → `RankService` bỏ qua, không cộng/trừ điểm rank
- `CreatePrivateRoom()` — sinh mã 6 chữ số ngẫu nhiên (`Random.Next(100000, 1000000)`), lưu vào `PrivateRooms` dictionary
- `JoinPrivateRoom(code)` — tra mã, tạo battle `BattleMode.Private` cho cả 2 người
- `CancelMatchmaking()` — cập nhật: hủy cả ranked queue lẫn casual queue
- `OnDisconnectedAsync()` — cập nhật: dọn casual queue + phòng private khi mất kết nối

**Client — `MatchmakingManager.cs`**
- `StartSearchingCasual()` — gọi hub `"FindCasualMatch"`, kích hoạt cùng flow UI (`OnSearchStarted` / `OnCountdownTick` / `OnSearchCancelled`) như Ranked
- `CreatePrivateRoom()` — gọi hub `"CreatePrivateRoom"`, nhận mã → fire `OnPrivateRoomCreated(string code)`
- `JoinPrivateRoom(string code)` — gọi hub `"JoinPrivateRoom"`, khi khớp server trả `MatchFound` → load Battle scene
- Events mới: `OnPrivateRoomCreated`, `OnServerError`

**Client — `ModeMenu.cs`**
- Case `Casual` → `StartSearchingCasual()`, disable buttons, chờ `OnSearchCancelled` (giống Ranked)
- Case `Private` → `privateRoomPanel.Show()`

**Client — `PrivateRoomPanel.cs`** *(file mới)*
- View "Tạo phòng": nút tạo → hiển thị mã 6 số, label "Chờ đối thủ nhập mã..."
- View "Nhập mã": input field chỉ nhận số, max 6 ký tự (`ContentType.IntegerNumber`), nút tham gia
- Nút chuyển giữa 2 view + nút huỷ đóng panel
- Subscribe `OnPrivateRoomCreated` và `OnServerError`

### Việc còn lại (Unity Editor)

**Casual**: không cần thêm gì — dùng lại `MatchmakingUI` panel sẵn có.

**Private** — cần dựng GameObject `PrivateRoomPanel` trong **Battle mode selection scene** (hoặc Menu scene nơi `ModeMenu` tồn tại):

1. Tạo Panel GameObject → gắn script `PrivateRoomPanel`
2. Cấu trúc con cần có:
   ```
   PrivateRoomPanel (Image full-screen overlay)
   ├── CreateView (GameObject)
   │   ├── CodeDisplay (TMP_Text) — hiển thị mã
   │   ├── CreateRoomBtn (Button)
   │   └── SwitchToJoinBtn (Button) — "Nhập mã thay thế"
   └── JoinView (GameObject)
       ├── CodeInput (TMP_InputField) — tự động chỉ nhận số
       ├── JoinRoomBtn (Button)
       └── SwitchToCreateBtn (Button) — "Tạo phòng thay thế"
   CancelBtn (Button)
   StatusLabel (TMP_Text)
   ```
3. Gán tất cả field `[SerializeField]` trong Inspector
4. Gán GameObject `PrivateRoomPanel` vào field `privateRoomPanel` của `ModeMenu`
5. Đặt `panel` (`root GameObject`) `SetActive(false)` mặc định

---

## Phase 1 — Battle scene loading overlay

### Vấn đề
Khi battle scene load, trước khi `BattleRunning` được nhận từ server (mất 2–12 giây), người chơi nhìn thấy:
- 4 `SpriteRenderer` slots trên arena với text `Label_Slot` placeholder (màu xanh/đỏ mờ)
- 4 `EntityHUD` ở trạng thái Unity default (text rỗng, HP bar đầy)
- `BattleFieldPanel` hiện "LƯỢT 0"
- `yield return new WaitForSeconds(0.8f)` hardcode trong `ConnectRoutine()` không có lý do

### Giải pháp: Full-screen loading overlay

**Bước 1 — Thêm event vào `BattleEvents.cs`**
```csharp
public static Action OnBattleConnected; // fire khi JoinBattle thành công
```

**Bước 2 — Tạo `BattleLoadingOverlay.cs`**
- Gắn vào một Image phủ toàn màn hình trong battle scene
- `Awake()`: `gameObject.SetActive(true)` — hiện ngay từ đầu
- Subscribe `BattleEvents.OnTeamPreviewStart` → `Hide()`
- Subscribe `BattleEvents.OnBattleConnected` → `Hide()` (phòng trường hợp không qua TeamPreview)
- Coroutine timeout 15s: nếu không nhận được event thì hiện lỗi và về menu

**Bước 3 — Xoá delay hardcode trong `BattleNetworkController.ConnectRoutine()`**
```csharp
// XOÁ dòng này — không có lý do kỹ thuật:
yield return new WaitForSeconds(0.8f);
```

**Bước 4 — Fire `OnBattleConnected` sau khi `JoinBattle` thành công**
Trong `BattleNetworkController.ConnectRoutine()`, sau khi `hub.InvokeAsync("JoinBattle", _battleId)`:
```csharp
BattleEvents.OnBattleConnected?.Invoke();
```

**Bước 5 — Ẩn HUD và arena slots từ đầu**
- Tất cả `EntityHUD.Awake()`: thêm `gameObject.SetActive(false)` — chỉ bật trong `SetupHUD()` khi có data thực
- Tất cả `SpriteRenderer` trong slot: set alpha = 0 từ `BattleSpriteLoader.Awake()` thay vì chờ `ClearBattleSprite`

**Bước 6 — Tạo Editor tool `BattleLoadingOverlayBuilder.cs`**
- Menu: **Pokemon → Battle Loading Overlay Builder**
- Tự tạo overlay panel trong battle scene

---

## Phase 2 — AudioManager foundation

### Bước

**Bước 1 — Mở `MainAudioMixer.mixer` trong Unity Editor**
- Thêm group con `BGM` của Master, expose parameter `BGMVolume`
- Thêm group con `SFX` của Master, expose parameter `SFXVolume`

**Bước 2 — Tạo `AudioManager.cs`** (`DontDestroyOnLoad` singleton)
```
Client/Assets/Scripts/Managers/AudioManager.cs
```
Thiết kế:
```csharp
// BGM
AudioSource bgmSource;           // loop = true, route qua BGM group
Coroutine   _bgmFadeCoroutine;

// SFX pool (8 source)
AudioSource[] _sfxPool;
int           _sfxIndex;

// API public
void PlayBGM(AudioClip clip, float fadeDuration = 0.5f);
void StopBGM(float fadeDuration = 0.3f);
void PlaySFX(AudioClip clip);
```
Crossfade BGM: fade out source hiện tại → swap clip → fade in.

**Bước 3 — Cập nhật `AudioSettingsManager.cs`**
- Thêm `BGMVolumeKey = "bgm_volume"` và `SFXVolumeKey = "sfx_volume"`
- Expose `SetBGMVolume(float)` và `SetSFXVolume(float)` (dùng mixer group params)
- Settings UI cần thêm 2 slider BGM / SFX (hiện chỉ có Master)

**Bước 4 — Tạo `SceneBGMConfig.cs`** (ScriptableObject)
```
Client/Assets/Resources/SceneBGMConfig.asset
```
```csharp
[Serializable]
public class SceneBGMEntry { public string sceneName; public AudioClip clip; }
SceneBGMEntry[] entries;
```
`AudioManager.Awake()` subscribe `SceneManager.activeSceneChanged` → tự động `PlayBGM` đúng track.

### Folder audio assets cần tạo
```
Client/Assets/Audio/
├── BGM/
│   ├── bgm_title.mp3        ← Start menu
│   ├── bgm_lobby.mp3        ← Menu scene
│   ├── bgm_battle.mp3       ← Battle (combat phase)
│   ├── bgm_preview.mp3      ← Battle (team preview phase)
│   ├── bgm_win.mp3          ← Battle result — thắng
│   ├── bgm_lose.mp3         ← Battle result — thua
│   ├── bgm_recruit.mp3      ← RecuitScene
│   ├── bgm_box.mp3          ← BoxScene
│   └── bgm_pokedex.mp3      ← PokedexScene
└── SFX/
    ├── sfx_ui_click.wav
    ├── sfx_ui_confirm.wav
    ├── sfx_battle_hit.wav
    ├── sfx_battle_crit.wav
    ├── sfx_battle_faint.wav
    ├── sfx_battle_hp_low.wav
    ├── sfx_status_burn.wav
    ├── sfx_status_para.wav
    ├── sfx_status_sleep.wav
    ├── sfx_stat_up.wav
    ├── sfx_stat_down.wav
    ├── sfx_weather_rain.wav
    ├── sfx_weather_sun.wav
    ├── sfx_weather_sand.wav
    ├── sfx_weather_snow.wav
    ├── sfx_tera.wav
    ├── sfx_match_found.wav
    ├── sfx_tick.wav
    ├── sfx_victory.wav
    ├── sfx_defeat.wav
    ├── sfx_coin_tick.wav
    ├── sfx_recruit_roll.wav
    ├── sfx_recruit_pop.wav
    └── sfx_recruit_confirm.wav
```

---

## Phase 3 — BGM per scene

**Bước 1 — Gán BGM clip vào `SceneBGMConfig.asset`** cho từng scene (xem bảng trong chat)

**Bước 2 — Battle BGM thay đổi theo trạng thái trận**

Trong `BattleNetworkController.cs`, bổ sung calls đến `AudioManager`:

| Sự kiện | BGM |
|---|---|
| `TeamPreviewReady` received | `PlayBGM(bgm_preview)` |
| `BattleRunning` received lần đầu | `PlayBGM(bgm_battle)` |
| `BattleEnded` — thắng | `PlayBGM(bgm_win, fade: 0.3f)`, không loop |
| `BattleEnded` — thua | `PlayBGM(bgm_lose, fade: 0.3f)`, không loop |

**Bước 3 — Matchmaking countdown SFX**

Trong `MatchmakingUI.HandleCountdownTick()`:
```csharp
AudioManager.Instance?.PlaySFX(sfxTick);
```

---

## Phase 4 — Battle SFX

Bổ sung vào `BattleNetworkController.ProcessEventVisuals()`:

| Điều kiện | SFX |
|---|---|
| `ev.Damage > 0` (hit thường) | `sfx_battle_hit` |
| `ev.Message` chứa "critical" | `sfx_battle_crit` |
| `ev.EventType == "PokemonFaintEvent"` | `sfx_battle_faint` |
| HP sau hit < 20% MaxHP | `sfx_battle_hp_low` (loop, dừng khi HP hồi) |
| `StatusInflictedEvent` — `Burn` | `sfx_status_burn` |
| `StatusInflictedEvent` — `Paralysis` | `sfx_status_para` |
| `StatusInflictedEvent` — `Sleep/Freeze` | `sfx_status_sleep` |
| `StatChangeEvent` stages > 0 | `sfx_stat_up` |
| `StatChangeEvent` stages < 0 | `sfx_stat_down` |
| Weather activated | `sfx_weather_{type}` |
| `IsTerastallized == true` | `sfx_tera` |

Bổ sung vào `BattleTeamPreviewPanel.cs`:
- Click chọn slot Pokémon → `PlaySFX(sfx_ui_click)`
- Bấm Confirm → `PlaySFX(sfx_ui_confirm)`

---

## Phase 5 — UI SFX global

**Bước 1 — Tạo `UIAudioHook.cs`** component tự gán SFX vào Button
```csharp
[RequireComponent(typeof(Button))]
public class UIAudioHook : MonoBehaviour
{
    [SerializeField] AudioClip clip; // để trống = dùng sfx_ui_click default
    void Awake() => GetComponent<Button>().onClick.AddListener(
        () => AudioManager.Instance?.PlaySFX(clip ?? AudioManager.Instance.DefaultClick)
    );
}
```
Gắn component này vào tất cả Button trong Menu scene, BoxScene, PokedexScene.

**Bước 2 — Matchmaking MatchFound fanfare**

Trong `MatchmakingManager.OnMatchFound()` (trước khi set `_shouldLoadBattle = true`):
```csharp
// Dispatch về main thread vì OnMatchFound chạy trên thread pool
UnityMainThreadDispatcher.Instance.Enqueue(() =>
    AudioManager.Instance?.PlaySFX(sfxMatchFound)
);
```

**Bước 3 — Battle result sounds**

Trong `BattleResultPanel.UpdateUI()`:
```csharp
AudioManager.Instance?.PlaySFX(_iWon ? sfxVictory : sfxDefeat);
```

Trong coroutine đếm VP/RP (nếu implement counter animation):
```csharp
AudioManager.Instance?.PlaySFX(sfxCoinTick); // mỗi số nhảy
```

**Bước 4 — Recruit scene**

Trong `RecruitManager.cs`:
- `OnRecruitButtonClicked()` → `PlaySFX(sfxRecruitRoll)`
- Trong callback `LoadIconIntoSlot` khi icon load xong → `PlaySFX(sfxRecruitPop)` với delay stagger 0.05s × index
- `ConfirmRecruitCoroutine` success → `PlaySFX(sfxRecruitConfirm)`

---

## Ghi chú kiến trúc

- `AudioManager` là singleton DontDestroyOnLoad, khởi tạo trong scene đầu tiên (Start menu)
- BGM crossfade 0.5s khi chuyển scene thông qua `SceneManager.activeSceneChanged`
- Battle BGM không phụ thuộc vào SceneBGMConfig — được điều khiển thủ công trong `BattleNetworkController`
- SFX pool 8 source tránh tình trạng nhiều âm thanh cắt nhau trong lượt chiến đấu
- Volume settings lưu vào `PlayerPrefs`: `master_volume`, `bgm_volume`, `sfx_volume`
