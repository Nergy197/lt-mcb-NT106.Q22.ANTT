# Kế hoạch sửa Battle Scene

## Tổng quan vấn đề
- [ ] **#1** Sprite đối thủ bị ngược với thanh máu
- [ ] **#2** Move button chưa hiển thị buff/debuff stat changes
- [ ] **#3** Move button chưa có indicator target type (đánh đôi, đánh toàn sân, v.v.)
- [ ] **#4** Move button chưa có indicator weather
- [ ] **#5** Chưa có UI chọn target slot khi dùng single-target move trong double battle

---

## #1 — Sprite đối thủ bị ngược với thanh máu

**Nguyên nhân:** Trong scene Unity, vị trí HUD (EntityHUD) của đối thủ và vị trí slot sprite (`enemyLeadSlot`, `enemySub2Slot`) không khớp nhau — một bên ở trái, bên kia ở phải.

**File liên quan:**
- `Client/Assets/Scripts/BattleUI/BattleSpriteLoader.cs` — load sprite vào slot
- `Client/Assets/Scripts/BattleUI/EntityHUD.cs` — hiển thị thanh máu
- Scene Unity (Battle scene hierarchy)

**Các bước sửa:**
1. Mở Battle scene trong Unity Editor
2. Xác định vị trí `enemyLeadSlot` và `enemySub2Slot` trong Hierarchy
3. So sánh vị trí X của slot sprite với HUD tương ứng (`EnemyHUD_A`, `EnemyHUD_B`)
4. Nếu bị hoán đổi: kéo HUD GameObject sang đúng phía của slot sprite tương ứng
5. Kiểm tra lại với bot battle hoặc PvP test

**Kết quả mong đợi:** Sprite đối thủ bên phải ↔ HUD bên phải, sprite bên trái ↔ HUD bên trái.

---

## #2 — Hiển thị buff/debuff stat changes trên move button

**Dữ liệu đã có:** `MoveSummaryDto.StatChanges: List<MoveStatChangeDto>` với `Stat` và `Stages`

**File liên quan:**
- `Client/Assets/Scripts/BattleUI/BattleSkillPanel.cs` — render move buttons
- Prefab move button trong Unity (thêm UI element mới)

**Các bước sửa:**

### Bước 2.1 — Thêm UI element vào move button prefab
- Trong prefab move button, thêm một `HorizontalLayoutGroup` nhỏ ở góc dưới phải
- Tên: `StatChangeTags` — chứa các tag label nhỏ

### Bước 2.2 — Tạo Tag prefab nhỏ
- Text: `ATK+1`, `DEF-2`, `SPD+1`, v.v.
- Màu background: xanh lá (`#3DD678`) cho buff (Stages > 0), đỏ (`#E65252`) cho debuff (Stages < 0)
- Font size nhỏ (10–12px)

### Bước 2.3 — Sửa `BattleSkillPanel.cs`
```csharp
// Trong hàm render từng move button, sau khi set tên/type/PP:
void PopulateStatChangeTags(Transform tagsContainer, List<MoveStatChangeDto> statChanges)
{
    // Xóa tags cũ
    foreach (Transform child in tagsContainer) Destroy(child.gameObject);

    if (statChanges == null || statChanges.Count == 0) return;

    foreach (var sc in statChanges)
    {
        var tag = Instantiate(statTagPrefab, tagsContainer);
        string sign = sc.Stages > 0 ? "+" : "";
        tag.GetComponentInChildren<TMP_Text>().text = $"{StatAbbrev(sc.Stat)}{sign}{sc.Stages}";
        tag.GetComponent<Image>().color = sc.Stages > 0 ? buffColor : debuffColor;
    }
}

string StatAbbrev(string stat) => stat switch
{
    "atk" => "ATK", "def" => "DEF", "spa" => "SpA", "spd" => "SpD",
    "spe" => "SPE", "acc" => "ACC", "eva" => "EVA", _ => stat.ToUpper()
};
```

**Kết quả mong đợi:** Move "Swords Dance" hiển thị tag `ATK+2`, "Growl" hiển thị `ATK-1`, v.v.

---

