# Văn phong tiếng Việt

Quy chuẩn viết cho README, CHANGELOG, Wiki, PRIVACY/SECURITY, issue template, chú thích code, commit và chữ trong app (`src/SoHocTap/lang/vi.json`, khung Luyện tập). Mỗi quy tắc đều có nguồn; quy tắc nào là lựa chọn của dự án (không có nguồn) thì ghi rõ.

## Nguồn

| Viết tắt | Nguồn |
|---|---|
| MSG | [Microsoft Vietnamese Localization Style Guide](https://aka.ms/vietnamese-styleguide) |
| MST | [Microsoft Terminology](https://learn.microsoft.com/en-us/globalization/reference/microsoft-terminology), bản tiếng Việt |
| Mozilla | [Mozilla Style Guide tiếng Việt](https://mozilla-l10n.github.io/styleguides/vi/) |
| QĐ 1989 | [Quyết định 1989/QĐ-BGDĐT (2018)](https://luatvietnam.vn/giao-duc/quyet-dinh-1989-qd-bgddt-2018-quy-dinh-ve-chinh-ta-trong-chuong-trinh-sach-giao-khoa-gdpt-307547-d1.html), áp cho sách giáo khoa, không bắt buộc với phần mềm |
| Google | [Error messages](https://developers.google.com/tech-writing/error-messages), [CL descriptions](https://google.github.io/eng-practices/review/developer/cl-descriptions.html) |
| KaC | [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) |
| CC | [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/) |
| CLDR | Dữ liệu locale `vi` của Unicode CLDR |

## Giọng văn

- Gọi người dùng là "bạn". Câu ngắn, từ thường ngày, thân thiện, không trang trọng quá (MSG).
- Viết lại theo ý, không dịch từng chữ (MSG). Đúng: "Không lưu được ảnh." Sai: "Lưu ảnh thất bại."
- Không quảng cáo: không "tuyệt vời", "mạnh mẽ", "liền mạch", "tối ưu trải nghiệm" (MSG cảnh báo câu phóng đại).
- Chỉ nói điều code thật sự làm. Câu hứa tuyệt đối ("không bao giờ", "luôn luôn") phải kiểm lại với code trước khi viết.

## Tránh mùi văn dịch

- Không mở câu bằng "Việc" trước động từ (MSG). Đúng: "Đọc kỹ đề trước khi làm." Sai: "Việc đọc kỹ đề…"
- Dùng câu chủ động (Mozilla). Đúng: "App tải tài liệu về máy." Sai: "Tài liệu được tải về bởi app."
- Bỏ "các/những" khi không cần (Mozilla). Đúng: "Cài đặt". Sai: "Các cài đặt".
- Rút gọn (MSG). Đúng: "Nếu còn lỗi". Sai: "Nếu bạn vẫn còn gặp lỗi".

## Viết hoa, dấu câu, số

- Nút, nhãn, tiêu đề: chỉ viết hoa chữ đầu (MSG, Mozilla). Đúng: "Tải tài liệu". Sai: "Tải Tài Liệu". Tên riêng viết hoa mọi âm tiết: "Bách Khoa", "Việt Nam".
- Không có khoảng trắng trước `. , : ; ! ? )`; có khoảng trắng sau. Không đặt dấu phẩy trước "và", "hoặc" (MSG).
- Dấu ba chấm là ký tự `…`, không gõ `...` (Mozilla). Chữ trên nút mở cửa sổ/hộp thoại tiếp theo kết thúc bằng `…`.
- Câu thông báo lỗi kết thúc bằng dấu chấm (MSG).
- Số: `5,25`, `1.526`, `250 GB` (cách bằng khoảng trắng), `80%` (không cách). Số phiên bản giữ dấu chấm: `1.1.0` (MSG).
- Ngày giờ trong app và văn bản: `dd/MM/yyyy`, `HH:mm` (CLDR; QĐ 1989 Điều 10). CHANGELOG dùng `YYYY-MM-DD` (KaC).

## Chính tả (lựa chọn của dự án)

- Bỏ dấu kiểu cũ: hòa, xóa, khóa, tùy, hủy (không viết hoà, xoá). Lý do: MST dùng kiểu này là chính, và toàn bộ repo đang dùng kiểu này (đếm ngày 02/10/2026: 60 chữ kiểu cũ, 0 chữ kiểu mới). QĐ 1989 dùng kiểu mới nhưng chỉ áp cho sách giáo khoa.
- Dùng "y": kỹ, lý, ký (MST: "kỹ" 41/0, "quản lý" 209/13).

## Thuật ngữ

Theo MST, trừ chỗ ghi khác:

| Tiếng Anh | Dùng | Ghi chú |
|---|---|---|
| Settings | Cài đặt | giống Windows |
| Sign in / Sign out | Đăng nhập / Đăng xuất | |
| Sync | đồng bộ | |
| Download / Upload | tải xuống / tải lên | không dùng "tải về" |
| Update | cập nhật | |
| Install / Uninstall | cài đặt / gỡ cài đặt | động từ ngắn: "cài", "gỡ" |
| Retry / Cancel / Close | Thử lại / Hủy / Đóng | |
| Copy / Paste | Sao chép / Dán | |
| Log | nhật ký (log) | trong tài liệu kỹ thuật giữ "log" |
| Error / Warning | lỗi / cảnh báo | |
| Details | Chi tiết | |
| File / Folder | file / thư mục | dự án giữ "file" (quen dùng), MST là "tệp" |
| Browse | Chọn… | nút mở hộp thoại chọn file/thư mục |
| Default | mặc định | |
| Notification | thông báo | |
| Privacy | quyền riêng tư | |
| Release / Changelog | bản phát hành / nhật ký thay đổi | |
| Repository, issue, pull request, commit, build, token, API | giữ tiếng Anh | MST dịch "cam kết", "yêu cầu kéo"… mang nghĩa khác hoặc ít người dùng |

- Tên sản phẩm, thương hiệu không dịch: BK-LMS, MyBK, Windows, GitHub (MSG).
- Từ viết tắt và từ đã quen giữ nguyên: API, URL, SSO, OK, tab (MSG). Lần đầu dùng khái niệm lạ thì ghi kèm: "đăng nhập một lần (SSO)".

## Nhắc tới giao diện

- Tên nút, mục menu in đậm; đường đi dùng `→`: **Cài đặt** → **Nhật ký hoạt động**.
- Dùng "Chọn" cho mọi cách bấm (chuột, bàn phím, cảm ứng, công cụ trợ năng) (MSG). "Bấm đúp" giữ nguyên cho double-click.

## Thông báo lỗi

Theo MSG và Google, mỗi thông báo có: chuyện gì xảy ra, vì sao (nếu biết), cần làm gì. Chữ kỹ thuật (mã lỗi, câu gốc của máy chủ) để ở mục **Chi tiết** và trong log, không đặt vào câu chính.

- Đúng: "Không tải được lịch LMS. Chọn **Thử lại** sau ít phút."
- Sai: "Lỗi: core_calendar_get_action_events_by_timesort thất bại!"
- Mẫu câu: "Không tìm thấy…", "Không kết nối được…", "Không đủ…" (MSG).
- Không dùng "Làm ơn", "Xin hãy"; cần lịch sự thì "Vui lòng", thường thì bỏ (Mozilla).

## Commit

- Theo CC: type giữ tiếng Anh (`feat`, `fix`, `docs`, `ci`, `build`, `refactor`, `test`), mô tả tiếng Việt, dạng câu mệnh lệnh ngắn (Google).
- Ví dụ: `fix(lms): đọc lịch theo trang 50 mục`. Thân commit nói thay đổi gì và vì sao.

## CHANGELOG

Theo KaC. Tên mục tiếng Việt là lựa chọn của dự án (KaC chưa có bản tiếng Việt):

| KaC | Dùng |
|---|---|
| Unreleased | Chưa phát hành |
| Added | Thêm |
| Changed | Thay đổi |
| Deprecated | Sắp bỏ |
| Removed | Bỏ |
| Fixed | Sửa lỗi |
| Security | Bảo mật |

Mỗi bản ghi `## [1.1.0] - YYYY-MM-DD`, cuối file có link so sánh giữa các tag. Số phiên bản theo [SemVer 2.0.0](https://semver.org/spec/v2.0.0.html): thêm tính năng là tăng số giữa, chỉ sửa lỗi là tăng số cuối.
