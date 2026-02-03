using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace DiffCheck.Readers;

/// <summary>
/// Reads CSV files into tabular data.
/// </summary>
public sealed class CsvReader : IFileReader
{
	private readonly CsvConfiguration _config;

	public CsvReader(CsvConfiguration? config = null)
	{
		_config =
			config
			?? new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = true,
				MissingFieldFound = null,
				BadDataFound = null,
				TrimOptions = TrimOptions.Trim,
			};
	}

	public IEnumerable<string> SupportedExtensions => [".csv", ".txt"];

	public async Task<Models.DataTable> ReadAsync(
		string filePath,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

		if (!File.Exists(filePath))
			throw new FileNotFoundException("File not found.", filePath);

		var path = Path.GetFullPath(filePath);
		var rows = new List<IReadOnlyList<string>>();
		string[] headers = [];

		using (var reader = new StreamReader(path))
		using (var csv = new CsvHelper.CsvReader(reader, _config))
		{
			await csv.ReadAsync();
			csv.ReadHeader();
			headers = csv.HeaderRecord ?? [];

			while (await csv.ReadAsync())
			{
				var values = new string[headers.Length];
				for (var i = 0; i < headers.Length; i++)
				{
					csv.TryGetField<string>(i, out var value);
					values[i] = value ?? string.Empty;
				}
				rows.Add(values);
			}
		}

		return new Models.DataTable(headers, rows, path);
	}
}
