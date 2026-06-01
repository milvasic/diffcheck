using System.Text.Json;
using System.Text.Json.Serialization;
using DiffCheck.Models;

namespace DiffCheck.Json;

/// <summary>
/// Serializes a <see cref="DiffResult"/> to a machine-readable JSON report.
/// </summary>
public static class DiffResultJsonSerializer
{
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
	};

	/// <summary>
	/// Serializes <paramref name="result"/> to a JSON string.
	/// </summary>
	public static string Serialize(DiffResult result)
	{
		ArgumentNullException.ThrowIfNull(result);
		return JsonSerializer.Serialize(ToReport(result), SerializerOptions);
	}

	/// <summary>
	/// Writes the JSON report to a file at <paramref name="outputPath"/>.
	/// </summary>
	public static async Task WriteToFileAsync(
		DiffResult result,
		string outputPath,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentException.ThrowIfNullOrEmpty(outputPath);

		var json = Serialize(result);
		await File.WriteAllTextAsync(outputPath, json, cancellationToken);
	}

	private static DiffReportJson ToReport(DiffResult result) =>
		new(
			Summary: new DiffSummaryJson(
				AddedRows: result.Summary.AddedRows,
				RemovedRows: result.Summary.RemovedRows,
				ModifiedRows: result.Summary.ModifiedRows,
				UnchangedRows: result.Summary.UnchangedRows,
				ReorderedRows: result.Summary.ReorderedRows
			),
			Columns: result.Headers,
			Rows: result.Rows.Select(ToRow).ToList()
		);

	private static DiffRowJson ToRow(DiffRow row) =>
		new(
			Status: row.Status,
			LeftRowIndex: row.LeftRowIndex,
			RightRowIndex: row.RightRowIndex,
			Cells: row.Cells.Select(ToCell).ToList()
		);

	private static DiffCellJson ToCell(DiffCell cell) =>
		new(
			Column: cell.Header,
			Status: cell.Status,
			LeftValue: cell.LeftValue,
			RightValue: cell.RightValue
		);

	private sealed record DiffReportJson(
		DiffSummaryJson Summary,
		IReadOnlyList<string> Columns,
		IReadOnlyList<DiffRowJson> Rows
	);

	private sealed record DiffSummaryJson(
		int AddedRows,
		int RemovedRows,
		int ModifiedRows,
		int UnchangedRows,
		int ReorderedRows
	);

	private sealed record DiffRowJson(
		DiffRowStatus Status,
		int? LeftRowIndex,
		int? RightRowIndex,
		IReadOnlyList<DiffCellJson> Cells
	);

	private sealed record DiffCellJson(
		string Column,
		DiffCellStatus Status,
		string? LeftValue,
		string? RightValue
	);
}
