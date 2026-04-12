# Giai đoạn 4 — Tích hợp UI pbs-unity vào Unity Client

> **Yêu cầu trước:** Giai đoạn 3 phải hoàn thành (scripts PBS đã được copy vào  
> `Client/Assets/Scripts/Battle/PBS/` và `SignalRBattleAdapter.cs` đã hoạt động).

---

## Tổng quan

Giai đoạn này thay thế toàn bộ battle UI hiện tại (7 scripts đơn giản) bằng  
hệ thống UI phong phú hơn dựa trên pbs-unity, đồng thời hiển thị các  
mechanics mới từ Giai đoạn 2 (status conditions, stat stages, weather).

### Trạng thái hiện tại vs mục tiêu

| Thành phần | Hiện tại | Sau Giai đoạn 4 |
|---|---|---|
| Command menu | `CommandMenuPanel.cs` — 2 nút Fight/Pokemon | `PBS_CommandPanel.cs` — Fight / Pokémon / (Run) với prompt text |
| Fight menu | `FightMenuPanel.cs` / `BattleFightMenu.cs` — hiện tên move | `PBS_FightPanel.cs` — tên + PP + type color + Mega/Dynamax button |
| Pokémon menu | `PokemonMenuPanel.cs` — list đơn giản | `PBS_PartyPanel.cs` — icon + HP bar màu + status badge |
| Pokemon HUD | `PokemonDataBox.cs` — name + HP | `PBS_PokemonHUD.cs` — name + level + HP bar + status badge + stat stage flash |
| Dialog box | Không có | `PBS_DialogBox.cs` — typewriter effect từ pbs-unity `Dialog.cs` |
| Weather indicator | Không có | `PBS_WeatherHUD.cs` — icon thời tiết + turns remaining |
| Battle orchestrator | `BattleUIManager.cs` — show/hide panels | `PBS_BattleUIController.cs` — nhận `TypedEvents`, điều phối animation |

---

## Bước 1 — Chuẩn bị Scene

### 1.1 Mở Battle Scene

Mở Scene chứa battle UI hiện tại trong Unity Editor.  
Nếu chưa có scene riêng cho battle, tạo mới: **File → New Scene → tên `BattleScene`**.

### 1.2 Tạo cấu trúc Canvas

Trong Hierarchy, tạo cấu trúc sau (nếu chưa có):

```
BattleCanvas (Canvas — Screen Space Overlay, CanvasScaler: 1920×1080)
├── BattleBackground       ← sprite nền
├── PokemonArea
│   ├── PlayerPokemonSlot  ← vị trí hiển thị pokemon của mình
│   └── EnemyPokemonSlot   ← vị trí pokemon đối thủ
├── HUDArea
│   ├── PlayerHUD          ← PBS_PokemonHUD (phía dưới-phải)
│   └── EnemyHUD           ← PBS_PokemonHUD (phía trên-trái)
├── WeatherHUD             ← PBS_WeatherHUD (góc trên-phải)
├── DialogBox              ← PBS_DialogBox (phía dưới)
└── MenuArea               ← panels hiện/ẩn theo trạng thái
    ├── CommandPanel       ← PBS_CommandPanel
    ├── FightPanel         ← PBS_FightPanel
    └── PartyPanel         ← PBS_PartyPanel
```

---

## Bước 2 — PBS_PokemonHUD

**Script:** `Client/Assets/Scripts/Battle/PBS/PBS_PokemonHUD.cs`  
**Nguồn tham khảo:** `tools/pbs-unity-reference/Assets/Scripts/Battle/View/UI/HUD/PokemonHUD.cs`

### 2.1 Cấu trúc GameObject

```
PBS_PokemonHUD (PBS_PokemonHUD.cs)
├── NameText         (TextMeshProUGUI)  — "Pikachu"
├── LevelText        (TextMeshProUGUI)  — "Lv.50"
├── HPText           (TextMeshProUGUI)  — "120/245"
├── HPBarFill        (Image, fillAmount)
├── StatusBadge      (Image)           — ẩn nếu None
│   └── StatusText   (TextMeshProUGUI) — "BRN", "PAR", v.v.
└── StatStageFlash   (GameObject)      — bật lên 0.5s khi stat thay đổi
    └── StatStageText (TextMeshProUGUI) — "+2 ATK", "-1 DEF"
```

