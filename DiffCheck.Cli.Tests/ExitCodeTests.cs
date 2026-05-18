using DiffCheck.Cli;

namespace DiffCheck.Cli.Tests;

[TestClass]
public class ExitCodeTests
{
	// left: Alice/30, Bob/25, Charlie/35
	// right: Alice/30, Bob/26 (modified), Diana/28 (added); Charlie removed
	// → 1 added, 1 removed, 1 modified
	private const string LeftCsv = "Name,Age\nAlice,30\nBob,25\nCharlie,35\n";
	private const string RightCsv = "Name,Age\nAlice,30\nBob,26\nDiana,28\n";
	private const string IdenticalCsv = "Name,Age\nAlice,30\nBob,25\n";

	private static (string Left, string Right) WriteTempCsvPair(
		string leftContent,
		string rightContent
	)
	{
		var left = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
		var right = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
		File.WriteAllText(left, leftContent);
		File.WriteAllText(right, rightContent);
		return (left, right);
	}

	private static void DeleteTempFiles(params string[] paths)
	{
		foreach (var path in paths)
			if (File.Exists(path))
				File.Delete(path);
	}

	[TestMethod]
	public async Task RunAsync_IdenticalFiles_NoFlags_ReturnsZero()
	{
		var (left, right) = WriteTempCsvPair(IdenticalCsv, IdenticalCsv);
		try
		{
			var exitCode = await CliApp.RunAsync([left, right, "--summary"]);

			Assert.AreEqual(0, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right);
		}
	}

	[TestMethod]
	public async Task RunAsync_DifferentFiles_NoFlags_ReturnsZero()
	{
		var (left, right) = WriteTempCsvPair(LeftCsv, RightCsv);
		try
		{
			var exitCode = await CliApp.RunAsync([left, right, "--summary"]);

			Assert.AreEqual(0, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right);
		}
	}

	[TestMethod]
	public async Task RunAsync_DifferentFiles_FailOnDiff_ReturnsOne()
	{
		var (left, right) = WriteTempCsvPair(LeftCsv, RightCsv);
		try
		{
			var exitCode = await CliApp.RunAsync([left, right, "--summary", "--fail-on-diff"]);

			Assert.AreEqual(1, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right);
		}
	}

	[TestMethod]
	public async Task RunAsync_IdenticalFiles_FailOnDiff_ReturnsZero()
	{
		var (left, right) = WriteTempCsvPair(IdenticalCsv, IdenticalCsv);
		try
		{
			var exitCode = await CliApp.RunAsync([left, right, "--summary", "--fail-on-diff"]);

			Assert.AreEqual(0, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right);
		}
	}

	[TestMethod]
	public async Task RunAsync_MaxAdded_Exceeded_ReturnsOne()
	{
		var (left, right) = WriteTempCsvPair(LeftCsv, RightCsv);
		try
		{
			// 1 added row; threshold 0 → exceeded
			var exitCode = await CliApp.RunAsync([left, right, "--summary", "--max-added", "0"]);

			Assert.AreEqual(1, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right);
		}
	}

	[TestMethod]
	public async Task RunAsync_MaxAdded_NotExceeded_ReturnsZero()
	{
		var (left, right) = WriteTempCsvPair(LeftCsv, RightCsv);
		try
		{
			// 1 added row; threshold 1 → not exceeded (threshold is a maximum, not a limit)
			var exitCode = await CliApp.RunAsync([left, right, "--summary", "--max-added", "1"]);

			Assert.AreEqual(0, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right);
		}
	}

	[TestMethod]
	public async Task RunAsync_MaxRemoved_Exceeded_ReturnsOne()
	{
		var (left, right) = WriteTempCsvPair(LeftCsv, RightCsv);
		try
		{
			// 1 removed row; threshold 0 → exceeded
			var exitCode = await CliApp.RunAsync([left, right, "--summary", "--max-removed", "0"]);

			Assert.AreEqual(1, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right);
		}
	}

