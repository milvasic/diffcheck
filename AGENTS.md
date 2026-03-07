# AGENTS.md — DiffCheck

Guidelines for AI agents working in this repository.

---

## Repository overview

DiffCheck compares CSV and XLSX files and produces color-coded HTML diff reports.  
It is a .NET 10 solution with four projects:

| Project | Type | Purpose |
|---------|------|---------|
| `DiffCheck.Core` | Class library | All comparison logic: readers, diff engine, HTML generator, models |
| `DiffCheck.Core.Tests` | MSTest test project | Unit tests for `DiffCheck.Core` |
| `DiffCheck.Cli` | Console / NuGet tool | `diffcheck` CLI — wraps `DiffCheckService` |
| `DiffCheck.Web` | ASP.NET Core Razor Pages | Browser UI — wraps `DiffCheckService` |

All production logic lives in `DiffCheck.Core`. The CLI and Web projects are thin wrappers.

---

## Build and run

```bash
# Restore dependencies
dotnet restore

# Build entire solution
dotnet build

# Build a single project
dotnet build DiffCheck.Core/DiffCheck.Core.csproj

# Run the web app
dotnet run --project DiffCheck.Web/DiffCheck.Web.csproj

# Run the web app via Docker
docker compose up --build
```

---

## Testing

The test project is `DiffCheck.Core.Tests` (MSTest, .NET 10).

```bash
# Run all tests
dotnet test

# Run tests for one project
dotnet test DiffCheck.Core.Tests/DiffCheck.Core.Tests.csproj

# Filter by test class or method name
dotnet test --filter FullyQualifiedName~HtmlReportGeneratorTests
dotnet test --filter FullyQualifiedName~DiffEngineTests
```

Test files mirror the source structure:
- `DiffCheck.Core.Tests/Diff/` → tests for `DiffCheck.Core/Diff/`
- `DiffCheck.Core.Tests/Html/` → tests for `DiffCheck.Core/Html/`
- `DiffCheck.Core.Tests/Readers/` → tests for `DiffCheck.Core/Readers/`
- `DiffCheck.Core.Tests/TestData/` → fixture CSV files used by reader tests

Always run `dotnet test` after making changes and ensure all tests pass before finishing a task.

---

## Formatting

The project uses **CSharpier** for code formatting (configured in `.csharpierrc.json`; enforced via Husky pre-commit hook).

```bash
# Format all files
dotnet csharpier .

# Format a single file
dotnet csharpier DiffCheck.Core/Html/HtmlReportGenerator.cs
```

Run `dotnet tool restore` first if `csharpier` is not available.  
Never commit unformatted code. If running CSharpier changes lines unrelated to your edit, format before and after to isolate the diff.

---

## Code conventions

- **Nullable reference types** are enabled everywhere (`<Nullable>enable</Nullable>`). Annotate new parameters and return types.
- **Implicit usings** are enabled — no need to add `using System;` etc. unless a non-default namespace is required.
- **`LangVersion` is `latest`** — modern C# features (primary constructors, pattern matching, collection expressions) are fine.
- **Root namespace for `DiffCheck.Core`** is `DiffCheck` (not `DiffCheck.Core`), matching the `<RootNamespace>` in the `.csproj`.
- Keep classes `sealed` unless inheritance is required.
- Use `ArgumentNullException.ThrowIfNull` for public API guard clauses.

---

## Architecture notes

### `DiffCheck.Core` key types

| Type | Location | Role |
|------|----------|------|
| `DiffCheckService` | `DiffCheckService.cs` | Entry point — orchestrates read → diff → generate |
| `IFileReader` | `Readers/IFileReader.cs` | Reader abstraction; implement to add new formats |
| `CsvReader` | `Readers/CsvReader.cs` | CSV/TXT reader (CsvHelper) |
| `XlsxReader` | `Readers/XlsxReader.cs` | XLSX reader (ClosedXML); reads first sheet by default |
| `FileReaderFactory` | `FileReaderFactory.cs` | Selects reader by file extension |
| `DiffEngine` | `Diff/DiffEngine.cs` | Compares two `DataTable`s; supports `columnMappings` and `keyColumns` |
| `HtmlReportGenerator` | `Html/HtmlReportGenerator.cs` | Builds the self-contained HTML report |
| `HtmlReportOptions` | `Html/HtmlReportOptions.cs` | Color/font customization for the report |
| `DiffResult` / `DiffRow` / `DiffCell` | `Models/` | Immutable result model |

### HTML report data contract

The generator embeds all grid data as `window.diffData` in a `<script>` tag using a compact JSON structure:

```
window.diffData = {
  "h":  string[],          // column headers
  "hr": string[]|undefined, // renamed headers (right-side names, same length as h)
  "c":  bool[],            // per-column has-changes flag
  "r":  row[]              // rows
};
```

Each row is `[rowIndex, leftRowIndex, rightRowIndex, rowStatusCode, cell[]]`.  
Each cell is `[displayValue, cellStatusCode, htmlOrNull, isFormatOnly, leftValue, rightValue]`.  
Status codes: 0 = unchanged, 1 = added, 2 = removed, 3 = modified, 4 = reordered.

The JavaScript in the report depends on the element IDs listed below — do not rename them:
`tools-curtain`, `tools-panel`, `tools-toggle`, `diff-grid`, `view-table`, `view-text`,
`text-diff-content`, `autosize-columns-btn`, `hide-unchanged-cols`, `highlight-rows`,
`highlight-cells`, `whole-value-diff`.

---

## Adding a new file reader

1. Implement `IFileReader` in `DiffCheck.Core/Readers/`.
2. Register the new type in `FileReaderFactory.cs`.
3. Add unit tests in `DiffCheck.Core.Tests/Readers/`.

---

## Open issues (`PLANNING.md`)

`PLANNING.md` tracks known findings and feature suggestions. After resolving a finding, remove it from the file.  
Current open findings (as of last update):

- **Medium** — Web upload validation lacks explicit file-size limits.
- **Medium** — CLI does not enforce non-zero exit codes on failure.
- **Low** — CLI README missing docs for `--column-map` and `--key-columns`.