### 2.2 Inspector fields cần kéo thả

| Field | Kiểu | Mô tả |
|---|---|---|
| `nameTxt` | TextMeshProUGUI | tên pokemon |
| `lvlTxt` | TextMeshProUGUI | level |
| `hpTxt` | TextMeshProUGUI | HP hiện tại/tối đa |
| `hpBar` | Image | HP bar (Image Type: Filled) |
| `hpHigh` | Color | màu HP > 50% (xanh lá) |
| `hpMed` | Color | màu HP 25–50% (vàng) |
| `hpLow` | Color | màu HP < 25% (đỏ) |
| `statusBadge` | GameObject | ẩn/hiện theo status |
| `statusText` | TextMeshProUGUI | tên status ngắn |
| `statFlashText` | TextMeshProUGUI | hiển thị stat change |

### 2.3 Method cần implement

```csharp
// Gọi khi nhận PokemonDamageEvent / BattleSync
public void UpdateHP(int currentHp, int maxHp)

// Gọi khi nhận StatusInflictedEvent / StatusHealedEvent
public void UpdateStatus(PokemonStatusCondition status)

// Gọi khi nhận StatChangeEvent — flash "+2 ATK" trong 0.5s
public IEnumerator FlashStatChange(StatIndex stat, int stages)

// Gọi lần đầu khi battle bắt đầu (BattleSync)
public void Initialize(BattlePokemonSnapshot snapshot)
```

### 2.4 Màu HP bar

```csharp
// Trong UpdateHP():
float ratio = (float)currentHp / maxHp;
hpBar.fillAmount = ratio;
hpBar.color = ratio > 0.5f ? hpHigh
            : ratio > 0.25f ? hpMed
            : hpLow;
```

---

## Bước 3 — PBS_FightPanel

**Script:** `Client/Assets/Scripts/Battle/PBS/PBS_FightPanel.cs`  
**Nguồn tham khảo:** `tools/pbs-unity-reference/Assets/Scripts/Battle/View/UI/Panels/Fight/Fight.cs`

### 3.1 Cấu trúc GameObject

```
PBS_FightPanel
├── Move1Button  (PBS_MoveButton.cs)
├── Move2Button  (PBS_MoveButton.cs)
├── Move3Button  (PBS_MoveButton.cs)
├── Move4Button  (PBS_MoveButton.cs)
├── SpecialButton (Button) — Mega / Dynamax (ẩn nếu không có)
└── MoveInfoText  (TextMeshProUGUI) — "Water / Special / 90 BP / 100% ACC"
```

### 3.2 PBS_MoveButton fields

| Field | Kiểu | Hiển thị |
|---|---|---|
| `moveName` | TextMeshProUGUI | tên move |
| `ppText` | TextMeshProUGUI | "10/10" |
| `typeText` | TextMeshProUGUI | "WATER" (màu theo type) |
| `background` | Image | màu nền theo type |

### 3.3 Màu theo type

Dùng dictionary mapping type → Color trong `PBS_TypeColors.cs`:

```csharp
public static readonly Dictionary<string, Color> TypeColors = new()
{
    ["normal"]   = new Color(0.66f, 0.65f, 0.47f),
    ["fire"]     = new Color(0.94f, 0.50f, 0.19f),
    ["water"]    = new Color(0.41f, 0.56f, 0.94f),
    ["electric"] = new Color(0.98f, 0.83f, 0.20f),
    ["grass"]    = new Color(0.47f, 0.73f, 0.29f),
    ["ice"]      = new Color(0.60f, 0.85f, 0.85f),
    ["fighting"] = new Color(0.75f, 0.24f, 0.17f),
    ["poison"]   = new Color(0.63f, 0.27f, 0.63f),
    ["ground"]   = new Color(0.88f, 0.75f, 0.41f),
    ["flying"]   = new Color(0.67f, 0.56f, 0.94f),
    ["psychic"]  = new Color(0.97f, 0.35f, 0.53f),
    ["bug"]      = new Color(0.66f, 0.73f, 0.09f),
    ["rock"]     = new Color(0.71f, 0.63f, 0.28f),
    ["ghost"]    = new Color(0.44f, 0.35f, 0.59f),
    ["dragon"]   = new Color(0.44f, 0.25f, 0.94f),
    ["dark"]     = new Color(0.44f, 0.35f, 0.29f),
    ["steel"]    = new Color(0.72f, 0.72f, 0.81f),
    ["fairy"]    = new Color(0.99f, 0.62f, 0.99f),
};
```

