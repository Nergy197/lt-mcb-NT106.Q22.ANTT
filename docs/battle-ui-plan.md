# Kế hoạch thiết kế UI Battle — VGC Double Battle

> **Stack**: Unity (Canvas UI) · TMP · SignalR · ASP.NET Core  
> **Format**: VGC — 2v2, Level 50, mang 4 trong 6 Pokemon

---

## 1. Tổng quan màn hình

```
┌─────────────────────────────────────────────────────────────────┐
│  [LƯỢT 3]  ☀ Sun (2)  🌿 Grassy Terrain (4)      [ForfeitBtn]  │  ← FieldHUD (luôn hiển thị)
├────────────────────────────┬────────────────────────────────────┤
│                            │                                    │
│    [Sprite địch A]         │       [Sprite địch B]             │
│    ┌──────────────────┐    │    ┌──────────────────┐           │
│    │ CHARIZARD  Lv50  │    │    │ GARCHOMP   Lv50  │           │  ← Enemy HUDs
│    │ ████████░░ 180HP │    │    │ ██████░░░░ 140HP │           │
│    │ [FIRE][FLYING]   │    │    │ [DRAGON][GROUND] │           │
│    └──────────────────┘    │    └──────────────────┘           │
│                            │                                    │
│         ARENA / FIELD (background)                             │
│                            │                                    │
│    [Sprite ta A] (back)    │       [Sprite ta B] (back)        │
│    ┌──────────────────┐    │    ┌──────────────────┐           │
│    │ INCINEROAR Lv50  │    │    │ RILLABOOM  Lv50  │           │  ← Player HUDs
│    │ ████████████ MAX │    │    │ ███████████░ 200 │           │
│    │ [FIRE][DARK]     │    │    │ [GRASS]    [BRN] │           │
│    └──────────────────┘    │    └──────────────────┘           │
│                            │                                    │
├────────────────────────────┴────────────────────────────────────┤
│                     PANEL AREA (thay đổi theo state)            │  ← Panel Area
│  [CommandPanel] / [SkillPanel] / [PartyPanel] / [DialogPanel]   │
└─────────────────────────────────────────────────────────────────┘
```

**Độ phân giải target**: 1920×1080 (16:9), responsive Canvas Scaler.

---

## 2. Scene Hierarchy

