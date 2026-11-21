# Nexus Safe Tweaks

A Windows 10/11 desktop PC tuning app named **Nexus Safe Tweaks**, with opt-in, current-user Windows tweaks and saved mouse DPI presets, plus a separate Focus module. Open **PC Tuner** from the app's left navigation to browse supported tweaks and advanced guidance.

Nexus Safe Tweaks is free and open source under the [MIT License](LICENSE). Read the complete license and first-launch notice before using the app.

## What works in this build

- Seven Windows account-level settings grouped into FPS & Gaming and UI & Responsiveness, with explicit confirmation, pre-change backups, and undo.
- A categorized tweak browser with Ping & Network, Power/USB, Privacy/Security, UI, FPS/Gaming, Mouse/Input, and Maintenance guidance. Ping guidance focuses on measuring the actual route/server; hardware power, security, and servicing changes that can be disruptive are guidance-only.
- Advanced GPU/registry, hosts-file, power, networking, security, and cleanup topics are visibly labeled as guidance only; the app does not silently apply these higher-risk changes.
- Moderate-risk settings show a specific warning followed by a separate apply confirmation.
- A current-user startup manager that can disable selected `Run` entries and restore their exact saved values.
- Local mouse DPI profile storage (100–25,600 DPI); profiles are **not** pushed to a mouse because a device/vendor API is not connected.
- An FPS advisor that offers transparent, rule-based suggestions from the user's selected bottleneck, resolution, and play-style; it does not connect to an AI service, scan games, or guarantee frame-rate gains.
- Official driver/support links for AMD, NVIDIA, Intel, MSI, ASUS, and Gigabyte. Nexus opens vendor pages in the browser and never downloads or installs a driver.
- A conservative debloat guide that opens Windows Installed apps rather than silently removing system components.
- No overclocking controls are included; Nexus does not write firmware or low-level registers.
- A first-launch “AS IS” notice must be accepted and is recorded under `%LOCALAPPDATA%\NexusBooster\`. Startup failures after the .NET app starts are reported and logged locally to `%LOCALAPPDATA%\NexusBooster\startup-error.log`; entries contain only the time, startup stage, exception type, and HRESULT.
- Recovery tools restore Nexus-managed registry backups and can back up then clear local mouse DPI profiles. Failed restores are reported and their backup files are retained; profile backup files can be selected and restored later.
- Nexus installs no Windows service or scheduled task and currently has no installer/uninstaller.
- A dedicated test console project; all build and test outputs are directed to the ignored `artifacts/test-build/` folder.

## Focus Module

The **Focus module** is a separate, optional window; the existing tuning dashboard remains unchanged. It provides one-line task entry, 25-minute focus and Sunday-only 90-minute boss sessions, an idle pause based on Windows' last-input timestamp, a local plain-text scratchpad, progress totals, and an IRL quest. A block is recorded only after its timer completes and the user submits work that receives a critique PASS. Missing a focus day preserves existing totals and requires a 25-minute recovery block before starting another block.

Critique supports Ruthless Code Auditor, Logic & Flaw Hunter, Bloat Cutter (which requires a `Cut: NN%` finding), and Goal Alignment Check. It has no chat/reply field and displays at most five findings. It requires the user to install and start [Ollama](https://ollama.com/) locally, then install the fixed local model:

```powershell
ollama pull qwen2.5:1.5b
```

The module sends critique requests only to `http://127.0.0.1:11434`; it does not call an AI API or automatically download a model. Model responses are not guaranteed to be correct. The in-app `Ctrl+Shift+C` shortcut critiques the work field only while the Focus module is focused; it does not read the clipboard or capture selections from other apps. Progress and scratchpad files stay under `%LOCALAPPDATA%\NexusBooster\FocusModule\`.

The browser helper is a separate, optional Manifest V3 extension in `src/NexusBooster.Focus/BrowserExtension`. The published ZIP carries its files in a `BrowserExtension` folder beside the app; review them, then install the folder manually into each supported Chromium-based browser. It blocks selected distraction paths and removes YouTube home/watch recommendations; it does not cover every browser or every site. Its IRL check-ins are self-reported, and browser activity is not sent to the app.

**Advanced supplied executable:** If `Nexus PC Optimizer.exe` is included in a release ZIP, it is a separately supplied advanced-tuner module, not source code built or reviewed by this repository's solution. It is packaged for the user's convenience only; verify the published hash and signature information, and do not disable SmartScreen or antivirus to trust it. The repository does not claim that the supplied module was signed or its behavior verified.

