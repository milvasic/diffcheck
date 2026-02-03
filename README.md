# DiffCheck

A .NET 10 library for comparing CSV and XLSX files. Produces HTML reports with color-coded differences.

## Features

- **CSV support** – Compare comma-separated and tab-delimited files
- **XLSX support** – Compare Excel workbooks (first sheet by default)
- **HTML output** – Color-coded table: green (added), red (removed), amber (modified)
- **Reusable library** – Use in CLI tools, web APIs, or other .NET projects

## Installation

Add the project reference to your solution, or publish as a NuGet package.

## Usage

### Basic comparison (auto-detect format)

```csharp
using DiffCheck;

var service = new DiffCheckService();

// Compare and save HTML report
await service.CompareAndSaveHtmlAsync(
    leftFilePath: "file1.csv",
    rightFilePath: "file2.csv",
    outputPath: "diff-report.html");
```

### Step-by-step (for custom workflows)

```csharp
// 1. Compare files
var result = await service.CompareAsync("old.xlsx", "new.xlsx");

// 2. Check summary
Console.WriteLine($"Added: {result.Summary.AddedRows}, Removed: {result.Summary.RemovedRows}");

// 3. Generate HTML
var html = service.GenerateHtml(result, "old.xlsx", "new.xlsx");

// Or write to file
await service.WriteHtmlToFileAsync(result, "report.html", "old.xlsx", "new.xlsx");
```

### Custom HTML colors

```csharp
var options = new HtmlReportOptions
{
    AddedColor = "#22c55e",    // green
    RemovedColor = "#ef4444",  // red
    ModifiedColor = "#f59e0b"   // amber
};
var service = new DiffCheckService(options);
```

### Custom readers (e.g., specific XLSX sheet)

```csharp
var xlsxReader = new XlsxReader(sheetIndex: 1); // second sheet
var service = new DiffCheckService(xlsxReader, xlsxReader);
var result = await service.CompareAsync("a.xlsx", "b.xlsx");
```

## Supported formats

| Extension | Format        |
|-----------|---------------|
| `.csv`    | CSV           |
| `.txt`    | CSV (treated) |
| `.xlsx`   | Excel 2007+   |
| `.xlsm`   | Excel macro   |

## Formatting

This project uses [CSharpier](https://csharpier.com/) for code formatting.

```bash
# Format all C# files
dotnet csharpier format .
```

Format-on-save is configured in `.vscode/settings.json` when using the CSharpier extension.

CSharpier runs automatically on pre-commit via [Husky.NET](https://alirezanet.github.io/Husky.Net/). Git hooks are installed automatically on `dotnet restore`.

## Requirements

- .NET 10.0 (or change `TargetFramework` to `net8.0` if needed)

## Dependencies

- [ClosedXML](https://github.com/ClosedXML/ClosedXML) – XLSX reading
- [CsvHelper](https://github.com/JoshClose/CsvHelper) – CSV parsing
