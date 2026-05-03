using DiffCheck.Models;

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

	public required TestContext TestContext { get; set; }
}
