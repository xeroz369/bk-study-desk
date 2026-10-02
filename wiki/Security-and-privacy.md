[Tiếng Việt](Bảo-mật-và-quyền-riêng-tư) | **English**

> Translated from Bảo-mật-và-quyền-riêng-tư (Vietnamese) for BK Study Desk 1.1.3.

- Your password is typed only on the university SSO page; the app never reads it. If you choose **Save** when asked, WebView2 stores it encrypted on your PC for autofill.
- The LMS token is encrypted with Windows DPAPI; cookies are encrypted by WebView2. Data lives in `%LOCALAPPDATA%`, readable only by your Windows account (plus SYSTEM and Administrators).
- The app only **reads**: it never submits, registers, cancels or pays. It calls the university servers as little as possible (LMS every 3 hours, about 15–30 requests; MyBK every 12 hours). Full list of addresses and APIs: [How the app works](How-the-app-works).
- It doesn't store your ID card number, address, phone number, date of birth or personal email.
- No server of its own, no analytics, no ads. GitHub is asked about new versions only if you allow it.
- Update packages are verified with SHA-256; quizzes from other people have their HTML sanitized and run no scripts.

Details: [PRIVACY.en.md](https://github.com/xeroz369/bk-study-desk/blob/main/PRIVACY.en.md) | Report a vulnerability: [SECURITY.en.md](https://github.com/xeroz369/bk-study-desk/blob/main/SECURITY.en.md).
