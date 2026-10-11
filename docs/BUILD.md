# Building & distributing DbMigration

## Run from source

```powershell
cd C:\Users\user\Desktop\DbMigration
dotnet run --project src\DbMigration.App
```

Requires the .NET 10 SDK.

## Standalone .exe (no .NET, no dependencies)

```powershell
powershell -ExecutionPolicy Bypass -File build\publish.ps1
```

Produces **`dist\DbMigration.exe`** — a single self-contained file.

| Property | Value |
|----------|-------|
| Runtime | bundled (.NET 10 Desktop) — **nothing to install** |
| Dependencies | bundled (SQLite, MySqlConnector, Npgsql, SqlClient, DPAPI) |
| Native libraries | self-extracted to a private temp folder on first launch |
| Target | `win-x64` by default (`-Runtime win-arm64` for ARM) |
| Trimming | **disabled** — WinForms is not trim-safe |
| Size | ~60–90 MB (compressed single file) |

Copy `dist\DbMigration.exe` to any Windows 10/11 x64 machine and double-click it.

### Framework-dependent (small) build

If you only want a small file for machines that already have the .NET 10 Desktop Runtime:

```powershell
powershell -ExecutionPolicy Bypass -File build\publish.ps1 -FrameworkDependent
```

## Tests

```powershell
dotnet test
```

## Notes

- The application stores settings and profiles under `%APPDATA%\DbMigration`.
- Passwords are protected with Windows DPAPI (per-user), so a profile file copied to
  another user account cannot be decrypted.
- The "cloud backup" feature is an intentional stub and never touches the network.