### 3.4 Method cần implement

```csharp
// Gọi khi BattleSync hoặc sau mỗi turn (PP thay đổi)
// moves: list từ BattlePokemonSnapshot.Moves, cần fetch tên/type từ API
public void SetMoves(List<MoveDisplayInfo> moves)

// Gọi khi hover/gamepad navigate tới move button
public void HighlightMove(int index)

// Gọi khi không có Mega/Dynamax — ẩn SpecialButton
public void SetSpecialButton(bool hasMega, bool hasDynamax)
```

> **Lưu ý:** `MoveDisplayInfo` là class local (không phải pbs-unity) chứa  
> `MoveId`, `Name`, `Type`, `Category`, `Power`, `Accuracy`, `CurrentPp`, `MaxPp`.  
> Data được fetch từ `/api/moves/{id}` qua `BattleDataProvider` (Giai đoạn 3).

---

## Bước 4 — PBS_PartyPanel

**Script:** `Client/Assets/Scripts/Battle/PBS/PBS_PartyPanel.cs`  
**Nguồn tham khảo:** `tools/pbs-unity-reference/Assets/Scripts/Battle/View/UI/Panels/Party/Party.cs`

### 4.1 Cấu trúc GameObject

```
PBS_PartyPanel
├── PartyButton1..6 (PBS_PartyButton.cs)
│   ├── PokemonIcon   (Image) — sprite icon
│   ├── NameText      (TextMeshProUGUI)
│   ├── LevelText     (TextMeshProUGUI)
│   ├── HPText        (TextMeshProUGUI)
│   ├── HPBar         (Image, filled)
│   ├── StatusBadge   (Image) — ẩn nếu None
│   └── FaintedOverlay (GameObject) — overlay xám nếu fainted
└── BackButton
```

### 4.2 HP bar màu (giống HUD)

```csharp
// Trong SetPartyButton():
float ratio = (float)snapshot.CurrentHp / snapshot.MaxHp;
hpBar.fillAmount = ratio;
hpBar.color = ratio > 0.5f ? Color.green
            : ratio > 0.25f ? Color.yellow
            : Color.red;
faintedOverlay.SetActive(snapshot.IsFainted);
```

### 4.3 Status badge

```csharp
private static string GetStatusShortName(PokemonStatusCondition s) => s switch
{
    PokemonStatusCondition.Burn      => "BRN",
    PokemonStatusCondition.Paralysis => "PAR",
    PokemonStatusCondition.Poison    => "PSN",
    PokemonStatusCondition.Toxic     => "TOX",
    PokemonStatusCondition.Sleep     => "SLP",
    PokemonStatusCondition.Freeze    => "FRZ",
    _                                => ""
};
```

---

## Bước 5 — PBS_DialogBox

**Script:** `Client/Assets/Scripts/Battle/PBS/PBS_DialogBox.cs`  
**Nguồn tham khảo:** `tools/pbs-unity-reference/Assets/Scripts/Battle/View/UI/Dialog.cs`

### 5.1 Cấu trúc

```
PBS_DialogBox
├── Background (Image)
└── DialogText (TextMeshProUGUI)
```

### 5.2 Tính năng cần giữ từ pbs-unity

- **Typewriter effect**: hiện từng ký tự theo `charPerSec` (mặc định 60)
- **Auto-advance**: sau `displayTime` giây tự chuyển dòng tiếp
- **Skip**: người chơi nhấn nút để hiện ngay toàn bộ text

