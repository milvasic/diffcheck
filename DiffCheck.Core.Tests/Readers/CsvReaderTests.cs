using DiffCheck.Readers;

namespace DiffCheck.Core.Tests.Readers;

[TestClass]
public class CsvReaderTests
{
	private static string GetPath(string fileName) =>
		Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

	[TestMethod]
	public async Task ReadAsync_ValidCsv_ReturnsDataTable()
	{
		var reader = new CsvReader();
		var path = GetPath("left.csv");

		var result = await reader.ReadAsync(path);

		Assert.IsNotNull(result);
		Assert.HasCount(3, result.Headers);
		Assert.AreEqual("Name", result.Headers[0]);
		Assert.AreEqual("Age", result.Headers[1]);
		Assert.AreEqual("City", result.Headers[2]);
		Assert.HasCount(3, result.Rows);
		Assert.AreEqual("Alice", result.Rows[0][0]);
		Assert.AreEqual("30", result.Rows[0][1]);
		Assert.AreEqual("London", result.Rows[0][2]);
	}

	[TestMethod]
	public async Task ReadAsync_NonExistentFile_ThrowsFileNotFoundException()
	{
		var reader = new CsvReader();
		await Assert.ThrowsExactlyAsync<FileNotFoundException>(async () =>
		{
			await reader.ReadAsync(GetPath("nonexistent.csv"), TestContext.CancellationToken);
		});
	}

	[TestMethod]
	public void SupportedExtensions_ContainsCsvAndTxt()
	{
		var reader = new CsvReader();
		var extensions = reader.SupportedExtensions.ToList();

		Assert.Contains(".csv", extensions);
		Assert.Contains(".txt", extensions);
	}

	public required TestContext TestContext { get; set; }
}
