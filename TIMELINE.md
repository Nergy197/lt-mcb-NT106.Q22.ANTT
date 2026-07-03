# Timeline dự án - Pokémon Battle Game (NT106.Q22.ANTT)

> Cột: **Giai đoạn | Nội dung công việc | Người thực hiện | Ngày bắt đầu | Ngày kết thúc | Tiến độ | Ghi chú**
> Các giai đoạn tính năng được đặt tên theo từng nút chức năng trong game; nội dung từng giai đoạn bám theo lịch sử commit thực tế.
> Nhân sự: **Cường** = `Nergy197`, **Chuẩn** = `ProCH2405`, **Ân** = `Trầm Tính Ân`.

| Giai đoạn | Nội dung công việc | Người thực hiện | Ngày bắt đầu | Ngày kết thúc | Tiến độ | Ghi chú |
|-----------|-------------------|-----------------|--------------|---------------|--------|---------|
| **Giai đoạn 1: Lên ý tưởng và chuẩn bị** | Chọn ý tưởng đồ án, thống nhất tính năng game | Cả nhóm | 29/01/2026 | 10/02/2026 | Đã hoàn thành | |
| | Chọn công nghệ (Unity, ASP.NET Core, SignalR, MongoDB) | Cả nhóm | 29/01/2026 | 10/02/2026 | Đã hoàn thành | |
| | Nghiên cứu SignalR, WebSocket, turn-based networking | Cả nhóm | 11/02/2026 | 18/02/2026 | Đã hoàn thành | |
| | Viết Game Design Document (GDD) | Cả nhóm | 19/02/2026 | 28/02/2026 | Đã hoàn thành | |
| | Hoàn thành chương 1 báo cáo | Cả nhóm | 01/03/2026 | 04/03/2026 | Đã hoàn thành | |
| **Giai đoạn 2: Phân tích & Thiết kế hệ thống** | Vẽ sơ đồ phân rã chức năng & Use Case | Cả nhóm | 05/03/2026 | 07/03/2026 | Đã hoàn thành | |
| | Thiết kế luồng giao tiếp Client–Server (SignalR Hub Methods & Events) | Cường | 08/03/2026 | 10/03/2026 | Đã hoàn thành | |
| | Thiết kế schema MongoDB (accounts, players, pokemoninstances, VP...) | Chuẩn | 11/03/2026 | 14/03/2026 | Đã hoàn thành | |
| | Thiết lập Docker Compose (MongoDB + ASP.NET Core hot-reload) | Cường | 15/03/2026 | 18/03/2026 | Đã hoàn thành | Khởi tạo `database v1`, `add gameroom` |
| | Hoàn thành chương 2 báo cáo (Phân tích & Thiết kế) | Ân | 19/03/2026 | 25/03/2026 | Đã hoàn thành | |
| **Giai đoạn 3: Tính năng Đăng nhập (Xác thực người dùng)** | [UI] Màn hình Đăng nhập / Đăng ký / Start Menu: form email-password, nút Login, báo lỗi | Cường | 26/03/2026 | 31/03/2026 | Đã hoàn thành | `add start menu`, `finish start menu` |
| | [Logic] Server JWT: đăng ký (hash bcrypt), đăng nhập, đăng xuất, reset password | Cường | 26/03/2026 | 31/03/2026 | Đã hoàn thành | `implement JWT authentication system` |
| | [Logic] Fix kết nối server, dọn cấu trúc project, fix giới hạn ký tự email | Cường | 01/04/2026 | 03/04/2026 | Đã hoàn thành | `fix(auth): fix server connection issues`, `fix lỗi giới hạn kí tự email` |
| | [UI] Màn hình Quên mật khẩu: ResetPanel nhập email + mã OTP 6 số, đặt mật khẩu mới | Chuẩn | 04/04/2026 | 07/04/2026 | Đã hoàn thành | `UI ResetPanel để nhập mã OTP` |
| | [Logic] Gửi OTP qua email (SMTP), tích hợp Swagger/Email, xác thực OTP, reset password | Chuẩn | 04/04/2026 | 07/04/2026 | Đã hoàn thành | `integrate Swagger/Email`, `handle 6-digit OTP` |
| | [Logic] Cập nhật DB Model + API Server cho xác thực OTP, bỏ qua check DNS email, cập nhật Gmail App Password | Chuẩn | 08/04/2026 | 10/04/2026 | Đã hoàn thành | `Bỏ qua check DNS của Email` |
| | [Logic] Client lưu JWT, tự động đăng nhập lại, fix login | Cường | 08/04/2026 | 10/04/2026 | Đã hoàn thành | `fix log in` |
| **Giai đoạn 4: Menu chính & Điều hướng** | [UI] Main Menu: avatar người chơi, số dư VP, các nút chức năng | Chuẩn | 10/04/2026 | 12/04/2026 | Đã hoàn thành | `menu UI`, `Hoan thien mot phan menu` |
| | [Logic] MenuSceneManager: điều hướng scene có transition | Cường | 12/04/2026 | 13/04/2026 | Đã hoàn thành | `implement MenuSceneManager` |
| | [Logic] Bộ điều khiển cho toàn bộ scene, quản lý trạng thái global (token, userId) xuyên scene | Chuẩn | 13/04/2026 | 15/04/2026 | Đã hoàn thành | `Thêm bộ điều khiển cho toàn bộ scene` |
| | [UI] Sắp xếp lại các button trong menu | Chuẩn | 15/04/2026 | 16/04/2026 | Đã hoàn thành | `Sắp xếp lại các button trong menu` |
| | [Logic] BottomMenuManager: exclusive panel, khóa nút khác khi một panel đang mở | Chuẩn | 15/04/2026 | 16/04/2026 | Đã hoàn thành | `Add BottomMenuManager` |
| | [UI] Popup xác nhận đăng xuất | Ân | 16/04/2026 | 16/04/2026 | Đã hoàn thành | `confirm logout popup` |
| | [UI] Nâng cấp UX tổng thể, fix UX click chuột | Cường | 16/04/2026 | 17/04/2026 | Đã hoàn thành | `nâng cấp UX`, `Sửa UX click chuột` |
| **Giai đoạn 5: Tính năng Chat** | [UI] Khung World Chat: ô nhập, danh sách tin nhắn thế giới | Chuẩn | 18/04/2026 | 20/04/2026 | Đã hoàn thành | `Thêm logic code cho phần chat và friend` |
| | [Logic] Server ChatHub: gửi/nhận tin nhắn world real-time qua SignalR | Chuẩn | 18/04/2026 | 21/04/2026 | Đã hoàn thành | `Hoan thien sap xong tinh nang chat` |
| | [Logic] Lưu & tải lịch sử tin nhắn (đổi event `ReceiveHistoryMessage` → `ChatHistory`) | Cường | 22/04/2026 | 23/04/2026 | Đã hoàn thành | `Fix chat history` |
| | [UI][Logic] Chat riêng với bạn bè (friend DM), định tuyến theo friendId | Chuẩn | 24/04/2026 | 27/04/2026 | Đã hoàn thành | `Hoàn thành chat friends và chat world` |
| | [Logic] Fix chat world, fix chat mất kết nối khi re-login | Chuẩn | 28/04/2026 | 29/04/2026 | Đã hoàn thành | `Fix chat mất kết nối khi re-login` |
| **Giai đoạn 6: Tính năng Kết bạn (Friend / Mail)** | [UI] Nút Mail / Bạn bè trên Menu | Ân | 22/04/2026 | 24/04/2026 | Đã hoàn thành | `complete MailButton`, `Add mail feature friends list` |
| | [UI] Ô nhập tên để thêm bạn (NameInputAddFriend) | Ân | 25/04/2026 | 26/04/2026 | Đã hoàn thành | `NameInputAddFriend` |
| | [UI] Danh sách lời mời kết bạn (AddFriendRequestScrollView) | Ân | 26/04/2026 | 27/04/2026 | Đã hoàn thành | `AddFriendRequestScrollView` |
| | [UI] Danh sách bạn bè (ListFriendScrollView) | Ân | 27/04/2026 | 28/04/2026 | Đã hoàn thành | `ListFriendScrollView` |
| | [Logic] Server API kết bạn: gửi/chấp nhận lời mời, lưu collection `friendships` | Chuẩn | 29/04/2026 | 01/05/2026 | Đã hoàn thành | `Thêm logic code cho phần chat và friend` |
| | [Logic] Fix add friend, giao diện list friends, friend highlight sau khi PopulateUI | Chuẩn | 02/05/2026 | 02/05/2026 | Đã hoàn thành | `Fix add friend`, `Fix giao diện list friends` |
| | [Logic] Fix mail button stuck (null guard NotifyOpen, auto-resolve mailButton) | Chuẩn | 03/05/2026 | 03/05/2026 | Đã hoàn thành | `Fix mail button stuck` |
| **Giai đoạn 7: Tính năng Xếp hạng (Ranked)** | [Logic] Lưu kết quả trận Ranked khi trận kết thúc, tính điểm rank | Ân | 05/05/2026 | 05/05/2026 | Đã hoàn thành | `thêm lưu kết quả ranked khi trận kết thúc` |
| | [UI] Nút Xếp hạng: tab Top 100 & tab Bạn bè | Ân | 06/05/2026 | 08/05/2026 | Đã hoàn thành | `UI + Logic Rank Button (Top100 & Friends Rank)` |
| | [Logic] Server RankService: điểm rank kiểu Elo, bảng xếp hạng top 100 + rank bạn bè | Ân | 08/05/2026 | 10/05/2026 | Đã hoàn thành | `Fix RankSystem` |
| **Giai đoạn 8: Tính năng Chiêu mộ (Recruit)** | [UI] Màn hình Recruit: lắc Pokéball, reveal sprite (10-roll) | Chuẩn | 06/05/2026 | 08/05/2026 | Đã hoàn thành | `Hoàn thiện 1 phần tính năng recuit` |
| | [Logic] Server RecruitService: gacha ngẫu nhiên, lọc database Pokedex | Chuẩn | 06/05/2026 | 08/05/2026 | Đã hoàn thành | `Hoan thien tinh nang recuit va loc database` |
| | [UI] Popup xác nhận chi phí 2500 VP trước khi chiêu mộ | Ân | 09/05/2026 | 10/05/2026 | Đã hoàn thành | `Them popup xac nhan chi phi 2500 VP` |
| | [Logic] Trừ VP khi Recruit | Chuẩn | 10/05/2026 | 10/05/2026 | Đã hoàn thành | `Cập nhật trừ VP Recruit` |
| | [UI] Popup thành công sau khi recruit, chuyển về Menu scene | Ân | 11/05/2026 | 11/05/2026 | Đã hoàn thành | `Them success popup sau khi recruit` |
| | [Logic] Liên kết Recruit với Box: thêm Pokémon vào box sau khi chiêu mộ | Chuẩn | 12/05/2026 | 13/05/2026 | Đã hoàn thành | `Hoàn thiện liên kết recuit với box` |
| | [UI] Fix menu recruit | Cường | 14/05/2026 | 15/05/2026 | Đã hoàn thành | `fix recuit menu` |
| **Giai đoạn 9: Tính năng Battle** | [Logic] Tạo battle domain model, đưa luật game vào hằng số | Ân | 05/04/2026 | 07/04/2026 | Đã hoàn thành | `Tạo battle domain model`, `Đưa luật game vào hằng số` |
| | [Logic] Đăng ký BattleService vào DI, gắn battle vào GameHub/BattleHub | Ân | 08/04/2026 | 09/04/2026 | Đã hoàn thành | `Gắn battle vào GameHub` |
| | [Logic] Công thức damage Gen 9 (STAB, type chart, weather, terrain, crit, random), anti-cheat, auto-resolve turn | Ân | 10/04/2026 | 13/04/2026 | Đã hoàn thành | `nâng công thức damage`, `resolve turn tự động`, `anti cheat` |
| | [Logic] Implement 2v2 Double Battle (VGC Gen 9), đồng bộ SignalR, seed Pokémon khởi đầu | Cường | 14/04/2026 | 26/04/2026 | Đã hoàn thành | `Implement 2v2 Double Battle logic`, `rebuild battle logic` |
| | [UI] Layout Battle Scene: 2 bên Pokémon, HP bar, tên, level, status | Cường | 27/04/2026 | 01/05/2026 | Đã hoàn thành | `sườn battle scene`, `1 phần battle UI` |
| | [UI] Panel chọn move: 4 nút move, hiển thị type/PP | Cường | 02/05/2026 | 03/05/2026 | Đã hoàn thành | `1 phần UI` |
| | [UI] Hiệu ứng đòn đánh (move visual effects), flash màu khi nhận đòn | Cường | 04/05/2026 | 05/05/2026 | Đã hoàn thành | `Implement move visual effects` |
| | [Logic] Secondary status: burn, poison, paralysis, flinch | Cường | 06/05/2026 | 07/05/2026 | Đã hoàn thành | `secondary status logic` |
| | [UI] Nút Đầu hàng (surrender) | Cường | 08/05/2026 | 08/05/2026 | Đã hoàn thành | `thêm nút đầu hàng` |
| | [UI] Màn hình chọn chế độ chơi (Casual / Ranked), matchmaking mode selection | Cường | 09/05/2026 | 12/05/2026 | Đã hoàn thành | `Hoàn thiện tính năng chọn chế độ chơi`, `matchmaking mode selection` |
| | [Logic] MatchmakingHub: hàng đợi tìm trận, bot fallback sau 20s, tách logic tìm trận và battle | Cường | 13/05/2026 | 16/05/2026 | Đã hoàn thành | `tách logic tìm trận và battle` |
| | [Logic] Fix kết nối PvP, fix matchmaking race condition, fix battle scene | Cường | 17/05/2026 | 20/05/2026 | Đã hoàn thành | `sửa lỗi kết nối PvP`, `matchmaking race condition fix` |
| **Giai đoạn 10: Tính năng Box chứa Pokémon** | [UI] Màn hình Box: lưới 6×5, 32 box, nút chuyển box | Chuẩn | 15/05/2026 | 17/05/2026 | Đã hoàn thành | `Hoàn thiện tính năng box chứa pokemon` |
| | [Logic] Server BoxService: lưu/lấy Pokémon (30 slot × 32 box) | Chuẩn | 15/05/2026 | 17/05/2026 | Đã hoàn thành | `ee2f3853 Hoàn thiện tính năng box` |
| | [UI][Logic] Quản lý đội hình (party): icon party, đưa Pokémon vào đội, logic Pokémon khởi đầu | Cường | 18/05/2026 | 21/05/2026 | Đã hoàn thành | `party management UI`, `Sửa logic pokemon khởi đầu` |
| | [Logic] Fix party trong box, cache box, prefetch, sửa đường dẫn `pokedex_final.json` | Chuẩn | 22/05/2026 | 23/05/2026 | Đã hoàn thành | `Fix box: icon party, cache box, prefetch` |
| | [UI] Popup thông báo đã đưa vào box / trial box | Ân | 24/05/2026 | 24/05/2026 | Đã hoàn thành | `Complete Popup thông báo đã đưa vào box/trial box` |
| **Giai đoạn 11: Tính năng Pokédex** | [Logic] Chuẩn hóa dữ liệu Pokedex & Moves từ API, nạp vào MongoDB (PokedexService seed) | Chuẩn | 26/03/2026 | 31/03/2026 | Đã hoàn thành | `hoàn thiện DB của Pokedex và Moves`, `link data from api into db` |
| | [UI] Màn hình Pokédex: duyệt danh sách Pokémon quốc gia | Chuẩn | 21/05/2026 | 23/05/2026 | Đã hoàn thành | `Hoàn thiện tính năng pokedex` |
| | [Logic] Fix lỗi pokedex | Chuẩn | 24/05/2026 | 24/05/2026 | Đã hoàn thành | `Fix lỗi pokedex` |
| **Giai đoạn 12: Tính năng Victory Points (VP) & Cửa hàng** | [UI] Hiển thị số dư VP ở Main Menu và góc màn hình Battle | Ân | 09/05/2026 | 10/05/2026 | Đã hoàn thành | `UI VP và cơ chế cộng trừ, lấy số dư VP` |
| | [Logic] CurrencyService: cộng/trừ VP (atomic FindOneAndUpdate), lấy số dư | Ân | 09/05/2026 | 10/05/2026 | Đã hoàn thành | `cơ chế cộng trừ, lấy số dư VP` |
| | [UI] Popup tùy chọn khi mua Pokémon (mua đứt / dùng thử) | Ân | 12/05/2026 | 13/05/2026 | Đã hoàn thành | `UI Option Popup khi mua pokemon` |
| | [Logic] Logic cửa hàng: mua đứt Pokémon, ghi log giao dịch vào `vp_transactions` | Ân | 13/05/2026 | 15/05/2026 | Đã hoàn thành | `logic cửa hàng` |
| | [UI] Panel quà tân thủ 30.000 VP (Welcome Panel) | Ân | 25/05/2026 | 26/05/2026 | Đã hoàn thành | `Thêm tính năng quà tân thủ 30,000 VP` |
| | [Logic] Nhận quà tân thủ 1 lần/tài khoản, fix welcome claimed query filter | Chuẩn | 26/05/2026 | 27/05/2026 | Đã hoàn thành | `Fix welcome claimed query filter` |
| | [Logic] Fix VP JSON mapping casing, cập nhật hiển thị VP UI | Ân | 27/05/2026 | 28/05/2026 | Đã hoàn thành | `Fix VP JSON mapping casing` |
| **Giai đoạn 13: Tính năng Âm thanh & Cài đặt** | [UI] Nút Cài đặt + Panel Setting (thanh âm lượng) | Ân | 25/05/2026 | 27/05/2026 | Đã hoàn thành | `UI thô button setting`, `Update UI Setting` |
| | [Logic] AudioSettingsManager: lưu master volume qua PlayerPrefs, route qua MainAudioMixer | Cường | 28/05/2026 | 30/05/2026 | Đã hoàn thành | `done hệ thống audio` |
| | [UI][Logic] Thêm & wire Sound Effect cho nút bấm và sự kiện game | Ân | 31/05/2026 | 02/06/2026 | Đã hoàn thành | `Add Sound Effect`, `Complete Wire Sound Effect` |
| **Giai đoạn 14: Triển khai (Deploy)** | [DevOps] Hoàn chỉnh Docker Compose (server + MongoDB), fix cấu hình docker | Cường | 03/06/2026 | 03/06/2026 | Đã hoàn thành | `fix docker` |
| | [DevOps] Deploy server lên Railway (bản chạy thử online) | Chuẩn | 04/06/2026 | 05/06/2026 | Đã hoàn thành | `Đưa server lên railway` |
| | [DevOps] Viết script triển khai + chuyển hosting server sang Azure | Chuẩn | 06/06/2026 | 07/06/2026 | Đã hoàn thành | `Thêm script cập nhật Server lên Azure`, `Cập nhật link server sang Azure` |
| | [Client] Cập nhật URL server trong các Unity Scene sang bản deploy, fix encoding tiếng Việt do đổi URL | Chuẩn | 07/06/2026 | 08/06/2026 | Đã hoàn thành | `Cập nhật link trong Unity Scenes`, `Fix encoding tiếng Việt` |
| **Giai đoạn 15: Kiểm thử & Sửa lỗi** | Test Auth: đăng ký, đăng nhập, OTP, edge case (sai mật khẩu, hết hạn OTP) | Cả nhóm | 09/06/2026 | 11/06/2026 | | |
| | Test Matchmaking & Battle: chọn move, đổi Pokémon, timeout turn, disconnect, bot fallback | Cả nhóm | 12/06/2026 | 16/06/2026 | | |
| | Test Recruit / Box / VP / Pokédex: gacha, mua bán, chuyển box, đội hình | Cả nhóm | 17/06/2026 | 19/06/2026 | | |
| | Test Chat / Friend / Ranked: world+DM, kết bạn, leaderboard | Cả nhóm | 20/06/2026 | 21/06/2026 | | |
| | Fix bug UI (layout vỡ, animation lỗi, text tràn) | Cả nhóm | 22/06/2026 | 24/06/2026 | | |
| | Fix bug logic (damage sai, VP trừ nhầm, rank không cập nhật) | Cả nhóm | 22/06/2026 | 24/06/2026 | | |
| **Giai đoạn 16: Hoàn thành & Nộp đồ án** | Hoàn thành báo cáo chương 3, 4, 5 (Cài đặt, Kết quả, Kết luận) | Cả nhóm | 25/06/2026 | 29/06/2026 | | |
| | Quay video demo gameplay (Đăng nhập → Menu → Recruit → Battle → Kết quả) | Cả nhóm | 30/06/2026 | 01/07/2026 | | |
| | Chuẩn bị slide thuyết trình | Cả nhóm | 30/06/2026 | 01/07/2026 | | |
| | Review toàn bộ source code, dọn dẹp comment/log thừa | Cả nhóm | 02/07/2026 | 03/07/2026 | | |
| | Nộp báo cáo và source code | Cả nhóm | 04/07/2026 | 04/07/2026 | | |
