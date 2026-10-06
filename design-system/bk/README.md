# BK: hệ thống thiết kế của BK Study Desk

Ngày 07/10/2026. Áp dụng cho app Avalonia và khung Luyện tập (Svelte, dùng cùng token qua biến CSS). Mọi trang theo tài liệu này; muốn làm khác thì sửa tài liệu trước.

## 0. Vì sao có tài liệu này

Giao diện 1.x xấu không phải vì thiếu tính năng mà vì **tự chế**: 17 style tự đặt dần, thanh báo tự dựng, bảng nhiều cột tự co giãn, khung không cố định. Bản đa nền tảng chỉ dùng control chuẩn và các khung, số đo cố định dưới đây.

## 1. Nguyên tắc

1. **Không tự chế.** Chỉ dùng control của Avalonia và theme Fluent có sẵn. Không viết control mới, không viết lại template của control.
2. **Không icon.** Nút, mục điều hướng, nhãn đều là chữ. Trạng thái báo bằng chữ kèm màu.
3. **Khung cố định.** Vùng, cột, chiều cao dòng, kích thước ô theo số đo ở mục 4; nội dung không làm khung co giãn.
4. **Chỉ mượn một thứ của web trường: thanh ngang trên cùng**, để người dùng quen tay. Bố cục, thẻ, bảng không bắt chước LMS.
5. **Không cắt dữ liệu.** Danh sách dài thì cuộn; không giới hạn số mục bằng con số đặt sẵn.
6. **Đo, không đoán.** Màu theo tỉ lệ tương phản đo được; mỗi trang kiểm bằng ảnh chụp ở 900 px và cửa sổ phóng to.

## 2. Bộ khung (đã chốt)

| Gói | Bản | Vì sao |
|---|---|---|
| `Avalonia`, `Avalonia.Desktop` | 12.1.x | chạy Windows, macOS, Linux; tự vẽ control nên giống nhau trên cả ba |
| `Avalonia.Themes.Fluent` | cùng bản Avalonia | theme chính thức, phát hành cùng Avalonia, mỗi control có tài liệu |
| `Avalonia.Controls.DataGrid` | 12.1.x | bảng chính thức, kèm theme Fluent của gói |
| `Avalonia.Controls.WebView` (`NativeWebView`) | 12.1.x | đăng nhập SSO, MyBK, khung Luyện tập; WebView2, WKWebView, WebKitGTK |

Không dùng thư viện giao diện bên thứ ba (đã xem Ursa, Semi, FluentAvalonia, Material, SukiUI ngày 07/10: hoặc phải tự sửa nhiều, hoặc còn nhiều issue mở, hoặc tài liệu mỏng).

Bắt buộc khi dùng NativeWebView trên Windows (thử 06/10): app có `app.manifest` khai báo Windows 10/11; đặt `UserDataFolder` vào `data/webview` trong `EnvironmentRequested`.

## 3. Token

Màu nhấn của Fluent đặt bằng `ColorPaletteResources Accent="#0388B4"` (không theo màu nhấn của máy người dùng). Các màu khác là tài nguyên của app, không dùng màu nào ngoài bảng.

| Tên | Giá trị | Tương phản trên trắng | Dùng cho |
|---|---|---|---|
| `Accent` | `#0388B4` | 4,05:1 | màu nhấn Fluent: nút chính, ô chọn, viền focus. **Không** dùng cho chữ thường. |
| `Bar` | `#02779E` | 5,08:1 (chữ trắng trên nền) | nền thanh trên cùng |
| `Link` | `#044CC8` | 7,31:1 | chữ có màu: link, số ngày trong ô ngày, giờ bắt đầu |
| `Page` | `#F4F6F8` | | nền trang |
| `Surface` | `#FFFFFF` | | nền thẻ, bảng |
| `Line` | `#E3E7EB` | | viền thẻ, đường kẻ |
| `Text` | `#1F2328` | | chữ chính |
| `Sub` | `#59636E` | | chữ phụ |
| `Danger` | `#C4314B` | | quá hạn, còn dưới mốc Rất gấp |
| `Warn` | `#8A5C00` | | còn dưới mốc Gấp |
| `Ok` | `#1A7F37` | | đã xong |
| `DateBox` | `#E6F3F8` | | nền ô ngày |
| `RowSelected` | `#EEF6F9` | | nền dòng bảng đang chọn |

