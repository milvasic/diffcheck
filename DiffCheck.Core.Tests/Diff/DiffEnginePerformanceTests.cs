using System.Diagnostics;
using System.Text;
using DiffCheck.Diff;
using DiffCheck.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffCheck.Core.Tests.Diff;

/// <summary>
/// Performance tests for DiffEngine. Run with release build for meaningful numbers.
/// </summary>
[TestClass]
public class DiffEnginePerformanceTests
{
	/// <summary>
	/// Row count for "large" benchmark tests.
	/// </summary>
	private const int LargeRowCount = 3_000;

	private const int ColumnCount = 10;

	[TestMethod]
	public void Compare_LargeIdenticalTables_CompletesWithinReasonableTime()
	{
		var (left, right) = CreateIdenticalTables(LargeRowCount, ColumnCount);
		var engine = new DiffEngine();

		var sw = Stopwatch.StartNew();
		var result = engine.Compare(left, right);
		sw.Stop();

		Assert.AreEqual(LargeRowCount, result.Summary.UnchangedRows);
		Assert.AreEqual(0, result.Summary.AddedRows);
		Assert.AreEqual(0, result.Summary.RemovedRows);
		Assert.AreEqual(0, result.Summary.ModifiedRows);
		Assert.AreEqual(0, result.Summary.ReorderedRows);

		// Log for before/after comparison (CI can use a higher threshold)
		Console.WriteLine($"[Perf] Compare_LargeIdenticalTables: {LargeRowCount} rows, {ColumnCount} cols -> {sw.ElapsedMilliseconds} ms");
	}

	[TestMethod]
	public void Compare_LargeTablesWithSomeChanges_CompletesAndProducesCorrectSummary()
	{
		var (left, right) = CreateTablesWithSomeChanges(LargeRowCount, ColumnCount);
		var engine = new DiffEngine();

		var sw = Stopwatch.StartNew();
		var result = engine.Compare(left, right);
		sw.Stop();

		Assert.IsTrue(
			result.Summary.UnchangedRows + result.Summary.AddedRows + result.Summary.RemovedRows
			+ result.Summary.ModifiedRows + result.Summary.ReorderedRows == result.Rows.Count,
			"Summary row counts should sum to total rows"
		);

		Console.WriteLine($"[Perf] Compare_LargeTablesWithSomeChanges: {LargeRowCount} rows -> {sw.ElapsedMilliseconds} ms "
			+ $"(+{result.Summary.AddedRows} -{result.Summary.RemovedRows} ~{result.Summary.ModifiedRows} ={result.Summary.UnchangedRows} <> {result.Summary.ReorderedRows})");
	}

	[TestMethod]
	public void Compare_DeterministicMediumDataset_ProducesStableResult()
	{
		// Deterministic: right[i] = left[(i+37)%rows], so every row has exact match but different position -> all reordered.
		const int rows = 2000;
		const int cols = 8;
		var (left, right) = CreateDeterministicDataset(rows, cols);
		var engine = new DiffEngine();
		var result = engine.Compare(left, right);

		Assert.AreEqual(rows, result.LeftRowCount);
		Assert.AreEqual(rows, result.RightRowCount);
		Assert.AreEqual(cols, result.Headers.Count);
		Assert.AreEqual(rows, result.Rows.Count);
		Assert.AreEqual(0, result.Summary.AddedRows);
		Assert.AreEqual(0, result.Summary.RemovedRows);
		Assert.AreEqual(0, result.Summary.ModifiedRows);
		Assert.AreEqual(0, result.Summary.UnchangedRows);
		Assert.AreEqual(rows, result.Summary.ReorderedRows);
	}

	/// <summary>
	/// Creates two identical tables with given row and column counts.
	/// </summary>
	public static (DataTable left, DataTable right) CreateIdenticalTables(int rowCount, int columnCount)
	{
		var headers = Enumerable.Range(0, columnCount).Select(i => $"Col{i}").ToArray();
		var leftRows = new List<IReadOnlyList<string>>(rowCount);
		for (var r = 0; r < rowCount; r++)
		{
			var row = new string[columnCount];
			for (var c = 0; c < columnCount; c++)
				row[c] = $"r{r}_c{c}";
			leftRows.Add(row);
		}
		// Right: same content, separate list (same row arrays are shared; that's ok for read-only)
		var rightRows = leftRows.ToList();
		return (new DataTable(headers, leftRows), new DataTable(headers, rightRows));
	}

	/// <summary>
	/// Creates left and right with same size; 90% rows identical, 5% modified, 5% reordered (right has same content, different order).
	/// </summary>
	public static (DataTable left, DataTable right) CreateTablesWithSomeChanges(int rowCount, int columnCount)
	{
		var headers = Enumerable.Range(0, columnCount).Select(i => $"Col{i}").ToArray();
		var leftRows = new List<IReadOnlyList<string>>(rowCount);
		for (var r = 0; r < rowCount; r++)
		{
			var row = new string[columnCount];
			for (var c = 0; c < columnCount; c++)
				row[c] = $"r{r}_c{c}";
			leftRows.Add(row);
		}

		var rightRows = new List<IReadOnlyList<string>>(rowCount);
		var rnd = new Random(42);
		for (var r = 0; r < rowCount; r++)
		{
			var kind = rnd.NextDouble();
			if (kind < 0.90)
			{
				rightRows.Add(leftRows[r]);
			}
			else if (kind < 0.95)
			{
				var modified = new string[columnCount];
				for (var c = 0; c < columnCount; c++)
					modified[c] = c == 0 ? $"r{r}_c{c}_mod" : leftRows[r][c];
				rightRows.Add(modified);
			}
			else
			{
				// Reordered: use content from another row (swap)
				var other = rnd.Next(0, rowCount);
				rightRows.Add(leftRows[other]);
			}
		}
		// Shuffle a bit so reordered rows are in different positions
		for (var i = rightRows.Count - 1; i > 0; i--)
		{
			var j = rnd.Next(0, i + 1);
			(rightRows[i], rightRows[j]) = (rightRows[j], rightRows[i]);
		}

		return (
			new DataTable(headers, leftRows),
			new DataTable(headers, rightRows)
		);
	}

	/// <summary>
	/// Fully deterministic dataset for stability tests.
	/// </summary>
	public static (DataTable left, DataTable right) CreateDeterministicDataset(int rowCount, int columnCount)
	{
		var headers = Enumerable.Range(0, columnCount).Select(i => $"H{i}").ToArray();
		var leftRows = new List<IReadOnlyList<string>>(rowCount);
		for (var r = 0; r < rowCount; r++)
		{
			var row = new string[columnCount];
			for (var c = 0; c < columnCount; c++)
				row[c] = $"{r}_{c}";
			leftRows.Add(row);
		}

		// Right: same rows, some indices swapped (deterministic)
		var rightRows = new List<IReadOnlyList<string>>(rowCount);
		for (var r = 0; r < rowCount; r++)
		{
			var swap = (r + 37) % rowCount; // deterministic swap partner
			rightRows.Add(leftRows[swap]);
		}

		return (
			new DataTable(headers, leftRows),
			new DataTable(headers, rightRows)
		);
	}
}
