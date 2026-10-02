**Tiếng Việt** | [English](FAQ)

**Windows báo "Windows đã bảo vệ máy tính của bạn".**
Bộ cài trên GitHub chưa có chữ ký số (chứng chỉ tốn phí hằng năm). Chọn **Thông tin thêm** > **Vẫn chạy**. Chỉ hiện một lần lúc cài; cập nhật sau không bị hỏi lại. Muốn chắc file không bị sửa, so mã SHA-256 như ở [[Cài đặt]].

**Mở LMS/MyBK trong app thấy "session timed out" hay bắt đăng nhập lại.**
Phiên đăng nhập trên server trường hết hạn sau vài giờ, dù app vẫn nhớ. App tự đi qua trang đăng nhập SSO; nếu SSO cũng hết phiên thì bạn đăng nhập lại một lần.

**Dữ liệu của app nằm ở đâu?**
`%LOCALAPPDATA%\BKStudyDesk.Data`, chỉ tài khoản Windows của bạn (cùng SYSTEM và Administrators) mở được. Tài liệu môn học nằm ở thư mục bạn chọn (mặc định `Documents\BK Study Desk`).

**App có nộp bài, đăng ký môn hay làm gì trên tài khoản của tôi không?**
Không. App chỉ gọi API **đọc**. Không nộp bài, không đăng ký/hủy môn, không thanh toán, không làm quiz thay bạn.

**App có gửi dữ liệu của tôi đi đâu không?**
Không có server riêng, không analytics. App chỉ nói chuyện với server của trường và với GitHub để hỏi bản mới nếu bạn cho phép. Xem [[Bảo mật và quyền riêng tư]].

**Có bản cho macOS, Linux không?**
Chưa. Bản đa nền tảng đang làm; khi ổn định sẽ có ở trang Releases.

**Máy chip ARM (Snapdragon) dùng được không?**
Được, tải bản `-Setup-arm64.exe`.

**Dùng thời khóa biểu trên Google Calendar được không?**
Được. Vào **Lịch** > **Thời khóa biểu** > **Xuất lịch (.ics)**, lưu file, rồi trên Google Calendar mở **Cài đặt** > **Nhập và xuất** và chọn file đó. Nên nhập vào một lịch riêng để sang kỳ sau xóa cho dễ.

**Báo lỗi ở đâu?**
[Issues](https://github.com/xeroz369/bk-study-desk/issues/new/choose). Đừng dán mật khẩu, MSSV hay token. Lỗ hổng bảo mật thì báo riêng, xem [SECURITY.md](https://github.com/xeroz369/bk-study-desk/blob/main/SECURITY.md). Việc cần trao đổi riêng: [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).