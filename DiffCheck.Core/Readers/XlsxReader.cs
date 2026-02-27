using System.Globalization;
using ClosedXML.Excel;

namespace DiffCheck.Readers;

/// <summary>
/// Reads XLSX (Excel) files into tabular data.
/// </summary>
public sealed class XlsxReader : IFileReader
{
	private readonly int _sheetIndex;

	/// <summary>
	/// Creates a new XLSX reader.
	/// </summary>
	/// <param name="sheetIndex">Zero-based index of the sheet to read. Default is 0 (first sheet).</param>
	public XlsxReader(int sheetIndex = 0)
	{
		_sheetIndex = sheetIndex >= 0 ? sheetIndex : 0;
	}

	public IEnumerable<string> SupportedExtensions => [".xlsx", ".xlsm"];

	public Task<Models.DataTable> ReadAsync(
		string filePath,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

		if (!File.Exists(filePath))
			throw new FileNotFoundException("File not found.", filePath);

		var path = Path.GetFullPath(filePath);

		using var workbook = new XLWorkbook(path);
		var sheetCount = workbook.Worksheets.Count;
		if (_sheetIndex >= sheetCount)
			throw new ArgumentOutOfRangeException(
				nameof(_sheetIndex),
				$"Sheet index {_sheetIndex} is out of range. Workbook has {sheetCount} sheet(s)."
			);

		var worksheet = workbook.Worksheet(_sheetIndex + 1); // 1-based in ClosedXML
		var usedRange = worksheet.RangeUsed();

		if (usedRange == null)
			return Task.FromResult(new Models.DataTable([], [], path));

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
		for (var rowNum = firstRow.RowNumber() + 1; rowNum <= lastRow.RowNumber(); rowNum++)
		{
			var rowValues = new List<string>();
			for (var col = firstCol.ColumnNumber(); col <= lastCol.ColumnNumber(); col++)
			{
				var cell = worksheet.Cell(rowNum, col);
				rowValues.Add(GetCellValue(cell));
			}
			rows.Add(rowValues);
		}

		return Task.FromResult(new Models.DataTable(headers, rows, path));
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
