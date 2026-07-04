# Nội dung Slide thuyết trình — Pokémon MMO

> Cấu trúc: 1 slide tổng quan → 3 slide sơ đồ (ảnh trong `docs/so-do-chuong3/`) → 6 slide tính năng.
> **Chỉ liệt kê tính năng đã có giao diện (UI) thực tế** — đã xác nhận qua rà soát mã nguồn client
> và scene Unity, loại bỏ các hub method/endpoint/logic có sẵn ở code nhưng chưa có phần tử giao
> diện tương ứng trong scene (VD: Tera — logic đầy đủ trong `BattleSkillPanel.cs` nhưng scene
> chiến đấu chưa có nút Tera nên tự ẩn, không dùng được trong thực tế).
> Mỗi slide giữ tối đa 4-6 gạch đầu dòng để trình chiếu, phần "Ghi chú" là nội dung nói thêm khi thuyết trình.

---

## Slide 1 — Tổng quan đề tài

**Tiêu đề:** Pokémon MMO — Game đối kháng thời gian thực

- Game nhập vai chiến thuật lấy cảm hứng từ đấu trường VGC (Pokémon), chơi **2 vs 2**
- Kiến trúc **Client–Server**: Unity 6 (client) ⇄ ASP.NET Core 9 (server), giao tiếp qua REST + SignalR (thời gian thực)
- Dữ liệu lưu trên **MongoDB**
- Các mảng chính: Tài khoản, Ghép trận, Đối kháng, Gacha, Quản lý Pokémon, Kinh tế/Xếp hạng, Cộng đồng

**Ghi chú thuyết trình:** Nêu lý do chọn đề tài, phạm vi đề tài dừng ở đâu (server tự viết, không dùng engine game-server có sẵn), công nghệ nổi bật (SignalR, JWT, MongoDB).

---

## Slide 2 — Sơ đồ Use Case

**Tiêu đề:** Hình 3.1 — Sơ đồ Use Case

*(Chèn ảnh `hinh3-1-usecase.png`)*

- Tác nhân chính: **Người chơi** — tương tác với toàn bộ ca sử dụng
- Tác nhân phụ: **Đối thủ** (ca Thi đấu), **Máy chủ Email** (ca Quên mật khẩu)
- Quan hệ **«extend»**: Đấu với Bot mở rộng từ Tìm trận

**Ghi chú thuyết trình:** Nhấn mạnh use case nào là lõi (Thi đấu VGC) và use case nào hỗ trợ (Xác thực, Cộng đồng).

---

## Slide 3 — Sơ đồ luồng dữ liệu (DFD)

**Tiêu đề:** Hình 3.2 — DFD mức 1

*(Chèn ảnh `hinh3-2-dfd.png`)*

- Người chơi gửi yêu cầu tới **6 tiến trình** nghiệp vụ trên server
- Mỗi tiến trình đọc/ghi xuống **kho dữ liệu MongoDB** tương ứng
- Riêng tiến trình Thi đấu và Chiêu mộ có ghi ngược vào kho VP/Rank

**Ghi chú thuyết trình:** Giải thích vì sao tách 6 tiến trình riêng (mỗi tiến trình ứng với 1 Service ở tầng code), thể hiện sự tách bạch trách nhiệm.

---

## Slide 4 — Sơ đồ phân rã chức năng

**Tiêu đề:** Hình 3.3 — Cây chức năng hệ thống

*(Chèn ảnh `hinh3-3-chucnang.png`)*

- Gốc: **Hệ thống Pokémon MMO** → các nhóm chức năng chính
- Mỗi nhóm phân rã tiếp thành các chức năng con cụ thể, đã có giao diện hoàn chỉnh

**Ghi chú thuyết trình:** Dùng slide này làm "bản đồ" dẫn vào 6 slide tính năng tiếp theo.

---

## Slide 5 — Tính năng: Tài khoản & Ghép trận

**Tiêu đề:** Nhóm 1 — Tài khoản & Matchmaking

- Đăng ký/đăng nhập bằng **JWT**, quên mật khẩu qua **OTP email**
- Quà tân thủ **30.000 VP** cho tài khoản mới
- 2 chế độ tìm trận: **Xếp hạng / Thường** + **Phòng riêng** (mã 6 số, đếm ngược hết hạn)
- Tự động ghép **Bot** nếu hết giờ chờ đối thủ
- Nút **Hủy tìm trận** hoạt động cả khi đang đếm ngược

