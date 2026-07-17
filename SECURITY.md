# Security

## Supported version

Only the latest release is actively supported.

## Reporting a vulnerability

Please use GitHub's private **Report a vulnerability** option rather than opening a public issue. Include the affected version, steps to reproduce and any relevant database structure with personal paths removed.

Do not upload a private G Hub `settings.db` to a public issue. It can contain local usernames, application paths and profile information.

## Trust model

The G Hub XBOX Game Pass Profile Fixer deliberately:

- makes no network requests;
- does not request administrator access;
- never modifies game executables;
- never overwrites the source database;
- validates the output database before success;
- is built from public source by GitHub Actions.
