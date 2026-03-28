# Pokémon MMO - Đồ án NT106.Q22.ANTT (Lập trình Mạng Căn bản)

Đồ án xây dựng một hệ thống Game trực tuyến nhiều người chơi (MMO) lấy chủ đề Pokémon. Dự án sử dụng kiến trúc Client-Server với Client được viết bằng **Unity (C#)** và Server được thiết kế bằng **C# ASP.NET Core** tích hợp **SignalR** cho kết nối theo thời gian thực (Real-time). Cơ sở dữ liệu sử dụng **MongoDB**, tất cả được đóng gói và vận hành qua **Docker**.

---

## 📂 Tổ chức mã nguồn (Project Structure)

Dự án được phân chia thành các thư mục chính, tách biệt rõ ràng giữa logic máy chủ, máy khách và hạ tầng (infrastructure):

```text
lt-mcb-NT106.Q22.ANTT/
├── Client/                 # (Tương lai) Chứa Source Code Unity - Giao diện Game và Logic Client.
│
├── Server/                 # C# ASP.NET Core Game Server (Logic xử lý tập trung)
│   ├── Data/               # Cấu hình kết nối MongoDB & Mapping Collections.
│   ├── Hubs/               # SignalR Hubs - Điểm chạm kết nối thiết lập phòng chơi/map.
│   ├── Models/             # Định nghĩa cấu trúc dữ liệu / Schema (Player, Pokemon, Account...).
│   ├── Services/           # Logic lõi của Game (Hồi máu, Bắt Pokemon, Đổi Pokemon, Kiểm tra Boss...).
│   ├── Program.cs          # Entry Point (Điểm khởi chạy) cấu hình Pipeline của ASP.NET.
│   └── Server.csproj       # File config Project C# khai báo các Package (MongoDB.Driver, SignalR).
│
├── infra/                  # Hạ tầng triển khai (Bọc trong Docker Compose)
│   ├── docker-compose.yml  # Khởi tạo container cho Database (MongoDB) và Server (dotnet).
│   ├── .env.example        # Mẫu biến môi trường (Environment variables).
│   └── .gitignore          # Ẩn file .env chứa thông tin nhạy cảm.
│
├── docs/                   # (Tương lai) Nơi lưu trữ Design Game, sơ đồ lớp, kịch bản báo cáo.
│
└── README.md               # File giới thiệu và hướng dẫn tổng quan của dự án (Tài liệu này).
```

---

## 🛠 Hướng dẫn khởi chạy Server (Local Development)

Dự án sử dụng cơ chế Server và Database bằng **Docker Prebuilt Image** (không cần cấu hình Dockerfile phức tạp cho quá trình dev). Quá trình code của bạn trên `Server/` sẽ được hot-reload tự động.

### 1. Yêu cầu hệ thống:
* **Docker Desktop** (hoặc Docker Engine kết hợp Docker Compose).
* (Tùy chọn) Máy tính nên cài sẵn `.NET 8 SDK` nếu bạn muốn edit code với IntelliSense bằng Visual Studio / Rider / VSCode.

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
`🚀 Starting Pokémon MMO Server (hot-reload)...`

---

## 📡 Các Endpoints và Kết nối

1. **MongoDB Connection String (Internal):** `mongodb://localhost:27017`
2. **SignalR WebSocket / Hub:** `ws://localhost:2567/game`
3. **Health Check API REST:** `http://localhost:2567/`