**Tiếng Việt** | [English](How-the-app-works)

Trang này ghi đúng những gì app gửi tới máy chủ của trường, để ai cũng kiểm tra được. Mã nguồn tương ứng nằm trong `src/SoHocTap/Shell` và `src/SoHocTap/Sources`; địa chỉ và API nằm trong `src/SoHocTap/Core/DefaultConfig.json`.

> BK Study Desk **không chính thức**: không được Trường Đại học Bách khoa hay Moodle HQ xác nhận. App chỉ dùng tài khoản của chính bạn và chỉ đọc. Nếu trường yêu cầu, dự án sẽ thay đổi hoặc dừng tính năng tương ứng.

## 1. Đăng nhập HCMUT (SSO)

- App mở trang đăng nhập SSO của trường (`sso.hcmut.edu.vn`, hệ thống CAS) trong một cửa sổ trình duyệt trong app (WebView2). Bạn tự gõ tài khoản và mật khẩu; app không đọc mật khẩu.
- Đăng nhập xong, máy chủ SSO đặt cookie phiên. Cookie nằm trong thư mục dữ liệu của WebView2 trên máy bạn.
- Phiên SSO do máy chủ của trường quyết định: máy chủ tự kết thúc phiên theo lịch riêng và khi để lâu không dùng. App giữ cookie đăng nhập và gia hạn mỗi lần đi qua SSO, nên không bao giờ tự xóa phiên sớm hơn máy chủ. Khi app đang mở, cứ 60 phút app mở lại cổng SSO một lần để phiên không bị hủy vì để lâu (tắt được trong Cài đặt). Phiên đã hết trên máy chủ thì bạn đăng nhập lại một lần.

## 2. BK-LMS (Moodle)

App dùng **web service của Moodle**, giống app Moodle chính thức trên điện thoại (trường hướng dẫn sinh viên dùng app đó với BK-LMS).

**Lấy token.** App mở địa chỉ đăng nhập dành cho app di động của Moodle:

```
https://lms.hcmut.edu.vn/admin/tool/mobile/launch.php?service=moodle_mobile_app&passport=<chuỗi ngẫu nhiên>&urlscheme=moodlemobile
```

- Đây là luồng có sẵn trong Moodle (mã nguồn mở: `admin/tool/mobile/launch.php`). Vì đã đăng nhập SSO ở bước 1 nên không phải gõ mật khẩu lần nữa.
- `urlscheme=moodlemobile` là URL scheme của app Moodle chính thức. Moodle mặc định chỉ trả token cho scheme này (cài đặt `forcedurlscheme`), nên app khai đúng scheme đó và **bắt lại đường dẫn `moodlemobile://token=...` ngay trong cửa sổ nhúng**. App không đăng ký scheme này với Windows và không mạo danh tên hay user-agent của app Moodle.
- App kiểm chữ ký của token (MD5 của địa chỉ site + passport) rồi lưu token vào `data\secrets\`, mã hóa bằng Windows DPAPI.

**Gọi API.** Mọi lời gọi là POST tới `https://lms.hcmut.edu.vn/webservice/rest/server.php` với token. Chỉ dùng các hàm **đọc** sau:

| Hàm | Dùng để |
|---|---|
| `core_webservice_get_site_info` | tên tài khoản, kiểm token |
| `core_enrol_get_users_courses` | danh sách lớp |
| `core_course_get_updates_since` | lớp nào có thay đổi từ lần trước |
| `core_course_get_contents` | cấu trúc lớp, danh sách tài liệu |
| `core_calendar_get_action_events_by_timesort` | lịch, hạn nộp (50 mục mỗi lần) |
| `mod_assign_get_assignments` | bài tập |
| `mod_quiz_get_quizzes_by_courses`, `mod_quiz_get_user_attempts`, `mod_quiz_get_attempt_review` | quiz, lượt đã nộp, trang xem lại của lượt **đã nộp** |
| `mod_forum_get_forums_by_courses`, `mod_forum_get_forum_discussions` | thông báo trên diễn đàn |
| `message_popup_get_popup_notifications` | thông báo |
| `gradereport_user_get_grade_items` | sổ điểm |
| `core_group_get_course_user_groups` | nhóm lớp của bạn (lọc hạn nộp đúng nhóm) |

