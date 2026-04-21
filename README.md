<p align="center">
  <img src="Server/Data/logo.png" alt="Pokémon Champions MMO Logo" height="200">
</p>
<p align="center">
  <img src="https://img.shields.io/badge/Unity-6-000000?style=flat&logo=unity&logoColor=white" alt="Unity">
  <img src="https://img.shields.io/badge/C%23-239120?style=flat&logo=csharp&logoColor=white" alt="C#">
  <img src="https://img.shields.io/badge/ASP.NET%20Core%209.0-512BD4?style=flat&logo=dotnet&logoColor=white" alt="ASP.NET Core">
  <img src="https://img.shields.io/badge/SignalR-0058A1?style=flat" alt="SignalR">
  <img src="https://img.shields.io/badge/MongoDB%207.0-4EA94B?style=flat&logo=mongodb&logoColor=white" alt="MongoDB">
  <img src="https://img.shields.io/badge/Docker-2496ED?style=flat&logo=docker&logoColor=white" alt="Docker">
</p>

# Pokémon Champions MMO 

**Pokémon Champions MMO** là một đồ án hệ thống Game trực tuyến nhiều người chơi (MMO) lấy cảm hứng từ lối chơi PvP cạnh tranh của tựa game Pokémon, kết hợp với phong cách đồ họa Pixel hoài cổ. Mục tiêu của dự án là loại bỏ sự cồng kềnh của thế giới mở để tập trung hoàn toàn vào trải nghiệm đấu trường thể thao điện tử (Esports) tốc độ cao.

