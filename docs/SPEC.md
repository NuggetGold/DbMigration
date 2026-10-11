# DbMigration — Technical Specification

> Single source of truth for all agents working on this repository. Read this fully before editing.

## 1. Product

A Windows desktop application (**C# / .NET 10 / WinForms**) to **inspect, edit, convert and
back up databases** across engines. Primary scenario: migrate a **MySQL** database to **SQLite3**,
but any source→target combination of the supported engines must work.

Supported engines: **SQLite, MySQL, PostgreSQL, SQL Server.**

### Mandatory features

| # | Feature | Notes |
|---|---------|-------|
| F1 | Connection profiles | Create/edit/delete/test; SQLite file or server engines; password stored via DPAPI |
| F2 | Schema browser | Tables → columns / indexes / FKs / row count |
| F3 | Data viewer & editor | Paged grid; edit cell, insert row, delete row; refresh |
| F4 | Migration | Source → target; choose tables; schema and/or data; batch size; progress; cancel; per-table result report |
| F5 | SQL console | Run arbitrary SQL; tabular results; row cap; timing |
| F6 | Export | Single table → SQL script / CSV / JSON |
| F7 | Cloud backup (stub) | Simulated upload that always reports success with a report URI |
| F8 | Activity log | Colour-coded, timestamped, in-app log of everything above |
| F9 | Settings | Animation on/off + FPS, page size, query row cap, batch size, backup folder; persisted |
| F10 | Look & feel | Dark opencode theme, animated GTA IV-style glowing text, custom window chrome |

## 2. Visual language (MANDATORY)

Palette — **exact values from opencode's default dark theme** (`DbMigration.App/Theme/OpencodeTheme.cs`):

| Token | Hex | Usage |
|-------|-----|-------|
| Background | `#0A0A0A` | window background |
| BackgroundPanel | `#141414` | panels, sidebar |
| BackgroundElement | `#1E1E1E` | cards, inputs, grid cells |
| BackgroundElementHover | `#282828` | hover |
| BorderSubtle / Border / BorderActive | `#3C3C3C` / `#484848` / `#606060` | separators, outlines |
| TextMuted | `#808080` | secondary text |
| Text | `#EEEEEE` | primary text (WHITE-ish, as requested) |
| Primary | `#FAB283` | brand accent, headings, focus |
| PrimaryBright | `#FFC09F` | glow highlight |
| Secondary | `#5C9CF5` | links, info |
| Accent | `#9D7CD8` | selection |
| Success / Warning / Error / Info | `#7FD88F` / `#F5A742` / `#E06C75` / `#56B6C2` | semantics |

Rules:
- Background is always dark; body text is `Text` (`#EEEEEE`).
- **Important words** (engine names, counts, statuses, warnings) are coloured with the semantic
  colours above — never all-white. Never rely on colour alone: pair with an icon/label.
- Text must be neat: consistent paddings (multiples of 4), one font family per role, no clipped text.
- **Glowing text**: `GlowLabel` pulses smoothly (sine) like GTA IV loading text. The main title
  uses `ShineEnabled = true`. All animations obey the global `AnimationClock` (single timer) and the
  `AnimationsEnabled` setting; when disabled the glow freezes at a readable constant intensity.
- Window: borderless custom chrome, dark, DWM rounded corners, custom min/max/close, draggable
  title bar, resizable on all edges, high-DPI aware, accessible (Tab order, mnemonics, contrast).

## 3. Solution layout

```
DbMigration.slnx
Directory.Build.props
docs/                      (this spec, ideas, review notes)
src/DbMigration.Core/      (net10.0, no WinForms)      -> Coder 1
src/DbMigration.App/       (net10.0-windows, WinForms) -> Coder 2
tests/DbMigration.Tests/   (net10.0, MSTest)           -> Testers
```

### File ownership (do not cross)

- **Coder 1** owns everything under `src/DbMigration.Core/**` except the contract files listed in §5,
  which are **frozen** (do not change signatures without asking the orchestrator).
- **Coder 2** owns everything under `src/DbMigration.App/**` except
  `Theme/OpencodeTheme.cs`, `Theme/AppFonts.cs`, `Theme/AnimationClock.cs`, `Controls/GlowLabel.cs`
  (frozen) and `GlobalUsings.cs`.
- **Testers** own `tests/DbMigration.Tests/**`.
- Nobody else edits those trees.

## 4. Contracts (frozen) — in `src/DbMigration.Core`

- `Models/Enums.cs`: `DatabaseEngine`, `CanonicalType`, `MigrationScope`, `ExistingTableBehavior`,
  `LogLevel`, `ExportFormat`.
- `Models/ConnectionProfile.cs`, `Models/Schema.cs` (`ColumnSchema`, `IndexSchema`,
  `ForeignKeySchema`, `TableSchema`), `Models/QueryResult.cs`, `Models/MigrationModels.cs`
  (`MigrationRequest`, `MigrationProgress`, `TableMigrationResult`, `MigrationResult`),
  `Models/CloudModels.cs` (`CloudBackupRequest`, `CloudUploadResult`), `Models/AppSettings.cs`.
- `Abstractions/IDatabaseProvider.cs`, `IDatabaseProviderFactory.cs`, `IMigrationEngine.cs`,
  `IDataExporter.cs`, `ICloudStorage.cs`, `IPersistence.cs` (`ISecretProtector`, `IProfileStore`,
  `ISettingsStore`).
- `CoreServices.cs`: `DbMigrationServices` record + `CoreServices.CreateDefault(string? dataDirectory = null)`.

The App depends **only** on these contracts. Read every file before implementing against it.

## 5. Reserved / frozen files

`src/DbMigration.App/Theme/*`, `src/DbMigration.App/Controls/GlowLabel.cs`,
`src/DbMigration.App/GlobalUsings.cs`, all of `src/DbMigration.Core/Models`,
`src/DbMigration.Core/Abstractions`, `src/DbMigration.Core/CoreServices.cs` (the file; Coder 1 fills
`CreateDefault`'s body but must not change the `DbMigrationServices` record shape).

## 6. Engineering rules (all code)

- Nullable reference types are **enabled**; no `!` to silence real nullability, use `?? throw`.
- Async all the way; every I/O method takes a `CancellationToken`; UI must never block.
- `ConfigureAwait(false)` in Core only; **never** in the App UI layer.
- Prefer immutable `record`s for data; `sealed` classes by default.
- File-scoped namespaces; one public type per file.
- **No comments in code** — not even XML doc comments. Names and small methods must carry
  the intent; do not explain the obvious.
- No `async void` except WinForms event handlers, which must catch and surface errors.
- Dispose everything (`using`); no finalizers; no `.Result`/`.Wait()`.
- No compiler warnings. Build must be clean (`dotnet build -warnaserror` should pass for Core/tests).
- Do not introduce new NuGet packages without need; available: `Microsoft.Data.Sqlite`,
  `MySqlConnector`, `Npgsql`, `Microsoft.Data.SqlClient`, `System.Security.Cryptography.ProtectedData`,
  `MSTest`.
- **Never touch GitHub / remote repositories.**
- Windows-only APIs are fine; `CA1416` is suppressed.

## 7. Definition of done

1. `dotnet build` of the whole solution is clean (0 warnings, 0 errors).
2. `dotnet test` passes for `DbMigration.Tests`.
3. The app launches, shows the themed shell with the animated glowing title, and every feature F1–F10
   is reachable and functional (SQLite end-to-end; other engines as configured).
4. Every public Core API used by the UI is covered by at least one test where it is testable offline.
5. Strict review (`docs/REVIEW.md`) has no open blocker.

## 8. Commands

```powershell
cd C:\Users\user\Desktop\DbMigration
dotnet build --nologo
dotnet test  --nologo
dotnet run --project src\DbMigration.App
```