```
Battle Scene
├── Main Camera
├── Directional Light
│
├── BattleArena (3D background + sprite objects)
│   ├── Enemy_Lead_Slot      [SpriteRenderer] — Pokemon địch A (front sprite)
│   ├── Enemy_Sub2_Slot      [SpriteRenderer] — Pokemon địch B
│   ├── Player_Lead_Slot     [SpriteRenderer] — Pokemon ta A (back sprite)
│   └── Player_Sub2_Slot     [SpriteRenderer] — Pokemon ta B
│
└── BattleCanvas [Canvas — Screen Space Overlay, 1920×1080]
    │
    ├── FieldHUD_Root                       ← Luôn visible trong Running state
    │   └── BattleFieldPanel [script]
    │       ├── TurnText          [TMP]
    │       ├── WeatherRow
    │       │   ├── WeatherIcon   [Image]
    │       │   └── Text          [TMP]
    │       └── TerrainRow
    │           ├── TerrainIcon   [Image]
    │           └── Text          [TMP]
    │
    ├── GlobalHUDs_Root                     ← Ẩn khi Dialog/TeamPreview/Result
    │   ├── Enemy1_HUD [EntityHUD]
    │   │   ├── Name              [TMP]
    │   │   ├── Level             [TMP]
    │   │   ├── HP_Fill_BG
    │   │   │   ├── HP_Fill_Image [Image — fillAmount]
    │   │   │   └── HP_Value      [TMP]
    │   │   ├── Avatar_Box
    │   │   │   └── Icon          [Image — mini sprite icon]
    │   │   ├── TypeRow
    │   │   │   ├── Type1Badge    [Image + TMP]
    │   │   │   └── Type2Badge    [Image + TMP]
    │   │   ├── StatusBadge       [TMP] — BRN/PSN/PAR/SLP/FRZ/TOX
    │   │   └── TeraBadge         [TMP] — "TERA/FIRE" (ẩn mặc định)
    │   ├── Enemy2_HUD [EntityHUD]   (cấu trúc như trên)
    │   ├── Player1_HUD [EntityHUD]  (cấu trúc như trên)
    │   └── Player2_HUD [EntityHUD]  (cấu trúc như trên)
    │
    ├── ForfeitBtn [Button]                 ← Ngoài panel, luôn visible
    │
    └── PanelManager [BattleUIManager]
        ├── CommandPanel [BattleCommandPanel — PanelType=Command]
        │   ├── FightBtn    [Button]
        │   ├── PokemonBtn  [Button]
        │   ├── InfoBtn     [Button]
        │   └── (ForfeitBtn tìm toàn scene)
        │
        ├── SkillPanel [BattleSkillPanel — PanelType=Skill]
        │   ├── MoveBtn_0..3 [Button × 4]
        │   │   ├── MoveName    [TMP]
        │   │   ├── TypeAccent  [Image — màu theo type]
        │   │   └── MetaRow
        │   │       ├── TypeBadge [Image + TMP]
        │   │       └── PP        [TMP]
        │   ├── BackBtn     [Button]
        │   └── TeraBtn     [Button]
        │       └── TeraLabel [TMP]
        │
        ├── DialogPanel [BattleDialogPanel — PanelType=Dialog]
        │   ├── DialogBox   [Image background]
        │   └── DialogText  [TMP — message queue]
        │
        ├── TeamPreviewPanel [BattleTeamPreviewPanel — PanelType=TeamPreview]
        │   ├── HeaderText  [TMP] — "CHỌN ĐỘI HÌNH (4/6)"
        │   ├── TimerText   [TMP] — "⏱ 90s"
        │   ├── PokemonGrid [GridLayoutGroup — 6 slots]
        │   │   └── SlotBtn × 6 [Button]
        │   │       ├── PokemonIcon [Image — mini sprite]
        │   │       ├── PokemonName [TMP]
        │   │       ├── Type1       [TMP]
        │   │       ├── Type2       [TMP]
        │   │       ├── Level       [TMP]
        │   │       └── OrderBadge  [TMP] — số 1/2/3/4
        │   └── ConfirmBtn  [Button]
        │       └── (TMP label — "CHỌN 0/4" → "✓ XÁC NHẬN")
        │
        └── PartyPanel [BattlePartyPanel — PanelType=Party]
            ├── HeaderText  [TMP] — "SLOT A BỊ HẠ – GỬI AI VÀO?"
            ├── SlotList    [VerticalLayoutGroup — 4 slots]
            │   └── PartyCard × 4 [Button]
            │       ├── Icon        [Image — mini sprite]
            │       ├── Name        [TMP]
            │       ├── HP_BG
            │       │   ├── HP_Fill [Image — fillAmount]
            │       │   └── HP_Text [TMP]
            │       ├── TypeRow/Type1 [TMP]
            │       └── StatusBadge   [TMP]
            └── BackBtn     [Button] — ẩn khi forced switch
```

---

## 3. Thiết kế từng Panel

### 3.1 EntityHUD (áp dụng cho cả 4 Pokemon trên sân)

```
┌─────────────────────────────────┐
│ [Icon 48px]  CHARIZARD   Lv50  │
│              ████████░░ 180/250 │  HP bar — gradient xanh→vàng→đỏ
│              [FIRE] [FLYING]    │  Type badges màu theo type
│              [BRN]  [TERA/FIRE] │  Status + Tera badge (ẩn nếu không có)
└─────────────────────────────────┘
```

**Màu HP bar**:
- ≥ 50% → `#76D679` (xanh)
- 20–50% → `#F4C543` (vàng)  
- < 20%  → `#E652299` (đỏ)

