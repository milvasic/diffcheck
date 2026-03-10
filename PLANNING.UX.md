# DiffCheck UX Planning

Date: 2026-03-10

## Idea B — Inline Settings Bar with Smart Chips

### Summary

Replace the four hidden Bootstrap collapse sections (Profiles, Key Columns, Column Mappings,
Normalization) with a single always-visible **settings bar** below the file drop zones. Use
tag/chip inputs instead of raw textareas, auto-apply profiles on selection, and inline toggle
pills for normalization. Profiles are extended to save all settings (including normalization
options).

### Motivation

Current UX problems:

1. **Invisible by default** — All 4 sections are collapsed behind tiny `btn-link` toggles.
   Users don't know they exist.
2. **Fragmented layout** — 3 independent collapses in a 3-column row + a separate row for
   normalization. No visual grouping.
3. **Raw text input** — Key columns and column mappings require memorising syntax
   (`LeftHeader:RightHeader`). Error-prone.
4. **Two-step profile apply** — Select a dropdown, then click "Apply". Save requires a separate
   name input. Takes 1/3 of horizontal space.
5. **No hierarchy** — The most commonly useful toggles (case-insensitive, trim) are buried the
   deepest.

---

### Phases

#### Phase 1 — Backend: Extend Profiles to Include Normalization Options

**Goal**: Profiles save & restore all comparison settings, not just key columns and mappings.

1. Update `OnGetProfiles` in `Index.cshtml.cs` to include `options` (caseSensitive,
   trimWhitespace, numericTolerance, matchThreshold) in the JSON response.
2. Update `OnPostSaveProfileAsync` in `Index.cshtml.cs` to accept normalization parameters
   (`caseInsensitive`, `trimWhitespace`, `numericToleranceRaw`, `matchThresholdRaw`) and pass a
   `ComparisonOptions` instance to the `ComparisonProfile` constructor.
3. Update the profiles JavaScript to send normalization fields on save and restore them on apply.

**Files to modify**:

- `DiffCheck.Web/Pages/Index.cshtml.cs` — `OnGetProfiles()` (add `options` to JSON shape),
  `OnPostSaveProfileAsync()` (accept + parse normalization params)
- `DiffCheck.Web/Pages/Index.cshtml` — profiles IIFE (send/receive normalization fields)

**Verification**: Save a profile with case-insensitive + tolerance set, reload, apply it —
fields should restore. Run `dotnet test`.

---

#### Phase 2 — HTML: Replace Collapse Sections with Settings Bar

**Goal**: Single horizontal bar with all settings visible at a glance.

4. Remove the three `col-md-4` collapse sections (Profiles, Key Columns, Column Mappings) and
   the separate `row` for Normalization options (currently lines ~67–165 in `Index.cshtml`).
5. Add a new **settings bar** `div` between the file drop zones row and the error alert:

   ```
   ┌───────────────────────────────────────────────────────────────────────────────┐
   │ [Profile ▾]  ●Case  ●Trim  Tolerance:[___]  Threshold:[___]  Keys:[chips]  Maps:[chips]  💾 🗑 │
   └───────────────────────────────────────────────────────────────────────────────┘
   ```

6. **Profile dropdown** — `<select>` that auto-applies on selection (no separate Apply button).
   Small save icon button and delete icon button inline.
7. **Normalization toggles** — Bootstrap `.btn-check` pill toggles for Case-insensitive and Trim
   whitespace (visually on/off pills, not plain checkboxes).
8. **Numeric tolerance & match threshold** — Keep as small inline `<input type="number">` with
   labels, always visible.
9. **Key columns chip input** — Replace the textarea with a chip/tag input:
   - A styled `<div>` that looks like a form control, containing chip `<span>` pills and a text
     `<input>`.
   - On Enter or comma: the typed value becomes a chip (small pill with an ✕ close button).
   - Chips are removable by clicking ✕.
   - A hidden `<input name="keyColumnsRaw">` is kept in sync with the chips (comma-separated).
10. **Column mappings chip input** — Same pattern but for paired values:
    - Each chip shows `Left → Right`.
    - Input accepts `Left:Right` or `Left,Right` syntax — on Enter, validates and creates a chip.
    - Alternatively: a small `+` button opens a two-field inline popover (Left input, Right
      input, Add button).
    - Hidden `<textarea name="columnMappingsRaw">` kept in sync (one `Left:Right` per line).

**Files to modify**:

- `DiffCheck.Web/Pages/Index.cshtml` — replace lines ~67–165 with the new settings bar HTML

**Verification**: Visual inspection — all settings visible in one row. Form still submits
correct values. Dark theme works.

---

#### Phase 3 — CSS: Style the Settings Bar and Chip Inputs

11. `.settings-bar` — subtle background, border-radius, horizontal flex layout that wraps
    gracefully on small screens.
12. `.chip-input` — container that mimics a form-control, with inline chips and a text input.
13. `.chip` — small rounded pills with text + close button, using the app's color scheme.
14. `.chip-input-pair` variant for column mappings (chips with arrow separator).
15. Dark theme overrides for all new classes (follow existing pattern in `theme.css`).
16. Responsive: on mobile (<768px) the settings bar stacks vertically; chip inputs take full
    width.

