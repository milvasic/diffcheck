using DiffCheck.Models;
using DiffCheck.Readers;

namespace DiffCheck.Core.Tests;

[TestClass]
public class DiffCheckServiceTests
{
	private static string GetPath(string fileName) =>
		Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

	[TestMethod]
	public async Task CompareAsync_CsvFiles_ReturnsDiffResult()
	{
		var service = new DiffCheckService();
		var result = await service.CompareAsync(
			GetPath("left.csv"),
			GetPath("right.csv"),
			cancellationToken: TestContext.CancellationToken
		);

		Assert.IsNotNull(result);
		Assert.IsNotEmpty(result.Headers);
		Assert.IsNotEmpty(result.Rows);
		Assert.AreEqual(3, result.LeftRowCount);
		Assert.AreEqual(3, result.RightRowCount);
	}

	[TestMethod]
	public async Task CompareAsync_XlsxFiles_ReturnsDiffResult()
	{
		var service = new DiffCheckService();
		var result = await service.CompareAsync(
			GetPath("left.xlsx"),
			GetPath("right.xlsx"),
			cancellationToken: TestContext.CancellationToken
		);

		Assert.IsNotNull(result);
		Assert.IsNotEmpty(result.Headers);
		Assert.IsNotEmpty(result.Rows);
	}

	[TestMethod]
	public async Task CompareAndSaveHtmlAsync_GeneratesHtmlFile()
	{
		var service = new DiffCheckService();
		var outputPath = Path.Combine(Path.GetTempPath(), $"diff-test-{Guid.NewGuid()}.html");

		try
		{
			await service.CompareAndSaveHtmlAsync(
				GetPath("left.csv"),
				GetPath("right.csv"),
				outputPath,
				cancellationToken: TestContext.CancellationToken
			);

			Assert.IsTrue(File.Exists(outputPath));
			var html = await File.ReadAllTextAsync(outputPath, TestContext.CancellationToken);
			Assert.Contains("Diff Report", html);
			Assert.Contains("<!DOCTYPE html>", html);
			Assert.Contains("Diff duration:", html);
		}
		finally
		{
			if (File.Exists(outputPath))
				File.Delete(outputPath);
		}
	}

	[TestMethod]
	public void Compare_DataTables_ReturnsDiffResult()
	{
		var left = new DataTable(["A"], new List<IReadOnlyList<string>> { new[] { "1" } });
		var right = new DataTable(["A"], new List<IReadOnlyList<string>> { new[] { "1" } });

		var service = new DiffCheckService();
		var result = service.Compare(left, right);

		Assert.IsNotNull(result);
		Assert.AreEqual(1, result.Summary.UnchangedRows);
	}

	[TestMethod]
	public async Task CompareAsync_UnsupportedFormat_ThrowsArgumentException()
	{
		var service = new DiffCheckService();
		await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
		{
			await service.CompareAsync(
				GetPath("left.csv"),
				"file.xyz",
				cancellationToken: TestContext.CancellationToken
			);
		});
	}

	[TestMethod]
	public async Task CompareAsync_WithProgressCallback_ReportsExpectedStages()
	{
		var leftRows = Enumerable
			.Range(1, 80)
			.Select(i => (IReadOnlyList<string>)[i.ToString(), $"A{i}"])
			.ToList();
		var rightRows = Enumerable
			.Range(1, 80)
			.Select(i => (IReadOnlyList<string>)[i.ToString(), $"B{i}"])
			.ToList();
		var left = new DataTable(["Id", "Value"], leftRows);
		var right = new DataTable(["Id", "Value"], rightRows);
		var service = new DiffCheckService(new FixedReader(left), new FixedReader(right));
		var events = new List<DiffOperationProgress>();

		_ = await service.CompareAsync(
			"left.csv",
			"right.csv",
			progressCallback: events.Add,
			cancellationToken: TestContext.CancellationToken
		);

		Assert.IsGreaterThanOrEqualTo(10, events.Count);
		CollectionAssert.IsSubsetOf(
			new[]
			{
				DiffOperationStage.Starting,
				DiffOperationStage.ReadingLeftFile,
				DiffOperationStage.ReadingRightFile,
				DiffOperationStage.Comparing,
				DiffOperationStage.Completed,
			},
			events.Select(e => e.Stage).Distinct().ToArray()
		);
		Assert.AreEqual(0, events[0].Percent);
		Assert.AreEqual(100, events[^1].Percent);

		var readingLeftPercents = events
			.Where(e => e.Stage == DiffOperationStage.ReadingLeftFile)
			.Select(e => e.Percent)
			.Distinct()
			.ToArray();
		var comparingPercents = events
			.Where(e => e.Stage == DiffOperationStage.Comparing)
			.Select(e => e.Percent)
			.Distinct()
			.ToArray();

		Assert.IsGreaterThanOrEqualTo(
			3,
			readingLeftPercents.Length,
			"Expected multiple incremental progress updates for reading left file."
		);
		Assert.IsGreaterThanOrEqualTo(
			3,
			comparingPercents.Length,
			"Expected multiple incremental progress updates for comparing stage."
		);
		Assert.IsTrue(readingLeftPercents.Contains(100));
		Assert.IsTrue(comparingPercents.Contains(100));
	}

	[TestMethod]
	public async Task CompareAndSaveHtmlAsync_WithProgressCallback_ReportsGenerateStage()
	{
		var service = new DiffCheckService();
		var outputPath = Path.Combine(Path.GetTempPath(), $"diff-test-{Guid.NewGuid()}.html");
		var events = new List<DiffOperationProgress>();

		try
		{
			_ = await service.CompareAndSaveHtmlAsync(
				GetPath("left.csv"),
				GetPath("right.csv"),
				outputPath,
				progressCallback: progress => events.Add(progress),
				cancellationToken: TestContext.CancellationToken
			);

			Assert.Contains(
				e => e.Stage == DiffOperationStage.GeneratingReport,
				events,
				"Expected GeneratingReport stage to be reported."
			);
			Assert.AreEqual(DiffOperationStage.Completed, events[^1].Stage);
			Assert.AreEqual(100, events[^1].Percent);
		}
		finally
		{
			if (File.Exists(outputPath))
				File.Delete(outputPath);
		}
	}

	private sealed class FixedReader(DataTable data) : IFileReader
	{
		public IEnumerable<string> SupportedExtensions => [".csv"];

		public Task<DataTable> ReadAsync(
			string filePath,
			Action<int>? progressCallback = null,
			CancellationToken cancellationToken = default
		)
		{
			progressCallback?.Invoke(0);
			progressCallback?.Invoke(50);
			progressCallback?.Invoke(100);
			return Task.FromResult(data);
		}
	}

	public required TestContext TestContext { get; set; }
}
