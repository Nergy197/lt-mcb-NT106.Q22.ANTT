# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commit policy
- Never add `Co-Authored-By` or any Claude/Anthropic attribution to commit messages in this repo.
- Never create commits unless explicitly asked by the user.

---

## Running the project

### Server (ASP.NET Core 9 + MongoDB)

```bash
# Start server + MongoDB via Docker (recommended, no SDK needed)
cd infra
cp .env.example .env          # first time only
docker compose up -d --build
docker compose logs -f server  # watch startup

# Server runs at http://localhost:2567
# Swagger UI: http://localhost:2567/swagger
# MongoDB:    mongodb://localhost:27017
```

Local dotnet (requires .NET 9 SDK + running MongoDB):
```bash
cd Server
dotnet run
```

There are no automated tests. Manual testing uses Swagger or the Unity client directly.

### Client (Unity 6)

Open `Client/` as a Unity 6 project in the Unity Editor. There is no CLI build step — all scene work is done through the Editor.

---

## Architecture overview

### Client–Server split

```
Client/ (Unity 6, C#)         Server/ (ASP.NET Core 9, C#)
  UI + scene logic     ←→       REST API  (auth, box, recruit, rank)
  SignalR hubs client  ←→       SignalR hubs (matchmaking, battle, chat)
                                MongoDB (pokemon_mmo database)
```

### SignalR hubs (server-side, `Server/Hubs/`)

| Hub | Path | Responsibility |
|---|---|---|
| `MatchmakingHub` | `/hubs/matchmaking` | Lobby, queue, bot fallback after 20 s |
| `BattleHub` | `/hubs/battle` (+ legacy `/game`) | VGC double-battle orchestration, authoritative turn resolution |
| `ChatHub` | `/hubs/chat` | World chat + friend-only DMs |

### Battle state machine (server + client mirror)

```
TeamPreview → BattleRunning ← → ForcedSwitch → BattleEnded
```

Server fires: `TeamPreviewReady` → `BattleRunning` → `TurnResolved` → (if faint) `ForcedSwitchRequired` → back to `BattleRunning` → `BattleEnded`.

Client panels mirror this exactly: `BattlePanelType` enum maps each state to a `BasePanel` subclass managed by `BattleUIManager`.

### Key server services (`Server/Services/`)

| Service | Lifetime | Key role |
|---|---|---|
| `BattleService` | **Singleton** | All in-memory `BattleSession`s; full Gen 9 damage formula, Tera, weather, terrain |
| `AuthService` | Scoped | JWT issue/revoke, BCrypt, OTP password reset |
| `CurrencyService` | Scoped | VP transactions (atomic `FindOneAndUpdateAsync`) |
| `RankService` | Scoped | Elo-style ranked points, top-100 leaderboard |
| `RecruitService` | Scoped | Gacha pulls (10-roll) |
| `BoxService` | Scoped | Pokemon storage (30 slots × 32 boxes) |
| `GameService` | Scoped | Party heal, trade |
| `PokedexService` | Scoped | Seed Pokedex + Moves from static JSON on startup |

### MongoDB collections (`Server/Data/MongoDbContext.cs`)

Database name: `pokemon_mmo`

`accounts`, `players`, `pokemoninstances`, `pokedex`, `moves`, `revoked_tokens` (TTL 1 day), `battle_logs`, `chat_messages`, `friendships`, `vp_transactions`

### Client scene list

| Scene file | Entry point script | What it does |
|---|---|---|
| `Start menu.unity` | `AuthUIManager` | Login / Register / Forgot-password (REST only) |
| `Menu scene.unity` | `MenuSceneManager` | Main lobby hub; navigates to all other scenes |
| `Battle scene.unity` | `BattleNetworkController` | Real PvP battle via SignalR |
| `0_BattleScene.unity` | `BattleNetworkController` | Offline/demo mode (`demoAutoMatch = true`) |
| `RecuitScene.unity` | `RecruitManager` | 10-roll gacha, confirm recruit |
| `BoxScene.unity` | `PokemonBoxPanel` | PC box management (6×5 grid, 32 boxes) |
| `PokedexScene.unity` | `PokedexSceneController` | National Pokédex browser |

### Client architectural patterns

**Event bus** — `BattleEvents` (static `Action` fields) decouples panels from network. Every panel subscribes in `OnEnable` / unsubscribes in `OnDisable`.

**Main-thread queue** — All SignalR callbacks arrive on a thread pool thread. The pattern used everywhere is:
```csharp
private readonly Queue<Action> _mainThreadQueue = new();
void Update() { lock (_mainThreadQueue) while (_mainThreadQueue.Count > 0) _mainThreadQueue.Dequeue()?.Invoke(); }
void Enqueue(Action a) { lock (_mainThreadQueue) _mainThreadQueue.Enqueue(a); }
```

**SignalR singleton** — `SignalRClient` (DontDestroyOnLoad) holds both `Battle` and `Matchmaking` `HubConnection` references. `MatchmakingManager` (also DontDestroyOnLoad) stores `CurrentBattleId` across scene loads.

**Panel switching** — `BattleUIManager.SwitchPanel(BattlePanelType)` activates exactly one `BasePanel` at a time. `BattlePanelType.Dialog` is the exception — it is always visible.

### Matchmaking events

`MatchmakingManager` (DontDestroyOnLoad singleton) exposes three static events for UI binding:

| Event | When fired |
|---|---|
| `OnSearchStarted` | Server confirms `SearchStarted` (bot fallback countdown begins) |
| `OnCountdownTick(int secondsLeft)` | Every server tick until bot fallback |
| `OnSearchCancelled` | After `CancelSearching()` completes (success or error) |

`MatchmakingUI` shows an elapsed search timer (client-side `Update` counter) plus a bot-fallback countdown from `OnCountdownTick`. The **cancel button** calls `MatchmakingManager.Instance.CancelSearching()` which invokes `"CancelMatchmaking"` on the hub then fires `OnSearchCancelled` in a `finally` block — guaranteed to fire even if the hub call fails. `ModeMenu` subscribes to `OnSearchCancelled` to re-enable mode buttons and unblock keyboard input.

### Audio system

`AudioSettingsManager` persists master volume via `PlayerPrefs` key `"master_volume"` and routes through `MainAudioMixer.mixer`. BGM/SFX groups do not yet exist; the mixer currently has only a `MasterVolume` parameter.

### JWT flow

REST calls use `Authorization: Bearer <token>` header. SignalR connections pass the token via query string `?access_token=<token>` (configured in `Program.cs`). Claims carried: `sub` = accountId, `player_id`, `unique_name`, `email`.

### Battle damage formula

Gen 5+: `floor((2*L/5+2) * Power * Atk/Def / 50 + 2)` then apply modifiers: STAB (1.5×), type effectiveness, weather, terrain, spread (0.75×), burn (0.5× physical), critical (1.5×), random variance (0.85–1.0). All logic lives in `BattleService.ExecuteMove()`.
