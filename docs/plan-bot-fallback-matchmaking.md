# Kế hoạch: Bot Fallback Matchmaking (20s timeout)

## Mục tiêu
Khi người chơi nhấn "Tìm trận", hệ thống chờ tối đa **20 giây** để ghép với người thật.
Nếu hết 20s mà không tìm được, **tự động ghép với Bot** — không cần thêm thao tác từ người chơi.

---

## Hiện trạng

| Thành phần | File | Vấn đề |
|---|---|---|
| `FindMatch` server | `MatchmakingHub.cs:180` | Hardcode `Task.Delay(30000)` — 30s, không cấu hình được |
| Thông báo client | `MatchmakingHub.cs:174` | Hardcode chuỗi "sau 30s" |
| Client countdown | `MatchmakingManager.cs` | Không có countdown UI — người chơi không biết còn bao lâu |
| `BattleOptions` | `Options/BattleOptions.cs` | Không có `BotFallbackSeconds` field |

---

## Thay đổi cần làm

### 1. Server — `BattleOptions.cs`

Thêm field `BotFallbackSeconds` để cấu hình thời gian chờ qua `appsettings.json`:

```csharp
// Options/BattleOptions.cs
public int BotFallbackSeconds { get; set; } = 20;  // Thêm field này
```

Trong `appsettings.json` (tuỳ chọn override):
```json
"Battle": {
  "BotFallbackSeconds": 20,
  "TurnTimeoutSeconds": 60
}
```

---

### 2. Server — `MatchmakingHub.cs`

**Inject `BattleOptions`** vào `MatchmakingHub` (hiện tại chưa có):

```csharp
// Constructor — thêm IOptions<BattleOptions>
private readonly BattleOptions _opts;

public MatchmakingHub(MongoDbContext db, BattleService battleService,
                      GameService gameService, IOptions<BattleOptions> opts)
{
    _db = db; _battleService = battleService;
    _gameService = gameService; _opts = opts.Value;
}
```

**Sửa `FindMatch`** — thay 30s hardcode bằng `_opts.BotFallbackSeconds`, đồng thời gửi **countdown tick** mỗi giây về client:

```csharp
public async Task FindMatch()
{
    // ... kiểm tra queue như hiện tại ...

    MatchmakingQueue[myPlayerId] = Context.ConnectionId;

    int countdown = _opts.BotFallbackSeconds;  // 20
    await Clients.Caller.SendAsync("SearchStarted", new { CountdownSeconds = countdown });

    var myCts = new CancellationTokenSource();
    MatchmakingTasks[myPlayerId] = myCts;

    try
    {
        // Gửi tick mỗi giây, hủy nếu tìm được đối thủ
        for (int i = countdown; i > 0; i--)
        {
            await Task.Delay(1000, myCts.Token);
            await Clients.Caller.SendAsync("SearchTick", new { SecondsLeft = i - 1 });
        }

        // Hết giờ → ghép Bot
        if (MatchmakingQueue.TryRemove(myPlayerId, out _))
        {
            MatchmakingTasks.TryRemove(myPlayerId, out _);
            await CreateAndNotifyBattle(myPlayerId, BattleService.BotPlayerId,
                                        Context.ConnectionId, null);
        }
    }
    catch (TaskCanceledException)
    {
        // Đã ghép với người thật hoặc bị hủy → không làm gì
    }
}
```

> **Lý do dùng vòng lặp 1s thay vì `Task.Delay(20000)` một lần:**
> Cho phép server gửi `SearchTick` về client để hiển thị countdown mà không cần client tự đếm.

---

### 3. Client — `MatchmakingManager.cs`

**Thêm event và handler** cho `SearchTick`:

```csharp
// MatchmakingManager.cs

// Event để UI lắng nghe
public static event Action<int> OnCountdownTick;  // seconds left

// Trong Start() — đăng ký thêm handler:
hub.On<SearchStartedDto>("SearchStarted", OnSearchStarted);
hub.On<SearchTickDto>("SearchTick",    dto => OnCountdownTick?.Invoke(dto.SecondsLeft));

private void OnSearchStarted(SearchStartedDto dto)
{
    Debug.Log($"[Matchmaking] Tìm trận... Bot fallback sau {dto.CountdownSeconds}s");
    OnCountdownTick?.Invoke(dto.CountdownSeconds);
}

// DTOs nhỏ
[Serializable] class SearchStartedDto { public int CountdownSeconds; }
[Serializable] class SearchTickDto    { public int SecondsLeft;      }
```

---

### 4. Client — UI Countdown (Menu scene)

Tạo hoặc cập nhật script quản lý UI tìm trận, lắng nghe `OnCountdownTick`:

```csharp
// MatchmakingUI.cs (script mới hoặc tích hợp vào script Menu hiện tại)

[SerializeField] Text countdownLabel;  // hoặc TMP_Text
[SerializeField] Button cancelButton;

void OnEnable()  => MatchmakingManager.OnCountdownTick += UpdateCountdown;
void OnDisable() => MatchmakingManager.OnCountdownTick -= UpdateCountdown;

void UpdateCountdown(int secondsLeft)
{
    if (secondsLeft > 0)
        countdownLabel.text = $"Đang tìm đối thủ... ({secondsLeft}s)";
    else
        countdownLabel.text = "Không tìm thấy đối thủ — ghép Bot...";
}
```

**Nút Cancel** gọi `MatchmakingManager.CancelSearch()` — đã có sẵn `CancelMatchmaking` trên server.

---

## Thứ tự triển khai

```
1. BattleOptions.cs        — thêm BotFallbackSeconds
2. appsettings.json        — thêm giá trị (tuỳ chọn)
3. MatchmakingHub.cs       — inject options, sửa FindMatch, gửi SearchTick
4. MatchmakingManager.cs   — thêm SearchTick handler + OnCountdownTick event + DTOs
5. MatchmakingUI.cs        — tạo/sửa UI countdown
6. Test                    — mở 1 client, chờ 20s xem có vào bot battle không
```

---

## Không cần thay đổi

| Thành phần | Lý do |
|---|---|
| `BattleService.CreateBattle` | Đã xử lý đúng bot battle (skip TeamPreview) |
| `BattleHub` | Không đổi |
| `BattleNetworkController` | Không đổi — flow `CurrentBattleId → JoinBattle` vẫn đúng |
| `TurnTimeoutService` | Không liên quan |
| `FightBot` hub method | Vẫn giữ cho demo/testing |

---

## Rủi ro & lưu ý

| Rủi ro | Giải pháp |
|---|---|
| 2 client cùng gọi `FindMatch` đúng lúc nhau → cả 2 cùng vào bot | `MatchmakingQueue.TryRemove` là atomic — chỉ 1 người remove được opponent, người còn lại tiếp tục chờ |
| Player disconnect trong khi đợi | `OnDisconnectedAsync` đã xoá khỏi queue, `CancellationToken` hủy task |
| Bot battle nhưng người chơi không có Pokemon trong party | Server tạo team rỗng → `ActiveIndex1b = 1` nhưng team chỉ có 1 con → slot B null → HUD ẩn — chấp nhận được |
