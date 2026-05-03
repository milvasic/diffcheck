using DiffCheck.Readers;

namespace DiffCheck.Core.Tests.Readers;

[TestClass]
public class XlsxReaderTests
{
	private static string GetPath(string fileName) =>
		Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

	[TestMethod]
	public async Task ReadAsync_ValidXlsx_ReturnsDataTable()
	{
		var reader = new XlsxReader();
		var path = GetPath("left.xlsx");

		var result = await reader.ReadAsync(path, TestContext.CancellationToken);

		Assert.IsNotNull(result);
		Assert.IsNotEmpty(result.Headers);
		Assert.IsNotEmpty(result.Rows);
	}

	[TestMethod]
	public async Task ReadAsync_NonExistentFile_ThrowsFileNotFoundException()
	{
		var reader = new XlsxReader();
		await Assert.ThrowsExactlyAsync<FileNotFoundException>(async () =>
		{
			await reader.ReadAsync(GetPath("nonexistent.xlsx"), TestContext.CancellationToken);
		});
	}

	[TestMethod]
	public void SupportedExtensions_ContainsXlsxAndXlsm()
	{
		var reader = new XlsxReader();
		var extensions = reader.SupportedExtensions.ToList();

		Assert.Contains(".xlsx", extensions);
		Assert.Contains(".xlsm", extensions);
	}

	public required TestContext TestContext { get; set; }
}