**System-integration limits:** The Focus module does not install a system-wide proxy, intercept HTTPS, modify the hosts file, terminate processes, minimize or lock browsers, or enforce access to other apps. It does not automatically change Windows color filters; the grayscale button opens Windows accessibility settings for a user-controlled change. Closing the module does not lock the PC, and quest completion is tracked locally rather than enforced outside Nexus.

## Build and test

Install the .NET 8 SDK on Windows, then run from the repository root:

```powershell
dotnet build .\NexusBooster.sln
dotnet run --project .\tests\NexusBooster.Core.Tests\NexusBooster.Core.Tests.csproj
dotnet run --project .\src\NexusBooster.App\NexusBooster.App.csproj
dotnet publish .\src\NexusBooster.App\NexusBooster.App.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\test-build\publish\win-x64
```

The framework-dependent WPF executable is emitted under `artifacts/test-build/bin/NexusBooster.App/`. The self-contained testable executable is published to `artifacts/test-build/publish/win-x64/NexusSafeTweaks.exe`. The test executable and intermediate build products are also kept under `artifacts/test-build/`, which is ignored by Git.

The self-contained single-file executable targets 64-bit Windows 10/11 and does not require the .NET runtime to be preinstalled. The app and its runtime dependencies are bundled, so the app does not require a separate `NexusBooster.App.dll` next to the executable. The release ZIP also includes this README so the included PC Tuner and separate Focus module are clear before launch. Extract the complete release ZIP before running the EXE. No driver, helper service, or hardware overclocking component is bundled.

## Release verification

Keep published binaries un-obfuscated and do not use packers. After publishing an executable, calculate its SHA-256 digest and publish the hash alongside that exact binary:

```powershell
Get-FileHash .\artifacts\test-build\publish\win-x64\NexusSafeTweaks.exe -Algorithm SHA256
# Or:
certutil -hashfile .\artifacts\test-build\publish\win-x64\NexusSafeTweaks.exe SHA256
```

Compare the hash against a copy obtained through a trusted channel. A hash verifies file integrity; it does not prove that the binary is safe or signed. Locally built/unsigned Windows applications may trigger SmartScreen or antivirus warnings. Do not disable security software to run a binary you do not trust. This project does not claim Microsoft or antivirus approval.

## Recovery and removing Nexus

Before removing the app, open **Recovery & removal** and choose **Restore all Nexus-managed settings**. Review any reported failures; their backups are kept in `%LOCALAPPDATA%\NexusBooster\backups\` so restoration can be retried. To clear mouse profiles, choose **Back up & clear saved mouse profiles**; a recovery copy is saved under `%LOCALAPPDATA%\NexusBooster\recovery\` and can be restored using **Restore a mouse profile backup**. These profiles are presets only and do not change mouse hardware.

Nexus currently has no installer or uninstaller and installs no background service or scheduled task. After restoring changes, close Nexus and manually remove the executable or the folder where you saved it. Only after verifying recovery is complete, you may remove `%LOCALAPPDATA%\NexusBooster\` yourself; doing so deletes the backups, recovery copies, and first-launch acceptance record. Do not delete that data while restoration failures remain.

## Safety and limitations

- The application runs as the current user, does not request administrator privileges, and only modifies enumerated `HKEY_CURRENT_USER` values.
- A backup is written before settings are changed. If applying one of a multi-value tweak fails, Nexus attempts rollback and reports rollback failures explicitly.
- Recovery restores values from Nexus backup files only. It does not create a Windows System Restore point and cannot reverse changes made by other tools or changes made after a Nexus tweak was applied.
- Some settings take effect only after signing out or restarting a game; each tweak states its expected refresh behavior.
- Debloating apps can remove functionality. The guide delegates removal to Windows Settings and leaves package selection to the user.
- DPI values are presets only. Windows pointer speed is not hardware DPI.
- Driver availability and compatibility depend on the exact GPU and computer model. For laptops and prebuilt systems, check the computer maker's support page as well as the GPU vendor.
- Shared online tweaks such as blanket USB/PCIe power changes, HAGS/TDR/GPU vendor registry edits, disabling scheduled tasks or security/update features, hosts-file blocking, broad cache purges, and DISM ResetBase are not automated because their compatibility, safety, and rollback depend on the exact system and Windows build.
