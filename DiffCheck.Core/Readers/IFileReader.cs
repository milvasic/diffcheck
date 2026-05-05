namespace DiffCheck.Readers;

/// <summary>
/// Interface for reading tabular data from files.
/// </summary>
public interface IFileReader
{
	/// <summary>
	/// Supported file extensions (e.g., ".csv", ".xlsx").
	/// </summary>
	IEnumerable<string> SupportedExtensions { get; }

	/// <summary>
	/// Reads a file and returns tabular data.
	/// </summary>
	/// <param name="filePath">Path to the file.</param>
	/// <param name="progressCallback">Optional callback for stage progress in range 0..100.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The loaded data table.</returns>
	Task<Models.DataTable> ReadAsync(
		string filePath,
		Action<int>? progressCallback = null,
		CancellationToken cancellationToken = default
	);
}
