[Tiếng Việt](Cài-đặt) · **English**

> Translated from Cài-đặt (Vietnamese) for BK Study Desk 1.1.2.

**Requirements:** Windows 10 1809 or later, or Windows 11, x64 or ARM64. No .NET installation needed. Microsoft Edge WebView2 Runtime is required (included in Windows 11).

## Option 1: installer from GitHub (recommended)

New versions appear here as soon as they're released, and the app tells you when the next one is out.

1. Go to [Releases](https://github.com/xeroz369/bk-study-desk/releases/latest) and download `BKStudyDesk-x.y.z-Setup-x64.exe`. ARM laptops (Surface Pro X, Snapdragon): download `-Setup-arm64.exe`.
2. Run it. If Windows shows **"Windows protected your PC"**, select **More info** > **Run anyway**. The warning appears because the installer isn't code-signed yet; it shows only once, when installing.
3. Choose the install folder (by default inside your user account; drive D: works too) and select **Cài đặt** (Install). No admin rights needed. The installer is in Vietnamese.
4. The app opens when installation finishes. Shortcuts are on the desktop and in the Start menu.

![Installer](images/bo-cai.png)

## Option 2: Microsoft Store

Open [BK Study Desk on the Microsoft Store](https://apps.microsoft.com/detail/9NX0KRZRR850) and select **Get**. Microsoft signs the package, so there's no SmartScreen warning. The Store has only an x64 package (ARM laptops run it through Windows emulation).

Every new version waits for Microsoft review (sometimes a few days), so the Store version usually lags behind GitHub. Compare the version number on the Store page with [Releases](https://github.com/xeroz369/bk-study-desk/releases/latest) before installing.

The two are separate installs with separate data. Install only one.

## First launch

- Choose the **folder for course documents** (a drive other than C: is suggested if you have one).
- Choose how to handle **updates**; see [Updating](Updating).
- Select **Sign in to HCMUT**. One sign-in covers both LMS and MyBK.

## Using the old zip version?

Install the new version, then go to **Settings** > **Updates** > **Import data from the zip version** and choose the zip version's folder. The app copies your LMS sign-in, practice results, quizzes and settings, then restarts. Sign in to HCMUT once more afterwards. You can then delete the zip version's folder.

## Verify the download (optional)

Open PowerShell in the folder that contains the file:
```powershell
(Get-FileHash .\BKStudyDesk-x.y.z-Setup-x64.exe -Algorithm SHA256).Hash
```
Compare it with the matching line in `SHA256SUMS.txt` on the Releases page.

For a stronger check (requires [GitHub CLI](https://cli.github.com)), confirm the installer was built from this exact source on GitHub and not altered along the way:
```powershell
gh attestation verify .\BKStudyDesk-x.y.z-Setup-x64.exe --repo xeroz369/bk-study-desk
```
