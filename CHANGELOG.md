# Changelog

## 0.3.0

- Replaced the WinForms interface with a responsive WPF interface.
- Rebuilt the approved three-step layout using device-independent sizing.
- Improved behaviour at 125%, 150% and other Windows display scales.
- Preserved the working XBOX scanner, game icons and safe database-copy logic.

## 0.2.1

- Fixed the main content rendering underneath the navigation sidebar.
- Replaced fixed header positioning with DPI-safe flow layout.
- Added explicit per-monitor DPI awareness and WinForms high-DPI auto-resizing.

## 0.2.0

- Refocused the utility exclusively on XBOX / PC Game Pass profile detection.
- Removed the unrelated device-profile conversion feature.
- Added a clean three-step interface: select database, choose games, create copy.
- Added game icons extracted locally from installed executables.
- Improved automatic multi-drive and `.GamingRoot` discovery.
- Retained manual install-folder and executable fallbacks.
- Renamed the release executable to `GHubXBOXGamePassProfileFixer.exe`.

## 0.1.0

- Initial combined profile utility release.