Chữ: phông hệ thống. Cỡ chỉ dùng 11 (nhãn ô ngày), 12 (dòng phụ), 13 (số đếm, trạng thái), 14 (chữ thân, tên), 16 (tiêu đề thẻ), 18 (số ngày), 24 (tiêu đề trang). Nhấn mạnh bằng SemiBold.

Chế độ màu mặc định Tối; Cài đặt chọn Theo hệ thống, Sáng hay Tối. Cặp tối, đo ngày 07/10, mọi màu chữ đạt 4,5:1 trên `Surface`, `Page`, `RowSelected` tối:

| Tên | Tối | Tên | Tối |
|---|---|---|---|
| `Bar` | `#035F7E` (7,1:1 chữ trắng) | `Text` | `#E6EDF3` |
| `Link` | `#6CB6FF` | `Sub` | `#9AA4AF` |
| `Page` | `#14181D` | `Danger` | `#FF6B7A` |
| `Surface` | `#1E232A` | `Warn` | `#D29922` |
| `Line` | `#2F363F` | `Ok` | `#3FB950` |
| `DateBox` | `#10384A` | `RowSelected` | `#0F3142` |

Màu nằm trong `ThemeDictionaries` của `Styles/Tokens.axaml`; View dùng `DynamicResource` để đổi chế độ không cần mở lại.

## 4. Khung và số đo

Đo trên hai trang thử (Hôm nay, Lịch) ngày 07/10, kiểm ở 496, 720, 1000, 1296 px và phóng to, cả sáng lẫn tối.

### Cửa sổ

- Nhỏ nhất 480 x 480 (nửa màn hình laptop nhỏ). Không đặt mốc độ rộng cứng cho từng phần: khung tự đo chỗ trống.
- Chiều cao thanh, đầu thẻ, dòng là mức tối thiểu (MinHeight): chữ to hơn thì khung cao theo, không cắt.
- Thanh trên cùng cao **48**, nền `Bar`, lề hai bên 24.
  - Trái: "BK Study Desk" (14, SemiBold, trắng), cách mục đầu 28.
  - Mục trang, tối đa 6, cách nhau 24, chữ 14 trắng độ đậm 85%. Mục đang mở: SemiBold, trắng 100%, gạch dưới trắng dày 3. Không nền khác màu.
  - Thứ tự cố định: Hôm nay, Môn học, Lịch, Luyện tập, MyBK. Ctrl+1 tới Ctrl+5.
  - Phải: trạng thái đồng bộ (13, trắng 85%), Tài khoản, Cài đặt, cách nhau 20.
- Thanh hẹp: đo chữ thật rồi bỏ bớt theo thứ tự: tên app, rồi gom Đồng bộ, Tài khoản, Cài đặt vào nút "Thêm" (MenuFlyout). Mục trang không bao giờ bị giấu.
- Vùng nội dung: lề 24, trên 24, dưới 32.
  - Trang chữ (Hôm nay): rộng tối đa **1440**, căn giữa, cả vùng cuộn dọc.
  - Trang bảng (`IFillPage`, như Lịch): lấp đầy cửa sổ, bảng tự cuộn dọc và ngang, thanh cuộn luôn thấy được.

### Trang

- Đầu trang: tiêu đề 24 SemiBold, dưới là một câu mô tả hay ngày (13, `Sub`), cách 2. Cách khối đầu 24.
- Lưới: hai cột tỉ lệ 2:1 cách 24, cột trái tối thiểu 560, cột phải 320 tới 400. Không đủ chỗ cho cả hai mức tối thiểu thì một cột (cột phải xuống dưới).

### Thẻ

- Nền `Surface`, viền 1 `Line`, bo 8.
- Đầu thẻ cao **48**, đệm ngang 16, kẻ dưới 1. Trái tiêu đề (16 SemiBold), phải số đếm (13 `Sub`): "6 việc trong 14 ngày".
- Thẻ trong cùng cột cách nhau 24.

