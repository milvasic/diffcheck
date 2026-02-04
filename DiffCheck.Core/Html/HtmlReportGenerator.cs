using System.Text;
using DiffCheck.Models;

namespace DiffCheck.Html;

/// <summary>
/// Generates HTML reports from diff results with color-coded differences.
/// </summary>
public sealed class HtmlReportGenerator
{
	private readonly HtmlReportOptions _options;

	public HtmlReportGenerator(HtmlReportOptions? options = null)
	{
		_options = options ?? new HtmlReportOptions();
	}

	/// <summary>
	/// Generates an HTML report from a diff result.
	/// </summary>
	/// <param name="result">The diff result.</param>
	/// <param name="leftFilePath">Path of the first file (for display).</param>
	/// <param name="rightFilePath">Path of the second file (for display).</param>
	/// <param name="leftFileSize">File size in bytes of the first file (optional).</param>
	/// <param name="rightFileSize">File size in bytes of the second file (optional).</param>
	/// <param name="theme">Theme for the report: "light" or "dark". Default: "light".</param>
	/// <returns>HTML string.</returns>
	public string Generate(
		DiffResult result,
		string? leftFilePath = null,
		string? rightFilePath = null,
		long? leftFileSize = null,
		long? rightFileSize = null,
		string? theme = null
	)
	{
		ArgumentNullException.ThrowIfNull(result);

		var effectiveTheme = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase)
			? "dark"
			: "light";

		var sb = new StringBuilder();
		sb.AppendLine($"<!DOCTYPE html>");
		sb.AppendLine($"<html lang=\"en\" data-theme=\"{effectiveTheme}\">");
		sb.AppendLine("<head>");
		sb.AppendLine("  <meta charset=\"UTF-8\">");
		sb.AppendLine(
			"  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">"
		);
		sb.AppendLine(
			$"  <title>Diff Report - {EscapeHtml(leftFilePath ?? "File 1")} vs {EscapeHtml(rightFilePath ?? "File 2")}</title>"
		);
		sb.AppendLine("  <style>");
		sb.AppendLine(GetStyles());
		sb.AppendLine("  </style>");
		sb.AppendLine("</head>");
		sb.AppendLine("<body>");
		sb.AppendLine("  <div class=\"container\">");
		sb.AppendLine("    <h1>Diff Report</h1>");

		{
			var leftCells = result.LeftRowCount * result.LeftColumnCount;
			var rightCells = result.RightRowCount * result.RightColumnCount;

			sb.AppendLine("    <div class=\"file-info\">");
			sb.AppendLine("      <div class=\"file-info-block\">");
			sb.AppendLine(
				$"        <div class=\"file-name\"><strong>Left:</strong> {EscapeHtml(leftFilePath ?? "-")}</div>"
			);
			sb.AppendLine(
				$"        <div class=\"file-stats\">{FormatFileStats(result.LeftRowCount, result.LeftColumnCount, leftCells, leftFileSize)}</div>"
			);
			sb.AppendLine("      </div>");
			sb.AppendLine("      <div class=\"file-info-block\">");
			sb.AppendLine(
				$"        <div class=\"file-name\"><strong>Right:</strong> {EscapeHtml(rightFilePath ?? "-")}</div>"
			);
			sb.AppendLine(
				$"        <div class=\"file-stats\">{FormatFileStats(result.RightRowCount, result.RightColumnCount, rightCells, rightFileSize)}</div>"
			);
			sb.AppendLine("      </div>");
			sb.AppendLine("    </div>");
		}

		sb.AppendLine("    <div class=\"summary\">");
		sb.AppendLine(
			$"      <span class=\"badge added\">Added: {result.Summary.AddedRows}</span>"
		);
		sb.AppendLine(
			$"      <span class=\"badge removed\">Removed: {result.Summary.RemovedRows}</span>"
		);
		sb.AppendLine(
			$"      <span class=\"badge modified\">Modified: {result.Summary.ModifiedRows}</span>"
		);
		sb.AppendLine(
			$"      <span class=\"badge reordered\">Reordered: {result.Summary.ReorderedRows}</span>"
		);
		sb.AppendLine(
			$"      <span class=\"badge unchanged\">Unchanged: {result.Summary.UnchangedRows}</span>"
		);
		sb.AppendLine("    </div>");