## #3 — Hiển thị target type indicator trên move button

**Dữ liệu đã có:** `MoveSummaryDto.TargetType` (int, cast về `MoveTargetType` enum)

**File liên quan:**
- `Client/Assets/Scripts/BattleUI/BattleSkillPanel.cs`
- Prefab move button (thêm icon nhỏ)

**Mapping hiển thị:**

| MoveTargetType | Label / Icon | Ghi chú |
|---|---|---|
| `SingleOpponent` (0) | _(ẩn)_ | Default, không cần hiển thị |
| `SpreadOpponents` (1) | `Spread` hoặc icon 2 mục tiêu | Đánh cả 2 đối thủ |
| `SingleAlly` (3) | `Ally` | Đánh đồng minh |
| `AllExceptUser` (4) | `All` | Đánh tất cả trừ bản thân |
| `Self` (5) | `Self` | Chỉ tác động bản thân |
| `RandomOpponent` (6) | `Random` | Chọn ngẫu nhiên |

**Các bước sửa:**

### Bước 3.1 — Thêm label vào prefab move button
- Thêm `TextMeshProUGUI` nhỏ tên `TargetLabel` ở góc trên phải move button

### Bước 3.2 — Sửa `BattleSkillPanel.cs`
```csharp
void SetTargetLabel(TMP_Text label, MoveTargetType targetType)
{
    label.gameObject.SetActive(true);
    switch (targetType)
    {
        case MoveTargetType.SpreadOpponents: label.text = "Spread"; break;
        case MoveTargetType.SingleAlly:      label.text = "Ally";   break;
        case MoveTargetType.AllExceptUser:   label.text = "All";    break;
        case MoveTargetType.Self:            label.text = "Self";   break;
        case MoveTargetType.RandomOpponent:  label.text = "Rnd";    break;
        default: label.gameObject.SetActive(false); break;
    }
}
```

**Kết quả mong đợi:** Earthquake hiển thị `Spread`, Helping Hand hiển thị `Ally`, v.v.

---

## #4 — Hiển thị weather indicator trên move button

**Dữ liệu đã có:** `MoveSummaryDto.Effect` (string) — các giá trị weather: `"sun"`, `"rain"`, `"sandstorm"`, `"snow"`

**File liên quan:**
- `Client/Assets/Scripts/BattleUI/BattleSkillPanel.cs`
- Prefab move button (thêm icon thời tiết)

**Mapping hiển thị:**

| Effect string | Icon/Text | Màu gợi ý |
|---|---|---|
| `"sun"` | ☀ Nắng | Vàng |
| `"rain"` | ☂ Mưa | Xanh dương |
| `"sandstorm"` | ≋ Bão cát | Cam nâu |
| `"snow"` | ❄ Tuyết | Trắng xanh |
| `"grassy-terrain"` | ✿ Cỏ | Xanh lá |
| `"electric-terrain"` | ⚡ Điện | Vàng |
| `"psychic-terrain"` | ✦ Tâm linh | Tím |
| `"misty-terrain"` | ◎ Sương | Hồng |

**Các bước sửa:**

### Bước 4.1 — Thêm weather icon vào prefab move button
- Thêm `Image` hoặc `TextMeshProUGUI` tên `WeatherIcon` ở cạnh tên move

### Bước 4.2 — Sửa `BattleSkillPanel.cs`
```csharp
static readonly HashSet<string> WeatherEffects = new() { "sun","rain","sandstorm","snow" };
static readonly HashSet<string> TerrainEffects = new() { "grassy-terrain","electric-terrain","psychic-terrain","misty-terrain" };

void SetWeatherIcon(GameObject iconObj, TMP_Text iconText, string effect)
{
    bool isWeather = WeatherEffects.Contains(effect);
    bool isTerrain = TerrainEffects.Contains(effect);
    iconObj.SetActive(isWeather || isTerrain);
    if (!isWeather && !isTerrain) return;

    iconText.text = effect switch
    {
        "sun"              => "☀",
        "rain"             => "☂",
        "sandstorm"        => "≋",
        "snow"             => "❄",
        "grassy-terrain"   => "✿",
        "electric-terrain" => "⚡",
        "psychic-terrain"  => "✦",
        "misty-terrain"    => "◎",
        _ => ""
    };
}
```

