<img width="1752" height="1191" alt="G Hub XBOX Profile Fixer" src="https://github.com/user-attachments/assets/fb5e451a-1da5-4fe5-8723-a123e46d3f35" />

# G Hub XBOX Profile Fixer

An unofficial, open-source Windows utility for PC games installed through the Xbox app or PC Game Pass that Logitech G HUB fails to detect.

The tool scans Xbox game installations, identifies the actual game executables and creates the missing G HUB application and profile records selected by the user.

It does not modify game files. It builds and validates the updated database, then applies it with an automatic dated backup. Exporting a separate copy remains available.

> This project is not affiliated with or endorsed by Logitech or Microsoft. Logitech, G HUB, Xbox and Game Pass are trademarks of their respective owners.

## Download

### [⬇ Download the latest version (.exe)](https://github.com/andybkly/ghub-xbox-profile-fixer/releases/latest/download/GHubXBOXGamePassProfileFixer.exe)

The EXE is built from the public source using GitHub Actions. Tagged release builds generate a SHA-256 checksum and build-provenance attestation.

**Windows security notice:** This application is currently unsigned. Windows SmartScreen or Smart App Control may warn about or block the executable. Smart App Control can prevent unsigned applications from running entirely. The public source code, automated build history and SHA-256 checksums are available for inspection.

**[View the latest release, release notes and checksums](https://github.com/andybkly/ghub-xbox-profile-fixer/releases/latest)**

## What it does

- Scans Xbox games installed on any ready drive
- Reads Xbox `.GamingRoot` markers and common `XboxGames` folders
- Supports custom Xbox installation folders
- Uses `MicrosoftGame.config` to identify the intended PC executable where available
- Shows games that are already registered or missing from G HUB
- Displays local Xbox manifest artwork, with embedded executable icons as a fallback
- Includes manual folder and executable options for unusual games
- Creates G HUB application and default-profile records
- Starts new profiles with the current G HUB Desktop assignments
- Automatically preserves the original database in a dated backup when applying profiles
- Performs SQLite integrity and profile-relationship checks on the output

## Requirements

- Windows 10 or Windows 11
- Logitech G HUB
- A PC game installed through the Xbox app or PC Game Pass
- No Python, PowerShell script or installer required
- No administrator access normally required

## Before using it

1. Quit G HUB from its system-tray icon.
2. In Task Manager, confirm that `lghub.exe`, `lghub_agent.exe` and `lghub_system_tray.exe` are closed.
3. The **Back up & update G HUB** option automatically creates a dated backup beside the selected database.

The utility checks for running G HUB processes and prevents database changes while they are open.

## How to use it

1. Download and open the utility.
2. Select `%LOCALAPPDATA%\LGHUB\settings.db` if it was not found automatically.
3. Continue to **Game library**.
4. Scan for installed games.
5. Review the executable path for each result.
6. Select the missing games you want to add.
7. Choose **Review selection**, then **Back up & update G HUB**.
8. The completion screen shows the backup path and recovery instructions. Choose **Open G HUB**. No manual copying or renaming is needed.

The original database is backed up beside the selected database using the current date, for example `settings-backup-DDMMYYYY.db`. Further backups on the same day use `-2`, `-3`, etc. Existing backups are preserved.

Applying is blocked while G HUB is running or SQLite cannot obtain the required locks. Leftover SQLite journals are recovered and checkpointed through SQLite before replacement; never delete them manually.

To save a separate database instead, choose **Save a separate copy…**.

If a game is not detected automatically, use **Add folder** or **Add executable**.

## Installing an exported copy (optional)

If you chose to export a separate database rather than applying changes automatically:

1. Keep G HUB closed.
2. Back up the original `%LOCALAPPDATA%\LGHUB\settings.db`.
3. Rename the generated file to `settings.db`.
4. Copy it into `%LOCALAPPDATA%\LGHUB\`, replacing the existing file.
5. Start G HUB and configure or test the new game profile.

If anything is wrong, close G HUB and restore your backup.

## Tested

The initial workflow was verified using Among Us installed through PC Game Pass. The game was absent from G HUB before conversion, then appeared and activated correctly after the updated database was installed.

Game installation layouts vary, so please report titles that are not detected correctly.

Compatibility with every G HUB version and Xbox game installation has not been established.

## Privacy and security

- The application has no networking code and sends no data anywhere.
- It runs as the current user and does not request elevation.
- It reads the database and game folders selected or discovered locally.
- Game executables are inspected by path only; their contents are not changed.
- Applying replaces the selected database only after validation, preserving the original in a dated backup.
- Exporting a separate copy leaves the selected database untouched.
- There is no telemetry, updater or background service.

See [SECURITY.md](SECURITY.md) for reporting security issues.

## Build from source

The project targets .NET Framework 4.8, allowing it to run as a standalone executable on supported Windows 10/11 systems with the framework installed.

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

Compare it with `GHubXBOXGamePassProfileFixer.exe.sha256` attached to the same release.

The repository's [GitHub Actions page](https://github.com/andybkly/ghub-xbox-profile-fixer/actions) shows the automated build history and tagged commits.

## Licence

[MIT](LICENSE)

## Development checks

The Windows Actions build runs database/scanner regression checks and WPF UI smoke checks before packaging the executable.

Synthetic fixtures cover duplicate additions, card links, absent lighting references, changed input, WAL snapshots, failed-export preservation, malformed manifests and direct game folders.

UI checks cover retained manual entries, retained deselections, database changes and button states, and capture screenshots at normal and minimum window sizes.

These checks do not establish compatibility with every G HUB version or actual game profile switching. Test the generated database with G HUB on Windows before release.
