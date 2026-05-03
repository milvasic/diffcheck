namespace DiffCheck.Models;

/// <summary>
/// Result of comparing two data tables.
/// </summary>
public sealed class DiffResult(
	IReadOnlyList<string> headers,
	IReadOnlyList<DiffRow> rows,
	DiffSummary summary,
	int leftRowCount = 0,
	int leftColumnCount = 0,
	int rightRowCount = 0,
	int rightColumnCount = 0,
	IReadOnlyList<string?>? columnHeaderRenames = null
)
{
	/// <summary>
	/// Merged headers from both files (union of all columns).
	/// </summary>
	public IReadOnlyList<string> Headers { get; } =
		headers ?? throw new ArgumentNullException(nameof(headers));

	/// <summary>
	/// All diff rows with their status and cell-level differences.
	/// </summary>
	public IReadOnlyList<DiffRow> Rows { get; } =
		rows ?? throw new ArgumentNullException(nameof(rows));

	/// <summary>
	/// Summary statistics.
	/// </summary>
	public DiffSummary Summary { get; } =
		summary ?? throw new ArgumentNullException(nameof(summary));

	/// <summary>
	/// Row count in the left file.
	/// </summary>
	public int LeftRowCount { get; } = leftRowCount;

	/// <summary>
	/// Column count in the left file.
	/// </summary>
	public int LeftColumnCount { get; } = leftColumnCount;

	/// <summary>
	/// Row count in the right file.
	/// </summary>
	public int RightRowCount { get; } = rightRowCount;

	/// <summary>
	/// Column count in the right file.
	/// </summary>
	public int RightColumnCount { get; } = rightColumnCount;

	/// <summary>
	/// For each header index, the right-side column name when the column was mapped (renamed).
	/// Same length as <see cref="Headers"/>; null or empty entries mean the column is not a rename.
	/// Used to display "LeftName → RightName" and to highlight the header as modified.
	/// </summary>
	public IReadOnlyList<string?>? ColumnHeaderRenames { get; } =
		columnHeaderRenames?.Count == headers.Count ? columnHeaderRenames : null;
}

/// <summary>
/// Status of a row in the diff.
/// </summary>
public enum DiffRowStatus
{
	/// <summary>
	/// Row exists in both files with identical values.
	/// </summary>
	Unchanged,

	/// <summary>
	/// Row was added (exists only in the second file).
	/// </summary>
	Added,

	/// <summary>
	/// Row was removed (exists only in the first file).
	/// </summary>
	Removed,

	/// <summary>
	/// Row exists in both but has modified cells.
	/// </summary>
	Modified,

	/// <summary>
	/// Row exists in both files with same content but at a different position.
	/// </summary>
	Reordered,
}

/// <summary>
/// A single row in the diff result.
/// </summary>
public sealed class DiffRow(
	int rowIndex,
	DiffRowStatus status,
	IReadOnlyList<DiffCell> cells,
	int? leftRowIndex = null,
	int? rightRowIndex = null
)
{
	/// <summary>
	/// 1-based row index for display.
	/// </summary>
	public int RowIndex { get; } = rowIndex;

	/// <summary>
	/// Status of this row.
	/// </summary>
	public DiffRowStatus Status { get; } = status;

	/// <summary>
	/// Cell values and their diff status.
	/// </summary>
	public IReadOnlyList<DiffCell> Cells { get; } =
		cells ?? throw new ArgumentNullException(nameof(cells));

	/// <summary>
	/// 1-based original row index in the left file. Null for added rows.
	/// </summary>
	public int? LeftRowIndex { get; } = leftRowIndex;

	/// <summary>
	/// 1-based original row index in the right file. Null for removed rows.
	/// </summary>
	public int? RightRowIndex { get; } = rightRowIndex;
}

/// <summary>
/// Status of a cell in the diff.
/// </summary>
public enum DiffCellStatus
{
	Unchanged,
	Added,
	Removed,
	Modified,
	Reordered,
}

/// <summary>
/// A single cell in the diff result.
/// </summary>
public sealed class DiffCell(
	string header,
	string? leftValue,
	string? rightValue,
	string displayValue,
	DiffCellStatus status
)
{
	/// <summary>
	/// Column header.
	/// </summary>
	public string Header { get; } = header ?? throw new ArgumentNullException(nameof(header));

	/// <summary>
	/// Value from the first (left) file.
	/// </summary>
	public string? LeftValue { get; } = leftValue;

	/// <summary>
	/// Value from the second (right) file.
	/// </summary>
	public string? RightValue { get; } = rightValue;

	/// <summary>
	/// Display value (for unchanged: either value; for modified: both shown).
	/// </summary>
	public string DisplayValue { get; } =
		displayValue ?? throw new ArgumentNullException(nameof(displayValue));

	/// <summary>
	/// Status of this cell.
	/// </summary>
	public DiffCellStatus Status { get; } = status;
}

/// <summary>
/// Summary of the diff.
/// </summary>
public sealed class DiffSummary(
	int addedRows,
	int removedRows,
	int modifiedRows,
	int unchangedRows,
	int reorderedRows = 0
)
{
	public int AddedRows { get; } = addedRows;
	public int RemovedRows { get; } = removedRows;
	public int ModifiedRows { get; } = modifiedRows;
	public int UnchangedRows { get; } = unchangedRows;
	public int ReorderedRows { get; } = reorderedRows;
	public int TotalRows { get; } =
		addedRows + removedRows + modifiedRows + unchangedRows + reorderedRows;
	public bool HasDifferences { get; } =
		addedRows > 0 || removedRows > 0 || modifiedRows > 0 || reorderedRows > 0;
}
