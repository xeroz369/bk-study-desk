[Tiếng Việt](Gỡ-cài-đặt) · **English**

> Translated from Gỡ-cài-đặt (Vietnamese) for BK Study Desk 1.1.3.

Go to Windows **Settings** > **Apps** > **BK Study Desk** > **Uninstall** (or right-click the app in Start and select **Uninstall**). The uninstaller is in Vietnamese.

![Uninstaller](images/bo-go.png)

- **Xóa cả dữ liệu của app** (Delete app data: sign-in session, practice results, your quizzes, settings) is **not selected** by default, so you can reinstall and continue. Select it to remove everything.
- Downloaded course documents are **never deleted**. The uninstaller has a button to open that folder if you want to delete them yourself.
- If the app is running, the uninstaller closes it.
- Afterwards, the install folder, shortcuts and the start-with-Windows entry are gone.

**Uninstall without the window** (for scripted installs): run `Uninstall.exe --uninstall --silent` in the install folder; add `--delete-data` to delete the app data too.