		sb.AppendLine("    <div class=\"table-wrapper\">");
		sb.AppendLine("      <table class=\"diff-table\">");
		sb.AppendLine("        <thead><tr>");
		sb.AppendLine("          <th class=\"row-num\">#</th>");
		sb.AppendLine("          <th class=\"row-indices\">Left → Right</th>");
		sb.AppendLine("          <th class=\"row-status\">Status</th>");
		foreach (var header in result.Headers)
			sb.AppendLine($"          <th>{EscapeHtml(header)}</th>");
		sb.AppendLine("        </tr></thead>");
		sb.AppendLine("        <tbody>");

		foreach (var row in result.Rows)
		{
			var rowClass = row.Status switch
			{
				DiffRowStatus.Added => "row-added",
				DiffRowStatus.Removed => "row-removed",
				DiffRowStatus.Modified => "row-modified",
				DiffRowStatus.Reordered => "row-reordered",
				_ => "row-unchanged",
			};
			var statusText = row.Status.ToString().ToLowerInvariant();
			var indicesDisplay =
				row.LeftRowIndex.HasValue && row.RightRowIndex.HasValue
					? $"{row.LeftRowIndex} → {row.RightRowIndex}"
				: row.LeftRowIndex.HasValue ? $"{row.LeftRowIndex} → —"
				: row.RightRowIndex.HasValue ? "— → " + row.RightRowIndex
				: "—";

			sb.AppendLine($"        <tr class=\"{rowClass}\">");
			sb.AppendLine($"          <td class=\"row-num\">{row.RowIndex}</td>");
			sb.AppendLine($"          <td class=\"row-indices\">{indicesDisplay}</td>");
			sb.AppendLine(
				$"          <td class=\"row-status\"><span class=\"status-badge {statusText}\">{statusText}</span></td>"
			);

			foreach (var cell in row.Cells)
			{
				var cellClass = cell.Status switch
				{
					DiffCellStatus.Added => "cell-added",
					DiffCellStatus.Removed => "cell-removed",
					DiffCellStatus.Modified => "cell-modified",
					DiffCellStatus.Reordered => "cell-reordered",
					_ => "cell-unchanged",
				};
				sb.AppendLine(
					$"          <td class=\"{cellClass}\">{EscapeHtml(cell.DisplayValue)}</td>"
				);
			}
			sb.AppendLine("        </tr>");
		}

		sb.AppendLine("        </tbody>");
		sb.AppendLine("      </table>");
		sb.AppendLine("    </div>");
		sb.AppendLine("  </div>");
		sb.AppendLine("</body>");
		sb.AppendLine("</html>");