**Màu Status badge**:
| Status | Màu     | Code    |
|--------|---------|---------|
| BRN    | Cam đỏ  | `#E6671A` |
| PSN    | Tím     | `#9933CC` |
| TOX    | Tím đậm | `#731A99` |
| PAR    | Vàng    | `#E6CC1A` |
| SLP    | Xanh xám| `#4D7299` |
| FRZ    | Xanh nhạt| `#8CD9F2` |

**Tera Badge**: chỉ hiện sau khi Pokemon đã Terastallize, màu theo Tera type.  
Format: `TERA/FIRE` — font nhỏ, nền semi-transparent.

---

### 3.2 CommandPanel (menu chính)

```
┌─────────────────────────────────────────┐
│  ┌──────────┐  ┌──────────┐            │
│  │  ⚔ FIGHT │  │ 🔄 PKMN  │            │
│  └──────────┘  └──────────┘            │
│  ┌──────────┐  ┌──────────┐            │
│  │  ℹ INFO  │  │ 🏳 QUIT  │            │
│  └──────────┘  └──────────┘            │
└─────────────────────────────────────────┘
```

- Layout: 2×2 grid, mỗi button ~200×80px
- Background: semi-transparent dark panel phía dưới màn hình
- Font: bold, uppercase
- FIGHT → mở SkillPanel
- PKMN → mở PartyPanel (voluntary switch)
- INFO → hiện dialog thông tin sân
- QUIT → forfeit

---

### 3.3 SkillPanel (chọn chiêu)

```
┌────────────────────────────────────────────────────────────────┐
│  ┌──────────────────────────┐  ┌──────────────────────────┐   │
│  │ ████ FLARE BLITZ         │  │ ████ CLOSE COMBAT        │   │
│  │ [FIRE] [Physical]   PP 8 │  │ [FIGHTING] [Physical] PP5│   │
│  └──────────────────────────┘  └──────────────────────────┘   │
│  ┌──────────────────────────┐  ┌──────────────────────────┐   │
│  │ ████ EARTHQUAKE          │  │ ████ FAKE OUT            │   │
│  │ [GROUND] [Physical] PP10 │  │ [NORMAL] [Physical]  PP10│   │
│  └──────────────────────────┘  └──────────────────────────┘   │
│                                           [◇ TERA]  [← BACK]  │
└────────────────────────────────────────────────────────────────┘
```

- Mỗi Move Button: `TypeAccent` bar màu trái, tên chiêu lớn, type badge nhỏ, PP text
- Nút Tera: góc dưới phải, mặc định ẩn nếu đã dùng
  - Off: nền xám `#4D4D66`, text `◇ TERA`
  - On:  nền tím `#D940D9`, text `✦ TERA ON`
- Sau khi chọn chiêu: 4 nút biến thành tên Pokemon mục tiêu (reuse slot buttons)

**Màu TypeAccent** theo 18 type — xem bảng màu trong `EntityHUD.cs` / `BattleSkillPanel.cs`.

---

### 3.4 TeamPreviewPanel (chọn đội 4/6)

```
┌─────────────────────────────────────────────────────────────────┐
│         VGC TEAM PREVIEW — CHỌN 4 POKEMON          ⏱ 90s       │
├──────────────┬──────────────┬──────────────────────────────────┤
│  ┌────────┐  │  ┌────────┐  │  ┌────────┐                      │
│  │[sprite]│  │  │[sprite]│  │  │[sprite]│                      │
│  │INCINEROAR│ │  │RILLABOOM│ │  │TORNADUS│                      │
│  │FIRE/DARK│ │  │  GRASS  │ │  │ FLYING │                      │
│  │  Lv 50  │ │  │  Lv 50  │ │  │  Lv 50 │                      │
│  │  [1]   ← đã chọn, badge số  │  │        │                      │
│  └────────┘  │  └────────┘  │  └────────┘                      │
├──────────────┴──────────────┴──────────────────────────────────┤
│  ┌────────┐     ┌────────┐     ┌────────┐                      │
│  │[sprite]│     │[sprite]│     │[sprite]│                      │
│  │GRIMMSNARL│   │REGIELEKI│    │LANDORUS│                      │
│  │  ...    │   │   ...   │    │  ...   │                       │
│  └────────┘     └────────┘     └────────┘                      │
├─────────────────────────────────────────────────────────────────┤
│                            [CHỌN 2/4]  →  [✓ XÁC NHẬN ĐỘI HÌNH]│
└─────────────────────────────────────────────────────────────────┘
```