	[TestMethod]
	public async Task RunAsync_MaxModified_Exceeded_ReturnsOne()
	{
		var (left, right) = WriteTempCsvPair(LeftCsv, RightCsv);
		try
		{
			// 1 modified row; threshold 0 → exceeded
			var exitCode = await CliApp.RunAsync([left, right, "--summary", "--max-modified", "0"]);

			Assert.AreEqual(1, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right);
		}
	}

	[TestMethod]
	public async Task RunAsync_MultipleThresholds_OneExceeded_ReturnsOne()
	{
		var (left, right) = WriteTempCsvPair(LeftCsv, RightCsv);
		try
		{
			// max-added and max-removed are generous, but max-modified=0 is exceeded
			var exitCode = await CliApp.RunAsync([
				left,
				right,
				"--summary",
				"--max-added",
				"10",
				"--max-removed",
				"10",
				"--max-modified",
				"0",
			]);

			Assert.AreEqual(1, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right);
		}
	}

	[TestMethod]
	public async Task RunAsync_MultipleThresholds_NoneExceeded_ReturnsZero()
	{
		var (left, right) = WriteTempCsvPair(LeftCsv, RightCsv);
		try
		{
			// all thresholds high enough
			var exitCode = await CliApp.RunAsync([
				left,
				right,
				"--summary",
				"--max-added",
				"5",
				"--max-removed",
				"5",
				"--max-modified",
				"5",
			]);

			Assert.AreEqual(0, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right);
		}
	}

	[TestMethod]
	public async Task RunAsync_SummaryAndOutputMutuallyExclusive_ReturnsTwo()
	{
		var (left, right) = WriteTempCsvPair(IdenticalCsv, IdenticalCsv);
		var outputPath = Path.ChangeExtension(Path.GetTempFileName(), ".html");
		try
		{
			var exitCode = await CliApp.RunAsync([
				left,
				right,
				"--summary",
				"--output",
				outputPath,
			]);

			Assert.AreEqual(2, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right, outputPath);
		}
	}

	[TestMethod]
	public async Task RunAsync_InvalidColumnMap_ReturnsTwo()
	{
		var (left, right) = WriteTempCsvPair(IdenticalCsv, IdenticalCsv);
		try
		{
			var exitCode = await CliApp.RunAsync([
				left,
				right,
				"--summary",
				"--column-map",
				"MISSING_COLON",
			]);

			Assert.AreEqual(2, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right);
		}
	}

	[TestMethod]
	public async Task RunAsync_ProfileNotFound_ReturnsTwo()
	{
		var (left, right) = WriteTempCsvPair(IdenticalCsv, IdenticalCsv);
		try
		{
			var exitCode = await CliApp.RunAsync([
				left,
				right,
				"--summary",
				"--profile",
				"nonexistent-profile-xyz",
			]);

			Assert.AreEqual(2, exitCode);
		}
		finally
		{
			DeleteTempFiles(left, right);
		}
	}

	[TestMethod]
	public async Task RunAsync_FileNotFound_ReturnsOne()
	{
		var exitCode = await CliApp.RunAsync([
			"/nonexistent/left.csv",
			"/nonexistent/right.csv",
			"--summary",
		]);

		Assert.AreEqual(1, exitCode);
	}

	[TestMethod]
	public async Task RunAsync_HtmlReport_GeneratedEvenWhenThresholdExceeded()
	{
		var (left, right) = WriteTempCsvPair(LeftCsv, RightCsv);
		var outputPath = Path.ChangeExtension(
			Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
			".html"
		);
		try
		{
			var exitCode = await CliApp.RunAsync([
				left,
				right,
				"--fail-on-diff",
				"--output",
				outputPath,
			]);

			Assert.AreEqual(1, exitCode);
			Assert.IsTrue(
				File.Exists(outputPath),
				"HTML report should be written even when exit code is 1."
			);
		}
		finally
		{
			DeleteTempFiles(left, right, outputPath);
		}
	}
}
