using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DiffCheck.Core.Tests.TestData;
using DiffCheck.Models;
// Alias required because the 'DiffEngine' NuGet package (pulled in transitively by Verify.MSTest)
// declares a 'DiffEngine' namespace that conflicts with our DiffCheck.Diff.DiffEngine class.
using DiffCheckDiffEngine = DiffCheck.Diff.DiffEngine;

namespace DiffCheck.Core.Tests.Diff;

[TestClass]
public sealed class DiffEngineBenchmarkSnapshotTests : VerifyBase
{
	private static readonly IReadOnlyList<string> KeyColumns = ["C1"];
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	[TestMethod]
	[DataRow(1_000)]
	[DataRow(10_000)]
	public Task Compare_NoKeys_MatchesSnapshot(int rowCount)
	{
		var snapshot = BuildScenarioSnapshot(scenarioName: "Compare_NoKeys", rowCount: rowCount);
		return VerifyJson(JsonSerializer.Serialize(snapshot, JsonOptions)).UseParameters(rowCount);
	}

	[TestMethod]
	[DataRow(1_000)]
	[DataRow(10_000)]
	public Task Compare_WithKeys_MatchesSnapshot(int rowCount)
	{
		var snapshot = BuildScenarioSnapshot(
			scenarioName: "Compare_WithKeys",
			rowCount: rowCount,
			keyColumns: KeyColumns
		);
		return VerifyJson(JsonSerializer.Serialize(snapshot, JsonOptions)).UseParameters(rowCount);
	}

	private static BenchmarkScenarioSnapshot BuildScenarioSnapshot(
		string scenarioName,
		int rowCount,
		IReadOnlyList<string>? keyColumns = null,
		ComparisonOptions? options = null
	)
	{
		var (left, right) = MockTestData.BuildDataSet(rowCount);
		var result = new DiffCheckDiffEngine().Compare(
			left,
			right,
			keyColumns: keyColumns,
			options: options
		);

		return new BenchmarkScenarioSnapshot(
			Scenario: scenarioName,
			Input: BuildInputSnapshot(left, right),
			Output: BuildOutputSnapshot(result)
		);
	}

	private static BenchmarkInputSnapshot BuildInputSnapshot(DataTable left, DataTable right)
	{
		return new BenchmarkInputSnapshot(
			Headers: [.. left.Headers],
			LeftRowCount: left.Rows.Count,
			RightRowCount: right.Rows.Count,
			LeftDataSha256: ComputeTableHash(left),
			RightDataSha256: ComputeTableHash(right),
			LeftFirstRows: [.. left.Rows.Take(3).Select(row => row.ToArray())],
			RightFirstRows: [.. right.Rows.Take(3).Select(row => row.ToArray())],
			LeftLastRows: [.. left.Rows.TakeLast(3).Select(row => row.ToArray())],
			RightLastRows: [.. right.Rows.TakeLast(3).Select(row => row.ToArray())]
		);
	}

	private static BenchmarkOutputSnapshot BuildOutputSnapshot(DiffResult result)
	{
		return new BenchmarkOutputSnapshot(
			Headers: [.. result.Headers],
			Summary: new DiffSummarySnapshot(
				result.Summary.AddedRows,
				result.Summary.RemovedRows,
				result.Summary.ModifiedRows,
				result.Summary.UnchangedRows,
				result.Summary.ReorderedRows,
				result.Summary.TotalRows,
				result.Summary.HasDifferences
			),
			RowCount: result.Rows.Count,
			RowsSha256: ComputeResultHash(result),
			FirstRows: [.. result.Rows.Take(5).Select(BuildRowPreview)],
			LastRows: [.. result.Rows.TakeLast(5).Select(BuildRowPreview)]
		);
	}

	private static RowPreviewSnapshot BuildRowPreview(DiffRow row)
	{
		return new RowPreviewSnapshot(
			RowIndex: row.RowIndex,
			Status: row.Status.ToString(),
			LeftRowIndex: row.LeftRowIndex,
			RightRowIndex: row.RightRowIndex,
			ChangedHeaders:
			[
				.. row
					.Cells.Where(cell => cell.Status != DiffCellStatus.Unchanged)
					.Select(cell => cell.Header),
			]
		);
	}

	private static string ComputeTableHash(DataTable table)
	{
		var sb = new StringBuilder();
		sb.AppendJoin('\u001F', table.Headers);
		sb.Append('\n');

		foreach (var row in table.Rows)
		{
			sb.AppendJoin('\u001F', row);
			sb.Append('\n');
		}

		return ComputeHash(sb.ToString());
	}

	private static string ComputeResultHash(DiffResult result)
	{
		var sb = new StringBuilder();
		sb.AppendJoin('\u001F', result.Headers);
		sb.Append('\n');
		sb.Append(
			$"{result.Summary.AddedRows}|{result.Summary.RemovedRows}|{result.Summary.ModifiedRows}|{result.Summary.UnchangedRows}|{result.Summary.ReorderedRows}"
		);
		sb.Append('\n');

		foreach (var row in result.Rows)
		{
			sb.Append($"{row.RowIndex}|{row.Status}|{row.LeftRowIndex}|{row.RightRowIndex}");
			sb.Append('\n');
			foreach (var cell in row.Cells)
			{
				sb.Append(
					$"{cell.Header}|{cell.Status}|{cell.LeftValue}|{cell.RightValue}|{cell.DisplayValue}"
				);
				sb.Append('\n');
			}
		}

		return ComputeHash(sb.ToString());
	}

	private static string ComputeHash(string value)
	{
		var bytes = Encoding.UTF8.GetBytes(value);
		var hashBytes = SHA256.HashData(bytes);
		return Convert.ToHexString(hashBytes);
	}
}

internal sealed record BenchmarkScenarioSnapshot(
	string Scenario,
	BenchmarkInputSnapshot Input,
	BenchmarkOutputSnapshot Output
);

internal sealed record BenchmarkInputSnapshot(
	IReadOnlyList<string> Headers,
	int LeftRowCount,
	int RightRowCount,
	string LeftDataSha256,
	string RightDataSha256,
	IReadOnlyList<string[]> LeftFirstRows,
	IReadOnlyList<string[]> RightFirstRows,
	IReadOnlyList<string[]> LeftLastRows,
	IReadOnlyList<string[]> RightLastRows
);

internal sealed record BenchmarkOutputSnapshot(
	IReadOnlyList<string> Headers,
	DiffSummarySnapshot Summary,
	int RowCount,
	string RowsSha256,
	IReadOnlyList<RowPreviewSnapshot> FirstRows,
	IReadOnlyList<RowPreviewSnapshot> LastRows
);

internal sealed record DiffSummarySnapshot(
	int AddedRows,
	int RemovedRows,
	int ModifiedRows,
	int UnchangedRows,
	int ReorderedRows,
	int TotalRows,
	bool HasDifferences
);

internal sealed record RowPreviewSnapshot(
	int RowIndex,
	string Status,
	int? LeftRowIndex,
	int? RightRowIndex,
	IReadOnlyList<string> ChangedHeaders
);