Tài liệu và ảnh trong câu hỏi quiz tải qua `webservice/pluginfile.php` kèm token. Tài liệu chỉ tải khi bạn chọn mục cần tải (hoặc khi bạn bật tự tải trong Cài đặt).

App **không** gọi hàm nào để nộp bài, bắt đầu hay lưu lượt làm quiz, đánh dấu đã xem, hay sửa gì trên LMS.

## 3. MyBK

- App mở MyBK trong một cửa sổ WebView2 ẩn bằng phiên SSO của bạn: `sso.hcmut.edu.vn/cas/login?service=.../my/homeSSO.action`, rồi `/app/login?type=cas`, rồi `/app/`.
- Trang `/app/` của MyBK chứa token đăng nhập của chính trang. App chạy `fetch()` **ngay trong trang** để gọi các API JSON mà trang MyBK dùng; token không ra khỏi trang.
- Danh sách API (đường dẫn sau `https://mybk.hcmut.edu.vn/api/`):

| API | Dữ liệu |
|---|---|
| `v1/student/get-student-info` | họ tên, MSSV, lớp (app không lưu CCCD, địa chỉ, điện thoại, ngày sinh, email) |
| `v1/semester-year/short` | học kỳ |
| `v1/student/schedule` | thời khóa biểu |
| `thoi-khoa-bieu/lich-thi-sinh-vien/v1` | lịch thi |
| `share/ket-qua-hoc-tap/...` (POST, gửi MSSV) | bảng điểm, chương trình đào tạo, điểm thành phần |
| `v1/student/status-decision/...`, `v1/student-activities/social-workdays/...`, `v1/tuition-fees/...`, `v1/course-register-results/...` | quyết định học vụ, ngày công tác xã hội, học phí, môn đã đăng ký |

- Các API POST `share/ket-qua-hoc-tap/...` là truy vấn đọc: chính trang **Bảng điểm môn học** và **Chương trình đào tạo** của MyBK gửi đúng các request này khi bạn chỉ mở trang để xem (kiểm ngày 02/10/2026). Các API còn lại lấy từ mã nguồn trang MyBK.
- Trang đăng ký môn (`/dkmh/dangKyMonHocForm.action`) chỉ được mở để đọc bảng các đợt đăng ký, tối đa một lần mỗi ngày.

## 4. Tần suất

| Việc | Khi nào |
|---|---|
| Đồng bộ LMS | khi mở app (nếu đã quá 3 giờ từ lần trước), sau đó mỗi 3 giờ; chỉ đọc lớp học kỳ này. Lần đồng bộ thường gửi khoảng 15–30 request |
| Đồng bộ MyBK | mỗi 12 giờ; 13 API, cộng trang đăng ký môn mỗi ngày một lần |
| Giữ phiên SSO | 60 phút một lần khi app đang mở |
| Giãn cách | API LMS cách nhau 300 ms, file cách nhau 1 giây, API MyBK cách nhau 300 ms |
| Máy chủ báo quá tải (HTTP 429/503) | app chờ theo `Retry-After` (không có thì chờ ngẫu nhiên tăng dần) rồi thử lại; tổng cộng tối đa 3 lần, vẫn bị thì dừng và báo lỗi |
| Chưa đăng nhập | không gửi gì |

Bạn luôn chọn **Đồng bộ** được để chạy ngay; app bỏ qua nếu vừa đồng bộ xong dưới 2 phút.

## 5. Ngoài trường

- **GitHub**: chỉ khi bạn cho phép kiểm tra bản mới (xem [[Cập nhật]]).
- Không có máy chủ riêng, không analytics, không quảng cáo.
- Trang của trường bạn mở trong app có thể tự tải tài nguyên của chính nó (font, script). App không chặn các tài nguyên đó, nhưng không cho cửa sổ ẩn chuyển sang trang ngoài trường.
