<p align="center">
  <img src="./logo.png" alt="Pokémon Champions MMO Logo" height="200">
</p>
<p align="center">
  <img src="https://img.shields.io/badge/Unity-100000?style=flat&logo=unity&logoColor=white" alt="Unity">
  <img src="https://img.shields.io/badge/C%23-239120?style=flat&logo=csharp&logoColor=white" alt="C#">
  <img src="https://img.shields.io/badge/ASP.NET%20Core-512BD4?style=flat&logo=dotnet&logoColor=white" alt="ASP.NET Core">
  <img src="https://img.shields.io/badge/SignalR-0058A1?style=flat" alt="SignalR">
  <img src="https://img.shields.io/badge/MongoDB-4EA94B?style=flat&logo=mongodb&logoColor=white" alt="MongoDB">
  <img src="https://img.shields.io/badge/Docker-2496ED?style=flat&logo=docker&logoColor=white" alt="Docker">
</p>

# Pokémon Champions MMO - Đồ án NT106.Q22.ANTT (Lập trình Mạng Căn bản)

Đồ án xây dựng một hệ thống Game trực tuyến nhiều người chơi (MMO) lấy cảm hứng từ lối chơi PvP cạnh tranh của tựa game **Pokémon Champions**, kết hợp với phong cách **đồ họa Pixel** hoài cổ. 

Dự án sử dụng kiến trúc Client-Server, trong đó Client được viết bằng **Unity (C#)** tập trung vào UI/UX và logic tay cầm (Gamepad). Server được thiết kế bằng **C# ASP.NET Core** tích hợp **SignalR** cho kết nối phòng đấu theo thời gian thực (Real-time Matchmaking & Turn-based Combat). 

Dữ liệu gốc về Pokémon (movesets, base stats, pixel sprites) được đồng bộ tự động từ **[PokeAPI](https://pokeapi.co/)**, trong khi dữ liệu hệ thống (tài khoản, đội hình, điểm VP) được lưu trữ trên **MongoDB**. Tất cả hạ tầng được vận hành qua **Docker**.

---

## 🌟 Tính năng nổi bật & Gameplay

Đồ án loại bỏ sự cồng kềnh của thế giới mở để tập trung hoàn toàn vào trải nghiệm đấu trường thể thao điện tử (Esports):

- ⚔️ **Real-time Matchmaking:** Tích hợp SignalR xử lý hệ thống tìm trận ngẫu nhiên và đồng bộ trạng thái lượt đánh khắt khe theo thời gian thực (Turn-based Combat). Tính toán sát thương hoàn toàn ở phía Server (Authoritative Server) để chống gian lận.
- 🏆 **Chế độ chơi đa dạng:** Cung cấp đấu hạng (**Ranked Battles**), đấu giao hữu (**Casual Battles**) và tạo phòng kín với bạn bè (**Private Battles**).
- 🧬 **Recruit Hàng ngày & JSON Import:** Người chơi xây dựng tổ đội thông qua hệ thống rút thăm ngẫu nhiên (dùng thử hạn giờ) hoặc nhập file JSON đội hình (mô phỏng Pokémon HOME).
- 💰 **Nền kinh tế Victory Points (VP):** Điểm thưởng kiếm được qua các trận PVP dùng để mua đứt các Pokémon dùng thử, thay đổi kỹ năng (Movesets), mua thuốc hồi phục phục vụ chiến thuật đấu giải.
- 💥 **Tiến hóa Mega (Omni Ring):** Hỗ trợ tính năng đột phá sức mạnh giới hạn (Mega Evolution) phân định thắng thua ở những phút cuối.

---

## 📂 Tổ chức mã nguồn (Project Structure)

Dự án được phân chia thành các thư mục chính, tách biệt rõ ràng giữa logic máy chủ, máy khách và hạ tầng (infrastructure):

```text
lt-mcb-NT106.Q22.ANTT/
├── Client/                 # (Tương lai) Chứa Source Code Unity - UI/UX Đấu trường, Pixel Sprites.
│
├── Server/                 # C# ASP.NET Core Game Server (Logic xử lý tập trung)
│   ├── Data/               # Cấu hình kết nối MongoDB & Mapping PokeAPI Collections.
│   ├── Hubs/               # SignalR Hubs - MatchmakingHub (Tìm trận) và BattleHub (Đấu turn-based).
│   ├── Models/             # Cấu trúc dữ liệu (PlayerAccount, TeamRoster, PokemonStats...).
│   ├── Services/           # Logic lõi (Tính sát thương, Random Recruit hàng ngày, Giao dịch VP).
│   ├── Program.cs          # Entry Point (Điểm khởi chạy) cấu hình Pipeline của ASP.NET.
│   └── Server.csproj       # File config Project C# khai báo các Package (MongoDB, SignalR).
│
├── infra/                  # Hạ tầng triển khai (Bọc trong Docker Compose)
│   ├── docker-compose.yml  # Khởi tạo container cho Database (MongoDB) và Server (dotnet).
│   ├── .env.example        # Mẫu biến môi trường (Environment variables).
│   └── .gitignore          # Ẩn file .env chứa thông tin nhạy cảm.
│
├── docs/                   # Nơi lưu trữ Design Game (Thiết Kế Game Pokémon Champions MMO.pdf)
│
└── README.md               # File giới thiệu và hướng dẫn tổng quan của dự án (Tài liệu này).
```

---

## 🛠 Hướng dẫn khởi chạy Server (Local Development)

Dự án sử dụng cơ chế Server và Database bằng **Docker Prebuilt Image** (không cần cấu hình Dockerfile phức tạp cho quá trình dev). Quá trình code của bạn trên `Server/` sẽ được hot-reload tự động.

### 1. Yêu cầu hệ thống:
- **Docker Desktop** (hoặc Docker Engine kết hợp Docker Compose).
- (Tùy chọn) Máy tính nên cài sẵn `.NET 8 SDK` để test các API/Hubs độc lập qua Postman.

### 2. Các bước chạy:

**Bước 1:** Di chuyển vào thư mục hạ tầng (`infra/`):
```bash
cd infra
```

**Bước 2:** Tạo file `.env` từ file mẫu:
```bash
cp .env.example .env
```
*(Bạn có thể mở `.env` lên để tùy chỉnh PORT nếu muốn, mặc định là 2567 cho Server).*

**Bước 3:** Khởi chạy các dịch vụ (Database & Dotnet Watch Server) bằng lệnh:
```bash
docker compose up -d
```

**Bước 4:** Kiểm tra log để biết server đã hoạt động (đặc biệt trong quá trình tải Package NuGet lần đầu):
```bash
docker compose logs -f server
```

✅ Nếu thành công, bạn sẽ thấy log báo:
`🚀 Starting Pokémon Champions MMO Server...`

---

## 📡 Các Endpoints và Kết nối

1. **MongoDB Connection String (Internal):** `mongodb://localhost:27017`
2. **SignalR WebSocket / Hub:** `ws://localhost:2567/game`
3. **Health Check API REST:** `http://localhost:2567/`