namespace DiffCheck;

/// <summary>
/// Factory for creating the appropriate file reader based on file extension.
/// </summary>
public static class FileReaderFactory
{
	private static readonly Readers.CsvReader DefaultCsvReader = new();
	private static readonly Readers.XlsxReader DefaultXlsxReader = new();

	/// <summary>
	/// Gets a reader for the given file path based on its extension.
	/// </summary>
	/// <param name="filePath">Path to the file.</param>
	/// <returns>The appropriate reader, or null if the format is not supported.</returns>
	public static Readers.IFileReader? GetReader(string filePath)
	{
		var ext = Path.GetExtension(filePath);
		return ext.ToLowerInvariant() switch
		{
			".csv" or ".txt" => DefaultCsvReader,
			".xlsx" or ".xlsm" => DefaultXlsxReader,
			_ => null,
		};
	}

	/// <summary>
	/// Checks if the file format is supported.
	/// </summary>
	public static bool IsSupported(string filePath)
	{
		return GetReader(filePath) != null;
	}

	/// <summary>
	/// Supported extensions.
	/// </summary>
	public static IReadOnlyList<string> SupportedExtensions { get; } =
	[".csv", ".txt", ".xlsx", ".xlsm"];
}