**Files to modify**:

- `DiffCheck.Web/wwwroot/css/site.css` — add `.settings-bar`, `.chip-input`, `.chip` rules
- `DiffCheck.Web/wwwroot/css/theme.css` — add `[data-theme="dark"]` overrides for new classes

**Verification**: Test both light/dark themes. Test at mobile and desktop widths.

---

#### Phase 4 — JavaScript: Chip Input Behavior and Profile Auto-Apply

17. Reusable `ChipInput` class/function that:
    - Takes a container element and a hidden input element.
    - Handles keydown (Enter, comma, Backspace to remove last chip).
    - Creates/removes chip DOM elements.
    - Syncs chips to the hidden input value.
    - Supports pre-populating from the hidden input's initial value.
18. `PairedChipInput` variant for column mappings:
    - Accepts `Left:Right` or `Left,Right` input.
    - Validates both sides are non-empty before creating a chip.
    - Optionally: `+` button that opens a mini two-field form.
    - Chip displays as `Left → Right`.
19. Rewrite profiles IIFE:
    - Remove the "Apply" button logic — `profileSelect` `change` event now auto-applies.
    - On apply: populate chip inputs (call `setChips()`) + set normalization toggles.
    - On save: read chips + toggles and send to server.
    - Delete: keep confirm dialog, use icon button.
20. Extend `saveOptionsToStorage` / `restoreOptionsFromStorage` to include normalization options
    and chip values.
21. Verify `checkAndSubmit` still works — hidden inputs stay in sync so FormData is unchanged.

**Files to modify**:

- `DiffCheck.Web/Pages/Index.cshtml` — rewrite the `@section Scripts` block (profiles IIFE,
  add chip input JS)
- Optionally extract chip input JS to `DiffCheck.Web/wwwroot/js/chip-input.js` if it exceeds
  ~80 lines

**Verification**:

- Type "ID" + Enter in key columns → chip appears, hidden input = "ID"
- Type "Name:FullName" + Enter in mappings → chip shows "Name → FullName"
- Select a profile → all chips, toggles, and inputs populate
- Save a profile → reload page → profile contains all settings
- Submit a comparison → server receives correct values

---

#### Phase 5 — Polish and Edge Cases

22. Placeholder text inside chip inputs: "Type column name + Enter" / "Type Left:Right + Enter".
23. Handle pasting multiple values (split by comma/newline, create multiple chips).
24. Add subtle animation on chip add/remove (CSS transition on opacity/transform).
25. Persist all settings bar state to localStorage — extend existing `saveOptionsToStorage`.
26. Ensure the settings bar collapses into the compact view when diff results are shown (follow
    the existing `drop-zone-compact` pattern).

**Files to modify**: Same as phases 3–4.

**Verification**: Paste "ID, Name, Date" → three chips appear. Refresh → settings restored.
Compare → settings bar shrinks.

---

### Relevant Files

| File                                         | What changes                                                                                |
| -------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `DiffCheck.Web/Pages/Index.cshtml`           | Replace lines ~67–165 with settings bar HTML. Rewrite profiles IIFE (~lines 568–694).       |
| `DiffCheck.Web/Pages/Index.cshtml.cs`        | `OnGetProfiles()` and `OnPostSaveProfileAsync()` — extend to include normalization options. |
| `DiffCheck.Web/wwwroot/css/site.css`         | Add `.settings-bar`, `.chip-input`, `.chip` rules.                                          |
| `DiffCheck.Web/wwwroot/css/theme.css`        | Add `[data-theme="dark"]` overrides for new classes.                                        |
| `DiffCheck.Core/Models/ComparisonProfile.cs` | Already has `Options` field — no changes needed.                                            |
| `DiffCheck.Core/Models/ComparisonOptions.cs` | Already has all fields — no changes needed.                                                 |

### Verification Checklist

1. `dotnet build` — no compile errors
2. `dotnet test` — all existing tests pass
3. Manual: light + dark theme both render the settings bar correctly
4. Manual: create chips for key columns and mappings, compare files, verify diff uses them
5. Manual: save a profile with all settings, reload, apply it, verify all fields restore
6. Manual: ≤768px viewport — settings bar wraps vertically
7. Manual: paste multi-value strings into chip inputs
8. `dotnet csharpier .` — formatting check

### Decisions

- Profiles now save **all settings** (normalization included) — confirmed
- No separate "Apply" button — auto-apply on dropdown selection
- Column mappings accept `Left:Right` syntax in chip input, chips display as `Left → Right`
- Hidden form inputs stay in sync with chips so FormData submission works without server changes
- No new server endpoints needed — existing handlers work with the same form field names
- Chip input JS lives inline initially; extract to a separate `.js` file if it exceeds ~80 lines

### Scope

**Included**: Settings bar UI, chip inputs, profile auto-apply, profile normalization
persistence, dark theme, responsive layout, localStorage persistence.

**Excluded**: No changes to `DiffCheck.Core` logic, the CLI, or the diff report HTML. No new
API endpoints.
