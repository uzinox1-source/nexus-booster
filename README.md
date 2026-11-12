# Nexus Booster

A Windows 10/11 desktop tuning dashboard with opt-in, current-user Windows tweaks and saved mouse DPI presets. The premium gradient interface enters with a smooth fade/slide animation; actions remain transparent and reversible where the OS settings support rollback.

## What works in this build

- Seven Windows account-level settings with explicit confirmation, pre-change backups, and undo.
- A current-user startup manager that can disable selected `Run` entries and restore their exact saved values.
- Local mouse DPI profile storage (100–25,600 DPI); profiles are **not** pushed to a mouse because a device/vendor API is not connected.
- An FPS advisor that offers transparent, rule-based suggestions from the user's selected bottleneck, resolution, and play-style; it does not connect to an AI service, scan games, or guarantee frame-rate gains.
- Official driver/support links for AMD, NVIDIA, Intel, MSI, ASUS, and Gigabyte. Nexus opens vendor pages in the browser and never downloads or installs a driver.
- A conservative debloat guide that opens Windows Installed apps rather than silently removing system components.
- A locked overclocking panel. CPU/GPU controls stay unavailable until a verified vendor backend and safety telemetry are integrated. Nexus does not write firmware or low-level registers.
- A dedicated test console project; all build and test outputs are directed to the ignored `artifacts/test-build/` folder.

## Build and test

Install the .NET 8 SDK on Windows, then run from the repository root:

```powershell
dotnet build .\NexusBooster.sln
dotnet run --project .\tests\NexusBooster.Core.Tests\NexusBooster.Core.Tests.csproj
dotnet run --project .\src\NexusBooster.App\NexusBooster.App.csproj
dotnet publish .\src\NexusBooster.App\NexusBooster.App.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\test-build\publish\win-x64
```

The framework-dependent WPF executable is emitted under `artifacts/test-build/bin/NexusBooster.App/`. The self-contained testable executable is published to `artifacts/test-build/publish/win-x64/NexusBooster.App.exe`. The test executable and intermediate build products are also kept under `artifacts/test-build/`, which is ignored by Git.

## Safety and limitations

- The application runs as the current user, does not request administrator privileges, and only modifies enumerated `HKEY_CURRENT_USER` values.
- A backup is written before settings are changed. If applying one of a multi-value tweak fails, Nexus attempts rollback and reports rollback failures explicitly.
- Some settings take effect only after signing out or restarting a game; each tweak states its expected refresh behavior.
- Debloating apps can remove functionality. The guide delegates removal to Windows Settings and leaves package selection to the user.
- DPI values are presets only. Windows pointer speed is not hardware DPI.
- Driver availability and compatibility depend on the exact GPU and computer model. For laptops and prebuilt systems, check the computer maker's support page as well as the GPU vendor.
- Overclocking can cause instability or damage. Direct controls are deliberately disabled without supported APIs and appropriate sensor guardrails.