**Màu slot theo thứ tự chọn**:
| Thứ tự | Màu       | Ý nghĩa  |
|--------|-----------|----------|
| 1      | `#33BF40` | Lead A — ra sân đầu |
| 2      | `#338CE6` | Lead B — ra sân đầu |
| 3      | `#D9B31A` | Bench 1 |
| 4      | `#D9661A` | Bench 2 |
| Chưa chọn | `#262633` | Mặc định |

- Click lần 1 → chọn (gán số thứ tự)
- Click lại → bỏ chọn, dồn số
- Nút Confirm chỉ active khi đủ 4

---

### 3.5 PartyPanel (bench picker)

```
┌──────────────────────────────────────────────────────┐
│  SLOT A BỊ HẠ — GỬI AI VÀO?                         │  ← Header thay đổi
├──────────────────────────────────────────────────────┤
│  ┌────────────────────────────────────────────────┐  │
│  │ [icon] TORNADUS       ████████░░  180/230      │  │  ← Available (sáng)
│  │        [FLYING]                                │  │
│  └────────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────────┐  │
│  │ [icon] GRIMMSNARL     ░░░░░░░░░░  FAINTED      │  │  ← Fainted (mờ, disabled)
│  │        [DARK][FAIRY]                           │  │
│  └────────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────────┐  │
│  │ [icon] REGIELEKI      ███████████ 240/240 [PAR]│  │  ← Active (mờ, disabled)
│  │        [ELECTRIC]                              │  │
│  └────────────────────────────────────────────────┘  │
├──────────────────────────────────────────────────────┤
│                               [← BACK] (ẩn nếu forced)│
└──────────────────────────────────────────────────────┘
```

- Card available: alpha = 1.0, interactable = true
- Card fainted / đang active: alpha = 0.4, interactable = false
- Click card → gửi switch action → panel tự đóng

---

### 3.6 FieldHUD (luôn hiển thị)

Đặt ở **góc trên bên trái** hoặc **thanh ngang trên cùng**:

```
[ LƯỢT 3 ]  |  ☀ Sun (2)  |  🌿 Grassy Terrain (4)
```

- Khi không có weather/terrain → ẩn row đó hoàn toàn
- Màu text theo loại weather/terrain (xem `BattleFieldPanel.cs`)

---

## 4. Luồng hoạt động (Flow)

```
Server gửi TeamPreviewReady
    │
    ▼
[TeamPreviewPanel hiện]        90 giây
Player chọn 4 Pokemon theo thứ tự → ConfirmBtn
    │
    ▼  SubmitTeamOrder([2,0,4,1])
Server → BattleRunning
    │
    ▼
[HUD 4 Pokemon cập nhật] [FieldHUD hiện Weather/Terrain]
[CommandPanel hiện] ← OnPlayerTurnStart
    │
    ├── FIGHT → [SkillPanel hiện] → Chọn chiêu → Chọn mục tiêu → SubmitBattleAction(Move)
    │           └── [TeraBtn] → toggle useTera=true → submit cùng move
    │
    ├── PKMN  → OnVoluntarySwitchRequested → [PartyPanel hiện] → Chọn → SubmitBattleAction(Switch)
    │
    └── (chờ slot B nếu còn sống)
            │
            ▼  Cả 2 slot đã submit
    Server → TurnResolved
        │
        ├── AnimEvents (dialog sequence: MoveUsed, Damage, Faint...)
        ├── HP bars animate (smooth ease-out 0.8s)
        ├── Status badges cập nhật
        └── ForcedSwitches? → [PartyPanel forced] → SubmitForcedSwitch
                │
                ▼  Tất cả resolved
        Server → TurnReady (= BattleRunning mới)
            │
            └── Vòng lặp từ đầu...

Server → BattleEnded
    │
    ▼
[ResultPanel hiện] "🏆 BẠN THẮNG!" / "💀 BẠN THUA..."
→ 5 giây → Load "Menu scene"
```

