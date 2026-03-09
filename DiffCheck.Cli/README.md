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
| `--column-map` | | Column mapping in `LeftHeader:RightHeader` format. Can be specified multiple times | |
| `--key-columns` | | Column name(s) to match rows by key (faster matching). Comma-separated or multiple flags | |
| `--profile` | | Load a saved profile by name. Explicit `--key-columns`/`--column-map` flags override profile values | |
| `--save-profile` | | After a successful run, save the effective settings as a named profile | |

### Subcommands

| Subcommand | Description |
|------------|-------------|
| `list-profiles` | List all saved comparison profiles |

### Examples

```bash
# Compare two CSV files, output to default diff-report.html
diffcheck old.csv new.csv

# Compare Excel files with custom output path
diffcheck v1.xlsx v2.xlsx -o report.html

# Same using long option
diffcheck left.csv right.csv --output changes.html

# Match rows by key column
diffcheck old.csv new.csv --key-columns ID

# Match rows by composite key
diffcheck old.csv new.csv --key-columns ID,Name

# Map renamed columns
diffcheck old.csv new.csv --column-map "Name:FullName" --column-map "Dept:Department"

# Save settings as a reusable profile
diffcheck old.csv new.csv --key-columns ID --column-map "Name:FullName" --save-profile my-preset

# Run using a saved profile
diffcheck old.csv new.csv --profile my-preset

# Run with a profile, overriding key columns
diffcheck old.csv new.csv --profile my-preset --key-columns OrderID

# List all saved profiles
diffcheck list-profiles
```

## Supported formats

- `.csv`, `.txt` – CSV files
- `.xlsx`, `.xlsm` – Excel workbooks (first sheet)

## Requirements

- .NET 10.0
