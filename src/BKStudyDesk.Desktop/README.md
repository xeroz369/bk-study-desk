# BK Study Desk (Avalonia): sửa ở đâu

| Muốn đổi | Sửa | Ai |
|---|---|---|
| Quy tắc nhìn (màu, cỡ, khung, control nào) | `design-system/bk/README.md` | người dùng đọc và quyết, AI làm theo |
| Màu, cỡ chữ, số đo khung | `Styles/Tokens.axaml` (chỉ giá trị và class) | người dùng sửa được |
| Bố cục một trang | `Views/<Trang>View.axaml` (chỉ XAML) | người dùng xem, AI sửa |
| Trang hiện mục nào, thứ tự, chữ trong dòng | `src/BKStudyDesk.Core/Presentation/<Trang>.cs` (thuần, test ở `tests/BKStudyDesk.Core.Tests`) | AI sửa kèm test |
| Khung cửa sổ, thanh trên cùng, chuyển trang | `MainWindow.axaml(.cs)` | AI |
| Chạy nền: nguồn, đồng bộ theo lịch, trạng thái | `AppHost.cs` (không giao diện) | AI |
| Đọc dữ liệu LMS, MyBK | `src/SoHocTap/Sources`, `Data` (dùng chung với bản 1.x) | AI |
| Luyện tập: màn, nút, cách hiện | `Views/Practice/*.axaml(.cs)`, chuyển màn ở `Views/Practice/PracticeHost.cs` | AI, chụp lại bằng `--luyen-man=` |
| Luyện tập: chấm điểm, lịch ôn, gói, nhập xuất | `src/BKStudyDesk.Core/Presentation/Practice/` (thuần, có test) | AI sửa kèm test |

Luyện tập vẽ bằng control Avalonia, không dùng khung Svelte (`src/ui`, `ui/` chỉ còn cho bản 1.x). WebView chỉ dùng cho đăng nhập, cửa sổ web trường và MyBK chạy ẩn.

Quy tắc: không icon; không control tự viết, không viết lại template control; màu, cỡ chỉ lấy từ `Tokens.axaml`; View không lọc hay sắp dữ liệu.