### 5.3 Cách dùng với TypedEvents

```csharp
// Trong PBS_BattleUIController, khi nhận TurnResolved:
foreach (var evt in result.TypedEvents)
{
    string msg = GetEventMessage(evt);
    if (!string.IsNullOrEmpty(msg))
        yield return StartCoroutine(dialogBox.DrawText(msg));
}

private string GetEventMessage(BattleEvent evt) => evt switch
{
    MoveUsedEvent e    => $"{e.PokemonName} used {e.MoveName}!",
    MoveMissedEvent e  => $"{e.PokemonName}'s attack missed!",
    PokemonFaintEvent e=> $"{e.PokemonName} fainted!",
    StatusInflictedEvent e => GetStatusMessage(e.PokemonName, e.Status),
    StatChangeEvent e  => GetStatChangeMessage(e),
    WeatherChangedEvent e => GetWeatherMessage(e.NewWeather),
    SuperEffectiveEvent _ => "It's super effective!",
    NotVeryEffectiveEvent _ => "It's not very effective...",
    MoveNoEffectEvent e => $"It had no effect on {e.TargetName}!",
    ParalysisStuckEvent e => $"{e.PokemonName} is fully paralyzed!",
    SleepSkipEvent e   => $"{e.PokemonName} is fast asleep!",
    FreezeThawEvent e  => $"{e.PokemonName} thawed out!",
    BattleEndEvent e   => e.WinnerPlayerId != null ? "You won!" : "You lost...",
    _ => ""
};
```

---

## Bước 6 — PBS_WeatherHUD

**Script:** `Client/Assets/Scripts/Battle/PBS/PBS_WeatherHUD.cs`  
*(Không có tương đương trong pbs-unity — viết mới hoàn toàn)*

### 6.1 Cấu trúc

```
PBS_WeatherHUD  (góc trên-phải, ẩn khi Weather = None)
├── WeatherIcon   (Image) — sprite theo loại weather
├── WeatherName   (TextMeshProUGUI) — "Rain", "Sun"...
└── TurnsText     (TextMeshProUGUI) — "5 turns left"
```

### 6.2 Sprites cần tạo

Tạo hoặc import sprite cho mỗi weather (có thể dùng emoji icons tạm):

| Weather | Sprite gợi ý |
|---|---|
| Sun | ☀️ icon vàng |
| Rain | 🌧️ icon xanh |
| Sandstorm | 🌪️ icon nâu |
| Hail | ❄️ icon trắng |

### 6.3 Method

```csharp
public void UpdateWeather(WeatherCondition weather, int turnsLeft)
{
    gameObject.SetActive(weather != WeatherCondition.None);
    if (weather == WeatherCondition.None) return;

    weatherIcon.sprite = weatherSprites[(int)weather - 1];
    weatherNameText.text = weather.ToString();
    turnsText.text = turnsLeft > 0 ? $"{turnsLeft} turns" : "";
}
```

---

## Bước 7 — PBS_BattleUIController (Orchestrator)

**Script:** `Client/Assets/Scripts/Battle/PBS/PBS_BattleUIController.cs`  
**Thay thế:** `BattleUIManager.cs` hiện tại

Đây là script trung tâm — nhận data từ `SignalRBattleAdapter` và  
điều phối tất cả UI components.

### 7.1 SignalR events cần lắng nghe

```csharp
// Đăng ký trong Start() hoặc OnEnable()
_adapter.OnBattleSync     += HandleBattleSync;
_adapter.OnTurnResolved   += HandleTurnResolved;
_adapter.OnBattleEnded    += HandleBattleEnded;
_adapter.OnActionAccepted += HandleActionAccepted;
```

### 7.2 Flow xử lý TurnResolved

```
Nhận TurnResolved
    │
    ├─► Disable MenuArea (không cho nhập input trong khi event play)
    │
    ├─► foreach TypedEvent:
    │       ├─ PokemonDamageEvent  → UpdateHUD() + cắt HP bar animation
    │       ├─ PokemonFaintEvent   → play faint animation (placeholder)
    │       ├─ SwitchEvent         → swap pokemon sprite
    │       ├─ StatusInflictedEvent→ UpdateHUD status badge
    │       ├─ StatChangeEvent     → FlashStatChange()
    │       ├─ WeatherChangedEvent → UpdateWeatherHUD()
    │       └─ các event khác      → show dialog text
    │
    └─► Enable MenuArea khi tất cả events đã play xong
```

