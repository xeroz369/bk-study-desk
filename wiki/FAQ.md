[Tiếng Việt](Câu-hỏi-thường-gặp) · **English**

> Translated from Câu-hỏi-thường-gặp (Vietnamese) for BK Study Desk 1.1.3.

**Windows says "Windows protected your PC".**
The GitHub installer isn't code-signed yet (a certificate costs a yearly fee). Select **More info** > **Run anyway**. This appears only once, when installing; later updates don't ask again. To make sure the file wasn't altered, compare its SHA-256 as described in [Installation](Installation).

**Opening LMS/MyBK in the app shows "session timed out" or asks me to sign in again.**
The university server ends the session after a few hours, even though the app remembers you. The app goes through the SSO sign-in page by itself; if the SSO session has also ended, sign in once more.

**Where is the app's data?**
In `%LOCALAPPDATA%\BKStudyDesk.Data`, readable only by your Windows account (plus SYSTEM and Administrators). Course documents are in the folder you chose (by default `Documents\BK Study Desk`).

**Does the app submit work, register for courses or do anything on my account?**
No. The app only calls **read** APIs. It never submits work, registers for or cancels courses, pays, or takes quizzes for you.

**Does the app send my data anywhere?**
It has no server of its own and no analytics. It only talks to the university's servers, and to GitHub to check for new versions if you allow it. See [Security and privacy](Security-and-privacy).

**Is there a macOS or Linux version?**
Not yet. A cross-platform version is in progress; it will appear on the Releases page when it's stable.

**Does it work on ARM (Snapdragon) laptops?**
Yes. Download `-Setup-arm64.exe`.

**Can I use the timetable in Google Calendar?**
Yes. Go to **Calendar** > **Timetable** > **Export calendar (.ics)**, save the file, then in Google Calendar open **Settings** > **Import & export** and choose that file. Importing into a separate calendar makes it easy to remove next term.

**Where do I report a bug?**
In [Issues](https://github.com/xeroz369/bk-study-desk/issues/new/choose). Don't paste passwords, student IDs or tokens. Report security vulnerabilities privately; see [SECURITY.en.md](https://github.com/xeroz369/bk-study-desk/blob/main/SECURITY.en.md). For private matters: [bkstudydesk@xerozsoft.com](mailto:bkstudydesk@xerozsoft.com).
