# BK Study Desk

**Tiếng Việt** · [English](#english)

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

Giao diện mặc định tiếng Việt, có thêm tiếng Anh.

**Hướng dẫn chi tiết:** [Wiki](../../wiki) (cài đặt, cập nhật, gỡ app, luyện tập, câu hỏi thường gặp).

## Cài đặt

**Cách 1: bộ cài trên GitHub (khuyên dùng).** Bản mới có ở đây ngay khi phát hành, và app tự báo khi có bản sau.

1. Tải `BKStudyDesk-x.y.z-Setup-x64.exe` ở [Releases](../../releases/latest) (máy chip ARM: bản `-Setup-arm64.exe`).
2. Mở file, chọn thư mục cài, chọn **Cài đặt**. Không cần quyền admin. Bộ cài chưa có chữ ký số nên Windows hiện cảnh báo SmartScreen: chọn **Thông tin thêm** → **Vẫn chạy** (chỉ một lần).
3. Lần mở đầu, chọn cách cập nhật và thư mục lưu tài liệu, rồi **Đăng nhập HCMUT**.

**Cách 2: Microsoft Store.** [BK Study Desk trên Microsoft Store](https://apps.microsoft.com/detail/9NX0KRZRR850): không có cảnh báo SmartScreen. Mỗi bản mới phải chờ Microsoft duyệt (có khi vài ngày), nên bản Store thường chậm hơn bản trên GitHub. So số phiên bản ở trang Store với [Releases](../../releases/latest) trước khi cài.

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
- **Lịch**:
  - việc sắp tới theo ngày, lịch thi;
  - thời khóa biểu dạng **lưới tuần** (giống Google Calendar) hoặc danh sách;
  - **Xuất lịch (.ics)…**: lưu thời khóa biểu cả kỳ và lịch thi ra file để nhập vào Google Calendar, Outlook, Lịch của Windows.
- **Môn học**:
  - duyệt thư mục từng môn kiểu File Explorer;
  - xem file mới, deadline, thông báo, sổ điểm và các lớp trên LMS.
  - **Tải tài liệu theo từng mục** của lớp trên LMS, chọn loại file (PDF, slide, khác), tùy chọn tự giải nén .zip/.rar/.7z.
  - Bật tự tải: mỗi lần đồng bộ, app tải file mới (cả đề và file đính kèm bài tập), LMS chậm vẫn mở được.
- **Điểm và học vụ**:
  - bảng điểm theo học kỳ kèm điểm thành phần;
  - tiến độ CTĐT theo khối kiến thức;
  - sổ điểm LMS, kết quả đăng ký môn (kèm giảng viên và giờ học), ngày CTXH, quyết định học vụ.
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

<details>
<summary><b>Show the English README</b></summary>

![BK Study Desk](docs/hero.png)

A small Windows app for students of **Ho Chi Minh City University of Technology (HCMUT)**. It brings **BK-LMS** and **MyBK** together in one window:
- deadlines and quizzes;
- timetable and exam schedule;
- course documents, downloaded section by section when you choose;
- grades (LMS gradebook and MyBK component scores);
- curriculum progress;
- **practice**: write your own quizzes, review LMS quizzes you have submitted, take mock exams;
- shortcuts to about 30 university services.

> **Unofficial.** This is a personal project, not affiliated with or endorsed by HCMUT or Moodle HQ. The app uses your own account and only reads what you can already see after signing in. Every address and API the app calls is listed on the Wiki page [How the app works](../../wiki/Cách-app-hoạt-động) (Vietnamese). If the university asks, the project will change or remove the feature concerned.

The interface is Vietnamese by default; English is available in **Settings**. The Wiki is written in Vietnamese.

## Install

**Option 1: installer from GitHub (recommended).** New versions appear here as soon as they are released, and the app tells you when the next one is out.

1. Download `BKStudyDesk-x.y.z-Setup-x64.exe` from [Releases](../../releases/latest) (ARM laptops: `-Setup-arm64.exe`).
2. Run it, choose an install folder and click **Install**. No admin rights needed. The installer is not code-signed yet, so Windows SmartScreen shows a warning once: click **More info** > **Run anyway**.
3. On first launch, choose how updates are handled and where documents are saved, then **Sign in to HCMUT**.

**Option 2: Microsoft Store.** [BK Study Desk on the Microsoft Store](https://apps.microsoft.com/detail/9NX0KRZRR850): no SmartScreen warning. Every new version waits for Microsoft review (sometimes a few days), so the Store version is usually behind GitHub. Compare the version number with [Releases](../../releases/latest) before installing.

The two are built from the same source but are separate installs with separate data. Install only one.

### After installing

Open the app, choose **Sign in to HCMUT** and sign in on the university SSO page. One sign-in covers both LMS and MyBK.

**Requirements:**
- Windows 10 1809 or later, or Windows 11 (x64 or ARM64).
- Microsoft Edge WebView2 Runtime (included in Windows 11).
- No .NET installation needed.

## Features

- **Today**:
  - countdown to the next exam;
  - deadlines in the next 7 days and quizzes;
  - new LMS announcements.
- **Calendar**:
  - upcoming items by day, exam schedule;
  - timetable as a **week grid** (like Google Calendar) or a list;
  - **Export calendar (.ics)…**: save the whole-term timetable and exams to a file you can import into Google Calendar, Outlook or Windows Calendar.
- **Subjects**:
  - browse each subject's folder like File Explorer;
  - see new files, deadlines, announcements, gradebook and classes on LMS;
  - **download documents section by section**, filter by file type (PDF, slides, other), optionally extract .zip/.rar/.7z;
  - optional auto-download: each sync fetches new files (including assignment attachments), so they open even when LMS is slow.
- **Grades & records**:
  - transcript by term with component scores;
  - curriculum progress by knowledge block;
  - LMS gradebook, course registration results (with lecturer and class times), community service days, academic decisions.
- **Practice**: quizzes for revision only; nothing affects your LMS grades.
  - **Write quizzes** per subject, chapter and lesson, with the 5 LMS question types (single choice, multiple choice, true/false, numeric, short answer), images and formulas.
  - **Ask AI**: the app writes the prompt; paste the AI's answer back into the app.
  - **Import and export** quizzes as Markdown (`.md`, or `.zip` with images), Moodle XML, GIFT, Aiken. Format: [`studypack/`](studypack/).
  - **Keep submitted LMS quizzes** for later review, even when LMS is slow (can be turned off). The app only reads the review page after you submit and never touches a quiz in progress. Closed quizzes can be shared.
  - **Review**: "Review today" brings back questions you often miss and spaces them out once you know them; random mock exams, flag questions, private notes.
- **Services**: open MyBK, LMS, BKPay, course registration… inside the app with the same sign-in.
- **Single sign-on**: sign in once to HCMUT SSO for both LMS and MyBK; while the session is valid the app signs back in by itself.
- **Behaves like a Windows app**:
  - Windows 11 (Fluent) theme that follows light/dark mode and your accent color;
  - sortable tables, right-click menus;
  - shortcuts Ctrl+1…7, F5, Alt+←;
  - system tray icon and deadline reminders;
  - start with Windows;
  - the close button minimizes to the tray or exits, your choice.
- **Choose the documents folder** in Settings.
- **Languages**: Vietnamese (source) and English. To add one, drop a JSON file into `lang\`; see [`src/SoHocTap/lang/README.md`](src/SoHocTap/lang/README.md).

## Security and privacy

Full policy: [PRIVACY.en.md](PRIVACY.en.md).

- Your password is typed only on the university SSO page. The app never reads it. If you choose **Save** when the sign-in window asks, the in-app browser (WebView2) stores the password encrypted on your PC, like Edge, to fill it in next time; otherwise nothing is saved.
- Everything lives in the data folder `%LOCALAPPDATA%\BKStudyDesk.Data\data`, not in the install folder:
  - cookies in `data\webview`, encrypted by WebView2;
  - the LMS token in `data\secrets\`, **encrypted with Windows DPAPI**, so it does not work on another PC or Windows account.
- The data folder sits in `%LOCALAPPDATA%`, readable only by your Windows account (plus SYSTEM and Administrators).
- **Stay signed in after closing the app** keeps cookies for at most 8 hours, the SSO server's session limit; it can be turned off in Settings.
- The LMS token comes from Moodle's built-in sign-in flow for the mobile app (`launch.php`, service `moodle_mobile_app`, URL scheme `moodlemobile` of the official Moodle app). Details on the Wiki page [How the app works](../../wiki/Cách-app-hoạt-động).
- Limit: malware running under your own Windows account can still read the session. This is true of any Windows app. If you suspect malware, choose **Sign out** and change your HCMUT password.
- The app only calls **read** APIs and sends data nowhere except the university servers (GitHub is asked for new versions only if you allow it). It never submits work, registers, cancels, pays or takes quizzes for you.
- The app does not store your ID card number, address, phone number, date of birth or personal email, even when MyBK returns them.
- It calls the university servers as little as possible:
  - nothing before you sign in;
  - LMS every 3 hours, only this term's classes and only what changed (usually 15–30 requests);
  - MyBK every 12 hours;
  - 300 ms between requests; when a server says it is busy, the app waits per `Retry-After`;
  - after an error it waits for the next cycle before retrying by itself (you can still choose **Retry**).

## Build from source

```powershell
cd src/ui; npm ci; npm run build; cd ../..     # Practice view (Svelte), builds into ui/
dotnet build src/SoHocTap -c Release
dotnet publish src/SoHocTap -c Release -r win-x64 --self-contained -o publish
```

Needs the .NET 10 SDK, Node 24 and Windows. Code layout:

| Folder | Contents |
|---|---|
| `src/SoHocTap/Core` | paths, config (`Core/DefaultConfig.json`), JSON storage, log, credential encryption, .ics export |
| `src/SoHocTap/Sources` | LMS connector (Moodle mobile web service), MyBK, sync scheduler |
| `src/SoHocTap/Files` | subject folders, duplicate detection, archive extraction |
| `src/SoHocTap/Shell` | sign-in, hidden WebView for MyBK, tray, notifications, autostart |
| `src/SoHocTap/Ui` | WPF UI (`MainWindow`, `Pages/`), language packs |
| `src/SoHocTap/lang` | language packs (`vi.json`, `en.json`) |
| `src/SoHocTap/Updates` | update check and install (Velopack), per the user's choice |
| `src/BKStudyDesk.Setup` | Vietnamese installer and uninstaller (.NET Framework 4.8) |
| `src/ui` | Practice view (Svelte) |
| `tests/SoHocTap.Tests` | unit tests |

University URLs, API paths, folder names and sync intervals live in config (`Core/DefaultConfig.json`), not scattered in code. `data\config.json` stores only what you changed from the defaults.

## Contributing

See [CONTRIBUTING.en.md](CONTRIBUTING.en.md) and the [code of conduct](CODE_OF_CONDUCT.en.md). Keep the **read-only** rule: no feature may submit work or change data on university systems.

## Contact

- Bugs and ideas: [GitHub Issues](https://github.com/xeroz369/bk-study-desk/issues). Issues are public; do not paste student IDs, grades or tokens.
- Security vulnerabilities: report privately per [SECURITY.en.md](SECURITY.en.md).
- Official project contact (privacy, licensing, private matters): [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).

## License

[PolyForm Noncommercial 1.0.0](LICENSE.md) (since 1.0.6; earlier versions were MIT). The source is public, **not for commercial use**:
- **Allowed:** use, modify and share for personal use, study, research, schools and nonprofits. Keep the author line (Required Notice) in LICENSE.
- **Not allowed:** selling the app or a modified version, or including it in a paid product or service.

By contributing code (a pull request) you agree to license your contribution under the same terms.

Third-party libraries keep their own licenses: NuGet packages in the `.csproj` files, npm packages in `src/ui/package.json` (mostly MIT), MathJax bundled in `src/ui/public/vendor/mathjax` (Apache-2.0, LICENSE included).





</details>
