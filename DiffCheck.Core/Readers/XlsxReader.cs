using System.Globalization;
using ClosedXML.Excel;

namespace DiffCheck.Readers;

/// <summary>
/// Reads XLSX (Excel) files into tabular data.
/// </summary>
public sealed class XlsxReader : IFileReader
{
	private readonly int _sheetIndex;
	private readonly string? _sheetName;

	/// <summary>
	/// Creates a new XLSX reader for the given zero-based sheet index (default: 0).
	/// </summary>
	public XlsxReader(int sheetIndex = 0)
	{
		_sheetIndex = sheetIndex >= 0 ? sheetIndex : 0;
		_sheetName = null;
	}

	/// <summary>
	/// Creates a new XLSX reader that selects a sheet by name (case-insensitive).
	/// </summary>
	public XlsxReader(string sheetName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sheetName);
		_sheetName = sheetName;
		_sheetIndex = 0;
	}

	/// <summary>
	/// Returns the names of all worksheets in the given XLSX workbook.
	/// </summary>
	public static IReadOnlyList<string> GetSheetNames(string filePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		if (!File.Exists(filePath))
			throw new FileNotFoundException("File not found.", filePath);
		using var workbook = new XLWorkbook(Path.GetFullPath(filePath));
		return workbook.Worksheets.Select(ws => ws.Name).ToList();
	}

	public IEnumerable<string> SupportedExtensions => [".xlsx", ".xlsm"];

	public Task<Models.DataTable> ReadAsync(
		string filePath,
		Action<int>? progressCallback = null,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

		if (!File.Exists(filePath))
			throw new FileNotFoundException("File not found.", filePath);

		var path = Path.GetFullPath(filePath);
		var reporter = new ProgressReporter(progressCallback);
		reporter.Report(0);

		using var workbook = new XLWorkbook(path);
		var sheetCount = workbook.Worksheets.Count;

		IXLWorksheet worksheet;
		if (_sheetName != null)
		{
			worksheet =
				workbook.Worksheets.FirstOrDefault(ws =>
					string.Equals(ws.Name, _sheetName, StringComparison.OrdinalIgnoreCase)
				)
				?? throw new InvalidOperationException(
					$"Sheet \"{_sheetName}\" not found in \"{Path.GetFileName(path)}\". "
						+ $"Available sheets: {string.Join(", ", workbook.Worksheets.Select(ws => $"\"{ws.Name}\""))}."
				);
		}
		else
		{
			if (_sheetIndex >= sheetCount)
				throw new ArgumentOutOfRangeException(
					nameof(_sheetIndex),
					$"Sheet index {_sheetIndex} is out of range. Workbook has {sheetCount} sheet(s)."
				);
			worksheet = workbook.Worksheet(_sheetIndex + 1); // 1-based in ClosedXML
		}
		var usedRange = worksheet.RangeUsed();

		if (usedRange == null)
		{
			reporter.Report(100);
			return Task.FromResult(new Models.DataTable([], [], path));
		}

		var firstRow = usedRange.FirstRow();
		var lastRow = usedRange.LastRow();
		var firstCol = usedRange.FirstColumn();
		var lastCol = usedRange.LastColumn();

		var headers = new List<string>();
		for (var col = firstCol.ColumnNumber(); col <= lastCol.ColumnNumber(); col++)
		{
			var cell = worksheet.Cell(firstRow.RowNumber(), col);
			headers.Add(GetCellValue(cell));
		}

		var rows = new List<IReadOnlyList<string>>();
		var firstDataRow = firstRow.RowNumber() + 1;
		var lastDataRow = lastRow.RowNumber();
		var totalDataRows = Math.Max(1, lastDataRow - firstDataRow + 1);
		var processedRows = 0;

		for (var rowNum = firstDataRow; rowNum <= lastDataRow; rowNum++)
		{
			var rowValues = new List<string>();
			for (var col = firstCol.ColumnNumber(); col <= lastCol.ColumnNumber(); col++)
			{
				var cell = worksheet.Cell(rowNum, col);
				rowValues.Add(GetCellValue(cell));
			}
			rows.Add(rowValues);
			processedRows++;
			reporter.Report((int)Math.Floor(processedRows * 100d / totalDataRows));
		}

		reporter.Report(100);

		return Task.FromResult(new Models.DataTable(headers, rows, path));
	}

	private sealed class ProgressReporter(Action<int>? callback)
	{
		private int _nextThreshold;

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

	private static string GetRawCellValue(IXLCell cell)
	{
		if (cell.TryGetValue(out string? text))
			return text ?? string.Empty;

		if (cell.TryGetValue(out double number))
			return number.ToString(CultureInfo.InvariantCulture);

		if (cell.TryGetValue(out DateTime date))
			return date.ToString("o");

		if (cell.TryGetValue(out bool boolean))
			return boolean.ToString();

		return cell.GetString() ?? string.Empty;
	}

	private static string GetCellValue(IXLCell cell)
	{
		// Prefer the formatted text as shown in Excel so that
		// format changes (e.g. 1 vs 1.00, date formats) are visible in the diff.
		var formatted = cell.GetFormattedString();
		return formatted ?? string.Empty;
	}
}