### Dòng danh sách (trong thẻ)

- Cao **64**, đệm ngang 16, kẻ dưới 1.
- Lưới cột: `48, 12, *, 12, 112`.
  - Cột đầu 48: **ô ngày** 48 x 48, bo 6, nền `DateBox`, số ngày 18 SemiBold `Link`, dưới "th 10" 11 `Sub`; hoặc **ô giờ** (giờ bắt đầu 13 SemiBold `Link`, giờ kết thúc 12 `Sub`).
  - Giữa: tên (14 SemiBold `Text`), dòng phụ "Môn, chi tiết" (12 `Sub`), cách 2; quá dài thì "...".
  - Cột phải 112, canh phải: dòng trên còn bao lâu (13 SemiBold, màu theo mức), dòng dưới trạng thái (12 `Sub`).

### Bảng (DataGrid)

- Đặt trong thẻ (đầu thẻ như trên), bảng không viền riêng.
- Đầu bảng cao **40**, dòng cao **48**, chỉ kẻ ngang màu `Line`.
- Cột ngắn (giờ, loại, trạng thái, còn) vừa theo chữ (`Auto`, có mức tối thiểu); cột tên (SemiBold) và cột môn chia phần còn lại 2:1. Hẹp hơn tổng mức tối thiểu thì bảng cuộn ngang, không giấu cột. Tối đa 6 cột. Cột số hay "còn..." canh phải, kể cả tiêu đề cột.
- Nhóm (theo ngày) dùng nhóm có sẵn của DataGrid, dải nhóm nền `Page`. Dòng chọn nền `RowSelected` (không dùng nền accent, để chữ màu vẫn đọc được).

## 5. Dùng control nào

| Cần | Dùng |
|---|---|
| Thanh trên cùng | `Border` + `Button` không nền (chữ), gạch dưới là `Border` cao 3 |
| Thẻ | `Border` theo mục 4 |
| Danh sách trong thẻ | `ItemsControl` hay `ListBox`, mỗi mục theo khung Dòng danh sách |
| Bảng | `DataGrid` theo khung Bảng |
| Chia nhóm trong trang | `TabControl` (tối đa 5 tab) |
| Thông báo ngắn sau thao tác | `WindowNotificationManager` (có sẵn của Avalonia) |
| Thông báo lỗi trên trang | `Border` nền nhạt màu `Danger` + một câu + một `Button` |
| Hộp thoại | `Window` hộp thoại hai nút, hành động chính bên phải |
| Khay hệ thống | `TrayIcon` (có sẵn của Avalonia) |
| Trang web trường, Luyện tập | `NativeWebView` |

## 6. Trạng thái

| Trạng thái | Hiển thị |
|---|---|
| Đang tải lần đầu | chữ "Đang tải..." trong thẻ |
| Trống | một câu nói vì sao trống, một nút nếu làm được gì |
| Lỗi | khung lỗi trong trang: vấn đề và cách xử lý một câu, nút "Thử lại" |
| Dữ liệu cũ | chữ 13 `Sub` ở đầu thẻ: "Cập nhật 2 giờ trước" |
| Chưa đăng nhập | cả trang một khối giới thiệu kèm nút Đăng nhập; không lặp câu trống ở từng thẻ |

## 7. Chữ trong giao diện

- Tiếng Việt viết hoa đầu câu. Nút là động từ: "Đăng nhập", "Tải về".
- Không gạch ngang dài, mũi tên, dấu chấm giữa, dấu ba chấm một ký tự, ngoặc cong, emoji.

## 8. Khung Luyện tập (Svelte)

- Dùng cùng token qua biến CSS (tên giống bảng mục 3).
- Component có sẵn, chỉ đổi biến giao diện. Định dạng Study Pack v1 và file tiến độ không đổi; đổi cấu trúc thì có bước chuyển đổi.

## 9. Kiểm trước khi bàn giao một trang

- Ảnh chụp ở 900 px và phóng to.
- Không icon, không cỡ chữ ngoài mục 3, không màu ngoài bảng token, không control tự chế.
- Khung đúng số đo mục 4.
- Đủ trạng thái trống, đang tải, lỗi.
