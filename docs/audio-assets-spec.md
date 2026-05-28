# Audio Assets Specification — PokemonMMO

> Tổng hợp toàn bộ BGM và SFX cần có, kèm đề xuất track cụ thể cho từng slot.  
> Trạng thái file: ✅ có file thật · ⚠️ placeholder silent · ❌ chưa có

---

## Nguồn tải

| Trang | Dùng cho | Ghi chú |
|---|---|---|
| [khinsider.com](https://downloads.khinsider.com) | BGM Pokemon | Tìm tên game → download MP3/FLAC |
| [sounds.theresource.com](https://sounds.theresource.com) | SFX Pokemon | Rip chính xác từ ROM |
| [freesound.org](https://freesound.org) | SFX thay thế | Lọc license CC0 để dùng tự do |
| [pixabay.com/sound-effects](https://pixabay.com/sound-effects) | SFX thay thế | Không cần attribution |
| [opengameart.org](https://opengameart.org) | BGM + SFX | Nhiều pack RPG/battle license rõ |

---

## BGM — Nhạc nền (9 file)

> Tất cả file đã có trong `Assets/Audio/BGM/`. Kiểm tra nội dung; thay nếu chưa đúng cảm xúc.

---

### `bgm_title.mp3` — Start menu (Login/Register) ✅

**Cảm xúc:** Huyền bí, chào đón. Cảm giác bắt đầu hành trình mới.

| Ưu tiên | Track | Game | Tìm trên |
|---|---|---|---|
| ⭐ Tốt nhất | *Pallet Town (Nostalgic)* — "Pokémon HeartGold/SoulSilver" — track "Pallet Town" remix | HGSS | khinsider → "pokemon heartgold" |
| 2 | *Title Screen* | Pokémon Black/White | khinsider → "pokemon black white" |
| 3 | *Main Title* | Pokémon Scarlet/Violet | khinsider → "pokemon scarlet violet" |
| Royalty-free | "Ethereal Visions" — Eric Matyas | soundimage.org | Free |

**Từ khóa Freesound:** `"peaceful piano ambient game menu"`

---

### `bgm_lobby.mp3` — Menu scene (Lobby chính) ✅

**Cảm xúc:** Thân thiện, sôi động vừa phải. Cảm giác Pokemon Center / đô thị náo nhiệt.

| Ưu tiên | Track | Game | Tìm trên |
|---|---|---|---|
| ⭐ Tốt nhất | *Pokémon Center* | Pokemon FireRed/LeafGreen | khinsider → "pokemon firered leafgreen" |
| 2 | *Pokémon Center* | Pokemon Black/White | khinsider → "pokemon black white" |
| 3 | *Motostoke (Day)* | Pokemon Sword/Shield | khinsider → "pokemon sword shield" |
| Royalty-free | "Pixel Adventure" pack — Juhani Junkala | opengameart.org | CC-BY 3.0 |

**Từ khóa Freesound:** `"upbeat chiptune town loop"`

---

### `bgm_preview.mp3` — Team Preview (trước khi battle) ✅

**Cảm xúc:** Hồi hộp, cân não. 15–30 giây, có thể loop ngắn.

| Ưu tiên | Track | Game | Tìm trên |
|---|---|---|---|
| ⭐ Tốt nhất | *Select Your Pokémon* (Team Preview BGM) | Pokemon Sword/Shield | khinsider → "pokemon sword shield" |
| 2 | *VS Trainer Intro Sting* — phần build-up trước battle | Pokemon X/Y | khinsider → "pokemon x y" |
| Royalty-free | "Tense Situation" — Eric Matyas | soundimage.org | Free |

**Từ khóa Freesound:** `"tense countdown game loop short"`

---

### `bgm_battle.mp3` — Battle chính (PvP) ✅

**Cảm xúc:** Mạnh mẽ, khẩn trương. Đây là track nghe nhiều nhất — phải đủ hứng.

| Ưu tiên | Track | Game | Tìm trên |
|---|---|---|---|
| ⭐ Tốt nhất | *Battle! (Ranked Battle)* | Pokemon Sword/Shield | khinsider → "pokemon sword shield" |
| 2 | *Battle! (Trainer)* | Pokemon Black/White 2 | khinsider → "pokemon black white 2" |
| 3 | *Battle! (Trainer)* | Pokemon Scarlet/Violet | khinsider → "pokemon scarlet violet" |
| 4 | *VS Trainer* | Pokemon X/Y | khinsider → "pokemon x y" |
| Royalty-free | "Boss Battle 3" — Eric Matyas | soundimage.org | Free |

**Từ khóa Freesound:** `"epic battle music loop game rpg"`

---

### `bgm_win.mp3` — Chiến thắng (loop: false, ~20–30s) ✅

**Cảm xúc:** Khải hoàn, vui vẻ. Chỉ phát 1 lần, không loop.

| Ưu tiên | Track | Game | Tìm trên |
|---|---|---|---|
| ⭐ Tốt nhất | *Victory! (Trainer)* jingle | Pokemon Black/White | khinsider → "pokemon black white" |
| 2 | *Battle! Victory* fanfare | Pokemon FireRed/LeafGreen | khinsider → "pokemon firered leafgreen" |
| 3 | *Victory (Trainer)* | Pokemon Scarlet/Violet | khinsider → "pokemon scarlet violet" |
| Royalty-free | "Victory Fanfare" — Juhani Junkala | opengameart.org | CC0 |

**Từ khóa Freesound:** `"short victory fanfare jingle game"`

---

### `bgm_lose.mp3` — Thất bại (loop: false, ~15–20s) ✅

**Cảm xúc:** Buồn, trầm lắng. Không quá nặng nề.

| Ưu tiên | Track | Game | Tìm trên |
|---|---|---|---|
| ⭐ Tốt nhất | *Blacked Out* / *White Out* screen music | Pokemon Black/White | khinsider → "pokemon black white" |
| 2 | *Defeat* jingle | Pokemon HeartGold/SoulSilver | khinsider → "pokemon heartgold soulsilver" |
| Royalty-free | "Game Over Sad" — Freesound | freesound.org | tìm `"sad game over short"` |

---

### `bgm_recruit.mp3` — RecuitScene (Gacha 10-roll) ✅

**Cảm xúc:** Kỳ bí, hồi hộp, hơi "ma thuật". Phải tạo cảm giác gacha.

| Ưu tiên | Track | Game | Tìm trên |
|---|---|---|---|
| ⭐ Tốt nhất | *Pokémon Lottery Corner* | Pokemon HeartGold/SoulSilver | khinsider → "pokemon heartgold soulsilver" |
| 2 | *Trick House* | Pokemon ORAS | khinsider → "pokemon omega ruby alpha sapphire" |
| 3 | *Game Corner* | Pokemon FireRed/LeafGreen | khinsider → "pokemon firered leafgreen" |
| Royalty-free | "Magic Shop" — Eric Matyas | soundimage.org | Free |

**Từ khóa Freesound:** `"mysterious magical loop ambient harp"`

---

### `bgm_box.mp3` — BoxScene (PC Box quản lý Pokemon) ✅

**Cảm xúc:** Thanh thản, yên tĩnh, nhẹ nhàng. Người dùng thao tác chậm.

| Ưu tiên | Track | Game | Tìm trên |
|---|---|---|---|
| ⭐ Tốt nhất | *PC Pokémon Storage System* | Pokemon HeartGold/SoulSilver | khinsider → "pokemon heartgold soulsilver" |
| 2 | *Pokémon Storage System* | Pokemon Diamond/Pearl | khinsider → "pokemon diamond pearl" |
| 3 | *Pokémon Box* | Pokemon Black/White | khinsider → "pokemon black white" |
| Royalty-free | "Peaceful Piano" — Bensound | bensound.com | Free với attribution |

**Từ khóa Freesound:** `"calm piano ambient loop soft"`

---

### `bgm_pokedex.mp3` — PokedexScene (tra cứu Pokemon) ✅

**Cảm xúc:** Tò mò, khoa học, nhẹ nhàng electronic.

| Ưu tiên | Track | Game | Tìm trên |
|---|---|---|---|
| ⭐ Tốt nhất | *Research Station* | Pokemon Legends: Arceus | khinsider → "pokemon legends arceus" |
| 2 | *Pokédex 3D* theme | Pokemon 3DS app | khinsider |
| 3 | *Pokémon Lab* | Pokemon Black/White 2 | khinsider → "pokemon black white 2" |
| Royalty-free | "Technology" — Eric Matyas | soundimage.org | Free |

**Từ khóa Freesound:** `"electronic ambient research lab loop"`

---

## SFX — Hiệu ứng âm thanh (24 file)

> Tất cả đang là placeholder silent. Tải về, đặt đúng tên, copy vào `Assets/Audio/SFX/`.

### Nguồn SFX Pokemon gốc

- **sounds.theresource.com** → chọn "Pokemon" → chọn game (Gen 5–9) → download từng SFX
- Hoặc tải **Pokemon SFX pack** sẵn trên [archive.org](https://archive.org/search?query=pokemon+sound+effects)

---

### 🌐 UI Toàn cục

| File | Đề xuất cụ thể | Tìm ở đâu |
|---|---|---|
| `sfx_ui_click.wav` | "Menu Select" sound — Pokemon DS games | theresource.com → Pokemon DS → "menu select" |
| | Thay thế: freesound `"ui click button short"` ID [319590](https://freesound.org/people/Leszek_Szary/sounds/319590/) | freesound.org (CC0) |
| `sfx_ui_confirm.wav` | "Menu Confirm/OK" sound — Pokemon DS games | theresource.com |
| | Thay thế: freesound `"ui confirm ding short"` | freesound.org |

---

### 🏠 Menu scene

| File | Đề xuất cụ thể | Tìm ở đâu |
|---|---|---|
| `sfx_match_found.wav` | "Level Up" jingle ngắn từ Pokemon — hoặc "Found!" notification | theresource.com hoặc freesound `"match found notification game"` |
| `sfx_tick.wav` | "Clock tick" — 1 tiếng tick đơn giản | freesound.org: `"clock tick short single"` |

---

### ⚔️ Battle SFX

#### Chiến đấu

| File | Đề xuất cụ thể | Tìm ở đâu |
|---|---|---|
| `sfx_battle_hit.wav` | Pokemon: "Normal Hit" sound Gen 5–9 | theresource.com → Pokemon B/W → battle sounds |
| | Thay thế freesound: `"punch hit thud game"` | freesound.org |
| `sfx_battle_crit.wav` | Pokemon: "Critical Hit" sound — tone sắc hơn hit bình thường | theresource.com → Pokemon B/W |
| | Thay thế: lấy sfx_battle_hit, pitch lên +3 semitone bằng Audacity | Audacity (free) |
| `sfx_battle_faint.wav` | Pokemon: "Faint" / "Pokemon Fainted" sound | theresource.com |
| | Thay thế freesound: `"defeat fall game character faint"` | freesound.org |
| `sfx_battle_hp_low.wav` | Pokemon: "Low HP" beep — tông cao lặp nhanh | theresource.com |
| | Thay thế freesound: `"warning beep high game"` | freesound.org |

#### Status conditions

| File | Đề xuất cụ thể | Tìm ở đâu |
|---|---|---|
| `sfx_status_burn.wav` | Pokemon: "Burn" status SFX | theresource.com → battle status |
| | Thay thế freesound: `"fire sizzle short game"` | freesound.org |
| `sfx_status_para.wav` | Pokemon: "Paralysis" SFX — electric zap | theresource.com |
| | Thay thế freesound: `"electric zap short"` | freesound.org |
| `sfx_status_sleep.wav` | Pokemon: "Sleep" SFX — gentle chime | theresource.com |
| | Thay thế freesound: `"sleep gentle chime magic"` | freesound.org |

#### Chỉ số (stat change)

| File | Đề xuất cụ thể | Tìm ở đâu |
|---|---|---|
| `sfx_stat_up.wav` | Pokemon: "Stat Up" — ascending tone | theresource.com → battle |
| | Thay thế freesound: `"power up ascending game rpg"` | freesound.org |
| `sfx_stat_down.wav` | Pokemon: "Stat Down" — descending tone | theresource.com → battle |
| | Thay thế freesound: `"power down descending game"` | freesound.org |

#### Thời tiết

| File | Đề xuất cụ thể | Tìm ở đâu |
|---|---|---|
| `sfx_weather_rain.wav` | Freesound: `"rain start short game"` | freesound.org |
| `sfx_weather_sun.wav` | Freesound: `"sun shimmer bright game"` | freesound.org |
| `sfx_weather_sand.wav` | Freesound: `"sand wind swoosh short"` | freesound.org |
| `sfx_weather_snow.wav` | Freesound: `"snow wind cold whoosh game"` | freesound.org |

#### Đặc biệt

| File | Đề xuất cụ thể | Tìm ở đâu |
|---|---|---|
| `sfx_tera.wav` | Pokemon: "Terastallize" SFX từ Scarlet/Violet | theresource.com → Pokemon SV |
| | Thay thế freesound: `"crystal sparkle transform epic game"` | freesound.org |

---

### 🏆 Kết quả trận

| File | Đề xuất cụ thể | Tìm ở đâu |
|---|---|---|
| `sfx_victory.wav` | Pokemon: "Victory Jingle" — trainer win fanfare ~1.5s | theresource.com |
| | Thay thế: opengameart.org `"victory fanfare short"` | opengameart.org (CC0) |
| `sfx_defeat.wav` | Pokemon: "Blacked Out" sting / defeat jingle ~1.5s | theresource.com |
| | Thay thế freesound: `"sad defeat short game jingle"` | freesound.org |
| `sfx_coin_tick.wav` | Freesound: `"coin clink metal single"` — ID [331640](https://freesound.org/people/nsstudios/sounds/331640/) | freesound.org (CC0) |

---

### 🎰 Gacha (RecuitScene)

| File | Đề xuất cụ thể | Tìm ở đâu |
|---|---|---|
| `sfx_recruit_roll.wav` | Pokemon: "Box shuffle/spin" sound, hoặc "Pokéball open" | theresource.com |
| | Thay thế freesound: `"magic whoosh reveal gacha"` | freesound.org |
| `sfx_recruit_pop.wav` | Freesound: `"bubble pop short game"` — dùng 1 tiếng pop nhỏ | freesound.org (CC0) |
| `sfx_recruit_confirm.wav` | Pokemon: "Caught Pokemon" jingle, hoặc "Obtained item" | theresource.com |
| | Thay thế freesound: `"item get short jingle game"` | freesound.org |

---

## Hướng dẫn tải nhanh từ The Sounds Resource

1. Vào [sounds.theresource.com](https://sounds.theresource.com)
2. Search: **"Pokemon Black"** → Battle → download file ZIP
3. Giải nén, đổi tên file theo đúng tên slot (ví dụ `sfx_battle_hit.wav`)
4. Copy vào `Assets/Audio/SFX/`
5. Unity tự import, không cần chạy lại tool

---

## Hướng dẫn chỉnh sửa nhanh bằng Audacity (free)

Cần tạo biến thể từ 1 file:
- `sfx_battle_crit` từ `sfx_battle_hit`: **Effect → Pitch → +3 semitones** + **Amplify +3dB**
- `sfx_stat_down` từ `sfx_stat_up`: **Effect → Reverse** rồi chỉnh pitch xuống
- Cắt ngắn bất kỳ SFX: **Edit → Select → Export Selection as WAV**

---

## Checklist thay placeholder

Khi có file thật → copy vào `Assets/Audio/SFX/` cùng tên → Unity tự replace.

**UI**
- [ ] sfx_ui_click.wav
- [ ] sfx_ui_confirm.wav

**Menu**
- [ ] sfx_match_found.wav
- [ ] sfx_tick.wav

**Battle — chiến đấu**
- [ ] sfx_battle_hit.wav
- [ ] sfx_battle_crit.wav
- [ ] sfx_battle_faint.wav
- [ ] sfx_battle_hp_low.wav

**Battle — status**
- [ ] sfx_status_burn.wav
- [ ] sfx_status_para.wav
- [ ] sfx_status_sleep.wav

**Battle — stat**
- [ ] sfx_stat_up.wav
- [ ] sfx_stat_down.wav

**Battle — thời tiết**
- [ ] sfx_weather_rain.wav
- [ ] sfx_weather_sun.wav
- [ ] sfx_weather_sand.wav
- [ ] sfx_weather_snow.wav

**Battle — đặc biệt**
- [ ] sfx_tera.wav

**Kết quả**
- [ ] sfx_victory.wav
- [ ] sfx_defeat.wav
- [ ] sfx_coin_tick.wav

**Gacha**
- [ ] sfx_recruit_roll.wav
- [ ] sfx_recruit_pop.wav
- [ ] sfx_recruit_confirm.wav
