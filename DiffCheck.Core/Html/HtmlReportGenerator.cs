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

		var columnHasChanges = BuildColumnHasChanges(result);

		var sb = new StringBuilder();
		sb.AppendLine("<!DOCTYPE html>");
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
		sb.AppendLine("  <div class=\"layout\" id=\"report-layout\">");
		sb.AppendLine("    <aside class=\"tools-curtain expanded\" id=\"tools-curtain\" aria-label=\"Tools\">");
		sb.AppendLine("      <div class=\"tools-header\">");
		sb.AppendLine("        <span class=\"tools-label\"><span class=\"tools-icon\" aria-hidden=\"true\"><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"14\" height=\"14\" fill=\"currentColor\" viewBox=\"0 0 16 16\"><path d=\"M.102 2.223A3.004 3.004 0 0 0 3.78 5.897l6.341 6.252A3.003 3.003 0 0 0 13 16a3 3 0 1 0-.851-5.878L5.897 3.781A3.004 3.004 0 0 0 2.223.1l2.141 2.142L4 4l-1.757.364zm13.37 9.019.528.026.287.445.445.287.026.529L15 13l-.242.471-.026.529-.445.287-.287.445-.529.026L13 15l-.471-.242-.529-.026-.287-.445-.445-.287-.026-.529L11 13l.242-.471.026-.529.445-.287.287-.445.529-.026L13 11z\"/></svg></span>Tools</span>");
		sb.AppendLine("        <button type=\"button\" class=\"tools-toggle\" id=\"tools-toggle\" title=\"Toggle tools\"><span class=\"tools-caret\" aria-hidden=\"true\">&#9654;</span></button>");
		sb.AppendLine("      </div>");
		sb.AppendLine("      <div class=\"tools-panel\" id=\"tools-panel\">");
		sb.AppendLine("        <span class=\"view-switcher-label\">Options</span>");
		sb.AppendLine("        <label class=\"tools-option\"><input type=\"checkbox\" id=\"hide-unchanged-rows\"> Hide unchanged rows</label>");
		sb.AppendLine("        <label class=\"tools-option\"><input type=\"checkbox\" id=\"hide-unchanged-cols\"> Hide unchanged columns</label>");
		sb.AppendLine("        <div class=\"view-switcher\">");
		sb.AppendLine("          <span class=\"view-switcher-label\">View</span>");
		sb.AppendLine("          <button type=\"button\" class=\"view-btn active\" data-view=\"table\" id=\"view-btn-table\"><span class=\"view-btn-icon\"><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"14\" height=\"14\" fill=\"currentColor\" viewBox=\"0 0 16 16\"><path d=\"M2 2h5v5H2V2zm7 0h5v5H9V2zM2 9h5v5H2V9zm7 0h5v5H9V9z\"/></svg></span>Table</button>");
		sb.AppendLine("          <button type=\"button\" class=\"view-btn\" data-view=\"text\" id=\"view-btn-text\"><span class=\"view-btn-icon\"><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"14\" height=\"14\" fill=\"currentColor\" viewBox=\"0 0 16 16\"><path d=\"M5 4a.5.5 0 0 0 0 1h6a.5.5 0 0 0 0-1H5zm-.5 2.5a.5.5 0 0 1 .5-.5h6a.5.5 0 0 1 0 1H5a.5.5 0 0 1-.5-.5zM5 8a.5.5 0 0 0 0 1h6a.5.5 0 0 0 0-1H5zm0 2a.5.5 0 0 0 0 1h3a.5.5 0 0 0 0-1H5z\"/><path d=\"M2 2a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V2zm2-1a1 1 0 0 0-1 1v12a1 1 0 0 0 1 1h8a1 1 0 0 0 1-1V2a1 1 0 0 0-1-1H4z\"/></svg></span>Text</button>");
		sb.AppendLine("        </div>");
		sb.AppendLine("      </div>");
		sb.AppendLine("    </aside>");
		sb.AppendLine("    <div class=\"main-content\">");
		sb.AppendLine("      <div class=\"container\">");
		var generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
		sb.AppendLine("        <h1>Diff Report</h1>");

		{
			var leftCells = result.LeftRowCount * result.LeftColumnCount;
			var rightCells = result.RightRowCount * result.RightColumnCount;

			sb.AppendLine("        <div class=\"file-info\">");
			sb.AppendLine("          <div class=\"file-info-block\">");
			sb.AppendLine(
				$"            <div class=\"file-name\"><strong>Left:</strong> {EscapeHtml(leftFilePath ?? "-")}</div>"
			);
			sb.AppendLine(
				$"            <div class=\"file-stats\">{FormatFileStats(result.LeftRowCount, result.LeftColumnCount, leftCells, leftFileSize)}</div>"
			);
			sb.AppendLine("          </div>");
			sb.AppendLine("          <div class=\"file-info-block\">");
			sb.AppendLine(
				$"            <div class=\"file-name\"><strong>Right:</strong> {EscapeHtml(rightFilePath ?? "-")}</div>"
			);
			sb.AppendLine(
				$"            <div class=\"file-stats\">{FormatFileStats(result.RightRowCount, result.RightColumnCount, rightCells, rightFileSize)}</div>"
			);
			sb.AppendLine("          </div>");
			sb.AppendLine("          <div class=\"file-info-block\">");
			sb.AppendLine(
				$"            <div class=\"file-name\"><strong>Generated:</strong></div>"
			);
			sb.AppendLine(
				$"            <div class=\"file-stats\">{EscapeHtml(generatedAt)}</div>"
			);
			sb.AppendLine("          </div>");
			sb.AppendLine("        </div>");
		}

		sb.AppendLine("        <div class=\"summary\">");
		sb.AppendLine(
			$"          <span class=\"badge added\">Added: {result.Summary.AddedRows}</span>"
		);
		sb.AppendLine(
			$"          <span class=\"badge removed\">Removed: {result.Summary.RemovedRows}</span>"
		);
		sb.AppendLine(
			$"          <span class=\"badge modified\">Modified: {result.Summary.ModifiedRows}</span>"
		);
		sb.AppendLine(
			$"          <span class=\"badge reordered\">Reordered: {result.Summary.ReorderedRows}</span>"
		);
		sb.AppendLine(
			$"          <span class=\"badge unchanged\">Unchanged: {result.Summary.UnchangedRows}</span>"
		);
		sb.AppendLine("        </div>");

		sb.AppendLine("        <div id=\"view-table\" class=\"diff-view\">");
		sb.AppendLine("          <div class=\"table-wrapper\">");
		sb.AppendLine("            <table class=\"diff-table\">");
		sb.AppendLine("              <thead><tr>");
		sb.AppendLine("                <th class=\"row-num\" data-col-has-changes=\"true\">#</th>");
		sb.AppendLine("                <th class=\"row-indices\" data-col-has-changes=\"true\">Left → Right</th>");
		sb.AppendLine("                <th class=\"row-status\" data-col-has-changes=\"true\">Status</th>");
		for (var i = 0; i < result.Headers.Count; i++)
		{
			var hasChanges = columnHasChanges[i];
			sb.AppendLine($"                <th data-col-index=\"{i}\" data-col-has-changes=\"{(hasChanges ? "true" : "false")}\">{EscapeHtml(result.Headers[i])}</th>");
		}
		sb.AppendLine("              </tr></thead>");
		sb.AppendLine("              <tbody>");

		foreach (var row in result.Rows)
		{
			var rowClass = row.Status switch
			{
				DiffRowStatus.Added => "row-added",
				DiffRowStatus.Removed => "row-removed",
				DiffRowStatus.Modified => "row-modified",
				DiffRowStatus.Reordered => "row-unchanged",
				_ => "row-unchanged",
			};
			var statusText = row.Status.ToString().ToLowerInvariant();
			var indicesDisplay =
				row.LeftRowIndex.HasValue && row.RightRowIndex.HasValue
					? $"{row.LeftRowIndex} → {row.RightRowIndex}"
				: row.LeftRowIndex.HasValue ? $"{row.LeftRowIndex} → —"
				: row.RightRowIndex.HasValue ? "— → " + row.RightRowIndex
				: "—";

			sb.AppendLine($"              <tr class=\"{rowClass}\">");
			sb.AppendLine($"                <td class=\"row-num\" data-col-has-changes=\"true\">{row.RowIndex}</td>");
			sb.AppendLine($"                <td class=\"row-indices\" data-col-has-changes=\"true\">{indicesDisplay}</td>");
			sb.AppendLine(
				$"                <td class=\"row-status\" data-col-has-changes=\"true\"><span class=\"status-badge {statusText}\">{statusText}</span></td>"
			);

			for (var i = 0; i < row.Cells.Count; i++)
			{
				var cell = row.Cells[i];
				var hasChanges = columnHasChanges[i];
				var cellClass = cell.Status switch
				{
					DiffCellStatus.Added => "cell-added",
					DiffCellStatus.Removed => "cell-removed",
					DiffCellStatus.Modified => "cell-modified",
					DiffCellStatus.Reordered => "cell-unchanged",
					_ => "cell-unchanged",
				};
				var cellContent =
					cell.Status == DiffCellStatus.Modified
						? $"<span class=\"diff-old\">{EscapeHtml(cell.LeftValue ?? "")}</span> → <span class=\"diff-new\">{EscapeHtml(cell.RightValue ?? "")}</span>"
						: EscapeHtml(cell.DisplayValue);
				sb.AppendLine($"                <td class=\"{cellClass}\" data-col-index=\"{i}\" data-col-has-changes=\"{(hasChanges ? "true" : "false")}\">{cellContent}</td>");
			}
			sb.AppendLine("              </tr>");
		}

		sb.AppendLine("              </tbody>");
		sb.AppendLine("            </table>");
		sb.AppendLine("          </div>");
		sb.AppendLine("        </div>");

		sb.AppendLine("        <div id=\"view-text\" class=\"diff-view\" hidden>");
		sb.AppendLine("          <pre class=\"text-diff\" id=\"text-diff-content\">");
		sb.Append(BuildTextView(result));
		sb.AppendLine("</pre>");
		sb.AppendLine("        </div>");

		sb.AppendLine("      </div>");
		sb.AppendLine("    </div>");
		sb.AppendLine("  </div>");
		sb.AppendLine(GetToolsScript());
		sb.AppendLine("</body>");
		sb.AppendLine("</html>");

		return sb.ToString();
	}

	private static bool[] BuildColumnHasChanges(DiffResult result)
	{
		var n = result.Headers.Count;
		var hasChanges = new bool[n];
		foreach (var row in result.Rows)
		{
			for (var i = 0; i < row.Cells.Count && i < n; i++)
			{
				var s = row.Cells[i].Status;
				if (s != DiffCellStatus.Unchanged && s != DiffCellStatus.Reordered)
					hasChanges[i] = true;
			}
		}
		return hasChanges;
	}

	private static string BuildTextView(DiffResult result)
	{
		var sb = new StringBuilder();
		foreach (var row in result.Rows)
		{
			var prefix = row.Status switch
			{
				DiffRowStatus.Added => "+",
				DiffRowStatus.Removed => "-",
				_ => " ",
			};
			var lineClass = row.Status switch
			{
				DiffRowStatus.Added => "text-line-added",
				DiffRowStatus.Removed => "text-line-removed",
				DiffRowStatus.Modified => "text-line-modified",
				_ => "text-line-unchanged",
			};
			if (row.Status == DiffRowStatus.Modified)
			{
				var leftLine = string.Join("\t", row.Cells.Select(c => c.LeftValue ?? ""));
				var rightLine = string.Join("\t", row.Cells.Select(c => c.RightValue ?? ""));
				sb.AppendLine($"<span class=\"text-line-removed\">- {EscapeHtml(leftLine)}</span>");
				sb.AppendLine($"<span class=\"text-line-added\">+ {EscapeHtml(rightLine)}</span>");
			}
			else
			{
				var line = string.Join("\t", row.Cells.Select(c => c.DisplayValue));
				sb.AppendLine($"<span class=\"{lineClass}\">{prefix} {EscapeHtml(line)}</span>");
			}
		}
		return sb.ToString();
	}

	private static string GetToolsScript()
	{
		return """
			<script>
			(function() {
				var LAYOUT = 'report-layout', STORAGE_HIDE_ROWS = 'diffcheck-hide-unchanged-rows', STORAGE_HIDE_COLS = 'diffcheck-hide-unchanged-cols', STORAGE_VIEW = 'diffcheck-view';
				var curtain = document.getElementById('tools-curtain');
				var panel = document.getElementById('tools-panel');
				var toggleBtn = document.getElementById('tools-toggle');
				var hideRowsCb = document.getElementById('hide-unchanged-rows');
				var hideColsCb = document.getElementById('hide-unchanged-cols');
				var layout = document.getElementById(LAYOUT);

				var STORAGE_TOOLS_EXPANDED = 'diffcheck-tools-expanded';
				function expand() { curtain.classList.add('expanded'); try { localStorage.setItem(STORAGE_TOOLS_EXPANDED, '1'); } catch (e) {} }
				function collapse() { curtain.classList.remove('expanded'); try { localStorage.setItem(STORAGE_TOOLS_EXPANDED, '0'); } catch (e) {} }
				function isExpanded() { return curtain.classList.contains('expanded'); }
				toggleBtn.addEventListener('click', function() { isExpanded() ? collapse() : expand(); });
				try {
					var savedExpanded = localStorage.getItem(STORAGE_TOOLS_EXPANDED);
					if (savedExpanded === '0') curtain.classList.remove('expanded');
				} catch (e) {}

				function updateHideRows() {
					var hide = hideRowsCb.checked;
					try { localStorage.setItem(STORAGE_HIDE_ROWS, hide ? '1' : '0'); } catch (e) {}
					layout.classList.toggle('hide-unchanged-rows', hide);
				}
				function updateHideCols() {
					var hide = hideColsCb.checked;
					try { localStorage.setItem(STORAGE_HIDE_COLS, hide ? '1' : '0'); } catch (e) {}
					layout.classList.toggle('hide-unchanged-cols', hide);
				}
				hideRowsCb.addEventListener('change', updateHideRows);
				hideColsCb.addEventListener('change', updateHideCols);

				function setView(view) {
					var tableEl = document.getElementById('view-table');
					var textEl = document.getElementById('view-text');
					var btnTable = document.getElementById('view-btn-table');
					var btnText = document.getElementById('view-btn-text');
					if (view === 'text') {
						tableEl.hidden = true;
						textEl.hidden = false;
						btnTable.classList.remove('active');
						btnText.classList.add('active');
					} else {
						tableEl.hidden = false;
						textEl.hidden = true;
						btnTable.classList.add('active');
						btnText.classList.remove('active');
					}
					try { localStorage.setItem(STORAGE_VIEW, view); } catch (e) {}
				}
				document.querySelectorAll('.view-btn').forEach(function(btn) {
					btn.addEventListener('click', function() { setView(btn.getAttribute('data-view')); });
				});

				try {
					if (localStorage.getItem(STORAGE_HIDE_ROWS) === '1') { hideRowsCb.checked = true; layout.classList.add('hide-unchanged-rows'); }
					if (localStorage.getItem(STORAGE_HIDE_COLS) === '1') { hideColsCb.checked = true; layout.classList.add('hide-unchanged-cols'); }
					var savedView = localStorage.getItem(STORAGE_VIEW);
					if (savedView === 'text') setView('text');
				} catch (e) {}
			})();
			</script>
			""";
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
body {{ font-family: {font}; margin: 0; padding: 0; background: #f5f5f5; }}
.layout {{ width: 100%; min-height: 100vh; }}
.tools-curtain {{ position: fixed; left: 0; top: 0; bottom: 0; width: 36px; z-index: 10; background: #e8e8e8; border-right: 1px solid #ddd; transition: width 0.2s ease; overflow: hidden; }}
.tools-curtain.expanded {{ width: 220px; }}
.tools-curtain:not(.expanded) .tools-panel {{ display: none; }}
.tools-curtain.expanded .tools-panel {{ display: block; }}
.tools-curtain:not(.expanded) .tools-label {{ display: none; }}
.tools-header {{ display: flex; align-items: center; margin-top: 16px; min-height: 36px; }}
.tools-curtain:not(.expanded) .tools-header {{ justify-content: center; }}
.tools-curtain.expanded .tools-header {{ justify-content: space-between; padding: 0 12px; width: 100%; }}
.tools-label {{ font-size: 12px; font-weight: 600; pointer-events: none; display: inline-flex; align-items: center; gap: 6px; }}
.tools-icon {{ display: inline-flex; flex-shrink: 0; }}
.tools-toggle {{ width: 36px; height: 36px; padding: 0; border: none; background: transparent; cursor: pointer; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }}
.view-btn {{ display: inline-flex; align-items: center; gap: 5px; }}
.view-btn-icon {{ display: inline-flex; flex-shrink: 0; }}
.tools-curtain.expanded .tools-toggle {{ width: auto; padding: 0 0 0 8px; }}
.tools-caret {{ display: inline-block; transition: transform 0.2s ease, background 0.15s ease, border-radius 0.15s ease; font-size: 10px; padding: 6px; border-radius: 6px; }}
.tools-toggle:hover .tools-caret {{ background: rgba(0,0,0,0.08); }}
.tools-curtain.expanded .tools-caret {{ transform: rotate(180deg); }}
.tools-panel {{ padding: 12px; min-width: 220px; }}
.main-content {{ margin-left: 36px; padding: 20px; min-width: 0; transition: margin-left 0.2s ease; flex: 1; }}
.tools-curtain.expanded + .main-content {{ margin-left: 220px; }}
.tools-option {{ display: block; margin-bottom: 10px; font-size: 13px; cursor: pointer; }}
.tools-option input {{ margin-right: 8px; }}
.view-switcher {{ margin-top: 14px; }}
.view-switcher-label {{ display: block; font-size: 12px; color: #666; margin-bottom: 6px; }}
.view-btn {{ padding: 6px 12px; margin-right: 4px; border: 1px solid #ccc; background: #fff; border-radius: 4px; cursor: pointer; font-size: 13px; }}
.view-btn.active {{ background: #333; color: #fff; border-color: #333; }}
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
.hide-unchanged-rows tr.row-unchanged {{ display: none !important; }}
.hide-unchanged-cols th[data-col-has-changes=""false""], .hide-unchanged-cols td[data-col-has-changes=""false""] {{ display: none !important; }}
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
.row-unchanged {{ }}
.cell-added {{ background: {add}44 !important; }}
.cell-removed {{ background: {rem}44 !important; }}
.cell-modified {{ background: {mod}44 !important; }}
.cell-unchanged {{ }}
.diff-old {{ background: {add}59; border-radius: 4px; padding: 2px 6px; margin-right: 4px; }}
.diff-new {{ background: {rem}59; border-radius: 4px; padding: 2px 6px; }}
tr:hover {{ background: #fafafa !important; }}
.text-diff {{ font-family: ui-monospace, monospace; font-size: 13px; line-height: 1.5; margin: 0; padding: 12px; overflow-x: auto; white-space: pre; border: 1px solid #ddd; border-radius: 4px; background: #fafafa; }}
.text-diff .text-line-added {{ display: block; background: {add}22; }}
.text-diff .text-line-removed {{ display: block; background: {rem}22; }}
.text-diff .text-line-modified {{ display: block; }}
.text-diff .text-line-unchanged {{ display: block; }}

[data-theme=""dark""] body {{ background: #1a1a1a; color: #e9ecef; }}
[data-theme=""dark""] .tools-curtain {{ background: #2d2d2d; border-color: #495057; }}
[data-theme=""dark""] .tools-label {{ color: #e9ecef; }}
[data-theme=""dark""] .tools-toggle {{ color: #e9ecef; }}
[data-theme=""dark""] .tools-toggle:hover .tools-caret {{ background: rgba(255,255,255,0.12); }}
[data-theme=""dark""] .view-btn {{ background: #3d3d3d; border-color: #495057; color: #e9ecef; }}
[data-theme=""dark""] .view-btn.active {{ background: #0d6efd; color: #fff; border-color: #0d6efd; }}
[data-theme=""dark""] .view-switcher-label {{ color: #adb5bd; }}
[data-theme=""dark""] .container {{ background: #2d2d2d; box-shadow: 0 2px 8px rgba(0,0,0,0.3); }}
[data-theme=""dark""] h1 {{ color: #e9ecef; }}
[data-theme=""dark""] .file-info {{ background: #3d3d3d; color: #e9ecef; }}
[data-theme=""dark""] .file-info .file-stats {{ color: #adb5bd; }}
[data-theme=""dark""] .diff-table th, [data-theme=""dark""] .diff-table td {{ border-color: #495057; color: #e9ecef; }}
[data-theme=""dark""] .diff-table th {{ background: #3d3d3d; }}
[data-theme=""dark""] .diff-table .row-num, [data-theme=""dark""] .diff-table .row-indices {{ color: #adb5bd; }}
[data-theme=""dark""] .badge.unchanged {{ background: #495057; color: #adb5bd; }}
[data-theme=""dark""] tr:hover {{ background: #3d3d3d !important; }}
[data-theme=""dark""] .text-diff {{ background: #252525; border-color: #495057; color: #e9ecef; }}
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
