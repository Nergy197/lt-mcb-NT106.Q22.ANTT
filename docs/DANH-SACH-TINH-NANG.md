# Danh sách tính năng — Pokémon MMO

> Chỉ liệt kê tính năng **đã có giao diện (UI) thực tế** — xác nhận bằng cách rà soát trực tiếp
> mã nguồn client (`Client/Assets/Scripts`) và scene Unity, xem chức năng đó có thật sự gọi được
> và có phần tử giao diện tương ứng trong scene hay không. Các hub method / endpoint / logic có sẵn
> ở tầng code nhưng chưa có UI thật đều **không được đưa vào danh sách này** — ví dụ: `Heal`,
> `StartMatch`, `PokemonController.purchase/update-stats/swap-move`, tìm kiếm bạn bè, trạng thái
> "đang gõ", trao đổi Pokémon, và **Terastallize (Tera)** — logic Tera có đầy đủ trong
> `BattleSkillPanel.cs` nhưng scene chiến đấu (`Battle scene.unity`) chưa có GameObject nút Tera
> nên tính năng tự ẩn, người chơi không thấy và không dùng được.

---

## 1. Tài khoản & Xác thực

- **Đăng ký tài khoản** — gửi OTP xác thực qua email, xác nhận đăng ký bằng mã
- **Đăng nhập** — cấp JWT, tự động điều hướng vào sảnh
- **Đăng xuất** — xóa token cục bộ, xóa lịch sử DM
- **Quên mật khẩu** — gửi OTP qua email, panel nhập OTP + đặt mật khẩu mới
- **Quà tân thủ** — nhận 30.000 VP lần đầu đăng nhập (`WelcomePanel`)

## 2. Tìm trận & Ghép trận (Matchmaking)

- **Tìm trận Xếp hạng (Ranked)** — hàng chờ, đếm ngược bot dự phòng, tự ghép Bot nếu hết giờ
- **Tìm trận Thường (Casual)** — tương tự Ranked, không tính điểm xếp hạng
- **Hủy tìm trận** — nút "Hủy tìm trận" trong lúc đang đếm ngược
- **Phòng riêng (Private Battle)**
  - Tạo phòng, nhận mã 6 số, đếm ngược hết hạn phòng (5 phút)
  - Nhập mã để vào phòng người khác
  - Hủy phòng đã tạo
  - Chuyển đổi tab Tạo phòng / Nhập mã, bấm ra ngoài để đóng
- UI đồng hồ tìm trận: đếm giây đã tìm + đếm ngược bot dự phòng

## 3. Đối kháng (Battle) — VGC 2vs2

- **Team Preview** — xem đội hình đối thủ, chọn 4/6 Pokémon + thứ tự ra trận
- **Thi đấu theo lượt** — chọn chiêu thức hoặc đổi Pokémon cho cả 2 slot mỗi lượt
- **Buộc đổi Pokémon** khi Pokémon gục giữa lượt
- **Đầu hàng** giữa trận
- Hiển thị đầy đủ cơ chế trận đấu qua thoại/hiệu ứng: chí mạng, hệ khắc chế, thời tiết, địa hình, trạng thái bất lợi
- **Giới hạn thời gian mỗi lượt** (đếm ngược trên HUD)
- **Xử lý mất kết nối giữa trận** — đối thủ thấy đếm ngược ân hạn; tự xử thua nếu không vào lại kịp
- **Panel kết quả** — hiện thắng/thua/hòa, số VP/RP nhận được (hiệu ứng đếm chạy số), bấm màn hình để về Menu
- Giao diện: HUD 4 Pokémon, panel Command/Skill/Target/Party, thoại luôn hiển thị, hiệu ứng chiến đấu theo chiêu thức

## 4. Chiêu mộ Pokémon (Gacha)

- **Roll 10 lượt** một lần, xem chi tiết từng Pokémon quay được
- **Popup xác nhận chi phí** (2.500 VP) trước khi quay
- Xác nhận nhận nuôi: **Dùng thử (miễn phí, 7 ngày)** hoặc **Vĩnh viễn (2.500 VP)**
- Popup thông báo thành công trước khi về sảnh
- Lưu & hiển thị lại kết quả roll gần nhất (trong 24 giờ)
- Bấm ra ngoài để quay lại banner/màn trước

## 5. Quản lý Pokémon

### 5.1 Box (Kho lưu trữ)
- 32 Box × 30 ô (lưới 6×5), điều hướng trái/phải giữa các box
- Rút ra / Đưa vào đội hình bằng **phím, click chuột, hoặc kéo-thả**
- Menu ngữ cảnh xác nhận (Rút ra/Đưa vào/Hủy) — hỗ trợ cả bàn phím lẫn chuột
- Nhãn **"TRIAL"** trên icon Pokémon dùng thử để phân biệt với Pokémon chính thức

### 5.2 Đội hình (Party)
- Quản lý 6 ô Party, xem HP/level từng Pokémon, đổi vị trí

### 5.3 Pokédex
- Tra cứu Pokédex quốc gia — danh sách cuộn, tìm kiếm, xem chi tiết (ảnh, tên, số hiệu)

## 6. Kinh tế trong game

- **Ví VP (Victory Points)** — hiển thị số dư ở góc màn hình Menu
- Nhận thưởng VP sau mỗi trận (thắng/thua khác mức)
- Quà tân thủ 30.000 VP

## 7. Xếp hạng (Ranked)

- **Bảng xếp hạng Top 100**
- **Bảng xếp hạng theo bạn bè**
- Điểm xếp hạng kiểu Elo — cập nhật và hiển thị sau mỗi trận Ranked

## 8. Bạn bè & Cộng đồng

- **Danh sách bạn bè**
- **Gửi lời mời kết bạn** bằng tên chính xác
- **Xem & phản hồi lời mời** đang chờ (chấp nhận/từ chối)
- **Hủy kết bạn**
- **Chat thế giới (World Chat)** — gửi/nhận công khai, lưu & tải lại lịch sử
- **Nhắn tin riêng (DM)** — chỉ giữa bạn bè, lưu & tải lại lịch sử
- Toàn bộ popup hỗ trợ **click chuột** + **bấm ra ngoài để đóng**

## 9. Giao diện Menu chính

- Sảnh chính: điều hướng tới Chiêu mộ, Đối kháng, Box, Mail, Pokédex, Bạn bè, Xếp hạng
- Thanh menu dưới — chỉ 1 popup mở tại 1 thời điểm
- Hiển thị VP hiện tại
- **Cài đặt** — chỉnh âm lượng Master, lưu lại lựa chọn
- **Đăng xuất**

---

*Danh sách được xác nhận qua việc rà soát trực tiếp script client gọi endpoint/hub method — không dựa trên suy đoán từ code server.*
