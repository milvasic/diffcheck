using System.Globalization;
using System.Text;
using System.Text.Json;
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
	/// <param name="initialView">Initial active view: "table" or "text". Default: "table".</param>
	/// <returns>HTML string.</returns>
	public string Generate(
		DiffResult result,
		string? leftFilePath = null,
		string? rightFilePath = null,
		long? leftFileSize = null,
		long? rightFileSize = null,
		string? theme = null,
		string? initialView = null
	)
	{
		ArgumentNullException.ThrowIfNull(result);

		var effectiveTheme = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase)
			? "dark"
			: "light";

		var effectiveView = string.Equals(initialView, "text", StringComparison.OrdinalIgnoreCase)
			? "text"
			: "table";

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
		sb.AppendLine(
			"  <link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/ag-grid-community@32/styles/ag-grid.min.css\">"
		);
		sb.AppendLine(
			"  <link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/ag-grid-community@32/styles/ag-theme-alpine.min.css\">"
		);
		sb.AppendLine("  <style>");
		sb.AppendLine(GetStyles());
		sb.AppendLine("  </style>");
		sb.AppendLine("</head>");
		sb.AppendLine("<body>");
		sb.AppendLine(
			$"  <div class=\"layout\" id=\"report-layout\" data-initial-view=\"{effectiveView}\">"
		);
		sb.AppendLine(
			"    <aside class=\"tools-curtain\" id=\"tools-curtain\" aria-label=\"Tools\">"
		);
		sb.AppendLine("      <div class=\"tools-header\">");
		sb.AppendLine(
			"        <span class=\"tools-label\"><span class=\"tools-icon\" aria-hidden=\"true\"><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"14\" height=\"14\" fill=\"currentColor\" viewBox=\"0 0 16 16\"><path d=\"M.102 2.223A3.004 3.004 0 0 0 3.78 5.897l6.341 6.252A3.003 3.003 0 0 0 13 16a3 3 0 1 0-.851-5.878L5.897 3.781A3.004 3.004 0 0 0 2.223.1l2.141 2.142L4 4l-1.757.364zm13.37 9.019.528.026.287.445.445.287.026.529L15 13l-.242.471-.026.529-.445.287-.287.445-.529.026L13 15l-.471-.242-.529-.026-.287-.445-.445-.287-.026-.529L11 13l.242-.471.026-.529.445-.287.287-.445.529-.026L13 11z\"/></svg></span>Tools</span>"
		);
		sb.AppendLine(
			"        <button type=\"button\" class=\"tools-toggle\" id=\"tools-toggle\" title=\"Toggle tools\"><span class=\"tools-caret\" aria-hidden=\"true\">&#9654;</span></button>"
		);
		sb.AppendLine("      </div>");
		sb.AppendLine("      <div class=\"tools-panel\" id=\"tools-panel\">");
		sb.AppendLine("        <div class=\"view-switcher\">");
		sb.AppendLine("          <span class=\"view-switcher-label\">View</span>");
		sb.AppendLine(
			"          <button type=\"button\" class=\"view-btn active\" data-view=\"table\" id=\"view-btn-table\"><span class=\"view-btn-icon\"><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"14\" height=\"14\" fill=\"currentColor\" viewBox=\"0 0 16 16\"><path d=\"M2 2h5v5H2V2zm7 0h5v5H9V2zM2 9h5v5H2V9zm7 0h5v5H9V9z\"/></svg></span>Table</button>"
		);
		sb.AppendLine(
			"          <button type=\"button\" class=\"view-btn\" data-view=\"text\" id=\"view-btn-text\"><span class=\"view-btn-icon\"><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"14\" height=\"14\" fill=\"currentColor\" viewBox=\"0 0 16 16\"><path d=\"M5 4a.5.5 0 0 0 0 1h6a.5.5 0 0 0 0-1H5zm-.5 2.5a.5.5 0 0 1 .5-.5h6a.5.5 0 0 1 0 1H5a.5.5 0 0 1-.5-.5zM5 8a.5.5 0 0 0 0 1h6a.5.5 0 0 0 0-1H5zm0 2a.5.5 0 0 0 0 1h3a.5.5 0 0 0 0-1H5z\"/><path d=\"M2 2a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V2zm2-1a1 1 0 0 0-1 1v12a1 1 0 0 0 1 1h8a1 1 0 0 0 1-1V2a1 1 0 0 0-1-1H4z\"/></svg></span>Text</button>"
		);
		sb.AppendLine("        </div>");
		sb.AppendLine("        <div class=\"view-switcher\" id=\"tools-grid-section\">");
		sb.AppendLine("          <span class=\"view-switcher-label\">Grid</span>");
		sb.AppendLine(
			"          <button type=\"button\" class=\"view-btn\" id=\"autosize-columns-btn\" title=\"Size columns to fit content\">Autosize columns</button>"
		);
		sb.AppendLine("        </div>");
		sb.AppendLine("        <div class=\"view-switcher\" id=\"tools-options-section\">");
		sb.AppendLine("          <span class=\"view-switcher-label\">Options</span>");
		sb.AppendLine(
			"          <label class=\"tools-option\"><input type=\"checkbox\" id=\"hide-unchanged-cols\"> Hide unchanged columns</label>"
		);
		sb.AppendLine(
			"          <label class=\"tools-option\"><input type=\"checkbox\" id=\"highlight-rows\" checked> Highlight changed rows</label>"
		);
		sb.AppendLine(
			"          <label class=\"tools-option\"><input type=\"checkbox\" id=\"highlight-cells\" checked> Highlight changed cells</label>"
		);
		sb.AppendLine(
			"          <label class=\"tools-option\"><input type=\"checkbox\" id=\"whole-value-diff\"> Whole value cell diff</label>"
		);
		sb.AppendLine("        </div>");
		sb.AppendLine("      </div>");
		sb.AppendLine("    </aside>");
		sb.AppendLine("    <div class=\"main-content\">");
		sb.AppendLine("      <div class=\"container\">");
		var generatedAt = DateTime.Now.ToString(
			"yyyy-MM-dd HH:mm",
			System.Globalization.CultureInfo.InvariantCulture
		);
		// sb.AppendLine("        <h1>Diff Report</h1>");

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
			sb.AppendLine($"            <div class=\"file-stats\">{EscapeHtml(generatedAt)}</div>");
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
		sb.AppendLine(
			"          <div id=\"diff-grid\" class=\"diff-grid-container ag-theme-alpine\"></div>"
		);
		var diffDataJson = BuildGridDataJson(result, columnHasChanges);
		sb.AppendLine("          <script>");
		sb.AppendLine("            window.diffData = " + diffDataJson + ";");
		sb.AppendLine("          </script>");
		sb.AppendLine("        </div>");

		sb.AppendLine("        <div id=\"view-text\" class=\"diff-view\" hidden>");
		sb.AppendLine("          <pre class=\"text-diff\" id=\"text-diff-content\">");
		sb.Append(BuildTextView(result));
		sb.AppendLine("</pre>");
		sb.AppendLine("        </div>");

		sb.AppendLine("      </div>");
		sb.AppendLine("    </div>");
		sb.AppendLine("  </div>");
		sb.AppendLine(
			"  <script src=\"https://cdn.jsdelivr.net/npm/ag-grid-community@32/dist/ag-grid-community.min.js\"></script>"
		);
		sb.AppendLine(GetToolsScript());
		sb.AppendLine("</body>");
		sb.AppendLine("</html>");

		return sb.ToString();
	}

	private static string BuildGridDataJson(DiffResult result, bool[] columnHasChanges)
	{
		// Compact JSON structure to minimize report size.
		// Root object: { h: headers[], c: columnHasChanges[], r: rows[] }
		// Each row: [rowIndex, leftRowIndex, rightRowIndex, rowStatusCode, cells[]]
		// Each cell: [displayValue, cellStatusCode, htmlOrNull, isFormatOnly, leftValue, rightValue]
		var rows = new List<object?>(result.Rows.Count);
		foreach (var row in result.Rows)
		{
			var cells = new List<object?>(row.Cells.Count);
			for (var i = 0; i < row.Cells.Count && i < columnHasChanges.Length; i++)
			{
				var cell = row.Cells[i];
				var left = cell.LeftValue ?? "";
				var right = cell.RightValue ?? "";
				string? html = null;
				var isFormatOnly = false;
				if (cell.Status == DiffCellStatus.Modified)
				{
					if (
						!string.Equals(left, right, StringComparison.Ordinal)
						&& AreSemanticallyEqual(left, right)
					)
						isFormatOnly = true;
					html = CharacterDiffCellHtml(left, right);
				}

				var cellEntry = new object?[]
				{
					cell.DisplayValue,
					MapCellStatus(cell.Status),
					html,
					isFormatOnly,
					left,
					right,
				};
				cells.Add(cellEntry);
			}

			var rowEntry = new object?[]
			{
				row.RowIndex,
				row.LeftRowIndex,
				row.RightRowIndex,
				MapRowStatus(row.Status),
				cells,
			};
			rows.Add(rowEntry);
		}

		var root = new Dictionary<string, object?>
		{
			["h"] = result.Headers.ToList(),
			["c"] = columnHasChanges,
			["r"] = rows,
		};
		if (
			result.ColumnHeaderRenames != null
			&& result.ColumnHeaderRenames.Count == result.Headers.Count
		)
		{
			root["hr"] = new List<string?>(result.ColumnHeaderRenames);
		}

		var json = JsonSerializer.Serialize(root);
		// Avoid closing script tag when embedding in HTML
		return json.Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
	}

	private static int MapRowStatus(DiffRowStatus status) =>
		status switch
		{
			DiffRowStatus.Added => 1,
			DiffRowStatus.Removed => 2,
			DiffRowStatus.Modified => 3,
			DiffRowStatus.Reordered => 4,
			_ => 0,
		};

	private static int MapCellStatus(DiffCellStatus status) =>
		status switch
		{
			DiffCellStatus.Added => 1,
			DiffCellStatus.Removed => 2,
			DiffCellStatus.Modified => 3,
			DiffCellStatus.Reordered => 4,
			_ => 0,
		};

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
				var (removedHtml, addedHtml) = CharacterDiffHtml(leftLine, rightLine);
				sb.AppendLine($"<span class=\"text-line-removed\">- {removedHtml}</span>");
				sb.AppendLine($"<span class=\"text-line-added\">+ {addedHtml}</span>");
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
				function escapeHtml(s) {
					if (s == null) return '';
					return String(s)
						.replace(/&/g, '&amp;')
						.replace(/</g, '&lt;')
						.replace(/>/g, '&gt;')
						.replace(/"/g, '&quot;')
						.replace(/'/g, '&#39;');
				}

				var LAYOUT = 'report-layout', STORAGE_HIDE_COLS = 'diffcheck-hide-unchanged-cols', STORAGE_VIEW_KEY = 'diffcheck-view';
				var curtain = document.getElementById('tools-curtain');
				var toggleBtn = document.getElementById('tools-toggle');
				var hideColsCb = document.getElementById('hide-unchanged-cols');
				var highlightRowsCb = document.getElementById('highlight-rows');
				var highlightCellsCb = document.getElementById('highlight-cells');
				var wholeDiffCb = document.getElementById('whole-value-diff');
				var layout = document.getElementById(LAYOUT);

				var STORAGE_TOOLS_EXPANDED = 'diffcheck-tools-expanded';
				var STORAGE_HIGHLIGHT_ROWS = 'diffcheck-highlight-rows';
				var STORAGE_HIGHLIGHT_CELLS = 'diffcheck-highlight-cells';
				var STORAGE_WHOLE_DIFF = 'diffcheck-whole-diff';
				function expand() { curtain.classList.add('expanded'); try { localStorage.setItem(STORAGE_TOOLS_EXPANDED, '1'); } catch (e) {} }
				function collapse() { curtain.classList.remove('expanded'); try { localStorage.setItem(STORAGE_TOOLS_EXPANDED, '0'); } catch (e) {} }
				function isExpanded() { return curtain.classList.contains('expanded'); }
				toggleBtn.addEventListener('click', function() { isExpanded() ? collapse() : expand(); });
				try {
					var savedExpanded = localStorage.getItem(STORAGE_TOOLS_EXPANDED);
					if (savedExpanded === '0') collapse(); else expand();
				} catch (e) { expand(); }

				var gridApi = null;
				var useWholeValueDiff = false;
				var diffData = window.diffData || null;
				var headers = diffData && diffData.h ? diffData.h : [];
				var columnHasChanges = diffData && diffData.c ? diffData.c : [];
				var headerRenames = diffData && diffData.hr ? diffData.hr : [];

				var ROW_STATUS_CLASS = ['row-unchanged', 'row-added', 'row-removed', 'row-modified', 'row-unchanged'];
				var ROW_STATUS_TEXT = ['unchanged', 'added', 'removed', 'modified', 'reordered'];
				var CELL_STATUS_CLASS = ['cell-unchanged', 'cell-added', 'cell-removed', 'cell-modified', 'cell-unchanged'];

				function buildRowData(data) {
					if (!data || !data.r) return [];
					var rowsPacked = data.r;
					var result = new Array(rowsPacked.length);
					for (var i = 0; i < rowsPacked.length; i++) {
						var rp = rowsPacked[i] || [];
						var rowIndex = rp[0];
						var leftRowIndex = rp[1];
						var rightRowIndex = rp[2];
						var rowStatusCode = rp[3] || 0;
						var cellsPacked = rp[4] || [];

						var statusText = ROW_STATUS_TEXT[rowStatusCode] || 'unchanged';
						var rowClass = ROW_STATUS_CLASS[rowStatusCode] || 'row-unchanged';

						var indicesDisplay;
						var hasLeft = leftRowIndex !== null && leftRowIndex !== undefined;
						var hasRight = rightRowIndex !== null && rightRowIndex !== undefined;
						if (hasLeft && hasRight) {
							indicesDisplay = leftRowIndex + ' \u2192 ' + rightRowIndex;
						} else if (hasLeft) {
							indicesDisplay = leftRowIndex + ' \u2192 \u2014';
						} else if (hasRight) {
							indicesDisplay = '\u2014 \u2192 ' + rightRowIndex;
						} else {
							indicesDisplay = '\u2014';
						}

						var rowObj = {
							rowIndex: rowIndex,
							indicesDisplay: indicesDisplay,
							status: statusText,
							rowStatus: rowClass
						};

						for (var c = 0; c < cellsPacked.length; c++) {
							var cp = cellsPacked[c] || [];
							var value = cp[0];
							var cellStatusCode = cp[1] || 0;
							var html = cp[2] || null;
							var isFormatOnly = !!cp[3];
							var leftVal = cp[4] != null ? cp[4] : null;
							var rightVal = cp[5] != null ? cp[5] : null;

							rowObj['col' + c] = value;
							var cls = CELL_STATUS_CLASS[cellStatusCode] || 'cell-unchanged';
							if (isFormatOnly) cls += ' cell-format-only';
							rowObj['_col' + c + '_class'] = cls;
							rowObj['_col' + c + '_status'] = cellStatusCode;
							if (html) {
								rowObj['_col' + c + '_html'] = html;
							}
							if (leftVal != null || rightVal != null) {
								rowObj['_col' + c + '_left'] = leftVal;
								rowObj['_col' + c + '_right'] = rightVal;
							}
						}

						result[i] = rowObj;
					}
					return result;
				}

				var ALL_STATUSES = ['added', 'removed', 'modified', 'reordered', 'unchanged'];
				function StatusFilter() {}
				StatusFilter.prototype.init = function(params) {
					this._params = params;
					this._selected = new Set(ALL_STATUSES);
					this._gui = document.createElement('div');
					this._gui.style.cssText = 'padding:10px 12px;width:100%;';
					var self = this;

					var CB_STYLE = 'accent-color:#0d6efd;cursor:pointer;flex-shrink:0;';

					// --- Select All row ---
					var allLabel = document.createElement('label');
					allLabel.className = 'sf-row sf-row-all';
					var allCb = document.createElement('input');
					allCb.type = 'checkbox';
					allCb.checked = true;
					allCb.style.cssText = CB_STYLE;
					this._allCb = allCb;
					allCb.addEventListener('change', function() {
						var checked = allCb.checked;
						allCb.indeterminate = false;
						if (checked) self._selected = new Set(ALL_STATUSES);
						else self._selected.clear();
						self._gui.querySelectorAll('.sf-item-cb').forEach(function(c) { c.checked = checked; });
						params.filterChangedCallback();
					});
					var allText = document.createElement('span');
					allText.textContent = '(Select All)';
					allText.style.cssText = 'font-size:13px;';
					allLabel.appendChild(allCb);
					allLabel.appendChild(allText);
					this._gui.appendChild(allLabel);

					// --- Per-status rows ---
					this._itemCbs = {};
					ALL_STATUSES.forEach(function(status) {
						var label = document.createElement('label');
						label.className = 'sf-row';
						var cb = document.createElement('input');
						cb.type = 'checkbox';
						cb.checked = true;
						cb.className = 'sf-item-cb';
						cb.style.cssText = CB_STYLE;
						self._itemCbs[status] = cb;
						cb.addEventListener('change', function() {
							if (cb.checked) self._selected.add(status);
							else self._selected.delete(status);
							self._syncAllCb();
							params.filterChangedCallback();
						});
						var badge = document.createElement('span');
						badge.className = 'status-badge ' + status;
						badge.textContent = status;
						label.appendChild(cb);
						label.appendChild(badge);
						self._gui.appendChild(label);
					});
				};
				StatusFilter.prototype._syncAllCb = function() {
					var n = this._selected.size;
					if (n === ALL_STATUSES.length) { this._allCb.checked = true; this._allCb.indeterminate = false; }
					else if (n === 0) { this._allCb.checked = false; this._allCb.indeterminate = false; }
					else { this._allCb.checked = false; this._allCb.indeterminate = true; }
				};
				StatusFilter.prototype.getGui = function() { return this._gui; };
				StatusFilter.prototype.isFilterActive = function() { return this._selected.size < ALL_STATUSES.length; };
				StatusFilter.prototype.doesFilterPass = function(params) {
					var val = params.data && params.data.status ? params.data.status : 'unchanged';
					return this._selected.has(val);
				};
				StatusFilter.prototype.getModel = function() {
					return this.isFilterActive() ? { values: Array.from(this._selected) } : null;
				};
				StatusFilter.prototype.setModel = function(model) {
					var self = this;
					if (model && model.values) {
						this._selected = new Set(model.values);
					} else {
						this._selected = new Set(ALL_STATUSES);
					}
					ALL_STATUSES.forEach(function(status) {
						if (self._itemCbs[status]) self._itemCbs[status].checked = self._selected.has(status);
					});
					self._syncAllCb();
				};

				var columnDefs = [
					{ field: 'rowIndex', headerName: '#', colId: 'rowIndex', type: 'numericColumn', filter: false, suppressMovable: true },
					{ field: 'indicesDisplay', headerName: 'Left → Right', colId: 'indicesDisplay', filter: false, suppressMovable: true },
					{ field: 'status', headerName: 'Status', colId: 'status', filter: StatusFilter, floatingFilter: false, suppressMovable: true,
					  cellRenderer: function(params) {
						if (!params.value) return null;
						var s = document.createElement('span');
						s.className = 'status-badge ' + params.value;
						s.textContent = params.value;
						return s;
					  }
					}
				];
				for (var i = 0; i < headers.length; i++) {
					(function(idx) {
						var rightName = headerRenames[idx];
						var isRenamed = (rightName != null && rightName !== '');
						columnDefs.push({
							field: 'col' + idx,
							headerName: isRenamed ? (headers[idx] + ' \u2192 ' + rightName) : headers[idx],
							headerClass: isRenamed ? 'column-header-modified' : '',
							colId: 'col' + idx,
							filter: 'agTextColumnFilter',
							cellClass: function(params) { return (params.data && params.data['_col' + idx + '_class']) || ''; },
							cellRenderer: function(params) {
								var span = document.createElement('span');
								var data = params.data || {};
								var htmlKey = '_col' + idx + '_html';
								var leftKey = '_col' + idx + '_left';
								var rightKey = '_col' + idx + '_right';
								var statusKey = '_col' + idx + '_status';

								var statusCode = data[statusKey];
								var left = data[leftKey];
								var right = data[rightKey];

								if (useWholeValueDiff && statusCode === 3 && (left != null || right != null)) {
									// Whole value diff preview
									if (left != null && right != null) {
										span.innerHTML = '<span class=\"diff-old\">' + escapeHtml(left) + '</span> \u2192 <span class=\"diff-new\">' + escapeHtml(right) + '</span>';
									} else if (left != null) {
										span.innerHTML = '<span class=\"diff-old\">' + escapeHtml(left) + '</span>';
									} else {
										span.innerHTML = '<span class=\"diff-new\">' + escapeHtml(right) + '</span>';
									}
								} else if (data && data[htmlKey]) {
									span.innerHTML = data[htmlKey];
								} else {
									span.textContent = params.value != null ? params.value : '';
								}
								return span;
							}
						});
					})(i);
				}

				var gridEl = document.getElementById('diff-grid');
				if (gridEl && typeof agGrid !== 'undefined' && diffData) {
					var rowData = buildRowData(diffData);
					var gridOptions = {
						rowData: rowData,
						columnDefs: columnDefs,
						defaultColDef: {
							filter: true,
						},
						getRowClass: function(params) { return params.data ? params.data.rowStatus || '' : ''; },
						domLayout: 'normal',
						suppressCellFocus: true,
						onGridReady: function(params) {
							gridApi = params.api;
							updateHideCols();
							updateHighlightRows();
							updateHighlightCells();
							gridApi.onFilterChanged();
							setTimeout(function() { sizeColumnsConditionally(params.api); }, 150);
						},
						suppressColumnVirtualisation: true,
					};
					agGrid.createGrid(gridEl, gridOptions);
				}

				const sizeColumnsConditionally = (gridApi) => {
					// Attempt to fit columns to grid width first
					gridApi.sizeColumnsToFit();

					// Check if horizontal scroll is present after sizeColumnsToFit
					// This requires accessing the DOM element directly
					const viewport = document.querySelector('.ag-body-viewport'); 
					
					// A scrollbar is present if the scrollWidth is greater than the clientWidth
					if (viewport && viewport.scrollWidth > viewport.clientWidth) {
						// If there is a scrollbar, it means sizeColumnsToFit didn't make all content fit 
						// or there's more content off-screen.
						
						// Use autoSizeAllColumns() to size columns based on their content, which will likely result in a scrollbar
						const allColumnIds = gridApi.getAllDisplayedColumns().map(col => col.getColId());
						gridApi.autoSizeColumns(allColumnIds);
					}
				};

				function updateHideCols() {
					var hide = !!hideColsCb.checked;
					try { localStorage.setItem(STORAGE_HIDE_COLS, hide ? '1' : '0'); } catch (e) {}
					if (gridApi) {
						columnDefs.forEach(function(col) {
							if (col.colId && col.colId.indexOf('col') === 0) {
								var idx = parseInt(col.colId.replace('col', ''), 10);
								gridApi.setColumnVisible(col.colId, !hide || (columnHasChanges[idx] === true));
							}
						});
					}
				}
				function updateHighlightRows() {
					var on = !highlightRowsCb || !!highlightRowsCb.checked;
					try { localStorage.setItem(STORAGE_HIGHLIGHT_ROWS, on ? '1' : '0'); } catch (e) {}
					if (layout) {
						if (on) layout.classList.add('highlight-rows');
						else layout.classList.remove('highlight-rows');
					}
				}
				function updateHighlightCells() {
					var on = !highlightCellsCb || !!highlightCellsCb.checked;
					try { localStorage.setItem(STORAGE_HIGHLIGHT_CELLS, on ? '1' : '0'); } catch (e) {}
					if (layout) {
						if (on) layout.classList.add('highlight-cells');
						else layout.classList.remove('highlight-cells');
					}
					if (gridApi) gridApi.refreshCells({ force: true });
				}
				function updateWholeDiff() {
					useWholeValueDiff = !!(wholeDiffCb && wholeDiffCb.checked);
					try { localStorage.setItem(STORAGE_WHOLE_DIFF, useWholeValueDiff ? '1' : '0'); } catch (e) {}
					if (gridApi) gridApi.refreshCells({ force: true });
				}
				var autosizeBtn = document.getElementById('autosize-columns-btn');
				if (autosizeBtn) {
					autosizeBtn.addEventListener('click', function() {
						if (!gridApi) return;
						gridApi.autoSizeAllColumns();
					});
				}
				if (hideColsCb) hideColsCb.addEventListener('change', updateHideCols);
				if (highlightRowsCb) highlightRowsCb.addEventListener('change', updateHighlightRows);
				if (highlightCellsCb) highlightCellsCb.addEventListener('change', updateHighlightCells);
				if (wholeDiffCb) wholeDiffCb.addEventListener('change', updateWholeDiff);

				function setView(view) {
					var v = view || 'table';
					try { localStorage.setItem(STORAGE_VIEW_KEY, v); } catch (e) {}
					var tableEl = document.getElementById('view-table');
					var textEl = document.getElementById('view-text');
					var btnTable = document.getElementById('view-btn-table');
					var btnText = document.getElementById('view-btn-text');
					var gridSection = document.getElementById('tools-grid-section');
					var optionsSection = document.getElementById('tools-options-section');
					if (v === 'text') {
						tableEl.hidden = true;
						textEl.hidden = false;
						btnTable.classList.remove('active');
						btnText.classList.add('active');
						if (gridSection) gridSection.hidden = true;
						if (optionsSection) optionsSection.hidden = true;
					} else {
						tableEl.hidden = false;
						textEl.hidden = true;
						btnTable.classList.add('active');
						btnText.classList.remove('active');
						if (gridSection) gridSection.hidden = false;
						if (optionsSection) optionsSection.hidden = false;
					}
				}
				document.querySelectorAll('.view-btn[data-view]').forEach(function(btn) {
					btn.addEventListener('click', function() { setView(btn.getAttribute('data-view')); });
				});

				try {
					if (hideColsCb && localStorage.getItem(STORAGE_HIDE_COLS) === '1') { hideColsCb.checked = true; }
					var initialView = layout ? layout.getAttribute('data-initial-view') : null;
					if (initialView !== 'text' && initialView !== 'table') initialView = 'table';
					setView(initialView);
					if (highlightRowsCb) {
						var savedHighlightRows = localStorage.getItem(STORAGE_HIGHLIGHT_ROWS);
						if (savedHighlightRows === '0') highlightRowsCb.checked = false;
					}
					if (highlightCellsCb) {
						var savedHighlightCells = localStorage.getItem(STORAGE_HIGHLIGHT_CELLS);
						if (savedHighlightCells === '0') highlightCellsCb.checked = false;
					}
					if (wholeDiffCb && localStorage.getItem(STORAGE_WHOLE_DIFF) === '1') {
						wholeDiffCb.checked = true;
						useWholeValueDiff = true;
					}
					updateHighlightRows();
					updateHighlightCells();
					updateWholeDiff();
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
html, body {{ height: 100%; margin: 0; padding: 0; overflow: hidden; font-family: {font}; background: #f5f5f5; display: flex; flex-direction: column; }}
.layout {{ display: flex; flex-direction: column; flex: 1; min-height: 0; width: 100%; }}
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
.tools-caret {{ display: inline-block; font-size: 10px; padding: 6px; border-radius: 999px; transition: transform 0.2s ease, background-color 0.15s ease; }}
.tools-toggle:hover .tools-caret {{ background: rgba(0,0,0,0.08); }}
.tools-curtain.expanded .tools-toggle {{ width: auto; padding: 0 0 0 8px; }}
.tools-curtain.expanded .tools-caret {{ transform: rotate(180deg); }}
.tools-panel {{ padding: 12px; padding-top: 0; min-width: 220px; }}
.main-content {{ margin-left: 36px; min-width: 0; transition: margin-left 0.2s ease; flex: 1; min-height: 0; display: flex; flex-direction: column; overflow: hidden; }}
.tools-curtain.expanded + .main-content {{ margin-left: 220px; }}
.tools-option {{ display: block; margin-bottom: 10px; font-size: 13px; cursor: pointer; }}
.tools-option input {{ margin-right: 8px; accent-color: #0d6efd; }}
.view-switcher {{ margin-top: 14px; }}
.view-switcher-label {{ display: block; font-size: 12px; color: #666; margin-bottom: 6px; }}
.view-btn {{ padding: 6px 12px; margin-right: 4px; border: 1px solid #ccc; background: #fff; border-radius: 4px; cursor: pointer; font-size: 13px; transition: background-color 0.15s ease, border-color 0.15s ease, color 0.15s ease; }}
.view-btn:hover:not(.active) {{ background: #e9ecef; border-color: #adb5bd; }}
.view-btn.active {{ background: #0d6efd; color: #fff; border-color: #0d6efd; }}
.view-btn.active:hover {{ background: #0b5ed7; border-color: #0a58ca; color: #fff; }}
.container {{ max-width: 100%; background: white; padding: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); flex: 1; min-height: 0; display: flex; flex-direction: column; overflow: hidden; }}
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
.diff-view:not([hidden]) {{ flex: 1; min-height: 0; display: flex; flex-direction: column; overflow: hidden; }}
.diff-view[hidden] {{ display: none !important; }}
.diff-grid-container {{ flex: 1; min-height: 0; width: 100%; }}
.ag-filter-body-wrapper {{ min-width: 0 !important; width: max-content !important; }}
.sf-row {{ display: flex; align-items: center; gap: 8px; margin-bottom: 8px; cursor: pointer; padding: 3px 4px; border-radius: 4px; transition: background 0.12s; }}
.sf-row:hover {{ background: rgba(0,0,0,0.06); }}
.sf-row-all {{ margin-bottom: 10px; padding-bottom: 11px; border-bottom: 1px solid #ccc; }}
[data-theme=""dark""] .sf-row:hover {{ background: rgba(255,255,255,0.08); }}
[data-theme=""dark""] .sf-row-all {{ border-bottom-color: #495057; }}
.status-badge {{ width: 100px; display: inline-block; line-height: 15px; text-align: center; padding: 2px 8px; border-radius: 3px; font-size: 12px; font-weight: 500; box-sizing: border-box; }}
.status-badge.added {{ background: {add}; color: white; }}
.status-badge.removed {{ background: {rem}; color: white; }}
.status-badge.modified {{ background: {mod}; color: white; }}
.status-badge.reordered {{ background: {reord}; color: white; }}
.status-badge.unchanged {{ background: #e0e0e0; color: #555; }}
.layout.highlight-rows .ag-row.row-added {{ background: {add}22 !important; }}
.layout.highlight-rows .ag-row.row-removed {{ background: {rem}22 !important; }}
.layout.highlight-rows .ag-row.row-modified {{ background: {mod}22 !important; }}
.ag-row.row-unchanged {{ }}
.layout.highlight-cells .ag-cell.cell-added {{ background: {add}44 !important; }}
.layout.highlight-cells .ag-cell.cell-removed {{ background: {rem}44 !important; }}
.layout.highlight-cells .ag-cell.cell-modified {{ background: {mod}44 !important; }}
.ag-cell.cell-unchanged {{ }}
.ag-cell.cell-format-only {{ box-shadow: inset 0 0 0 1px rgba(0,0,0,0.18); }}
.ag-header-cell.column-header-modified {{ background: {mod}44 !important; }}
.diff-old {{ background: {rem}59; border-radius: 4px; padding: 2px 6px; margin-right: 4px; }}
.diff-new {{ background: {add}59; border-radius: 4px; padding: 2px 6px; }}
.ag-row:hover .ag-cell {{ background: #fafafa !important; }}
.layout.highlight-rows .ag-row.row-added:hover .ag-cell {{ background: {add}33 !important; }}
.layout.highlight-rows .ag-row.row-removed:hover .ag-cell {{ background: {rem}33 !important; }}
.layout.highlight-rows .ag-row.row-modified:hover .ag-cell {{ background: {mod}33 !important; }}
.text-diff {{ font-family: ui-monospace, monospace; font-size: 13px; line-height: 1.5; margin: 0; padding: 12px; overflow-x: auto; white-space: pre; border: 1px solid #ddd; border-radius: 4px; background: #fafafa; }}
.text-diff .text-line-added {{ display: block; background: {add}22; }}
.text-diff .text-line-removed {{ display: block; background: {rem}22; }}
.text-diff .text-line-modified {{ display: block; }}
.text-diff .text-line-unchanged {{ display: block; }}

[data-theme=""dark""] body {{ background: #1a1a1a; color: #e9ecef; }}
[data-theme=""dark""] .tools-curtain {{ background: #2d2d2d; border-color: #495057; }}
[data-theme=""dark""] .tools-label {{ color: #e9ecef; }}
[data-theme=""dark""] .tools-toggle {{ color: #e9ecef; }}
[data-theme=""dark""] .tools-toggle:hover .tools-caret {{ background: rgba(255,255,255,0.16); }}
[data-theme=""dark""] .view-btn {{ background: #3d3d3d; border-color: #495057; color: #e9ecef; }}
[data-theme=""dark""] .view-btn:hover:not(.active) {{ background: #4d4d4d; border-color: #6c757d; }}
[data-theme=""dark""] .view-btn.active {{ background: #0d6efd; color: #fff; border-color: #0d6efd; }}
[data-theme=""dark""] .view-btn.active:hover {{ background: #0b5ed7; border-color: #0a58ca; color: #fff; }}
[data-theme=""dark""] .tools-option input {{ accent-color: #0d6efd; }}
[data-theme=""dark""] .view-switcher-label {{ color: #adb5bd; }}
[data-theme=""dark""] .container {{ background: #2d2d2d; box-shadow: 0 2px 8px rgba(0,0,0,0.3); }}
[data-theme=""dark""] h1 {{ color: #e9ecef; }}
[data-theme=""dark""] .file-info {{ background: #3d3d3d; color: #e9ecef; }}
[data-theme=""dark""] .file-info .file-stats {{ color: #adb5bd; }}
[data-theme=""dark""] .badge.unchanged {{ background: #495057; color: #adb5bd; }}
[data-theme=""dark""] .diff-grid-container.ag-theme-alpine {{
  --ag-background-color: #2d2d2d;
  --ag-foreground-color: #e9ecef;
  --ag-header-background-color: #3d3d3d;
  --ag-header-foreground-color: #e9ecef;
  --ag-border-color: #495057;
  --ag-odd-row-background-color: #252525;
  --ag-row-hover-color: rgba(255,255,255,0.04);
  --ag-alpine-active-color: #0d6efd;
  --ag-control-panel-background-color: #3d3d3d;
  --ag-input-focus-border-color: #0d6efd;
}}
[data-theme=""dark""] .ag-floating-filter-input input,
[data-theme=""dark""] .ag-text-field-input,
[data-theme=""dark""] .ag-number-field-input {{ background: #2d2d2d; color: #e9ecef; border-color: #495057; }}
[data-theme=""dark""] .ag-filter {{ background: #3d3d3d; color: #e9ecef; }}
[data-theme=""dark""] .ag-filter label {{ color: #e9ecef; }}
[data-theme=""dark""] .ag-filter .sf-item-cb,
[data-theme=""dark""] .ag-filter input[type=checkbox] {{ accent-color: #0d6efd; }}
[data-theme=""dark""] .ag-filter label[style*=""border-bottom""] {{ border-bottom-color: #495057 !important; }}
[data-theme=""dark""] .ag-picker-field-wrapper {{ background: #2d2d2d !important; border-color: #495057 !important; }}
[data-theme=""dark""] .ag-picker-field-display {{ color: #e9ecef !important; }}
[data-theme=""dark""] .ag-select-list {{ background: #2d2d2d; border-color: #495057; color: #e9ecef; }}
[data-theme=""dark""] .ag-select-list-item {{ color: #e9ecef; }}
[data-theme=""dark""] .ag-select-list-item:hover,
[data-theme=""dark""] .ag-select-list-item.ag-active-item {{ background: #3d3d3d !important; color: #e9ecef !important; }}
[data-theme=""dark""] .ag-row:hover .ag-cell {{ background: #3d3d3d !important; }}
[data-theme=""dark""] .layout.highlight-rows .ag-row.row-added:hover .ag-cell {{ background: {add}33 !important; }}
[data-theme=""dark""] .layout.highlight-rows .ag-row.row-removed:hover .ag-cell {{ background: {rem}33 !important; }}
[data-theme=""dark""] .layout.highlight-rows .ag-row.row-modified:hover .ag-cell {{ background: {mod}33 !important; }}
[data-theme=""dark""] .ag-header-cell.column-header-modified {{ background: {mod}44 !important; }}
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

	/// <summary>
	/// Character-level diff (git-diff style). Returns HTML for the "removed" line (left with removals in diff-old) and "added" line (right with additions in diff-new).
	/// </summary>
	private static (string RemovedLineHtml, string AddedLineHtml) CharacterDiffHtml(
		string left,
		string right
	)
	{
		if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right))
			return ("", "");
		if (string.IsNullOrEmpty(left))
			return ("", $"<span class=\"diff-new\">{EscapeHtml(right)}</span>");
		if (string.IsNullOrEmpty(right))
			return ($"<span class=\"diff-old\">{EscapeHtml(left)}</span>", "");

		var n = left.Length;
		var m = right.Length;
		var dp = new int[n + 1, m + 1];
		for (var i = 1; i <= n; i++)
		{
			for (var j = 1; j <= m; j++)
			{
				if (left[i - 1] == right[j - 1])
					dp[i, j] = dp[i - 1, j - 1] + 1;
				else
					dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
			}
		}

		var leftMatched = new bool[n];
		var rightMatched = new bool[m];
		var i0 = n;
		var j0 = m;
		while (i0 > 0 && j0 > 0)
		{
			if (left[i0 - 1] == right[j0 - 1])
			{
				leftMatched[i0 - 1] = true;
				rightMatched[j0 - 1] = true;
				i0--;
				j0--;
			}
			else if (dp[i0 - 1, j0] >= dp[i0, j0 - 1])
				i0--;
			else
				j0--;
		}

		var removedHtml = BuildLineWithHighlights(left, leftMatched, "diff-old");
		var addedHtml = BuildLineWithHighlights(right, rightMatched, "diff-new");
		return (removedHtml, addedHtml);
	}

	private static string CharacterDiffCellHtml(string left, string right)
	{
		var (removedHtml, addedHtml) = CharacterDiffHtml(left, right);
		if (string.IsNullOrEmpty(removedHtml) && string.IsNullOrEmpty(addedHtml))
			return "";
		if (string.IsNullOrEmpty(removedHtml))
			return addedHtml;
		if (string.IsNullOrEmpty(addedHtml))
			return removedHtml;
		return $"{removedHtml} → {addedHtml}";
	}

	private static string BuildLineWithHighlights(string text, bool[] matched, string spanClass)
	{
		var sb = new StringBuilder();
		var i = 0;
		while (i < text.Length)
		{
			if (matched[i])
			{
				sb.Append(EscapeHtml(text[i].ToString()));
				i++;
			}
			else
			{
				var start = i;
				while (i < text.Length && !matched[i])
					i++;
				sb.Append(
					$"<span class=\"{spanClass}\">{EscapeHtml(text.Substring(start, i - start))}</span>"
				);
			}
		}
		return sb.ToString();
	}

	private static bool AreSemanticallyEqual(string left, string right)
	{
		if (string.Equals(left, right, StringComparison.Ordinal))
			return true;

		// Numbers (using invariant culture, matching XlsxReader behavior)
		if (
			double.TryParse(left, NumberStyles.Any, CultureInfo.InvariantCulture, out var d1)
			&& double.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out var d2)
			&& d1.Equals(d2)
		)
			return true;

		// ISO dates or other invariant date formats
		if (
			DateTime.TryParse(left, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t1)
			&& DateTime.TryParse(
				right,
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var t2
			)
			&& t1.Equals(t2)
		)
			return true;

		// Booleans
		if (bool.TryParse(left, out var b1) && bool.TryParse(right, out var b2) && b1 == b2)
			return true;

		return false;
	}
}
