# Nhật ký thay đổi

[![Tiếng Việt](https://img.shields.io/badge/lang-Ti%E1%BA%BFng%20Vi%E1%BB%87t-red.svg)](CHANGELOG.md) [![English](https://img.shields.io/badge/lang-English-blue.svg)](docs/en/CHANGELOG.md)

Ghi các thay đổi đáng chú ý của BK Study Desk. Định dạng theo [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) (tên mục dịch sang tiếng Việt), số phiên bản theo [SemVer 2.0.0](https://semver.org/spec/v2.0.0.html).

## [Chưa phát hành]

### Sửa lỗi

- Nút **Đóng góp tài liệu** mở thẳng trang [Gửi tài liệu](https://bk-study-library.github.io/hcmut-library/gui-tai-lieu/) của thư viện.

## [1.2.0] - 2026-10-04

### Thêm

- **Thư viện** tài liệu chung ([hcmut-library](https://bk-study-library.github.io/hcmut-library/)) nối thẳng vào app: tab **Thư viện** của từng môn, tải về vào thư mục `Thư viện` của đúng môn đó.

### Sửa lỗi

- Cửa sổ đăng nhập không còn trống: báo đang mở trang, trang chậm hay lỗi, có nút **Thử lại**.
- Thanh báo "Đã đăng nhập" tự ẩn sau vài giây (theo cài đặt Windows "Ẩn thông báo sau").

## [1.1.11] - 2026-10-04

### Thêm

- **Thư viện** tài liệu chung ([hcmut-library](https://bk-study-library.github.io/hcmut-library/)) bật sẵn: tab **Thư viện** ở trang **Môn học**, có nút **Mở trên web**. Tắt ở **Cài đặt** > **Thư viện**.

## [1.1.10] - 2026-10-04

### Thêm

- **Cài đặt** > **Chữ**: đổi phông và cỡ chữ.

### Sửa lỗi

- MyBK không còn tự đăng xuất sau khoảng 8 giờ.
- **Sắp tới** hiện đủ mọi mốc, không dừng ở 14 ngày. Việc quá hạn chưa làm nằm ở nhóm **Quá hạn**.
- Hạn nộp có mã nhóm (L01...) không còn bị ẩn khi app không đọc được nhóm.
- Giờ học kiểu "7g30" hiện đúng trên lưới tuần.
- Ngày, giờ luôn theo giờ Việt Nam.
- Thanh trạng thái không còn ghi "Đã cập nhật lên ..." sau khi cập nhật.
- Chữ bị cắt có tooltip hiện đủ.
- Chữ lớn không còn làm cắt cột bảng hay số phiên bản ở thanh trạng thái.

## [1.1.9] - 2026-10-03

### Thêm

- **Thư viện** tài liệu chung (BK Study Library), **tắt sẵn cho tới khi thư viện mở**. Bật ở **Cài đặt** > **Thư viện** thì trang **Môn học** có thêm tab **Thư viện** cho môn khớp mã: tài liệu nhóm theo loại, lọc theo giảng viên, học kỳ, loại kiểm tra. Mỗi tài liệu: **Mở trên web**, **Tải về** (vào thư mục `Thư viện` của môn, kiểm tra file trước khi lưu, không ghi đè file của bạn), **Cài vào Luyện tập** với gói quiz, **Sao chép link**. Có nút **Đóng góp tài liệu** mở trang đóng góp trên web. Tài liệu bị gỡ khỏi thư viện thì ẩn và xóa bản app đã tải.

### Thay đổi

- Tải bản cập nhật có thanh tiến trình với số phần trăm thật và dung lượng đã tải (vd. "Đang tải bản 1.2.0... 42% (12,3/29,1 MB)"), ở cửa sổ cập nhật, **Cài đặt** > **Cập nhật** và thanh trạng thái (cả khi chế độ **Tự động** tải ở nền). Thanh không chạy lùi, chỉ đầy khi đã tải xong và kiểm tra gói. Có nút **Hủy tải**.
- Cài bản cập nhật không còn hiện cửa sổ tiếng Anh "Installing Update" với thanh chạy mãi. App báo "Đang cài bản ..., app sẽ tự mở lại sau vài giây" ở thanh trạng thái rồi tự mở lại.
- Thanh tiến trình đồng bộ giữ số phần trăm đã đạt khi một bước chạy lâu, không chuyển sang thanh chạy qua lại.

### Sửa lỗi

- **Lịch** > **Thời khóa biểu**: mất vạch giờ hiện tại vào buổi tối hoặc sáng sớm, vì lưới chỉ kéo từ buổi học sớm nhất tới buổi muộn nhất ([#25](https://github.com/xeroz369/bk-study-desk/issues/25)). Lưới giờ luôn đủ 0:00 tới 24:00, mở tuần thì tự cuộn tới buổi học sớm nhất, hoặc tới giờ hiện tại nếu đang ngoài khung nhìn.
- **Lịch** và **Hôm nay**: hạn nộp, quiz đặt xa hơn 120 ngày trên LMS nay cũng hiện (trước đây app chỉ đọc lịch LMS tới 120 ngày sau). **Hôm nay** hiện đủ mọi tin LMS trong 7 ngày, khớp số ở ô **Thông báo LMS trong 7 ngày** (trước đây chỉ 8 tin).
- MyBK có lúc không đồng bộ được và báo "không phản hồi sau 45 giây (đang ở .../my/homeSSO.action)" dù máy chủ trường vẫn chạy bình thường. Lỗi xảy ra khi phiên MyBK hết hạn và app phải đăng nhập lại qua SSO.

## [1.1.8] - 2026-10-03

### Thêm

- **Lịch** > **Thời khóa biểu** dạng lưới: vạch màu nhấn chỉ giờ hiện tại ở cột hôm nay ([#22](https://github.com/xeroz369/bk-study-desk/issues/22)).
- **Thêm sự kiện...** trên trang **Lịch**: tự nhập sự kiện hoặc lịch học bù ([#22](https://github.com/xeroz369/bk-study-desk/issues/22)). Gõ nhanh một dòng như "Học bù Giải tích 2 - 12/10 - 7:00-8:50 - H1-201" là app tự điền. Sự kiện tự thêm hiện ở lưới tuần (viền nét đứt), **Sắp tới**, **Hôm nay**, có trong file .ics và được nhắc trước khoảng 1 giờ. Chuột phải để sửa, xóa, sao chép. Chỉ lưu trên máy.
- **Cài đặt** > **Cập nhật**: chọn khoảng kiểm tra bản mới 6, 12 hoặc 24 giờ.

### Thay đổi

- Thanh tiến trình đồng bộ hiện đúng tiến độ theo từng bước (vd. "LMS: Đang tải tệp 2/5 (Giải tích 2)..."), không chạy lùi, không nháy với lượt đồng bộ ngắn.
- Đồng bộ không có gì mới thì không ghi lại file, không vẽ lại trang, không thông báo. Nhắc hạn không lặp lại, kể cả sau khi mở lại app.
- Trang **Môn học** mở nhanh hơn: quét thư mục chạy nền, chỉ nạp tab đang xem; bỏ qua file rác và thư mục ẩn (`.history`, `~$...`, `desktop.ini`...).
- Nút **Giới thiệu** chỉ còn biểu tượng.
- Kiểm tra bản mới 6 giờ một lần (trước là 24), và khi máy thức dậy hoặc có mạng lại. Chế độ **Tự động**: bản đã tải được cài ở lần mở app kế tiếp; mở lên thanh trạng thái báo "Đã cập nhật lên ...".

### Sửa lỗi

- Khi LMS cập nhật một file trùng nội dung với file bạn tự lưu ở chỗ khác trong thư mục môn, app có thể chuyển **file của bạn** vào `_Lưu trữ\Bản cũ`. Giờ app chỉ thay file trong thư mục `Tài liệu LMS`.
- Tên môn có dấu viết theo hai kiểu mã hóa Unicode (thường gặp với thư mục tạo trên máy khác) không khớp được với môn trên LMS.
- Tab **Sắp tới** của một môn có lúc hiện cả hạn nộp của môn khác có tên bắt đầu giống nhau.

## [1.1.7] - 2026-10-03

Bản thử nghiệm: sắp xếp lại phần lớn mã nguồn để dễ thêm tính năng và sửa lỗi hơn. Nếu thấy lỗi lạ, hãy mở issue.

### Bảo mật

- Thêm kiểm tra bảo mật tự động cho mỗi thay đổi và hằng tuần: OSV-Scanner, npm audit, NuGet advisory, gitleaks, zizmor, Semgrep, BinSkim, OpenSSF Scorecard (cùng CodeQL và Dependabot đã có). Quy trình và thời hạn xử lý: [docs/quy-trinh-bao-mat.md](docs/quy-trinh-bao-mat.md).

### Thay đổi

- Cửa sổ hẹp (từ 1024 px trở xuống): thanh điều hướng chỉ còn biểu tượng thay vì bị tràn, mọi trang vẫn mở được.
- Bảng giữ cột chính đủ rộng khi cửa sổ hẹp; tên cột dài có dấu ba chấm và tooltip; các hàng tab không còn xuống hai dòng.
- Đồng bộ xong không còn làm mất cách sắp xếp, vị trí cuộn và dòng đang chọn. Chỉ trang đang mở được làm mới.
- **Luyện tập** giải phóng bộ nhớ khi rời trang 3 phút hoặc khi app thu xuống khay (khoảng 300 MB), mở lại thì tạo lại.
- **Đề ngẫu nhiên** giữ thứ tự câu như trong đề gốc và giữ các câu liên quan đi cùng nhau (trường `group` mới trong Study Pack).
- Menu chuột phải ở **Luyện tập** thống nhất tên lệnh; tiêu đề chương có menu chuột phải.
- Đồng bộ bỏ qua lượt khi mất mạng thay vì báo lỗi, có mạng lại hoặc máy thức dậy thì đồng bộ sớm; chế độ Tiết kiệm pin thì giãn chu kỳ.
- Thông báo lỗi đồng bộ dựa trên loại lỗi (hết phiên, mất mạng, máy chủ lỗi...), có thêm bản tiếng Anh.
- Một phần dữ liệu MyBK lỗi thì giữ dữ liệu cũ của phần đó thay vì để trống. Dữ liệu đọc không được thì báo rõ thay vì để trang trống.
- Cài đặt: ô nhắc hạn nhập 0 để tắt mốc nhắc thứ hai; chu kỳ đồng bộ MyBK tối thiểu 6 giờ.
- File log ghi đủ chi tiết lỗi và thông tin môi trường để dễ gửi khi báo lỗi.

### Sửa lỗi

- Phiên MyBK hết hạn thì đồng bộ MyBK treo 45 giây rồi báo "MyBK không phản hồi" (lỗi ở 1.1.5, 1.1.6). App giờ tự đăng nhập lại qua SSO như trước.
- Trang trống không có dòng giải thích: các tab ở **Môn học**, **Lịch** > **Sắp tới**, **Lịch thi**, ô **Môn học** ở **Luyện tập**.
- Chữ bị cắt: ô "giữ đăng nhập" ở **Cài đặt**, dòng báo lỗi trong hộp **Tải tài liệu**; thanh trạng thái đẩy mất tên kỳ thi và số phiên bản.
- GPA lẫn dấu chấm và dấu phẩy; cột học phí chưa định dạng tiền; chép ở **Lịch** không báo "Đã chép".
- **Luyện tập**: lỗi khi nhập, lưu hay gỡ gói không còn bị im lặng; gợi ý trong ô nhập không còn ghi tên chương của môn khác.
- Lưu một file đang mở trong ứng dụng khác (vd. PDF) không còn làm hỏng cả lượt đồng bộ LMS.
- Cửa sổ nhớ vị trí theo đúng màn hình đang dùng khi có nhiều màn hình.
- App tự gọi giữ phiên SSO ngay sau khi vừa đồng bộ MyBK xong (thừa).

## [1.1.6] - 2026-10-03

### Sửa lỗi

- Đồng bộ MyBK báo "Trang đăng ký môn không có bảng đợt đăng ký". Lần đầu vào hệ thống đăng ký, MyBK trả một trang trung gian rồi mới chuyển sang trang có bảng; app đọc quá sớm. Giờ app chờ và mở lại trang khi chưa thấy bảng.
- Mỗi bài tập LMS hiện hai lần ở **Hôm nay**, **Lịch** và trong thông báo nhắc hạn, nên số "hạn nộp trong 7 ngày" bị gấp đôi.
- Bài tập đã nộp vẫn bị nhắc hạn. App giờ dựa vào lịch của Moodle: Moodle gỡ mốc của bài đã nộp, nên app cũng không đếm và không nhắc bài đó nữa. Đề và file đính kèm vẫn được lưu về máy như trước.

## [1.1.5] - 2026-10-03

### Sửa lỗi

- Đang ở **Luyện tập** thì phím tắt của app (Ctrl+1 tới Ctrl+7, F5, Alt+Left) không chạy, và link sang trang khác của app không mở.
- Đồng bộ MyBK báo lỗi "MyBK chuyển sang trang ngoài trường (mybk.hcmut.edu.vn/app/login)" khi phiên MyBK hết hạn. MyBK chuyển về trang đăng nhập qua http, app chặn nhầm. Giờ app đổi sang https và tự đăng nhập lại qua SSO.
- **Lịch** > **Thời khóa biểu** dạng lưới: tuần không có buổi học Thứ 7, CN thì mất hai cột này, dù nhãn tuần vẫn tính tới CN. Giờ lưới luôn đủ 7 ngày. Thanh cuộn không còn che cột cuối.

## [1.1.4] - 2026-10-03

### Thêm

- Tab **Đăng ký và học vụ** có bảng **Giảng viên và lịch dạy kỳ này**, nhóm theo giảng viên: môn, mã, nhóm lớp, thứ, giờ, phòng ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)). Cột giảng viên và giờ học chuyển từ bảng **Kết quả đăng ký** sang bảng này.
- Đăng nhập lại qua nút **Mở MyBK** / **Mở LMS** trên thanh báo lỗi xong thì app tự đồng bộ lại nguồn đó, không cần bấm **Đồng bộ** ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- **Luyện tập**: menu chuột phải ở gói quiz, môn và bài có đủ thao tác: mở, tạo câu, nhập vào môn, **Xuất để chia sẻ**, gỡ.

### Thay đổi

- **Luyện tập**: nút **Soạn** đổi tên thành **Tạo**; **Tạo**, **Nhập**, **Xuất** là ba nút riêng.
- Chữ trong giao diện bỏ các ký tự trang trí (dấu chấm giữa, dấu ba chấm, mũi tên), dùng dấu phẩy và chữ thường.

- Bản tiếng Anh của tài liệu chuyển vào thư mục `docs/en/`; gốc repo chỉ còn bản tiếng Việt. Trang **Giới thiệu** mở `docs/en/PRIVACY.md` khi giao diện tiếng Anh.
- Tài liệu (README, PRIVACY, SECURITY, CONTRIBUTING, CHANGELOG, quy tắc ứng xử, studypack) dùng hàng badge **Tiếng Việt** | **English** ở đầu để chuyển ngôn ngữ, theo mẫu multilanguage-readme-pattern.

### Sửa lỗi

- Hộp **Tải tài liệu** không mở được ở bản 1.1.3.
- **Luyện tập**: **Gỡ quiz này**, **Xóa câu**, cập nhật hoặc thay gói đã có không chạy (hộp hỏi xác nhận không hiện). Giờ hiện hộp thoại Windows.
- **Luyện tập**: tab đang chọn (Tạo, Nhập, Xuất, Nhờ AI) không có dấu hiệu nào khác tab còn lại.
- **Lịch** > **Sắp tới**: menu chuột phải có hai mục mở giống nhau.
- Bảng đợt đăng ký môn trống khi MyBK chuyển về trang chủ thay vì trang đăng ký. App mở lại đúng trang, báo lỗi nếu vẫn không có bảng, và không lưu kết quả trống.

## [1.1.3] - 2026-10-03

### Thêm

- **Lịch** > **Thời khóa biểu** có dạng **lưới tuần** giống Google Calendar ([#5](https://github.com/xeroz369/bk-study-desk/issues/5)). Lưới tự canh bố cục: Thứ 7, CN chỉ hiện khi có buổi học; khung giờ theo buổi sớm nhất và muộn nhất; buổi trùng giờ chia đôi cột. Dạng danh sách vẫn còn, chọn ở ô **Kiểu xem**.
- **Xuất lịch (.ics)**: lưu thời khóa biểu cả kỳ và lịch thi ra file iCalendar để nhập vào Google Calendar, Outlook, Lịch của Windows.
- Đồng bộ LMS/MyBK lỗi thì thanh báo có thêm nút **Mở MyBK** / **Mở LMS** để xem trang đang lỗi gì hoặc đăng nhập lại ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- **Tải tài liệu** chọn được loại file: PDF, slide (.ppt, .pptx), khác; số file và dung lượng tính theo lựa chọn ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- Bảng **Kết quả đăng ký** có thêm cột giảng viên và giờ học, lấy từ thời khóa biểu MyBK ([#6](https://github.com/xeroz369/bk-study-desk/issues/6)).
- Tài liệu tiếng Anh tách thành file riêng, dịch đầy đủ: `README.en.md`, `PRIVACY.en.md`, `SECURITY.en.md`, `CODE_OF_CONDUCT.en.md`, `CONTRIBUTING.en.md`, `CHANGELOG.en.md`, tài liệu studypack, và các trang Wiki tiếng Anh. Chuẩn viết tiếng Anh: `docs/english-style.md`.

### Thay đổi

- README và Wiki: khuyên dùng bộ cài trên GitHub trước, Microsoft Store sau (bản Store phải chờ Microsoft duyệt nên thường chậm hơn).

### Sửa lỗi

- Thời khóa biểu dạng danh sách có nhóm "Thứ 0" với giờ lạ (ví dụ "0:00–6:50"). Môn không có giờ cố định giờ ghi riêng ở dòng "Không có giờ cố định".

## [1.1.2] - 2026-10-03 [YANKED]

Đã gỡ khỏi trang phát hành. Mọi thay đổi chuyển sang 1.1.3.

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
- Thanh trạng thái hiện bước đang làm khi đồng bộ (ví dụ "LMS: đang đọc quiz...", "đang kiểm tra lớp... (3/5)").
- InfoBar báo lỗi theo chuẩn Windows: biểu tượng theo mức độ, câu dễ hiểu kèm cách xử lý, mục **Chi tiết** chứa chữ gốc của máy chủ (có nút **Sao chép**). Thanh trạng thái có biểu tượng lỗi cạnh LMS/MyBK.
- Cửa sổ LMS/MyBK báo khi trang tải quá 10 giây, mất mạng, máy chủ lỗi hoặc phiên đăng nhập đã hết, kèm nút **Thử lại**.
- Giữ phiên đăng nhập khi app đang mở: mỗi 60 phút ghé SSO một lần (tắt được trong Cài đặt). Phiên SSO của trường vẫn hết sau tối đa 8 giờ kể từ lúc đăng nhập.
- **Cài đặt** > **Nhật ký hoạt động**: nút mở file log; bật ghi log chẩn đoán khi cần báo lỗi (tự tắt sau 7 ngày).

### Thay đổi

- Mặc định chỉ đọc môn của học kỳ này và không tự tải tài liệu. Muốn tải thì vào **Môn học** > **Tải tài liệu** và chọn mục cần tải; bật lại tự tải trong Cài đặt nếu muốn.
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

[Chưa phát hành]: https://github.com/xeroz369/bk-study-desk/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.11...v1.2.0
[1.1.11]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.10...v1.1.11
[1.1.10]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.9...v1.1.10
[1.1.9]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.8...v1.1.9
[1.1.8]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.7...v1.1.8
[1.1.7]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.6...v1.1.7
[1.1.6]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.5...v1.1.6
[1.1.5]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.4...v1.1.5
[1.1.4]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.3...v1.1.4
[1.1.3]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.1...v1.1.3
[1.1.2]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/xeroz369/bk-study-desk/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/xeroz369/bk-study-desk/compare/v1.0.6...v1.1.0
[1.0.6]: https://github.com/xeroz369/bk-study-desk/releases/tag/v1.0.6
