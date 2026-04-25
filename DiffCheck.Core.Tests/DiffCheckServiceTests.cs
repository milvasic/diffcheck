using DiffCheck;
using DiffCheck.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
		var result = await service.CompareAsync(GetPath("left.csv"), GetPath("right.csv"));

		Assert.IsNotNull(result);
		Assert.IsTrue(result.Headers.Count > 0);
		Assert.IsTrue(result.Rows.Count > 0);
		Assert.AreEqual(3, result.LeftRowCount);
		Assert.AreEqual(3, result.RightRowCount);
	}

	[TestMethod]
	public async Task CompareAsync_XlsxFiles_ReturnsDiffResult()
	{
		var service = new DiffCheckService();
		var result = await service.CompareAsync(GetPath("left.xlsx"), GetPath("right.xlsx"));

		Assert.IsNotNull(result);
		Assert.IsTrue(result.Headers.Count > 0);
		Assert.IsTrue(result.Rows.Count > 0);
	}

	[TestMethod]
	public async Task CompareAndSaveHtmlAsync_GeneratesHtmlFile()
	{
		var service = new DiffCheckService();
		var outputPath = Path.Combine(Path.GetTempPath(), $"diff-test-{Guid.NewGuid()}.html");

		try
		{
			var result = await service.CompareAndSaveHtmlAsync(
				GetPath("left.csv"),
				GetPath("right.csv"),
				outputPath
			);

			Assert.IsTrue(File.Exists(outputPath));
			var html = await File.ReadAllTextAsync(outputPath);
			Assert.IsTrue(html.Contains("Diff Report"));
			Assert.IsTrue(html.Contains("<!DOCTYPE html>"));
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
		var left = new DataTable(new[] { "A" }, new List<IReadOnlyList<string>> { new[] { "1" } });
		var right = new DataTable(new[] { "A" }, new List<IReadOnlyList<string>> { new[] { "1" } });

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
			await service.CompareAsync(GetPath("left.csv"), "file.xyz");
		});
	}
}