		return sb.ToString();
	}

	/// <summary>
	/// Writes the HTML report to a file.
	/// </summary>
	public async Task WriteToFileAsync(
		DiffResult result,
		string outputPath,
		string? leftFilePath = null,
		string? rightFilePath = null,
		long? leftFileSize = null,
		long? rightFileSize = null,
		string? theme = null,
		CancellationToken cancellationToken = default
	)
	{
		var html = Generate(
			result,
			leftFilePath,
			rightFilePath,
			leftFileSize,
			rightFileSize,
			theme
		);
		await File.WriteAllTextAsync(outputPath, html, cancellationToken);
	}

	private string GetStyles()
	{
		var add = _options.AddedColor;
		var rem = _options.RemovedColor;
		var mod = _options.ModifiedColor;
		var reord = _options.ReorderedColor;
		var font = _options.FontFamily;
		return $@"
* {{ box-sizing: border-box; }}
body {{ font-family: {font}; margin: 0; padding: 20px; background: #f5f5f5; }}
.container {{ max-width: 100%; overflow-x: auto; background: white; padding: 24px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
h1 {{ margin-top: 0; color: #333; }}
.file-info {{ margin-bottom: 16px; padding: 12px; background: #f9f9f9; border-radius: 4px; font-size: 14px; display: flex; flex-wrap: wrap; gap: 16px; }}
.file-info-block {{ flex: 1; min-width: 200px; }}
.file-info .file-name {{ margin-bottom: 4px; }}
.file-info .file-stats {{ font-size: 12px; color: #666; }}
.summary {{ margin-bottom: 20px; display: flex; flex-wrap: wrap; gap: 8px; }}
.badge {{ padding: 6px 12px; border-radius: 4px; font-size: 14px; font-weight: 500; }}
.badge.added {{ background: {add}; color: white; }}
.badge.removed {{ background: {rem}; color: white; }}
.badge.modified {{ background: {mod}; color: white; }}
.badge.reordered {{ background: {reord}; color: white; }}
.badge.unchanged {{ background: #e0e0e0; color: #555; }}
.table-wrapper {{ overflow-x: auto; }}
.diff-table {{ width: 100%; border-collapse: collapse; font-size: 14px; }}
.diff-table th, .diff-table td {{ padding: 10px 12px; text-align: left; border: 1px solid #ddd; }}
.diff-table th {{ background: #f0f0f0; font-weight: 600; position: sticky; top: 0; }}
.diff-table .row-num {{ width: 50px; text-align: right; color: #666; }}
.diff-table .row-indices {{ width: 90px; text-align: center; color: #666; font-size: 12px; }}
.diff-table .row-status {{ width: 90px; }}
.status-badge {{ padding: 2px 8px; border-radius: 3px; font-size: 12px; font-weight: 500; }}
.status-badge.added {{ background: {add}; color: white; }}
.status-badge.removed {{ background: {rem}; color: white; }}
.status-badge.modified {{ background: {mod}; color: white; }}
.status-badge.reordered {{ background: {reord}; color: white; }}
.status-badge.unchanged {{ background: #e0e0e0; color: #555; }}
.row-added {{ background: {add}22 !important; }}
.row-removed {{ background: {rem}22 !important; }}
.row-modified {{ background: {mod}22 !important; }}
.row-reordered {{ background: {reord}22 !important; }}
.row-unchanged {{ }}
.cell-added {{ background: {add}44 !important; }}
.cell-removed {{ background: {rem}44 !important; }}
.cell-modified {{ background: {mod}44 !important; }}
.cell-reordered {{ background: {reord}44 !important; }}
.cell-unchanged {{ }}
tr:hover {{ background: #fafafa !important; }}

[data-theme=""dark""] body {{ background: #1a1a1a; color: #e9ecef; }}
[data-theme=""dark""] .container {{ background: #2d2d2d; box-shadow: 0 2px 8px rgba(0,0,0,0.3); }}
[data-theme=""dark""] h1 {{ color: #e9ecef; }}
[data-theme=""dark""] .file-info {{ background: #3d3d3d; color: #e9ecef; }}
[data-theme=""dark""] .file-info .file-stats {{ color: #adb5bd; }}
[data-theme=""dark""] .diff-table th, [data-theme=""dark""] .diff-table td {{ border-color: #495057; color: #e9ecef; }}
[data-theme=""dark""] .diff-table th {{ background: #3d3d3d; }}
[data-theme=""dark""] .diff-table .row-num, [data-theme=""dark""] .diff-table .row-indices {{ color: #adb5bd; }}
[data-theme=""dark""] .badge.unchanged {{ background: #495057; color: #adb5bd; }}
[data-theme=""dark""] tr:hover {{ background: #3d3d3d !important; }}
";
	}

	private static string FormatFileStats(
		int rows,
		int columns,
		int totalCells,
		long? fileSizeBytes
	)
	{
		var parts = new List<string>
		{
			$"{rows} rows",
			$"{columns} columns",
			$"{totalCells} cells",
		};
		if (fileSizeBytes.HasValue)
			parts.Add(FormatFileSize(fileSizeBytes.Value));
		return string.Join(" · ", parts);
	}

	private static string FormatFileSize(long bytes)
	{
		string[] sizes = { "B", "KB", "MB", "GB" };
		var order = 0;
		var size = (double)bytes;
		while (size >= 1024 && order < sizes.Length - 1)
		{
			order++;
			size /= 1024;
		}
		return $"{size:0.##} {sizes[order]}";
	}

	private static string EscapeHtml(string text)
	{
		return text.Replace("&", "&amp;")
			.Replace("<", "&lt;")
			.Replace(">", "&gt;")
			.Replace("\"", "&quot;")
			.Replace("'", "&#39;");
	}
}