### 7.3 State machine đơn giản

```csharp
public enum BattleUIState
{
    Idle,           // Chờ input
    PlayingEvents,  // Đang play event queue
    WaitingAction,  // Đã submit action, chờ đối thủ
    BattleEnded
}
```

### 7.4 Inspector fields

| Field | Component |
|---|---|
| `playerHUD` | PBS_PokemonHUD |
| `enemyHUD` | PBS_PokemonHUD |
| `weatherHUD` | PBS_WeatherHUD |
| `dialogBox` | PBS_DialogBox |
| `commandPanel` | PBS_CommandPanel |
| `fightPanel` | PBS_FightPanel |
| `partyPanel` | PBS_PartyPanel |
| `adapter` | SignalRBattleAdapter |
| `dataProvider` | BattleDataProvider |

---

## Bước 8 — Migration từ code cũ

### 8.1 Scripts cũ có thể xóa sau khi test xong

```
Client/Assets/Scripts/Battle/BattleUIManager.cs     → thay bằng PBS_BattleUIController
Client/Assets/Scripts/Battle/CommandMenuPanel.cs    → thay bằng PBS_CommandPanel
Client/Assets/Scripts/Battle/FightMenuPanel.cs      → thay bằng PBS_FightPanel
Client/Assets/Scripts/Battle/BattleFightMenu.cs     → merge vào PBS_FightPanel
Client/Assets/Scripts/Battle/PokemonMenuPanel.cs    → thay bằng PBS_PartyPanel
Client/Assets/Scripts/Battle/PokemonDataBox.cs      → thay bằng PBS_PokemonHUD
Client/Assets/Scripts/Battle/MenuBackButton.cs      → logic gộp vào các panel
```

### 8.2 Không xóa ngay

Giữ scripts cũ trong một subfolder `Battle/Legacy/` cho đến khi test  
toàn bộ golden path thành công. Xóa sau khi verify.

---

## Bước 9 — Testing checklist

Sau khi hoàn thành, test theo thứ tự:

- [ ] Battle bắt đầu → cả 2 HUD hiển thị đúng pokemon name, level, HP
- [ ] Chọn move → FightPanel hiện tên move + PP + type color đúng
- [ ] Move gây damage → HP bar animation, số HP cập nhật đúng
- [ ] Move super effective → dialog "It's super effective!" xuất hiện
- [ ] Pokemon faint → faint animation + auto-switch
- [ ] Inflict burn → status badge "BRN" xuất hiện trên HUD
- [ ] End-of-turn burn damage → HP giảm, dialog hiện
- [ ] Stat stage change → stat flash "+2 ATK" trên HUD
- [ ] Weather change → WeatherHUD hiện icon + turns remaining
- [ ] Switch pokemon → PartyPanel hiện HP bar + status đúng
- [ ] Battle kết thúc → dialog "You won!" / "You lost..."
- [ ] Reconnect (BattleSync) → toàn bộ state restore đúng

---

## Phụ lục — Animation placeholders

Ở giai đoạn này, tất cả animation chỉ cần placeholder:

| Animation | Placeholder đơn giản |
|---|---|
| HP giảm | `DOTween.To()` fillAmount trong 0.3s |
| Pokemon faint | `Animator` trigger "Faint" → fade alpha về 0 |
| Stat flash | `SetActive(true)` → `yield return 0.5s` → `SetActive(false)` |
| Move use | flash sprite tint → reset |
| Weather change | crossfade background color |

Tất cả animation đều có thể thay bằng sprite animation thật sau  
mà không cần refactor script — chỉ cần thay đổi trong Animator Controller.

---

*Tài liệu này được tạo tự động dựa trên phân tích pbs-unity và hệ thống hiện tại.  
Cập nhật lần cuối: 2026-04-12*
