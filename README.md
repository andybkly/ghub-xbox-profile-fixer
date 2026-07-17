# G Hub Profile Utility

An unofficial, open-source Windows utility for two Logitech G Hub profile problems:

1. Copying G502 X wired button assignments to matching G502 X LIGHTSPEED profiles.
2. Adding Xbox / PC Game Pass games that G Hub has failed to detect.

The utility does not modify game files and never overwrites the selected `settings.db`. It creates a separate database and runs SQLite's integrity check before reporting success.

> This project is not affiliated with or endorsed by Logitech or Microsoft. Logitech, G Hub, G502 X, Xbox and Game Pass are trademarks of their respective owners.

## Download

### [Download G Hub Profile Utility v0.1.0 (.exe)](https://github.com/andybkly/ghub-profile-utility/releases/download/v0.1.0/GHubProfileUtility.exe)

[View release notes and SHA-256 checksum](https://github.com/andybkly/ghub-profile-utility/releases/tag/v0.1.0)

The EXE is built from the public source by GitHub Actions.

## Requirements

- Windows 10 or Windows 11
- Logitech G Hub
- No Python, PowerShell script or installer required
- No administrator access normally required

## Before using it

1. Quit G Hub from its system-tray icon.
2. In Task Manager, confirm that `lghub.exe`, `lghub_agent.exe` and `lghub_system_tray.exe` are closed.
3. Keep a backup of `%LOCALAPPDATA%\LGHUB\settings.db`.

The utility also checks for running G Hub processes and refuses to create a database while they are open.

## Transfer G502 X profiles

1. Open the utility and select `%LOCALAPPDATA%\LGHUB\settings.db` if it was not found automatically.
2. Open **Transfer G502 X Profiles**.
3. Click **Analyse and create converted copy**.
4. Review the number of assignments and profiles found.
5. Save the converted database.

The conversion copies button, scroll-wheel and G-Shift assignments. It intentionally keeps DPI, polling, lighting firmware and onboard settings separate.

## Fix Xbox / PC Game Pass profiles

1. Open **Fix Xbox Game Pass Profiles**.
2. Click **Scan Xbox games**.
3. Review the executable path for each missing game.
4. Tick the games you want to add.
5. Click **Create fixed database**.

The scanner checks every ready drive for `XboxGames` and `Xbox Games` folders. It prefers the official `MicrosoftGame.config` manifest to identify the PC executable. You can add a custom install folder or executable manually when a game uses an unusual layout.

New game profiles begin with the current Desktop profile's assignments and receive their own application/profile records and private setting cards.

## Installing the converted database

1. Keep G Hub closed.
2. Back up the original `%LOCALAPPDATA%\LGHUB\settings.db`.
3. Rename the generated file to `settings.db`.
4. Copy it into `%LOCALAPPDATA%\LGHUB\`, replacing the existing file.
5. Start G Hub and test the profiles.

If anything is wrong, close G Hub and restore your backup.

## Privacy and security

- The application has no networking code and sends no data anywhere.
- It runs as the current user and does not request elevation.
- It reads only the database and game folders selected or discovered locally.
- Game executables are inspected by path only; their contents are not changed.
- The original G Hub database is never overwritten by the utility.
- There is no telemetry, updater or background service.

See [SECURITY.md](SECURITY.md) for reporting security issues.

## Build from source

The project targets .NET Framework 4.8 so it can remain a small, standalone executable on current Windows 10/11 systems.

Using Visual Studio 2022:

1. Install the **.NET desktop development** workload and .NET Framework 4.8 targeting pack.
2. Open `GHubProfileUtility.csproj`.
3. Select **Release** and build.

Or from a Visual Studio Developer Command Prompt:

```powershell
msbuild GHubProfileUtility.csproj /restore /p:Configuration=Release
```

Output: `bin\Release\net48\GHubProfileUtility.exe`

## Release verification

Calculate the downloaded file's checksum:

```powershell
Get-FileHash .\GHubProfileUtility.exe -Algorithm SHA256
```

Compare it with `GHubProfileUtility.exe.sha256` attached to the same release. The repository's Actions page also shows the exact tagged commit used for the build.

## Licence

[MIT](LICENSE)
