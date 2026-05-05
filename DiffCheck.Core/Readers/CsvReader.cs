using System.Globalization;
using CsvHelper.Configuration;

namespace DiffCheck.Readers;

/// <summary>
/// Reads CSV files into tabular data.
/// </summary>
public sealed class CsvReader(CsvConfiguration? config = null) : IFileReader
{
	private readonly CsvConfiguration _config =
		config
		?? new CsvConfiguration(CultureInfo.InvariantCulture)
		{
			HasHeaderRecord = true,
			MissingFieldFound = null,
			BadDataFound = null,
			TrimOptions = TrimOptions.Trim,
		};

	public IEnumerable<string> SupportedExtensions => [".csv", ".txt"];

	public async Task<Models.DataTable> ReadAsync(
		string filePath,
		Action<int>? progressCallback = null,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

		if (!File.Exists(filePath))
			throw new FileNotFoundException("File not found.", filePath);

		var path = Path.GetFullPath(filePath);
		var totalBytes = Math.Max(1L, new FileInfo(path).Length);
		var rows = new List<IReadOnlyList<string>>();
		string[] headers;

		var reporter = new ProgressReporter(progressCallback);
		reporter.Report(0);

		await using (var stream = File.OpenRead(path))
		using (var reader = new StreamReader(stream))
		using (var csv = new CsvHelper.CsvReader(reader, _config))
		{
			await csv.ReadAsync();
			csv.ReadHeader();
			headers = csv.HeaderRecord ?? [];
			reporter.ReportByFraction(stream.Position, totalBytes);

			while (await csv.ReadAsync())
			{
				var values = new string[headers.Length];
				for (var i = 0; i < headers.Length; i++)
				{
					csv.TryGetField<string>(i, out var value);
					values[i] = value ?? string.Empty;
				}
				rows.Add(values);
				reporter.ReportByFraction(stream.Position, totalBytes);
			}
		}

		reporter.Report(100);

		return new Models.DataTable(headers, rows, path);
	}

	private sealed class ProgressReporter(Action<int>? callback)
	{
		private int _nextThreshold;

		public void ReportByFraction(long completed, long total)
		{
			var percent = (int)Math.Floor(completed * 100d / Math.Max(1L, total));
			Report(percent);
		}

		public void Report(int percent)
		{
			if (callback == null)
				return;

			var clamped = Math.Clamp(percent, 0, 100);
			if (clamped < _nextThreshold && clamped < 100)
				return;

			if (clamped == 100)
			{
				callback(100);
				_nextThreshold = 101;
				return;
			}

			var snapped = clamped - (clamped % 5);
			if (snapped < _nextThreshold)
				return;

			callback(snapped);
			_nextThreshold = snapped + 5;
		}
	}
}
