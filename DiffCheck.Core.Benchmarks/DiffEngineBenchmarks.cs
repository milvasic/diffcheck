using System.Globalization;
using BenchmarkDotNet.Attributes;
using DiffCheck.Diff;
using DiffCheck.Models;

namespace DiffCheck.Core.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 1, iterationCount: 3)]
public class DiffEngineScaleBenchmarks
{
	private static readonly IReadOnlyList<string> KeyColumns = ["C1"];

	private DataTable _leftNoKeys = null!;
	private DataTable _rightNoKeys = null!;
	private DiffEngine _engine = null!;

	[Params(
		10_000 /*, 100_000, 200_000*/
	)]
	public int RowCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_engine = new DiffEngine();
		(_leftNoKeys, _rightNoKeys) = BenchmarkData.BuildDataSet(RowCount);
	}

	[Benchmark(Baseline = true, Description = "No keys, content index (NumericTolerance = 0)")]
	public DiffSummary Compare_NoKeys_ContentIndex()
	{
		var result = _engine.Compare(_leftNoKeys, _rightNoKeys);
		return result.Summary;
	}

	[Benchmark(Description = "Key columns lookup")]
	public DiffSummary Compare_WithKeys()
	{
		var result = _engine.Compare(_leftNoKeys, _rightNoKeys, keyColumns: KeyColumns);
		return result.Summary;
	}
}

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 1, iterationCount: 3)]
public class DiffEngineBeforeAfterBenchmarks
{
	private DataTable _leftNoKeys = null!;
	private DataTable _rightNoKeys = null!;
	private DiffEngine _engine = null!;

	[Params(1_000, 10_000)]
	public int RowCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_engine = new DiffEngine();
		(_leftNoKeys, _rightNoKeys) = BenchmarkData.BuildDataSet(RowCount);
	}

	[Benchmark(Baseline = true, Description = "Before proxy: linear scan (NumericTolerance > 0)")]
	public DiffSummary Compare_NoKeys_LinearFallback()
	{
		var result = _engine.Compare(
			_leftNoKeys,
			_rightNoKeys,
			options: new ComparisonOptions { NumericTolerance = 0.0001 }
		);
		return result.Summary;
	}

	[Benchmark(Description = "After: content index (NumericTolerance = 0)")]
	public DiffSummary Compare_NoKeys_ContentIndex()
	{
		var result = _engine.Compare(_leftNoKeys, _rightNoKeys);
		return result.Summary;
	}
}

internal static class BenchmarkData
{
	internal static readonly string[] Headers = [.. Enumerable.Range(1, 10).Select(i => $"C{i}")];

	internal static (DataTable Left, DataTable Right) BuildDataSet(int rowCount)
	{
		var leftRows = new List<IReadOnlyList<string>>(rowCount);
		var rightRows = new List<IReadOnlyList<string>>(rowCount);

		var removedStart = rowCount * 9 / 10;
		for (var i = 0; i < rowCount; i++)
		{
			var id = i + 1;
			var leftRow = CreateBaseRow(id);
			leftRows.Add(leftRow);

			if (i >= removedStart)
				continue;

			var rightRow = i % 5 == 0 ? CreateModifiedRow(leftRow) : leftRow;
			rightRows.Add(rightRow);
		}

		var addedCount = rowCount - removedStart;
		for (var i = 0; i < addedCount; i++)
		{
			var id = rowCount + i + 1;
			rightRows.Add(CreateBaseRow(id));
		}

		return (new DataTable(Headers, leftRows), new DataTable(Headers, rightRows));
	}

	private static string[] CreateBaseRow(int id)
	{
		return
		[
			id.ToString(CultureInfo.InvariantCulture),
			$"Name_{id % 1000}",
			$"Category_{id % 50}",
			$"Region_{id % 20}",
			(id * 13 % 10000).ToString(CultureInfo.InvariantCulture),
			(id * 0.125).ToString("0.000", CultureInfo.InvariantCulture),
			$"Status_{id % 7}",
			$"Code_{id % 97}",
			$"Flag_{id % 2}",
			$"Notes_{id % 23}",
		];
	}

	private static string[] CreateModifiedRow(string[] baseRow)
	{
		return
		[
			baseRow[0],
			baseRow[1],
			baseRow[2],
			baseRow[3],
			baseRow[4],
			baseRow[5],
			baseRow[6],
			baseRow[7],
			baseRow[8],
			baseRow[9] + "_changed",
		];
	}
}
