# G Hub XBOX Profile Fixer

An unofficial, open-source Windows utility for PC games installed through the XBOX app or PC Game Pass that Logitech G Hub fails to detect.

The tool scans XBOX game installations, identifies the actual game executables and creates the missing G Hub application and profile records selected by the user.

It does not modify game files and never overwrites the selected `settings.db`. It creates a separate database and runs SQLite integrity and relationship checks before reporting success.

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
- Displays icons extracted locally from installed game executables
- Includes manual folder and executable options for unusual games
- Creates proper G Hub application and default-profile records
- Starts new profiles with the current G Hub Desktop assignments
- Keeps the original G Hub database untouched
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
3. Keep a backup of `%LOCALAPPDATA%\LGHUB\settings.db`.

The utility checks for running G Hub processes and refuses to create a database while they are open.

## How to use it

1. Open the utility.
2. Select `%LOCALAPPDATA%\LGHUB\settings.db` if it was not found automatically.
3. Continue to **XBOX games**.
4. Scan for installed games.
5. Review the executable path for each result.
6. Select the missing games you want to add.
7. Continue to **Create copy** and save the fixed database.

If a game is not detected automatically, use **Add folder** or **Add executable**.

## Installing the fixed database

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
- The original G Hub database is never overwritten by the utility.
- There is no telemetry, updater or background service.

See [SECURITY.md](SECURITY.md) for reporting security issues.

## Build from source

The project targets .NET Framework 4.8 so it can remain a small standalone executable on current Windows 10/11 systems.

Using Visual Studio 2022:

1. Install the **.NET desktop development** workload and .NET Framework 4.8 targeting pack.
2. Open `GHubProfileUtility.csproj`.
3. Select **Release** and build.

Or from a Visual Studio Developer Command Prompt:

```powershell
msbuild GHubProfileUtility.csproj /restore /p:Configuration=Release
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