**Kết quả mong đợi:** Move "Rain Dance" hiển thị icon ☂ bên cạnh tên.

---

## #5 — UI chọn target slot cho single-target move

**Vấn đề:** Khi dùng `SingleOpponent` move trong double battle, cần cho người chơi chọn tấn công đối thủ nào (slot A hay B). Hiện tại `BattleAction.TargetSlot` chưa được set từ UI.

**File liên quan:**
- `Client/Assets/Scripts/BattleUI/BattleSkillPanel.cs`
- `Client/Assets/Scripts/BattleUI/BattleNetworkController.cs`
- `Client/Assets/Scripts/BattleUI/BattleEvents.cs`

**Luồng hiện tại:**
```
Người chơi chọn move → BattleSkillPanel gửi action → BattleNetworkController → Server
```

**Luồng mới:**
```
Người chơi chọn move
  → Nếu SingleOpponent/AllExceptUser/RandomOpponent: hiện Target Picker UI
      → Người chơi click vào sprite/HUD của đối thủ muốn tấn công
      → Set TargetSlot = 0 (Opp A) hoặc 1 (Opp B)
  → Gửi action với TargetSlot đúng
```

**Các bước sửa:**

### Bước 5.1 — Thêm event `OnMoveSelectedAwaitingTarget` vào `BattleEvents.cs`
```csharp
public static event Action<MoveSummaryDto> OnMoveSelectedAwaitingTarget;
public static void RaiseMoveSelectedAwaitingTarget(MoveSummaryDto move)
    => OnMoveSelectedAwaitingTarget?.Invoke(move);

public static event Action<int> OnTargetSlotSelected; // 0 hoặc 1
public static void RaiseTargetSlotSelected(int slot)
    => OnTargetSlotSelected?.Invoke(slot);
```

### Bước 5.2 — Tạo `TargetPickerUI` component
- Hiển thị 2 nút hoặc highlight lên sprite của 2 đối thủ
- Click vào → gọi `BattleEvents.RaiseTargetSlotSelected(slot)`
- Ẩn đi sau khi chọn xong

### Bước 5.3 — Sửa `BattleSkillPanel.cs`
```csharp
void OnMoveButtonClicked(MoveSummaryDto move)
{
    bool needsTargetPick = move.TargetType == (int)MoveTargetType.SingleOpponent;
    if (needsTargetPick)
    {
        _pendingMove = move;
        BattleEvents.RaiseMoveSelectedAwaitingTarget(move);
        // TargetPickerUI sẽ lắng nghe event này và hiện lên
    }
    else
    {
        SubmitMove(move, defaultTargetSlot: 0);
    }
}
```

### Bước 5.4 — Sửa `BattleNetworkController.cs`
```csharp
// Lắng nghe OnTargetSlotSelected
BattleEvents.OnTargetSlotSelected += slot => SubmitMove(_pendingMove, slot);
```

**Kết quả mong đợi:** Khi dùng Thunderbolt trong double battle, UI hỏi "Tấn công đối thủ nào?" và người chơi click chọn.

---

## Thứ tự thực hiện

| Bước | Task | Độ khó | Ước tính |
|---|---|---|---|
| 1 | Sửa vị trí sprite/HUD trong Unity scene | Thấp | 30 phút |
| 2 | Thêm stat change tags vào move button | Trung bình | 2–3 giờ |
| 3 | Thêm target type label vào move button | Thấp | 1 giờ |
| 4 | Thêm weather/terrain icon vào move button | Thấp | 1 giờ |
| 5 | Target picker UI cho single-target move | Cao | 3–4 giờ |

**Tổng ước tính:** 7–9 giờ

---

## Ghi chú

- Các thay đổi #2, #3, #4 chỉ là UI-only, không cần sửa server
- Thay đổi #5 yêu cầu client biết đúng `TargetSlot` trước khi gửi lên server — server đã xử lý đúng nếu nhận đúng slot
- Ưu tiên #1 trước vì ảnh hưởng trực quan nhất khi chơi
