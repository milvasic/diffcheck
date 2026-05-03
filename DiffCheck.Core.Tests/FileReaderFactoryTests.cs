namespace DiffCheck.Core.Tests;

[TestClass]
public class FileReaderFactoryTests
{
	[TestMethod]
	public void GetReader_CsvExtension_ReturnsCsvReader()
	{
		var reader = FileReaderFactory.GetReader("data.csv");
		Assert.IsNotNull(reader);
		Assert.Contains(".csv", reader.SupportedExtensions);
	}

	[TestMethod]
	public void GetReader_TxtExtension_ReturnsCsvReader()
	{
		var reader = FileReaderFactory.GetReader("data.txt");
		Assert.IsNotNull(reader);
	}

	[TestMethod]
	public void GetReader_XlsxExtension_ReturnsXlsxReader()
	{
		var reader = FileReaderFactory.GetReader("data.xlsx");
		Assert.IsNotNull(reader);
		Assert.Contains(".xlsx", reader.SupportedExtensions);
	}

	[TestMethod]
	public void GetReader_XlsmExtension_ReturnsXlsxReader()
	{
		var reader = FileReaderFactory.GetReader("data.xlsm");
		Assert.IsNotNull(reader);
	}

	[TestMethod]
	public void GetReader_UnsupportedExtension_ReturnsNull()
	{
		var reader = FileReaderFactory.GetReader("data.xyz");
		Assert.IsNull(reader);
	}

	[TestMethod]
	public void IsSupported_SupportedExtensions_ReturnsTrue()
	{
		Assert.IsTrue(FileReaderFactory.IsSupported("a.csv"));
		Assert.IsTrue(FileReaderFactory.IsSupported("a.xlsx"));
	}

	[TestMethod]
	public void IsSupported_UnsupportedExtension_ReturnsFalse()
	{
		Assert.IsFalse(FileReaderFactory.IsSupported("a.xyz"));
	}
}
