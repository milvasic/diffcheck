using DiffCheck.Readers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

		var result = await reader.ReadAsync(path);

		Assert.IsNotNull(result);
		Assert.IsTrue(result.Headers.Count > 0);
		Assert.IsTrue(result.Rows.Count > 0);
	}

	[TestMethod]
	public async Task ReadAsync_NonExistentFile_ThrowsFileNotFoundException()
	{
		var reader = new XlsxReader();
		await Assert.ThrowsExactlyAsync<FileNotFoundException>(async () =>
		{
			await reader.ReadAsync(GetPath("nonexistent.xlsx"));
		});
	}

	[TestMethod]
	public void SupportedExtensions_ContainsXlsxAndXlsm()
	{
		var reader = new XlsxReader();
		var extensions = reader.SupportedExtensions.ToList();

		Assert.IsTrue(extensions.Contains(".xlsx"));
		Assert.IsTrue(extensions.Contains(".xlsm"));
	}
}
