using ClosedXML.Excel;
using DiffCheck.Readers;

namespace DiffCheck.Core.Tests.Readers;

[TestClass]
public class XlsxReaderTests
{
	private static string GetPath(string fileName) =>
		Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

	private static string CreateMultiSheetXlsx(string fileName)
	{
		var path = Path.Combine(Path.GetTempPath(), fileName);
		using var wb = new XLWorkbook();
		var ws1 = wb.AddWorksheet("Alpha");
		ws1.Cell(1, 1).Value = "ID";
		ws1.Cell(1, 2).Value = "Name";
		ws1.Cell(2, 1).Value = 1;
		ws1.Cell(2, 2).Value = "Alice";
		var ws2 = wb.AddWorksheet("Beta");
		ws2.Cell(1, 1).Value = "Code";
		ws2.Cell(2, 1).Value = "X";
		wb.SaveAs(path);
		return path;
	}

	[TestMethod]
	public async Task ReadAsync_ValidXlsx_ReturnsDataTable()
	{
		var reader = new XlsxReader();
		var path = GetPath("left.xlsx");

		var result = await reader.ReadAsync(path, cancellationToken: TestContext.CancellationToken);

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
			await reader.ReadAsync(
				GetPath("nonexistent.xlsx"),
				cancellationToken: TestContext.CancellationToken
			);
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

	[TestMethod]
	public async Task ReadAsync_BySheetName_ReadsCorrectSheet()
	{
		var path = CreateMultiSheetXlsx("multisheet_name_test.xlsx");
		try
		{
			var reader = new XlsxReader("Beta");
			var result = await reader.ReadAsync(
				path,
				cancellationToken: TestContext.CancellationToken
			);

			Assert.IsNotNull(result);
			Assert.AreEqual(1, result.Headers.Count);
			Assert.AreEqual("Code", result.Headers[0]);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[TestMethod]
	public async Task ReadAsync_BySheetName_CaseInsensitive()
	{
		var path = CreateMultiSheetXlsx("multisheet_case_test.xlsx");
		try
		{
			var reader = new XlsxReader("alpha"); // lowercase
			var result = await reader.ReadAsync(
				path,
				cancellationToken: TestContext.CancellationToken
			);

			Assert.IsNotNull(result);
			Assert.AreEqual("ID", result.Headers[0]);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[TestMethod]
	public async Task ReadAsync_BySheetIndex_ZeroBasedInternally_ReadsSecondSheet()
	{
		var path = CreateMultiSheetXlsx("multisheet_idx_test.xlsx");
		try
		{
			// 0-based internally: index 1 = second sheet "Beta"
			var reader = new XlsxReader(sheetIndex: 1);
			var result = await reader.ReadAsync(
				path,
				cancellationToken: TestContext.CancellationToken
			);

			Assert.IsNotNull(result);
			Assert.AreEqual("Code", result.Headers[0]);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[TestMethod]
	public async Task ReadAsync_SheetNameNotFound_ThrowsInvalidOperationException()
	{
		var path = CreateMultiSheetXlsx("multisheet_notfound_test.xlsx");
		try
		{
			var reader = new XlsxReader("DoesNotExist");
			var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
			{
				await reader.ReadAsync(path, cancellationToken: TestContext.CancellationToken);
			});
			StringAssert.Contains(ex.Message, "DoesNotExist");
			StringAssert.Contains(ex.Message, "Alpha");
		}
		finally
		{
			File.Delete(path);
		}
	}

	[TestMethod]
	public void GetSheetNames_ReturnsAllSheetNames()
	{
		var path = CreateMultiSheetXlsx("multisheet_getnames_test.xlsx");
		try
		{
			var names = XlsxReader.GetSheetNames(path);

			Assert.AreEqual(2, names.Count);
			Assert.AreEqual("Alpha", names[0]);
			Assert.AreEqual("Beta", names[1]);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[TestMethod]
	public void GetSheetNames_FileNotFound_ThrowsFileNotFoundException()
	{
		Assert.ThrowsExactly<FileNotFoundException>(() =>
		{
			XlsxReader.GetSheetNames(GetPath("nonexistent.xlsx"));
		});
	}

	public required TestContext TestContext { get; set; }
}
