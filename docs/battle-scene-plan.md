# Kế hoạch sửa lỗi và hoàn thiện Battle Scene

> Ngày lập: 2026-05-09  
> Phạm vi: VGC double battle – toàn bộ server + client

---

## 1. Sơ đồ kiến trúc nhanh

```
MatchmakingManager ──MatchFound──► BattleNetworkController
                                        │
                          SignalR (BattleHub) ──► BattleService
                                        │              │
                                  BattleEvents     BattleSession
                                        │
                    ┌───────────────────┼────────────────────┐
              BattleUIManager    EntityHUD x4        BattleFieldPanel
                    │
      ┌─────────────┼──────────────┬────────────┬───────────┐
 CommandPanel  SkillPanel    PartyPanel  TeamPreviewPanel  DialogPanel
```

---

## 2. Bugs đã xác nhận – phân loại theo mức độ

### 🔴 Critical (game-breaking)

#### BUG-01 · Duplicate "Error" SignalR handler
**File:** [BattleNetworkController.cs](Client/Assets/Scripts/BattleUI/BattleNetworkController.cs#L151-L160)  
**Mô tả:** Sự kiện `"Error"` được đăng ký hai lần sau khi xóa:
```csharp
hub.On<string>("Error", msg => Enqueue(() => Debug.LogError(msg)));        // dòng ~151
...
hub.On<string>("Error", msg => Enqueue(() => Debug.LogError("[Battle] " + msg)));  // dòng ~160
```
Handler `"Debug"` cũng không được `Remove()` trước khi đăng ký lại.  
**Hậu quả:** Mỗi lần error xảy ra, callback được gọi hai lần; tích lũy qua các lần kết nối lại.  
**Fix:** Xóa dòng đăng ký "Error" đầu tiên (dòng ~151) và thêm `"Debug"` vào danh sách `Remove`.

---

#### BUG-02 · Status condition gửi dưới dạng enum int, client nhận string
**File (server):** [BattleHub.cs](Server/Hubs/BattleHub.cs#L370)  
**File (client):** `PokemonDto.Status` (string)  
**Mô tả:** `ToFieldPokemon()` gán `Status = p.NonVolatileStatus` kiểu `PokemonStatusCondition` (enum). ASP.NET Core SignalR dùng `System.Text.Json` mặc định serialize enum là số (`0`, `1`, `2`…). Client deserialize sang `string` nhận được `"1"` thay vì `"Burn"` → `AbbrevStatus()` không khớp → **status badge không bao giờ hiển thị**.  
**Fix:** Trong `FieldPokemonDto`, đổi kiểu `Status` thành `string` và assign:
```csharp
Status = p.NonVolatileStatus == PokemonStatusCondition.None
         ? null
         : p.NonVolatileStatus.ToString().ToLower(),
```

---

#### BUG-03 · Target selection (phase 2) chuyển về Command panel thay vì ở lại Skill panel
**File:** [BattleNetworkController.cs](Client/Assets/Scripts/BattleUI/BattleNetworkController.cs#L484-L486)  
**Mô tả:** Sau khi player chọn chiêu lần 1 (`!_pickingTarget`), code gọi:
```csharp
BattleEvents.OnPrintDialog?.Invoke("Đánh vào ai?", true);
BattleEvents.OnPlayerTurnStart?.Invoke(); // "giữ Skill panel mở"
```
`OnPrintDialog` → `HandlePrintDialog()` → `SwitchPanel(Dialog)`.  
`OnPlayerTurnStart` → `HandleTurnStart()` → `SwitchPanel(Command)`.  
Kết quả: player thấy Command panel, **không thấy target buttons** của Skill panel.  
**Fix:** Bỏ cả hai lệnh gọi event trên. Thay bằng trực tiếp:
```csharp
skillPanel?.SetTargetLabels(oppA, oppB, yourA, yourB);
// Skill panel đang hiển thị (player vừa click Fight → Skill), không cần switch lại
```
Thêm method `SetTargetLabels()` vào `BattleSkillPanel` để set tên 4 nút và disable nút có nhãn `"---"`.

---

#### BUG-04 · `_teamFainted[]` không được khởi tạo từ dữ liệu server
**File:** [BattleNetworkController.cs](Client/Assets/Scripts/BattleUI/BattleNetworkController.cs#L69)  
**Mô tả:** Mảng `_teamFainted[4]` mặc định `false`. Nó chỉ được set `true` trong `AnimEvents()` khi tìm thấy `ForcedSwitches`. Không bao giờ reset. Nếu player join lại mid-battle hoặc bot battle bỏ qua TeamPreview, mảng sẽ sai.  
**Fix:** Sau khi nhận `BattleRunning`, sync `_teamFainted` từ `YourTeamHp`:
```csharp
for (int i = 0; i < 4 && i < dto.YourTeamHp.Count; i++)
    _teamFainted[i] = (dto.YourTeamHp[i] <= 0);
```

---

### 🟠 Logic bugs (ảnh hưởng cân bằng gameplay)

#### BUG-05 · Snow SpDef bonus chỉ kiểm tra Type1, bỏ qua Type2
**File:** [BattleService.cs](Server/Services/BattleService.cs#L967-L969)  
**Mô tả:**
```csharp
double snowSpDef = (!isPhysical && (snow) && SnowImmune.Contains(defender.Type1)) ? ...
```
Bỏ qua `defender.Type2`. Ví dụ Lapras (Water/Ice) sẽ **không nhận bonus** vì Type1 là `"water"`.  
**Fix:**
```csharp
bool isIce = SnowImmune.Contains(defender.Type1)
          || (!string.IsNullOrEmpty(defender.Type2) && SnowImmune.Contains(defender.Type2));
double snowSpDef = (!isPhysical && (snow) && isIce) ? (1.0 / 1.5) : 1.0;
```

---

#### BUG-06 · Self stat change dùng sai slot trong `ApplyStatusMoveEffect`
**File:** [BattleService.cs](Server/Services/BattleService.cs#L1072-L1074)  
**Mô tả:**
```csharp
var statTargets = effect.StartsWith("self")
    ? new List<TargetRef> { new(actingPlayerId, targets.FirstOrDefault()?.Slot ?? 0) }
    : targets;
```
Khi move self-boost (e.g. "self-atk+2"), `targets.FirstOrDefault()?.Slot` là slot của *đối thủ*. Hậu quả: stat boost áp lên Pokemon ở slot sai.  
**Fix:** Truyền `action.SourceIndex` vào `ApplyStatusMoveEffect` và dùng nó:
```csharp
? new List<TargetRef> { new(actingPlayerId, action.SourceIndex) }
```

---

#### BUG-07 · TurnTimeoutService không xử lý timeout trong trạng thái ForcedSwitch
**File:** [TurnTimeoutService.cs](Server/Services/TurnTimeoutService.cs)  
**Mô tả:** Service chỉ auto-resolve `BattleState.Running`. Nếu player ngắt kết nối trong lúc `ForcedSwitch`, battle bị kẹt mãi mãi.  
**Fix:** Thêm logic: sau N giây (VD: 30s) ở trạng thái `ForcedSwitch`, auto-resolve bằng cách cho bot hoặc auto-pick slot đầu tiên hợp lệ.

---

#### BUG-08 · OnDisconnectedAsync không dọn `ConnectedPlayers`
**File:** [BattleHub.cs](Server/Hubs/BattleHub.cs#L433-L439)  
**Mô tả:**
```csharp
public override async Task OnDisconnectedAsync(Exception? exception)
{
    if (ConnectedPlayers.TryGetValue(Context.ConnectionId, out var playerId))
        PlayerConnections.TryRemove(playerId, out _);
    // THIẾU: ConnectedPlayers.TryRemove(Context.ConnectionId, out _);
    await base.OnDisconnectedAsync(exception);
}
```
**Fix:** Thêm `ConnectedPlayers.TryRemove(Context.ConnectionId, out _);` vào `OnDisconnectedAsync`.

---

### 🟡 UI / sync bugs

#### BUG-09 · `TurnDto` thiếu Weather/Terrain fields → field condition desync
**File (client):** [BattleNetworkController.cs](Client/Assets/Scripts/BattleUI/BattleNetworkController.cs#L668-L674)  
**Mô tả:** Sau `TurnResolved`, client tự decrement turns -1. Nhưng nếu lượt đó có move thay đổi weather (set 5 turns mới), client chỉ giảm từ giá trị cũ → hiển thị sai số turns còn lại.  
Server `BattleTurnResult` đã có `Weather`, `WeatherTurnsLeft`, `Terrain`, `TerrainTurnsLeft` nhưng client `TurnDto` không có các field này.  
**Fix:** Thêm 4 field vào `TurnDto`:
```csharp
public string Weather         { get; set; }
public int    WeatherTurnsLeft{ get; set; }
public string Terrain         { get; set; }
public int    TerrainTurnsLeft{ get; set; }
```
Và dùng chúng trong `OnTurnResolved()` thay vì tự decrement.

---

#### BUG-10 · Status heal không cập nhật HUD
**File:** [BattleNetworkController.cs](Client/Assets/Scripts/BattleUI/BattleNetworkController.cs#L697-L699)  
**Mô tả:** `AnimEvents()` chỉ listen `"StatusInflictedEvent"` để gọi `UpdateHudStatus()`. Không xử lý `"StatusHealedEvent"` (khi Pokemon thức giấc/tan băng).  
**Fix:** Thêm case:
```csharp
if (ev.EventType == "StatusHealedEvent")
    UpdateHudStatus(ev.PokemonName, null); // null = xóa status badge
```

---

#### BUG-11 · `StatusDamageEvent` formatter không có event tương ứng từ server
**File:** [BattleNetworkController.cs](Client/Assets/Scripts/BattleUI/BattleNetworkController.cs#L827)  
**Mô tả:** `Fmt()` có case `"StatusDamageEvent"` nhưng server không emit event này. Server dùng `PokemonDamageEvent` cho mọi loại damage (kể cả burn/poison EOT). Dẫn đến: EOT damage từ burn/poison hiển thị thông điệp generic `"{name} -{dmg} HP!"` không có context.  
**Fix (option A):** Thêm `StatusDamageEvent` vào server emit khi burn/poison/toxic gây damage.  
**Fix (option B):** Trong `PokemonDamageEvent`, thêm field `Source` ("burn", "poison", "toxic", "weather", "move") để client format đúng.

---

#### BUG-12 · PP không cập nhật trong một lượt
**File:** [BattleSkillPanel.cs](Client/Assets/Scripts/BattleUI/BattleSkillPanel.cs#L200-L213)  
**Mô tả:** PP chỉ được render khi `SetMove()` được gọi từ `LoadMoves()` → chỉ xảy ra khi nhận `BattleRunning`. Trong cùng lượt: Slot A dùng move, Slot B mở Skill panel → PP của Slot B hiển thị đúng, nhưng sau khi quay lại nhìn Slot A trong lượt sau vẫn thấy PP cũ cho đến lượt tiếp theo.  
Đây là minor issue vì server sẽ sync PP qua `BattleRunning` sau mỗi lượt.

---

#### BUG-13 · Target buttons "---" vẫn clickable khi mục tiêu fainted
**File:** [BattleNetworkController.cs](Client/Assets/Scripts/BattleUI/BattleNetworkController.cs#L479-L483)  
**Mô tả:** Khi target slot bị fainted, button được set nhãn `"---"` nhưng không được disable. Player có thể click vào "---" → gửi targetSlot không hợp lệ.  
**Fix:** Trong `SetTargetLabels()` mới (xem BUG-03), disable interactable cho slot có nhãn "---".

---

#### BUG-14 · `_activeIdxA / _activeIdxB` không sync sau voluntary switch
**File:** [BattleNetworkController.cs](Client/Assets/Scripts/BattleUI/BattleNetworkController.cs#L636-L638)  
**Mô tả:** Sau voluntary switch, client cập nhật `_activeIdxA/B` ngay khi player chọn (`OnPartySlotChosen`). Nhưng server chưa confirmed — nếu switch bị reject (e.g. battle state sai), client state sẽ desync.  
**Fix:** Chỉ cập nhật `_activeIdxA/B` khi nhận `BattleRunning` từ server (trong `OnBattleRunning()`), không cập nhật ngay trong `OnPartySlotChosen`.

---

## 3. Kế hoạch hoàn thiện (missing features)

### 3.1 Thứ tự ưu tiên cao (gameplay core)

#### FEAT-01 · Timer countdown trực quan
**Mô tả:** Mỗi lượt có 30 giây (BattleOptions.TurnTimeoutSeconds). Hiện không có countdown hiển thị.  
**Kế hoạch:**
- Server: `BattleRunningDto` đã có `TurnDeadlineUtc` — dùng field này.
- Client: Trong `OnBattleRunning()`, đọc `dto.TurnDeadlineUtc`, tính giây còn lại.
- UI: Thêm `TimerText` vào `BattleFieldPanel` (hoặc `BattleCommandPanel`), cập nhật mỗi giây bằng Coroutine. Chuyển màu đỏ khi < 10s.

#### FEAT-02 · Kết quả trận đấu hoàn chỉnh
**Mô tả:** `BattlePanelType.Result` tồn tại nhưng `ResultPanel` chỉ là `BasePanel` trống.  
**Kế hoạch:**
- Tạo `BattleResultPanel.cs` với: ảnh win/loss, tên winner, MMR ±change, nút "Return to Menu".
- Server: Sau trận kết thúc, tính và ghi MMR/VP vào DB (BattleOptions.WinnerMmrGain = 25, LoserMmrLoss = 20). Hiện chưa implement phần ghi DB này.
- Client: `OnBattleEnded()` → `BattleEvents.OnBattleResult(won, mmrChange)` → ResultPanel hiển thị.

#### FEAT-03 · Forfeit với xác nhận
**Mô tả:** Nút Forfeit hiện hiển thị dialog "Đầu hàng!" rồi không làm gì tiếp.  
**Kế hoạch:**
- Thêm dialog xác nhận 2 nút (Yes/No) vào `BattleDialogPanel` hoặc separate popup.
- Nếu Yes: gửi tín hiệu về server (thêm method `Forfeit(battleId)` vào BattleHub) → server đặt winner = opponent → broadcast `BattleEnded`.

#### FEAT-04 · Reconnect handling
**Mô tả:** Khi player disconnect rồi reconnect, `JoinBattle` được gọi lại và `SendBattleStateToPlayer` gửi lại state. Nhưng SignalR handlers đã được đăng ký trong `Start()` — nếu reconnect tạo một connection mới, `Start()` chạy lại và sẽ duplicate handlers.  
**Kế hoạch:**
- Kiểm tra: nếu `_battleId` đã có từ `MatchmakingManager.CurrentBattleId`, xóa tất cả handlers cũ trước khi đăng ký lại (code hiện đã có `hub.Remove(ev)` loop — cần đảm bảo nó chạy trước mỗi lần register).
- Xử lý case `BattleState.ForcedSwitch` khi join lại (server đã gửi `ForcedSwitchRequired` qua `SendBattleStateToPlayer` nhưng client cần đảm bảo PartyPanel mở).

---

### 3.2 Thứ tự ưu tiên trung bình (polish)

#### FEAT-05 · Move animations
**Mô tả:** Hiện tại game chỉ hiển thị text. Cần animation cơ bản.  
**Kế hoạch (minimal):**
- Shake effect khi Pokemon bị hit: dùng `DOTween` hoặc coroutine tự viết trên `SpriteRenderer`.
- Flash hiệu ứng màu type khi move được sử dụng.
- Faint animation: fade-out sprite khi HP về 0.
- Có thể dùng Unity Particle System cho hiệu ứng đơn giản.

#### FEAT-06 · Damage number pop-up
**Mô tả:** Số damage "-{X}" bay lên từ Pokemon bị hit.  
**Kế hoạch:**
- Tạo prefab `DamagePopup` với `TextMeshPro`, animate scale + fade + move up.
- `BattleNetworkController.AnimEvents()` instantiate popup khi xử lý `PokemonDamageEvent`.

#### FEAT-07 · Sound effects
**Mô tả:** Không có âm thanh nào hiện tại.  
**Kế hoạch:**
- Thêm `BattleAudioManager.cs` với AudioSource.
- Events cần sound: move used, hit, super-effective, not-very-effective, faint, status inflicted, switch, weather change.
- Có thể dùng free Pokémon-style SFX từ resource pack hoặc tự tạo.

#### FEAT-08 · Stat stage badge trên HUD
**Mô tả:** `EntityHUD` không hiển thị stat stage changes (+1 ATK, -2 DEF, etc.).  
**Kế hoạch:**
- Thêm `statStageText` TMP vào `EntityHUD` hiển thị các stage đang active.
- Trong `AnimEvents()`, khi `StatChangeEvent`, gọi `UpdateHudStatStage(pokemonName, stat, newStage)`.

#### FEAT-09 · Bot AI nâng cấp
**Mô tả:** Bot hiện chỉ chọn move có power cao nhất và target HP thấp nhất — rất đơn giản.  
**Kế hoạch:**
- Xem xét type effectiveness khi chọn move.
- Ưu tiên OHKO (tính ước lượng damage vs target HP).
- Đôi khi switch để tránh super-effective.
- MaxPp bot hiện hardcoded là 15 — fix bằng cách dùng `_moveCache` để lấy `MaxPp` thực sự.

#### FEAT-10 · Team Preview timer
**Mô tả:** `BattleTeamPreviewPanel` có `timerText` nhưng không chạy countdown.  
**Kế hoạch:**
- Server: thêm `TeamPreviewDeadlineUtc` vào `TeamPreviewDto` (hoặc dùng cùng TurnTimeoutSeconds).
- Client: khi nhận `TeamPreviewReady`, start coroutine đếm ngược. Hết giờ → auto-confirm với thứ tự mặc định [0,1,2,3].

---

### 3.3 Thứ tự ưu tiên thấp (nice-to-have)

#### FEAT-11 · Sprite loading fallback
**Mô tả:** `BattleSpriteLoader` load bằng `name.ToLower()` → có thể fail với tên Pokemon có dấu cách, ký tự đặc biệt.  
**Fix:** Dùng `SpeciesId` làm primary lookup key (`Resources/Sprites/pokemon/{id}.png`), `name.ToLower()` làm fallback.

#### FEAT-12 · Move type badge động
**Mô tả:** Sau Terastallization, type của Pokemon thay đổi nhưng type badge trên SkillPanel không update.  
**Fix:** Sau khi nhận `BattleRunning` (hoặc xử lý `TerastallizationEvent`), gọi lại `LoadMoves()` với type mới.

#### FEAT-13 · Hiển thị PP đã dùng trong lượt
**Mô tả:** Khi Slot A dùng move xong và Slot B đang chọn, PP của Slot A không giảm trực tiếp.  
**Kế hoạch:** Sau khi gửi action thành công (`ActionAccepted`), decrement PP trực tiếp trên client (optimistic update). Server sẽ confirm lại ở `BattleRunning` lượt sau.

#### FEAT-14 · Status effect thông điệp cuối lượt chi tiết hơn
**Kế hoạch:** Implement BUG-11 Fix (option B) — thêm `Source` field vào `PokemonDamageEvent`. Client sẽ hiển thị: `"{name} bị thiêu đốt! -{dmg} HP!"` thay vì chung chung.

#### FEAT-15 · Level display trên EntityHUD
**Mô tả:** `EntityHUD.levelText` tìm kiếm child tên `"Level"` — có thể null nếu scene không có.  
**Fix:** Đảm bảo scene hierarchy có object `"Level"` trong mỗi HUD, hoặc fallback tìm bằng `GetComponentInChildren`.

---

## 4. Thứ tự thực hiện gợi ý

### Giai đoạn 1 – Stabilize (sửa critical bugs để battle chạy được)
| # | Task | File | Ưu tiên |
|---|------|------|---------|
| 1 | BUG-01: Xóa duplicate Error handler | BattleNetworkController.cs | 🔴 |
| 2 | BUG-02: Fix Status enum → string | BattleHub.cs + FieldPokemonDto | 🔴 |
| 3 | BUG-03: Fix target selection flow | BattleNetworkController.cs + BattleSkillPanel.cs | 🔴 |
| 4 | BUG-04: Init `_teamFainted` từ server | BattleNetworkController.cs | 🔴 |
| 5 | BUG-08: Fix OnDisconnectedAsync | BattleHub.cs | 🟠 |
| 6 | BUG-09: Thêm Weather/Terrain vào TurnDto | BattleNetworkController.cs | 🟡 |

### Giai đoạn 2 – Fix logic bugs
| # | Task | File | Ưu tiên |
|---|------|------|---------|
| 7 | BUG-05: Snow SpDef fix Type2 check | BattleService.cs | 🟠 |
| 8 | BUG-06: Self stat target fix | BattleService.cs | 🟠 |
| 9 | BUG-07: ForcedSwitch timeout | TurnTimeoutService.cs | 🟠 |
| 10 | BUG-10: StatusHealedEvent → HUD | BattleNetworkController.cs | 🟡 |
| 11 | BUG-11: StatusDamageEvent source | BattleService.cs + EventDto | 🟡 |
| 12 | BUG-13: Disable "---" target buttons | BattleSkillPanel.cs | 🟡 |
| 13 | BUG-14: Sync activeIdx từ BattleRunning | BattleNetworkController.cs | 🟡 |

### Giai đoạn 3 – Core features
| # | Task | Ưu tiên |
|---|------|---------|
| 14 | FEAT-01: Timer countdown | Cao |
| 15 | FEAT-02: ResultPanel hoàn chỉnh + MMR ghi DB | Cao |
| 16 | FEAT-03: Forfeit confirmation | Cao |
| 17 | FEAT-04: Reconnect handling | Cao |
| 18 | FEAT-10: TeamPreview timer | Trung |

### Giai đoạn 4 – Polish & animations
| # | Task | Ưu tiên |
|---|------|---------|
| 19 | FEAT-05: Move animations | Trung |
| 20 | FEAT-06: Damage popup | Trung |
| 21 | FEAT-07: Sound effects | Trung |
| 22 | FEAT-08: Stat stage badge | Thấp |
| 23 | FEAT-09: Bot AI nâng cấp | Thấp |
| 24 | FEAT-11: Sprite fallback | Thấp |
| 25 | FEAT-12: Type badge sau Tera | Thấp |

---

## 5. Checklist xác nhận trước khi ship

- [ ] Hai người chơi thực có thể join và chơi một trận đầy đủ (TeamPreview → Running → Win/Loss)
- [ ] Bot battle khởi động qua `demoAutoMatch = true` trong editor
- [ ] Status badges hiển thị đúng (BRN, PSN, PAR, SLP, FRZ) trên EntityHUD
- [ ] Weather/Terrain hiển thị đúng và đếm ngược đúng số lượt
- [ ] ForcedSwitch Panel mở khi Pokemon fainted và không thể bị đóng
- [ ] Tera button disable sau khi dùng
- [ ] Nếu player disconnect và reconnect, trận tiếp tục bình thường
- [ ] Trận kết thúc: quay về Menu scene sau 5 giây
- [ ] MMR thay đổi sau trận được ghi vào DB (FEAT-02)
- [ ] Không có NullReferenceException trong Unity console trong suốt một trận bình thường

---

## 6. File index

| File | Mô tả |
|------|-------|
| [BattleService.cs](Server/Services/BattleService.cs) | Engine chính (1687 dòng): damage, status, weather, terrain, turn resolution |
| [BattleHub.cs](Server/Hubs/BattleHub.cs) | SignalR hub: nhận action, broadcast kết quả |
| [TurnTimeoutService.cs](Server/Services/TurnTimeoutService.cs) | Background worker: auto-resolve expired turns |
| [BattleNetworkController.cs](Client/Assets/Scripts/BattleUI/BattleNetworkController.cs) | Cầu nối SignalR ↔ UI events |
| [BattleUIManager.cs](Client/Assets/Scripts/BattleUI/BattleUIManager.cs) | Quản lý panel switching |
| [BattleEvents.cs](Client/Assets/Scripts/BattleUI/BattleEvents.cs) | Static event delegates |
| [EntityHUD.cs](Client/Assets/Scripts/BattleUI/EntityHUD.cs) | HUD per-Pokemon (HP, type, status, tera) |
| [BattleSkillPanel.cs](Client/Assets/Scripts/BattleUI/BattleSkillPanel.cs) | Move selection + Tera button |
| [BattleCommandPanel.cs](Client/Assets/Scripts/BattleUI/BattleCommandPanel.cs) | Fight / Pokemon / Info / Forfeit |
| [BattlePartyPanel.cs](Client/Assets/Scripts/BattleUI/BattlePartyPanel.cs) | Chọn Pokemon switch |
| [BattleTeamPreviewPanel.cs](Client/Assets/Scripts/BattleUI/BattleTeamPreviewPanel.cs) | Chọn 4/6 Pokemon khi bắt đầu |
| [BattleDialogPanel.cs](Client/Assets/Scripts/BattleUI/BattleDialogPanel.cs) | Queue-based message display |
| [BattleFieldPanel.cs](Client/Assets/Scripts/BattleUI/BattleFieldPanel.cs) | Weather / Terrain / Turn HUD |
| [BattleTurnResult.cs](Server/Models/Battle/BattleTurnResult.cs) | DTO kết quả lượt + ForPlayer2Perspective() |
| [BattleSession.cs](Server/Models/Battle/BattleSession.cs) | State in-memory của một trận |
| [BattlePokemonSnapshot.cs](Server/Models/Battle/BattlePokemonSnapshot.cs) | Snapshot Pokemon tại thời điểm chiến đấu |
| [GameHubDtos.cs](Server/Models/DTOs/GameHubDtos.cs) | DTOs giao tiếp server ↔ client |