Dự án sử dụng kiến trúc **Client-Server**, trong đó Client được viết bằng **Unity (C#)** tập trung vào UI/UX và animation. Server được thiết kế bằng **C# ASP.NET Core 9.0** tích hợp **SignalR** cho kết nối phòng đấu theo thời gian thực (Real-time Matchmaking & Turn-based Combat) cùng với cơ sở dữ liệu **MongoDB**.

---

## 🌟 Tính năng hiện tại (Implemented Features)

Hệ thống Core đã được xây dựng hoàn thiện các tính năng cốt lõi để đảm bảo một game Online hoạt động ổn định và bảo mật:

### 🛡️ Hệ thống Tài khoản & Bảo mật (Auth)
- **Đăng ký / Đăng nhập** với mã hóa mật khẩu `BCrypt`.
- Cấp phát bảo mật bằng cơ chế **JWT Tokens**.
- Hỗ trợ **Thu hồi Token (Logout)** thông qua Blacklist.
- Quên & Khôi phục mật khẩu (Gửi mã OTP 6 số tự động qua **Email SMTP**).

### ⚔️ Đấu trường & Matchmaking (Gameplay Lõi)
- **Matchmaking Hub:** Tìm trận Server-Side nhanh chóng. Tự động thêm **AI Bot** vào trận nếu người chơi chờ quá 30 giây.
- **Battle Hub (Turn-based):** Cơ chế chiến đấu Authoritative Server chặn mọi hình thức Hack/Cheat.
- Tính toán công thức sát thương nghiêm ngặt (tính tỷ lệ Random, hệ phái, STAB, Crit).
- Hệ thống Timeout cho mỗi lượt đánh (30s) và xử lý Forfeit (Đầu hàng) khi người chơi ngắt kết nối.
- Cơ chế **Elo (MMR)** & thưởng **Victory Points (VP)** khi thắng trận.

### 👥 Tương tác Xã hội (Social)
- **Hệ thống Bạn bè:** Gửi lời mời, chấp nhận, từ chối và quản lý danh sách bạn bè dựa trên Account ID.
- **Global & Private Chat:** Kênh chat thế giới (Global) và tin nhắn mật (Direct Messages - DM) theo thời gian thực sử dụng kênh Hub riêng rẽ.

### 💾 Dữ liệu & Cơ sở Hạ tầng
- Tự động nạp Seed Data cho Pokedex và Moves (tích hợp dữ liệu chuẩn hóa PokeAPI).
- Đóng gói Server 100% bằng **Dockerfile (Multi-stage build)** đảm bảo chạy được trên mọi nền tảng (Mac, Linux, Windows, VPS) không cần cài SDK.

---

## 🚀 Lộ trình & Các tính năng Tương lai (Roadmap)

Dự án vẫn đang tiếp tục hoàn thiện. Dưới đây là các tính năng dự định tích hợp trong tương lai:

- [ ] **Hoàn thiện Client Unity:** Animations chất lượng cao cho các chiêu thức (VFX/SFX).
- [ ] **Cơ chế Đột phá sức mạnh (Mega Evolution / Omni Ring):** Thêm tính năng tiến hóa trong trận kích hoạt ở điều kiện ngặt nghèo.
- [ ] **Hệ thống Giao dịch (P2P Trading):** Người chơi có thể trao đổi Pokémon với nhau qua sàn giao dịch (Trade Center) để tối ưu đội hình.
- [ ] **Guild & Clan System:** Cho phép lập bang hội, tổ chức giải đấu Clan vs Clan mùa giải (Seasons).
- [ ] **Bộ AI Chiến thuật Nâng cao:** Nâng cấp Bot nội bộ từ mức "đánh ngẫu nhiên" lên mức "đánh theo Meta/Chiến thuật".
- [ ] **Bảng Xếp hạng Thế giới (Leaderboards):** Tích hợp Redis Cache để hệ thống hiển thị top 100 người chơi Global Real-time.

---

## 📂 Tổ chức mã nguồn (Project Structure)

```text
lt-mcb-NT106.Q22.ANTT/
├── Client/                 # Source Code Unity 6 (UI/UX, Battle Animations, Client Logic)
│
├── Server/                 # C# ASP.NET Core 9.0 Game Server 
│   ├── Controllers/        # API Endpoints (Auth, Friends,...)
│   ├── Data/               # Lớp truy xuất MongoDB (PokeAPI Collections) & JSON Data
│   ├── Hubs/               # Giao thức WebSocket (MatchmakingHub, BattleHub, ChatHub)
│   ├── Models/             # Domain Entities & DTOs
│   ├── Services/           # Logic lõi, Matchmaking queues, Damage Calculators...
│   ├── Dockerfile          # Kịch bản đóng gói Container cho Server
│   └── Program.cs          # Pipeline và Dependency Injection
│
├── infra/                  # Các file cấu hình Hệ thống và Triển khai
│   ├── docker-compose.yml  # Dựng nhanh DB & Server 
│   └── .env.example        # Mẫu biến môi trường
│
└── README.md               # Tài liệu tổng quan
```

---

## 🛠 Hướng dẫn khởi chạy (Run Locally)

Ứng dụng được chứa trong Container (Docker), do đó máy của bạn **không cần cài đặt gì ngoài Docker**. Server và Database (MongoDB) sẽ được dựng sẵn trong 1 cú click.

### 1. Chuẩn bị
1. Cài đặt **Docker Desktop** (Đảm bảo Engine đang chạy màu xanh lá).
2. Clone repository này về máy.

### 2. Khởi động nhanh

1. Đi tới thư mục chứa hạ tầng:
   ```bash
   cd infra
   ```
2. Copy biến môi trường từ file mẫu:
   ```bash
   cp .env.example .env
   ```
   *(Sửa thông số SMTP Email trong `Server/appsettings.json` nếu muốn test logic Quên mật khẩu).*

3. Build và khởi chạy tất cả lên nền tảng bằng Docker Compose:
   ```bash
   docker compose up -d --build
   ```

4. Theo dõi log xem khởi động thành công chưa:
   ```bash
   docker compose logs -f server
   ```

✅ Mặc định, Server sẽ chạy tại cổng `2567`.

---

## 📡 Các Endpoints Nội bộ quan trọng

- **REST API (Auth):** `http://localhost:2567/api/auth`
- **REST API (Friends):** `http://localhost:2567/api/friends`
- **SignalR (Matchmaking):** `ws://localhost:2567/hubs/matchmaking`
- **SignalR (Batte):** `ws://localhost:2567/hubs/battle` (hoặc `/game` backward compatibility)
- **SignalR (Chat):** `ws://localhost:2567/hubs/chat`
- **Swagger Docs API:** `http://localhost:2567/swagger`
- **MongoDB Direct:** `mongodb://localhost:27017`

---

> **Lưu ý:** Mã nguồn này là thành quả học tập thuộc Đề án Lập trình Mạng Căn Bản.
