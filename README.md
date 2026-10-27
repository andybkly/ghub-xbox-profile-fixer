# G Hub XBOX Profile Fixer

An unofficial, open-source Windows utility for PC games installed through the XBOX app or PC Game Pass that Logitech G Hub fails to detect.

The tool scans XBOX game installations, identifies the actual game executables and creates the missing G Hub application and profile records selected by the user.

It does not modify game files. It builds and validates the updated database, then applies it with an automatic dated backup. Exporting a separate copy remains available.

> This project is not affiliated with or endorsed by Logitech or Microsoft. Logitech, G Hub, XBOX and Game Pass are trademarks of their respective owners.

## Download

### [Download the latest version (.exe)](https://github.com/andybkly/ghub-xbox-profile-fixer/releases/latest/download/GHubXBOXGamePassProfileFixer.exe)

[View releases, notes and SHA-256 checksums](https://github.com/andybkly/ghub-xbox-profile-fixer/releases)

The EXE is built from the public source by GitHub Actions. Each release includes a SHA-256 checksum and build-provenance attestation.

Windows SmartScreen may display an **Unknown publisher** warning because the project does not currently use a paid code-signing certificate. The source code and automated build history are available for inspection.

## What it does

- Scans XBOX games installed on any ready drive
- Reads XBOX `.GamingRoot` markers and common `XboxGames` folders
- Supports custom XBOX installation folders
- Uses `MicrosoftGame.config` to identify the intended PC executable where available
- Shows games that are already registered or missing from G Hub
- Displays local Xbox manifest artwork, with embedded executable icons as a fallback
- Includes manual folder and executable options for unusual games
- Creates proper G Hub application and default-profile records
- Starts new profiles with the current G Hub Desktop assignments
- Automatically preserves the original database in a dated backup when applying profiles
- Performs SQLite integrity and profile-relationship checks on the output

## Requirements

- Windows 10 or Windows 11
- Logitech G Hub
- A PC game installed through the XBOX app or PC Game Pass
- No Python, PowerShell script or installer required
- No administrator access normally required

## Before using it

1. Quit G Hub from its system-tray icon.
2. In Task Manager, confirm that `lghub.exe`, `lghub_agent.exe` and `lghub_system_tray.exe` are closed.
3. The **Apply to G HUB** option automatically creates a dated backup beside the selected database.

The utility checks for running G Hub processes and refuses to create a database while they are open.

## How to use it

1. Open the utility.
2. Select `%LOCALAPPDATA%\LGHUB\settings.db` if it was not found automatically.
3. Continue to **Game library**.
4. Scan for installed games.
5. Review the executable path for each result.
6. Select the missing games you want to add.
7. Choose **Review selection**, then **Apply to G HUB**.
8. Start G HUB. No manual copying or renaming is needed.

The original is saved beside the selected database as `settings-backup-27102026.db` (using the current date). Further backups on the same day use `-2`, `-3`, etc. Existing backups are preserved. Applying is blocked while G HUB is running or SQLite journal files are present.

To save a separate database instead, choose **Export copy…**.

If a game is not detected automatically, use **Add folder** or **Add executable**.

## Installing an exported copy (optional)

1. Keep G Hub closed.
2. Back up the original `%LOCALAPPDATA%\LGHUB\settings.db`.
3. Rename the generated file to `settings.db`.
4. Copy it into `%LOCALAPPDATA%\LGHUB\`, replacing the existing file.
5. Start G Hub and configure or test the new game profile.

If anything is wrong, close G Hub and restore your backup.

## Tested

The initial workflow was verified using Among Us installed through PC Game Pass. The game was absent from G Hub before conversion, then appeared and activated correctly after the fixed database was installed.

Game installation layouts vary, so please report titles that are not detected correctly.

## Privacy and security

- The application has no networking code and sends no data anywhere.
- It runs as the current user and does not request elevation.
- It reads only the database and game folders selected or discovered locally.
- Game executables are inspected by path only; their contents are not changed.
- Applying replaces the selected database only after validation, preserving the original in a dated backup. Export copy leaves the selected database untouched.
- There is no telemetry, updater or background service.

See [SECURITY.md](SECURITY.md) for reporting security issues.

## Build from source

The project targets .NET Framework 4.8 so it can remain a small standalone executable on current Windows 10/11 systems.

Using Visual Studio 2022:

1. Install the **.NET desktop development** workload and .NET Framework 4.8 targeting pack.
2. Open `GHubXBOXProfileFixer.csproj`.
3. Select **Release** and build.

Or from a Visual Studio Developer Command Prompt:

```powershell
msbuild GHubXBOXProfileFixer.csproj /restore /p:Configuration=Release
```

Output:

```text
bin\Release\net48\GHubXBOXGamePassProfileFixer.exe
```

## Release verification

Calculate the downloaded file's checksum:

```powershell
Get-FileHash .\GHubXBOXGamePassProfileFixer.exe -Algorithm SHA256
```

Compare it with `GHubXBOXGamePassProfileFixer.exe.sha256` attached to the same release. The repository's Actions page also shows the tagged commit used for the build.

## Licence

[MIT](LICENSE)

## Development checks

The Windows Actions build runs database/scanner regression checks and WPF UI smoke checks before packaging the executable. Synthetic fixtures cover duplicate additions, card links, absent lighting references, changed input, WAL snapshots, failed-export preservation, malformed manifests and direct game folders. UI checks cover retained manual entries, retained deselections, database changes and button states, and capture screenshots at normal and minimum window sizes.

These checks do not establish compatibility with every G HUB version or actual game profile switching. Test the generated database with G HUB on Windows before release.
