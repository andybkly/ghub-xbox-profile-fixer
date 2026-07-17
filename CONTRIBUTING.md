# Contributing

Bug reports and pull requests are welcome.

For XBOX game-detection problems, include:

- the game name;
- the XBOX install-folder layout;
- the relevant `MicrosoftGame.config` executable entry, if present;
- the executable that actually appears in Task Manager while the game runs.

Remove Windows usernames and other personal paths before posting logs or snippets. Never attach a complete private `settings.db` to a public issue.

Changes to database-writing logic should preserve these rules:

1. Never overwrite the input database.
2. Validate the expected JSON structure before editing.
3. Use a transaction for the SQLite BLOB update.
4. Delete failed output files.
5. Run `PRAGMA integrity_check` before reporting success.
