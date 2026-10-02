# Nhật ký thay đổi

**Tiếng Việt** · [English](CHANGELOG.en.md)

Ghi các thay đổi đáng chú ý của BK Study Desk. Định dạng theo [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) (tên mục dịch sang tiếng Việt), số phiên bản theo [SemVer 2.0.0](https://semver.org/spec/v2.0.0.html).

## [Chưa phát hành]

## [1.1.3] - 2026-10-03

### Thêm

- **Lịch** → **Thời khóa biểu** có dạng **lưới tuần** giống Google Calendar ([#5](https://github.com/xeroz369/bk-study-desk/issues/5)). Lưới tự canh bố cục: Thứ 7, CN chỉ hiện khi có buổi học; khung giờ theo buổi sớm nhất và muộn nhất; buổi trùng giờ chia đôi cột. Dạng danh sách vẫn còn, chọn ở ô **Kiểu xem**.
- **Xuất lịch (.ics)…**: lưu thời khóa biểu cả kỳ và lịch thi ra file iCalendar để nhập vào Google Calendar, Outlook, Lịch của Windows.
- Đồng bộ LMS/MyBK lỗi thì thanh báo có thêm nút **Mở MyBK** / **Mở LMS** để xem trang đang lỗi gì hoặc đăng nhập lại ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- **Tải tài liệu…** chọn được loại file: PDF, slide (.ppt, .pptx), khác; số file và dung lượng tính theo lựa chọn ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- Bảng **Kết quả đăng ký** có thêm cột giảng viên và giờ học, lấy từ thời khóa biểu MyBK ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- Tài liệu tiếng Anh tách thành file riêng, dịch đầy đủ: phần English cuối README, `PRIVACY.en.md`, `SECURITY.en.md`, `CODE_OF_CONDUCT.en.md`, `CONTRIBUTING.en.md`, `CHANGELOG.en.md`, và các trang Wiki tiếng Anh. Chuẩn viết tiếng Anh: `docs/english-style.md`.
- Mục **Ủng hộ** trong README và Wiki: Ko-fi và mã QR MoMo/VietQR.

### Thay đổi

- README và Wiki: khuyên dùng bộ cài trên GitHub trước, Microsoft Store sau (bản Store phải chờ Microsoft duyệt nên thường chậm hơn).

### Sửa lỗi

- Thời khóa biểu dạng danh sách có nhóm "Thứ 0" với giờ lạ (ví dụ "0:00–6:50"). Môn không có giờ cố định giờ ghi riêng ở dòng "Không có giờ cố định".

## [1.1.2] - 2026-10-03 [YANKED]

Đã gỡ khỏi trang phát hành vì bản này có mục ủng hộ trong app. Mọi thay đổi chuyển sang 1.1.3.

## [1.1.1] - 2026-10-03

### Thay đổi

- Chữ trên giao diện và tài liệu theo chuẩn văn phong `docs/van-phong.md`: "Sao chép" thay "Copy", "Chọn" thay "Bấm", "tải xuống" thay "tải về"; câu lỗi nói rõ cần làm gì.
- Trang Luyện tập: mục "Các môn" đổi tên thành "Môn học".

### Thêm

- README và Wiki: hướng dẫn cài từ Microsoft Store, lưu ý kiểm tra số phiên bản vì bản Store phải chờ Microsoft duyệt.
- Ảnh minh họa mới (dữ liệu demo) cho README, Wiki và trang Store.
- Email liên hệ chính thức của dự án: bkstudydesk@xerozsoft.com (README, SECURITY, PRIVACY, quy tắc ứng xử).

### Sửa lỗi

- Mốc **quiz mở** trên LMS bị hiện như hạn nộp ("còn 2 ngày" tính tới giờ mở). Giờ mốc này ghi "Quiz mở" kèm hạn đóng, và không còn được tính vào "Quiz trong 14 ngày".
- Đồng bộ MyBK báo "không phản hồi sau 45 giây (đang ở blank)" và lần **Thử lại** nào cũng lỗi y hệt:
  - Lần mở MyBK bị hủy hoặc lỗi thì app báo ngay, kèm lý do.
  - Sau lỗi, app bỏ WebView ẩn cũ, lần thử lại dùng WebView mới.
  - Log ghi rõ khi WebView2 bị lỗi hoặc khi MyBK chuyển sang trang ngoài trường.
- Bảng trống (Sổ điểm LMS, Bảng điểm, Chương trình đào tạo, đăng ký, công tác xã hội, quyết định học vụ) hiện các cột co lại, không có chữ. Giờ bảng trống ghi rõ lý do: đang đồng bộ, đồng bộ lỗi, hoặc thật sự không có gì.
- Bấm chuột vào tab hoặc mục trên thanh điều hướng thì hiện khung trắng quanh mục đó. Giờ khung focus chỉ hiện khi dùng bàn phím.

## [1.1.0] - 2026-10-03

### Thêm

- Có trên Microsoft Store: cài không bị cảnh báo SmartScreen, Store tự cập nhật.
- Thanh trạng thái hiện bước đang làm khi đồng bộ (ví dụ "LMS: đang đọc quiz…", "đang kiểm tra lớp… (3/5)").
- InfoBar báo lỗi theo chuẩn Windows: biểu tượng theo mức độ, câu dễ hiểu kèm cách xử lý, mục **Chi tiết** chứa chữ gốc của máy chủ (có nút **Sao chép**). Thanh trạng thái có biểu tượng lỗi cạnh LMS/MyBK.
- Cửa sổ LMS/MyBK báo khi trang tải quá 10 giây, mất mạng, máy chủ lỗi hoặc phiên đăng nhập đã hết, kèm nút **Thử lại**.
- Giữ phiên đăng nhập khi app đang mở: mỗi 60 phút ghé SSO một lần (tắt được trong Cài đặt). Phiên SSO của trường vẫn hết sau tối đa 8 giờ kể từ lúc đăng nhập.
- **Cài đặt** → **Nhật ký hoạt động**: nút mở file log; bật ghi log chẩn đoán khi cần báo lỗi (tự tắt sau 7 ngày).

### Thay đổi

- Mặc định chỉ đọc môn của học kỳ này và không tự tải tài liệu. Muốn tải thì vào **Môn học** → **Tải tài liệu…** và chọn mục cần tải; bật lại tự tải trong Cài đặt nếu muốn.
- Lần đầu đăng nhập, lịch, hạn nộp, quiz và điểm hiện ngay, không chờ tải tài liệu.
- Đọc ít request hơn: thông báo diễn đàn chỉ lấy lớp học kỳ này; quiz không có hạn đóng chỉ kiểm tra ngày một lần; trang Môn học chỉ quét thư mục khi đang mở.
- Giãn cách request tới LMS và MyBK. Máy chủ báo quá tải (HTTP 429/503) thì app chờ theo `Retry-After` rồi mới thử lại.
- **Giữ đăng nhập khi tắt rồi mở lại app** giờ giữ tối đa 8 giờ, bằng giới hạn phiên của máy chủ trường (trước đây ghi 30 ngày nhưng máy chủ đã hủy phiên từ lâu).
- `data\config.json` chỉ lưu những gì bạn đổi so với mặc định.
- Trang trống vì chưa có dữ liệu thì ghi rõ đang tải lần đầu, bị lỗi hay chưa đăng nhập.

### Sửa lỗi

- Lịch LMS không đọc được (app xin quá 50 mục một lần; giờ đọc theo trang).
- Lỗi đọc từng phần (sổ điểm một môn, một API MyBK, tải file) trước đây bị bỏ qua im lặng; giờ hiện trên thanh báo và ghi log.
- Đồng bộ báo "Access denied" khi giao diện đang đọc dữ liệu đúng lúc đồng bộ ghi lại.
- Cuộn chuột bị kẹt khi đi qua ô nhập hoặc bảng; tiêu đề cột "Điểm chữ", "Thang điểm" bị cắt; ngày và số ngày công tác xã hội sai định dạng.
- Cửa sổ chính và cửa sổ đăng nhập tràn ra ngoài màn hình nhỏ.
- Nút **Cài đặt** và **Giới thiệu** không phản hồi khi dùng trình đọc màn hình.

### Bỏ

- Tham số `downloads.watch` trong config (không có tác dụng).

### Bảo mật

- Nguồn cập nhật chỉ nhận repo GitHub qua https; thư mục trên máy chỉ dùng cho bản cài thử.
- Log che token, cookie, ticket đăng nhập CAS và email trước khi ghi.
- Tắt tự điền dữ liệu chung của WebView2. Mật khẩu chỉ được lưu khi bạn chọn **Lưu** lúc trình duyệt trong app hỏi.

## [1.0.6] - 2026-10-02

### Thêm

- Bộ cài tiếng Việt: chọn thư mục cài, không cần quyền admin. Bộ gỡ hỏi giữ hay xóa dữ liệu app.
- Tự cập nhật theo lựa chọn của bạn: báo khi có bản mới, tự cài khi tắt app hoặc không kiểm tra.
- Luyện tập: soạn quiz, nhờ AI soạn, nhập/xuất Markdown, ôn quiz LMS đã nộp, thi thử.
- Lấy dữ liệu từ bản zip cũ trong Cài đặt.

### Thay đổi

- Dữ liệu app ở `%LOCALAPPDATA%\BKStudyDesk.Data`.
- Giấy phép đổi sang PolyForm Noncommercial 1.0.0 (không dùng cho mục đích thương mại). Các bản trước vẫn theo MIT.

### Sửa lỗi

- Mở LMS/MyBK khi phiên hết hạn hiện "session timed out".

### Bảo mật

- Vá một số lỗi bảo mật nhỏ (giới hạn điều hướng của WebView ẩn, không chạy file tải xuống, giới hạn kích thước file).

Các bản trước: xem [Releases](https://github.com/xeroz369/bk-study-desk/releases).

[Chưa phát hành]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.3...HEAD
[1.1.3]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.1...v1.1.3
[1.1.2]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/xeroz369/bk-study-desk/compare/v1.0.6...v1.1.0
[1.0.6]: https://github.com/xeroz369/bk-study-desk/releases/tag/v1.0.6
