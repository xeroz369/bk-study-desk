# BK Study Desk

![BK Study Desk](docs/hero.png)

App Windows nhỏ gọn cho sinh viên **Bách Khoa TP.HCM (HCMUT)**. App gom **BK-LMS** và **MyBK** vào một cửa sổ:
- deadline, quiz;
- thời khóa biểu, lịch thi;
- tài liệu từng môn, chọn mục nào thì tải xuống máy mục đó;
- điểm (sổ điểm LMS lẫn điểm thành phần trên MyBK);
- tiến độ chương trình đào tạo;
- **luyện tập**: tự soạn quiz, ôn lại quiz LMS đã nộp, thi thử;
- lối tắt tới khoảng 30 dịch vụ của trường.

> **Không chính thức.** Đây là dự án cá nhân, không liên quan và không được Trường Đại học Bách khoa hay Moodle HQ xác nhận. App dùng tài khoản của chính bạn và chỉ đọc những gì bạn vốn xem được sau khi đăng nhập. Mọi địa chỉ và API app gọi được liệt kê ở Wiki [Cách app hoạt động](../../wiki/Cách-app-hoạt-động). Nếu trường yêu cầu, dự án sẽ thay đổi hoặc dừng tính năng tương ứng.

Giao diện mặc định tiếng Việt, có thêm tiếng Anh. *English summary at the bottom.*

**Hướng dẫn chi tiết:** [Wiki](../../wiki) (cài đặt, cập nhật, gỡ app, luyện tập, câu hỏi thường gặp).

## Cài đặt