---

## 5. Sprites & Assets

### Sprite Pokemon (từ server local)

Server serve tại `http://<host>:5000/data/pokemon/`:

| Endpoint | Dùng cho |
|----------|----------|
| `/front/{name}.png` | Pokemon địch (facing front) |
| `/back/{name}.png`  | Pokemon ta (facing back) |
| `/icons/{name}.png` | Mini icon trong EntityHUD |

- Filter mode: **Point** (giữ pixel art sắc nét)
- Không scale cứng — để editor tự chỉnh Transform

### Tổ chức sprite slots trong Scene

| GameObject | Script | Sprite | HUD |
|------------|--------|--------|-----|
| `Enemy_Lead_Slot`  | SpriteRenderer | front sprite địch A | `Enemy1_HUD` |
| `Enemy_Sub2_Slot`  | SpriteRenderer | front sprite địch B | `Enemy2_HUD` |
| `Player_Lead_Slot` | SpriteRenderer | back sprite ta A    | (Player1_HUD) |
| `Player_Sub2_Slot` | SpriteRenderer | back sprite ta B    | (Player2_HUD) |

`BattleSpriteLoader` tự động load cả **main sprite** lẫn **icon** mỗi khi `OnBattleRunning` cập nhật.

---

## 6. Color Reference

### Type Colors (18 types)

| Type     | Hex       | | Type     | Hex       |
|----------|-----------|-|----------|-----------| 
| Fire     | `#DC7238` | | Ghost    | `#7A4C99` |
| Water    | `#4894DC` | | Dragon   `#5C38C7` |
| Grass    | `#6DD16B` | | Dark     | `#6A604C` |
| Electric | `#F0D938` | | Steel    | `#9A9EB8` |
| Ice      | `#A9E0EC` | | Fairy    | `#EBAEC7` |
| Fighting | `#B84D2E` | | Normal   | `#B2AD9E` |
| Poison   | `#993399` | | Psychic  | `#E14769` |
| Ground   | `#C0A556` | | Bug      | `#9EC838` |
| Flying   | `#8585DB` | | Rock     | `#948960` |

### Panel Background

| Panel       | Màu nền                          |
|-------------|----------------------------------|
| CommandPanel | `#1A1A2E` semi-transparent 85%  |
| SkillPanel   | `#0D0D1A` semi-transparent 90%  |
| DialogPanel  | `#000000` semi-transparent 80%  |
| TeamPreview  | `#0A0A1E` solid, full screen    |
| PartyPanel   | `#111122` semi-transparent 92%  |

---

## 7. Todo Setup trong Unity Editor

- [ ] Tạo Canvas + CanvasScaler (1920×1080, Scale With Screen Size)
- [ ] Tạo `PanelManager` GameObject, gán script `BattleUIManager`
- [ ] Tạo và thiết kế từng Panel theo hierarchy Section 2
- [ ] Gán `PanelType` enum đúng cho mỗi `BasePanel`
- [ ] Gán public fields trong Inspector (hoặc để script tự tìm qua `transform.Find`)
- [ ] Đặt `BattleFieldPanel` ngoài `PanelManager` — không phải BasePanel
- [ ] Đặt `ForfeitBtn` ngoài tất cả panels (luôn visible)
- [ ] Gán `playerHUD1/2`, `enemyHUD1/2`, `skillPanel`, `commandPanel` vào `BattleNetworkController`
- [ ] Gán `BattleSpriteLoader` component vào cùng GameObject với `BattleNetworkController`
- [ ] Test với bot battle trước: bật server → chạy Unity → `MatchmakingManager.CurrentBattleId` phải có giá trị
