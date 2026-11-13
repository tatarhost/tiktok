# TikTok Mod PC

This is a Windows x64 launcher for the Android APK released as v46.4.3 by namecallfilter/tiktokmodcloud.

It downloads the APK, checks its SHA-256 against the GitHub release digest, installs it into an existing BlueStacks 5 instance through the bundled ADB executable, and launches the installed app.

This is not a native Windows port: the real Android application runs inside BlueStacks. The linked GitHub repository contains a TypeScript download CLI, not TikTok's Android source code. A true native port would require the original application source and a Windows-specific implementation.

Requirements:
- Windows 10/11 x64
- BlueStacks 5 installed
- ADB enabled in BlueStacks Settings → Advanced → Android Debug Bridge
- Internet and around 502 MB of free space for the APK (plus emulator storage)

The APK is an unofficial third-party modification. A matching SHA-256 only verifies that the download matches the release file; it does not prove the app is safe. Do not sign into an important account in software you have not independently trusted.
