# DiffCheck Planning

Date: 2026-03-07

## Findings

1. Medium - CLI error handling does not clearly enforce non-zero process exit codes for automation usage.
   - Evidence:
     - `DiffCheck.Cli/Program.cs:103`
     - `DiffCheck.Cli/Program.cs:105`
   - Risk: CI scripts may treat failed runs as successful.
   - Suggested fix:
     - Return explicit non-zero exit codes on argument parsing and runtime failures.
     - Add CLI tests for success/failure exit behavior.

2. Low - CLI README does not document advanced options (`--column-map`, `--key-columns`).
   - Evidence:
     - `DiffCheck.Cli/README.md:38`
     - `DiffCheck.Cli/Program.cs:15`
     - `DiffCheck.Cli/Program.cs:23`
   - Risk: discoverability gap for existing power features.
   - Suggested fix:
     - Update usage/options table and add examples for mapped headers and key-based matching.

## Feature Suggestions

1. Workbook-aware Excel diff (high value)
   - Add `--left-sheet`, `--right-sheet`, and `--all-sheets` options.
   - Provide per-sheet and aggregate summaries in HTML output.
   - References:
     - `DiffCheck.Core/Readers/XlsxReader.cs:17`
     - `DiffCheck.Core/Readers/XlsxReader.cs:44`

2. Configurable matching and normalization rules (high value)
   - Add options for match threshold, case-insensitive compare, trim/whitespace normalization, and numeric/date tolerance.
   - Keep defaults compatible with current behavior.
   - References:
     - `DiffCheck.Core/Diff/DiffEngine.cs:11`
     - `DiffCheck.Core/Diff/DiffEngine.cs:282`
     - `DiffCheck.Core/Diff/DiffEngine.cs:308`

3. Machine-readable outputs and API mode (medium-high value)
   - Add `--format json` for CLI and optional `/api/compare` endpoint for integrations.
   - Preserve HTML as default while enabling CI/data pipeline usage.
   - References:
     - `DiffCheck.Core/Models/DiffResult.cs:8`
     - `DiffCheck.Cli/Program.cs:8`
     - `DiffCheck.Web/Program.cs:4`

4. CI gating features (medium value)
   - Add options like `--fail-on-diff`, `--max-added`, `--max-removed`, and `--max-modified`.
   - Return non-zero exit code when thresholds are exceeded.
   - References:
     - `DiffCheck.Cli/Program.cs:31`
     - `DiffCheck.Cli/Program.cs:101`

## Suggested Execution Order

1. Align stale tests and docs (low effort, immediate quality gain).
2. Add CLI exit-code guarantees and CI gating options.
3. Harden web uploads with explicit limits and validation.
4. Implement workbook-aware sheet diff.
5. Add machine-readable/API output.