**Cách 1: Microsoft Store (khuyên dùng).** Mở [BK Study Desk trên Microsoft Store](https://apps.microsoft.com/detail/9NX0KRZRR850), chọn **Tải**. Không có cảnh báo SmartScreen vì Microsoft ký gói; Store tự cập nhật app.

**Kiểm tra số phiên bản trước khi cài.** Mỗi bản mới phải chờ Microsoft duyệt (thường vài giờ, có khi tới 3 ngày làm việc), nên bản trên Store có thể chậm hơn bản mới nhất trên GitHub. So số phiên bản ở trang Store với [Releases](../../releases/latest); cần bản mới ngay thì dùng bộ cài GitHub.

**Cách 2: bộ cài trên GitHub.**

1. Tải `BKStudyDesk-x.y.z-Setup-x64.exe` ở [Releases](../../releases) (máy chip ARM: bản `-Setup-arm64.exe`).
2. Mở file, chọn thư mục cài, chọn **Cài đặt**. Không cần quyền admin. Bộ cài chưa có chữ ký số nên Windows hiện cảnh báo SmartScreen: chọn **Thông tin thêm** → **Vẫn chạy** (chỉ một lần).
3. Lần mở đầu, chọn cách cập nhật và thư mục lưu tài liệu, rồi **Đăng nhập HCMUT**.

Hai cách cài dùng chung mã nguồn nhưng là hai bản riêng, dữ liệu không dùng chung. Chỉ nên cài một trong hai.

![Bộ cài](docs/bo-cai.png)

Hướng dẫn chi tiết trên **[Wiki](../../wiki)**: [Cài đặt](../../wiki/Cài-đặt) · [Cập nhật](../../wiki/Cập-nhật) · [Gỡ cài đặt](../../wiki/Gỡ-cài-đặt) · [Luyện tập](../../wiki/Luyện-tập) · [Câu hỏi thường gặp](../../wiki/Câu-hỏi-thường-gặp).

### Sau khi cài

Mở app, chọn **Đăng nhập HCMUT**, rồi đăng nhập trên trang SSO của trường. Một lần đăng nhập dùng được cho cả LMS và MyBK.

**Yêu cầu:**
- Windows 10 1809 trở lên, hoặc Windows 11 (x64 hoặc ARM64).
- Microsoft Edge WebView2 Runtime (Windows 11 có sẵn).
- Không cần cài .NET.

## Tính năng

- **Hôm nay**:
  - đếm ngược tới kỳ thi gần nhất;
  - deadline trong 7 ngày và quiz;
  - thông báo mới trên LMS.
- **Lịch**: việc sắp tới theo ngày, thời khóa biểu tuần, lịch thi.
- **Môn học**:
  - duyệt thư mục từng môn kiểu File Explorer;
  - xem file mới, deadline, thông báo, sổ điểm và các lớp trên LMS.
  - **Tải tài liệu theo từng mục** của lớp trên LMS, tùy chọn tự giải nén .zip/.rar/.7z.
  - Bật tự tải: mỗi lần đồng bộ, app tải file mới (cả đề và file đính kèm bài tập), LMS chậm vẫn mở được.
- **Điểm và học vụ**:
  - bảng điểm theo học kỳ kèm điểm thành phần;
  - tiến độ CTĐT theo khối kiến thức;
  - sổ điểm LMS, kết quả đăng ký môn, ngày CTXH, quyết định học vụ.
- **Luyện tập**: tự làm quiz để ôn bài, không ảnh hưởng gì tới điểm trên LMS. **Hướng dẫn từng bước có ảnh: [Wiki** → **Luyện tập](../../wiki/Luyện-tập).**
  - **Tự soạn quiz** cho từng môn, chương, bài. Có đủ 5 dạng câu như quiz LMS (chọn một, chọn nhiều, đúng/sai, điền số, điền chữ), chèn được ảnh và công thức.
  - **Nhờ AI soạn**: app viết sẵn câu lệnh cho AI, bạn chỉ cần dán câu trả lời của AI vào app.
  - **Nhập và xuất** quiz dạng Markdown (`.md` hoặc `.zip` nếu có ảnh), Moodle XML, GIFT, Aiken. Định dạng file xem ở [`studypack/`](studypack/).
  - **Lưu lại quiz LMS đã nộp** để xem lại sau, kể cả lúc LMS chậm (tắt được trong Cài đặt). App chỉ đọc trang xem lại sau khi bạn nộp, không đụng tới quiz đang làm. Quiz đã đóng thì chia sẻ được.
  - **Ôn tập**: mục "Ôn hôm nay" nhắc lại những câu bạn hay sai, nhớ rồi thì giãn lịch ra; có đề thi thử ngẫu nhiên, đánh dấu câu nghi sai và ghi chú riêng.
- **Dịch vụ**: mở MyBK, LMS, BKPay, đăng ký môn… ngay trong app, dùng chung một lần đăng nhập.
- **Đăng nhập một lần**: SSO HCMUT một lần là vào được cả LMS và MyBK. Phiên đăng nhập còn hạn thì app tự đăng nhập lại.
- **Dùng như app Windows thật**:
  - theme Windows 11 (Fluent), tự theo sáng/tối và màu nhấn;
  - bảng sắp xếp được, menu chuột phải;
  - phím tắt Ctrl+1…7, F5, Alt+←;
  - biểu tượng ở khay hệ thống, nhắc deadline;
  - tự chạy cùng Windows;
  - chọn X thì thu xuống khay hệ thống hoặc thoát hẳn, tùy bạn chọn.
- **Chọn thư mục lưu tài liệu** trong Cài đặt.
- **Ngôn ngữ**: tiếng Việt (gốc) và tiếng Anh. Muốn thêm ngôn ngữ thì thả một file JSON vào `lang\`, xem [`src/SoHocTap/lang/README.md`](src/SoHocTap/lang/README.md).

## Bảo mật và quyền riêng tư

Chính sách đầy đủ: [PRIVACY.md](PRIVACY.md).

- Mật khẩu chỉ gõ trên trang SSO của trường. App không đọc mật khẩu. Nếu bạn chọn **Lưu** khi cửa sổ đăng nhập hỏi, trình duyệt trong app (WebView2) sẽ lưu mật khẩu (mã hóa trên máy, giống Edge) để tự điền lần sau; không chọn **Lưu** thì không lưu.
- Mọi thứ nằm trong thư mục dữ liệu `%LOCALAPPDATA%\BKStudyDesk.Data\data`, không nằm trong thư mục cài:
  - cookie trong `data\webview`, do WebView2 tự mã hóa;
  - token LMS trong `data\secrets\`, **mã hóa bằng Windows DPAPI**. Chép sang máy khác hay tài khoản Windows khác thì không dùng được.
- Thư mục dữ liệu nằm trong `%LOCALAPPDATA%`, chỉ tài khoản Windows của bạn (cùng SYSTEM và Administrators) mở được.
- **Giữ đăng nhập khi tắt rồi mở lại app** giữ cookie tối đa 8 giờ, bằng giới hạn phiên của máy chủ SSO; tắt được trong Cài đặt.
- Token LMS lấy bằng luồng đăng nhập có sẵn của Moodle dành cho app di động (`launch.php`, service `moodle_mobile_app`, URL scheme `moodlemobile` của app Moodle chính thức). Chi tiết ở Wiki [Cách app hoạt động](../../wiki/Cách-app-hoạt-động).
- Giới hạn: malware chạy dưới chính tài khoản Windows của bạn vẫn có thể đọc được phiên đăng nhập. App nào trên Windows cũng vậy. Nghi máy dính malware thì chọn **Đăng xuất** rồi đổi mật khẩu HCMUT.
- App chỉ gọi API **đọc** và không gửi dữ liệu đi đâu ngoài server của trường (riêng GitHub chỉ được hỏi phiên bản mới khi bạn cho phép). App không bao giờ nộp bài, đăng ký, hủy, thanh toán hay làm quiz thay bạn.
- App không lưu CCCD, địa chỉ, số điện thoại, ngày sinh hay email cá nhân mà MyBK trả về.
- Gọi server trường càng ít càng tốt:
  - chưa đăng nhập thì không gọi gì;
  - LMS mỗi 3 giờ, chỉ đọc lớp học kỳ này và chỉ hỏi phần thay đổi (thường 15–30 request);
  - MyBK đồng bộ 12 giờ một lần;
  - request cách nhau 300 ms; máy chủ báo quá tải thì app chờ theo `Retry-After`;
  - lỗi thì chờ hết chu kỳ mới tự thử lại (bạn vẫn chọn **Thử lại** được).

## Tự build

```powershell
cd src/ui; npm ci; npm run build; cd ../..     # khung Luyện tập (Svelte), build ra ui/
dotnet build src/SoHocTap -c Release
dotnet publish src/SoHocTap -c Release -r win-x64 --self-contained -o publish
```

Cần .NET 10 SDK, Node 24 và Windows. Cấu trúc code:

| Thư mục | Nội dung |
|---|---|
| `src/SoHocTap/Core` | path, config (`Core/DefaultConfig.json`), lưu JSON, log, mã hóa thông tin đăng nhập |
| `src/SoHocTap/Sources` | connector LMS (Moodle mobile web service), MyBK, lịch đồng bộ |
| `src/SoHocTap/Files` | thư mục môn học, tìm file trùng, giải nén |
| `src/SoHocTap/Shell` | login, WebView ẩn chạy MyBK, tray, thông báo, autostart |
| `src/SoHocTap/Ui` | UI WPF (`MainWindow`, `Pages/`), load language pack |
| `src/SoHocTap/lang` | language pack (`vi.json`, `en.json`) |
| `src/SoHocTap/Updates` | kiểm tra và cài bản mới (Velopack), theo chế độ người dùng chọn |
| `src/BKStudyDesk.Setup` | bộ cài và bộ gỡ tiếng Việt (.NET Framework 4.8) |
| `src/ui` | khung Luyện tập (Svelte) |
| `tests/SoHocTap.Tests` | unit test (thư mục app, chính sách cập nhật, chọn thư mục cài) |

URL của trường, API path, tên thư mục và chu kỳ đồng bộ nằm trong config (`Core/DefaultConfig.json`), không rải trong code. `data\config.json` chỉ lưu những gì bạn đổi so với mặc định.

## Đóng góp

Xem [CONTRIBUTING.md](CONTRIBUTING.md) và [quy tắc ứng xử](CODE_OF_CONDUCT.md). Nhớ giữ quy tắc **chỉ đọc**: không tính năng nào được nộp bài hay sửa dữ liệu trên hệ thống của trường.

## Liên hệ

- Báo lỗi, góp ý: [GitHub Issues](https://github.com/xeroz369/bk-study-desk/issues). Issue là công khai, đừng dán MSSV, điểm hay token.
- Lỗ hổng bảo mật: báo riêng theo [SECURITY.md](SECURITY.md).
- Liên hệ chính thức của dự án (quyền riêng tư, giấy phép, việc cần trao đổi riêng): [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).

## Giấy phép

[PolyForm Noncommercial 1.0.0](LICENSE.md) (từ bản 1.0.6; các bản trước theo MIT). Mã nguồn công khai, **không dùng cho mục đích thương mại**:
- **Được:** dùng, sửa, chia sẻ cho cá nhân, học tập, nghiên cứu, trường học, tổ chức phi lợi nhuận. Giữ dòng ghi tác giả (Required Notice) trong LICENSE.
- **Không được:** bán app hay bản sửa, đưa vào sản phẩm hoặc dịch vụ thu tiền.

Đóng góp code (PR) nghĩa là bạn đồng ý phần đóng góp theo cùng giấy phép này.

Thư viện bên thứ ba giữ giấy phép riêng của chúng: gói NuGet trong các file `.csproj`, gói npm trong `src/ui/package.json` (phần lớn MIT), MathJax đi kèm sẵn ở `src/ui/public/vendor/mathjax` (Apache-2.0, có file LICENSE).

---

## English

**BK Study Desk** is an unofficial Windows desktop app for HCMUT students. It combines BK-LMS and MyBK in one window:
- deadlines and quizzes;
- timetable and exam schedule;
- course documents, downloadable section by section;
- grades and curriculum progress;
- practice: make your own quizzes, review LMS quizzes you have submitted (saved locally, can be turned off), mock exams; saved LMS quizzes are shareable once the quiz has closed;
- shortcuts to school services.

**Language:** Vietnamese is the default UI language; English is available in Settings.

**Install:** download `BKStudyDesk-x.y.z-Setup-x64.exe` (or `-arm64.exe`) from Releases, pick an install folder and click Install; no admin rights needed. SmartScreen may warn once because the app is not code-signed: click *More info → Run anyway*. On first launch the app asks how to handle updates (notify, auto-install on exit, or never check); nothing is checked until you choose. Uninstall from Windows Settings → Apps; you choose whether to keep the app data.


**Contact:** bugs and ideas in [Issues](https://github.com/xeroz369/bk-study-desk/issues); security reports per [SECURITY.md](SECURITY.md); official project contact: [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).

**Privacy:** see [PRIVACY.md](PRIVACY.md). The app is read-only. Your password is typed only on the school's SSO page. The LMS token is DPAPI-encrypted, and the data folder is restricted to your Windows account.
