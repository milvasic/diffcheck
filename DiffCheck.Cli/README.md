# diffcheck

Command-line tool for comparing CSV and XLSX files. Generates an HTML diff report with color-coded differences.

## Installation

### Run from source

```bash
dotnet run --project DiffCheck.Cli -- left.csv right.csv
```

### Install as global .NET tool

```bash
cd DiffCheck.Cli
dotnet pack
dotnet tool install --global --add-source ./builds diffcheck
```

## Usage

```bash
diffcheck <left> <right> [options]
```

### Arguments

| Argument | Description |
|----------|--------------|
| `left`   | Path to the first (original) file |
| `right`  | Path to the second (modified) file |

### Options

| Option | Short | Description | Default |
|--------|-------|--------------|---------|
| `--output` | `-o` | Path for the output HTML report | `diff-report.html` |

### Examples

```bash
# Compare two CSV files, output to default diff-report.html
diffcheck old.csv new.csv

# Compare Excel files with custom output path
diffcheck v1.xlsx v2.xlsx -o report.html

# Same using long option
diffcheck left.csv right.csv --output changes.html
```

## Supported formats

- `.csv`, `.txt` – CSV files
- `.xlsx`, `.xlsm` – Excel workbooks (first sheet)

## Requirements

- .NET 10.0
