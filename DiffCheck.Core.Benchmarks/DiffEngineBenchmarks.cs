using BenchmarkDotNet.Attributes;
using DiffCheck.Core.Tests.TestData;
using DiffCheck.Diff;
using DiffCheck.Models;

namespace DiffCheck.Core.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 1, iterationCount: 3)]
public class DiffEngineScaleBenchmarks
{
	private static readonly IReadOnlyList<string> KeyColumns = ["C1"];

	private DataTable _left = null!;
	private DataTable _right = null!;
	private DiffEngine _engine = null!;

	[Params(1_000, 10_000)]
	public int RowCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_engine = new();
		(_left, _right) = MockTestData.BuildDataSet(RowCount);
	}

	[Benchmark(Baseline = true, Description = "Linear scan (NumericTolerance > 0)")]
	public DiffSummary Compare_NoKeys_LinearFallback()
	{
		var result = _engine.Compare(_left, _right, options: new() { NumericTolerance = 0.0001 });
		return result.Summary;
	}

	[Benchmark(Description = "No keys, content index (NumericTolerance = 0)")]
	public DiffSummary Compare_NoKeys_ContentIndex()
	{
		var result = _engine.Compare(_left, _right);
		return result.Summary;
	}

	[Benchmark(Description = "Key columns lookup")]
	public DiffSummary Compare_WithKeys()
	{
		var result = _engine.Compare(_left, _right, keyColumns: KeyColumns);
		return result.Summary;
	}
}