**Ghi chú thuyết trình:** Nhấn thời gian chờ ghép trận mặc định (20s) và cơ chế chống lỗi ghép trùng người chơi đang bận trận khác.

---

## Slide 6 — Tính năng: Đối kháng (Battle)

**Tiêu đề:** Nhóm 2 — Hệ thống Chiến đấu VGC 2vs2

- Vòng đời trận: **Team Preview → Thi đấu theo lượt → Panel kết quả**
- Chọn 4/6 Pokémon + thứ tự ra trận
- Chiến đấu qua HUD 4 Pokémon + panel Command/Skill/Target/Party
- Đầu hàng giữa trận + xử lý **mất kết nối** (đếm ngược ân hạn cho đối thủ thấy, tự xử thua nếu không vào lại kịp)
- Panel kết quả hiện thắng/thua/hòa kèm số VP/RP nhận được

**Ghi chú thuyết trình:** Đây là phần lõi kỹ thuật nặng nhất — có thể demo trực tiếp 1 trận ngắn nếu thời gian cho phép.

---

## Slide 7 — Tính năng: Gacha & Quản lý Pokémon

**Tiêu đề:** Nhóm 3 — Chiêu mộ & Kho Pokémon

- **Gacha**: quay 10 lượt/lần, popup xác nhận chi phí trước khi quay
- Nhận nuôi: **Dùng thử (miễn phí, 7 ngày)** hoặc **Vĩnh viễn (2.500 VP)**
- **Box**: 32 box × 30 ô, rút/gửi Pokémon bằng phím, chuột, hoặc **kéo-thả**
- **Party**: quản lý 6 ô đội hình
- **Pokédex**: tra cứu quốc gia, tìm kiếm, xem chi tiết

**Ghi chú thuyết trình:** Nhấn khác biệt Trial vs Permanent (nhãn "TRIAL" trên icon) — cơ chế kinh tế cốt lõi tạo động lực chi tiêu VP.

---

## Slide 8 — Tính năng: Kinh tế & Xếp hạng

**Tiêu đề:** Nhóm 4 — VP & Bảng xếp hạng

- Ví **VP (Victory Points)** hiển thị trên Menu chính
- Thưởng VP theo kết quả trận (thắng/thua khác mức) + quà tân thủ 30.000 VP
- Điểm xếp hạng kiểu **Elo** — cập nhật sau mỗi trận Ranked
- Bảng xếp hạng **Top 100** và bảng xếp hạng theo **bạn bè**

**Ghi chú thuyết trình:** Giải thích vì sao trận Ranked mới ảnh hưởng điểm xếp hạng.

---

## Slide 9 — Tính năng: Bạn bè & Cộng đồng

**Tiêu đề:** Nhóm 5 — Xã hội trong game

- Danh sách bạn bè, gửi lời mời (theo tên chính xác), chấp nhận/từ chối, hủy kết bạn
- **Chat thế giới** — công khai toàn server, lưu & tải lại lịch sử
- **Nhắn tin riêng (DM)** — chỉ giữa bạn bè, lưu & tải lại lịch sử
- Toàn bộ popup hỗ trợ **click chuột đầy đủ** + bấm ra ngoài để đóng

**Ghi chú thuyết trình:** Nhấn đây là lớp giữ chân người chơi (social layer), tận dụng SignalR cho tin nhắn thời gian thực.

---

## Slide 10 — Giao diện Menu chính & Trải nghiệm người dùng

**Tiêu đề:** Nhóm 6 — Sảnh chính & UX

- Sảnh chính điều hướng tới Chiêu mộ, Đối kháng, Box, Mail, Pokédex, Bạn bè, Xếp hạng
- Thanh menu dưới — chỉ 1 popup mở tại 1 thời điểm, hiển thị VP hiện tại
- **Cài đặt** — chỉnh âm lượng Master, lưu lựa chọn qua các lần chơi
- **Đăng xuất**
- Chuẩn hóa tương tác: mọi popup đều hỗ trợ click chuột + bấm ra ngoài để đóng

**Ghi chú thuyết trình:** Slide tổng kết trải nghiệm điều hướng — nhấn tính nhất quán trong cách người chơi tương tác xuyên suốt toàn bộ game.

---

*Ảnh sơ đồ dùng cho slide 2-4 nằm tại `docs/so-do-chuong3/hinh3-1-usecase.png`, `hinh3-2-dfd.png`, `hinh3-3-chucnang.png`.*
