namespace DiffCheck.Models;

/// <summary>
/// Represents tabular data loaded from CSV or XLSX files.
/// </summary>
public sealed class DataTable(
	IReadOnlyList<string> headers,
	IReadOnlyList<IReadOnlyList<string>> rows,
	string? sourcePath = null
)
{
	/// <summary>
	/// Column headers (first row).
	/// </summary>
	public IReadOnlyList<string> Headers { get; } =
		headers ?? throw new ArgumentNullException(nameof(headers));

	/// <summary>
	/// Data rows (excluding header).
	/// </summary>
	public IReadOnlyList<IReadOnlyList<string>> Rows { get; } =
		rows ?? throw new ArgumentNullException(nameof(rows));

	/// <summary>
	/// Source file path (for display purposes).
	/// </summary>
	public string? SourcePath { get; } = sourcePath;

	/// <summary>
	/// Gets the number of columns.
	/// </summary>
	public int ColumnCount => Headers.Count;

	/// <summary>
	/// Gets the number of data rows.
	/// </summary>
	public int RowCount => Rows.Count;
}
