using DiffCheck.Models;

namespace DiffCheck.Diff;

/// <summary>
/// Compares two data tables and produces a diff result.
/// Uses content-based row matching to detect reordered rows.
/// </summary>
public sealed class DiffEngine
{
	private const double MatchThreshold = 0.5;

	/// <summary>
	/// Compares two data tables.
	/// </summary>
	/// <param name="left">The first (original) table.</param>
	/// <param name="right">The second (modified) table.</param>
	/// <param name="columnMappings">Optional pairs (left header, right header) to treat as the same column (e.g. renames).</param>
	/// <param name="keyColumns">Optional list of column names to match rows by (faster than content-based matching).</param>
	/// <returns>The diff result.</returns>
	public DiffResult Compare(
		DataTable left,
		DataTable right,
		IReadOnlyList<ColumnMapping>? columnMappings = null,
		IReadOnlyList<string>? keyColumns = null
	)
	{
		ArgumentNullException.ThrowIfNull(left);
		ArgumentNullException.ThrowIfNull(right);

		var (headers, leftColIndex, rightColIndex, headerRenames) = BuildColumnIndices(
			left.Headers,
			right.Headers,
			columnMappings
		);

		// Build indexed rows: (row, originalIndex)
		var leftIndexed = left.Rows.Select((row, i) => (Row: row, OriginalIndex: i + 1)).ToList();
		var rightIndexed = right.Rows.Select((row, i) => (Row: row, OriginalIndex: i + 1)).ToList();

		// When key columns are specified, match by key (O(1) lookup per row) instead of content-based (O(n) per row)
		var keyColumnsFiltered = FilterKeyColumns(keyColumns, headers, leftColIndex, rightColIndex);
		Dictionary<string, Queue<int>>? leftKeyToIndices =
			keyColumnsFiltered.Count > 0
				? BuildLeftKeyMultimap(leftIndexed, keyColumnsFiltered, leftColIndex)
				: null;

		var diffRows = new List<DiffRow>();
		var leftMatched = new HashSet<int>();
		var added = 0;
		var removed = 0;
		var modified = 0;
		var unchanged = 0;
		var reordered = 0;
		var displayIndex = 1;

		// Process right rows in original order to preserve output sequence
		for (var rightIdx = 0; rightIdx < rightIndexed.Count; rightIdx++)
		{
			var (rightRow, rightOrigIdx) = rightIndexed[rightIdx];

			int? bestLeft = null;
			if (leftKeyToIndices != null)
			{
				var rightKey = GetRowKey(rightRow, keyColumnsFiltered, rightColIndex);
				if (leftKeyToIndices.TryGetValue(rightKey, out var queue) && queue.Count > 0)
				{
					bestLeft = queue.Dequeue();
					if (queue.Count == 0)
						leftKeyToIndices.Remove(rightKey);
				}
			}
			else
			{
				// Find best matching left row (>= 50% columns match)
				bestLeft = FindBestMatch(
					rightRow,
					leftIndexed,
					leftMatched,
					headers,
					leftColIndex,
					rightColIndex
				);
			}

			if (bestLeft.HasValue)
			{
				var (leftRow, leftOrigIdx) = leftIndexed[bestLeft.Value];
				leftMatched.Add(bestLeft.Value);

				var cells = CompareCells(headers, leftRow, rightRow, leftColIndex, rightColIndex);
				var cellStatus = GetRowCellStatus(cells);
				var isReordered = leftOrigIdx != rightOrigIdx;

				DiffRowStatus status;
				if (cellStatus == DiffCellStatus.Unchanged && isReordered)
				{
					status = DiffRowStatus.Reordered;
					reordered++;
				}
				else if (cellStatus == DiffCellStatus.Modified)
				{
					status = DiffRowStatus.Modified;
					modified++;
				}
				else
				{
					status = DiffRowStatus.Unchanged;
					unchanged++;
				}

				// For reordered rows, mark cells as Reordered for display
				if (status == DiffRowStatus.Reordered)
				{
					cells = cells
						.Select(c => new DiffCell(
							c.Header,
							c.LeftValue,
							c.RightValue,
							c.DisplayValue,
							DiffCellStatus.Reordered
						))
						.ToList();
				}

				diffRows.Add(new DiffRow(displayIndex++, status, cells, leftOrigIdx, rightOrigIdx));
			}
			else
			{
				var cells = BuildCells(
					headers,
					null,
					rightRow,
					rightColIndex,
					DiffCellStatus.Added
				);
				diffRows.Add(
					new DiffRow(displayIndex++, DiffRowStatus.Added, cells, null, rightOrigIdx)
				);
				added++;
			}
		}

		// Remaining unmatched left rows -> Removed
		for (var leftIdx = 0; leftIdx < leftIndexed.Count; leftIdx++)
		{
			if (leftMatched.Contains(leftIdx))
				continue;

			var (leftRow, leftOrigIdx) = leftIndexed[leftIdx];
			var cells = BuildCells(headers, leftRow, null, leftColIndex, DiffCellStatus.Removed);
			diffRows.Add(
				new DiffRow(displayIndex++, DiffRowStatus.Removed, cells, leftOrigIdx, null)
			);
			removed++;
		}

		// Order so added rows appear at their right index, removed at their left index
		// At same position: removed first, then matched, then added
		var orderedRows = diffRows
			.OrderBy(r => GetSortPosition(r))
			.ThenBy(r => GetSortType(r))
			.Select(
				(r, i) => new DiffRow(i + 1, r.Status, r.Cells, r.LeftRowIndex, r.RightRowIndex)
			)
			.ToList();

		var summary = new DiffSummary(added, removed, modified, unchanged, reordered);
		return new DiffResult(
			headers,
			orderedRows,
			summary,
			leftRowCount: left.Rows.Count,
			leftColumnCount: left.Headers.Count,
			rightRowCount: right.Rows.Count,
			rightColumnCount: right.Headers.Count,
			columnHeaderRenames: headerRenames
		);
	}

	private static int GetSortPosition(DiffRow r)
	{
		return r.Status == DiffRowStatus.Removed
			? r.LeftRowIndex ?? int.MaxValue
			: r.RightRowIndex ?? int.MaxValue;
	}

	private static int GetSortType(DiffRow r)
	{
		return r.Status switch
		{
			DiffRowStatus.Removed => 0,
			DiffRowStatus.Added => 2,
			_ => 1, // Unchanged, Modified, Reordered
		};
	}

	private const char KeySeparator = '\u0001';

	/// <summary>Returns key column names that exist in both left and right and in headers.</summary>
	private static IReadOnlyList<string> FilterKeyColumns(
		IReadOnlyList<string>? keyColumns,
		IReadOnlyList<string> headers,
		IReadOnlyDictionary<string, int> leftColIndex,
		IReadOnlyDictionary<string, int> rightColIndex
	)
	{
		if (keyColumns == null || keyColumns.Count == 0)
			return Array.Empty<string>();
		var headerSet = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
		var result = new List<string>();
		foreach (var col in keyColumns)
		{
			if (string.IsNullOrWhiteSpace(col))
				continue;
			var name = col.Trim();
			if (
				headerSet.Contains(name)
				&& leftColIndex.ContainsKey(name)
				&& rightColIndex.ContainsKey(name)
			)
				result.Add(name);
		}
		return result;
	}

	private static Dictionary<string, Queue<int>> BuildLeftKeyMultimap(
		IReadOnlyList<(IReadOnlyList<string> Row, int OriginalIndex)> leftIndexed,
		IReadOnlyList<string> keyColumns,
		IReadOnlyDictionary<string, int> leftColIndex
	)
	{
		var map = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
		for (var i = 0; i < leftIndexed.Count; i++)
		{
			var key = GetRowKey(leftIndexed[i].Row, keyColumns, leftColIndex);
			if (!map.TryGetValue(key, out var queue))
			{
				queue = new Queue<int>();
				map[key] = queue;
			}
			queue.Enqueue(i);
		}
		return map;
	}

	private static string GetRowKey(
		IReadOnlyList<string> row,
		IReadOnlyList<string> keyColumns,
		IReadOnlyDictionary<string, int> colIndex
	)
	{
		var parts = new string[keyColumns.Count];
		for (var i = 0; i < keyColumns.Count; i++)
		{
			var val = GetValue(row, colIndex, keyColumns[i]);
			parts[i] = val ?? string.Empty;
		}
		return string.Join(KeySeparator, parts);
	}

	private static int? FindBestMatch(
		IReadOnlyList<string> rightRow,
		IReadOnlyList<(IReadOnlyList<string> Row, int OriginalIndex)> leftIndexed,
		HashSet<int> leftMatched,
		IReadOnlyList<string> headers,
		IReadOnlyDictionary<string, int> leftColIndex,
		IReadOnlyDictionary<string, int> rightColIndex
	)
	{
		double bestScore = 0;
		int? bestIdx = null;

		for (var i = 0; i < leftIndexed.Count; i++)
		{
			if (leftMatched.Contains(i))
				continue;

			var leftRow = leftIndexed[i].Row;
			var score = GetMatchScore(leftRow, rightRow, headers, leftColIndex, rightColIndex);

			if (score >= MatchThreshold && score > bestScore)
			{
				bestScore = score;
				bestIdx = i;
			}
		}

		return bestIdx;
	}

	private static double GetMatchScore(
		IReadOnlyList<string> leftRow,
		IReadOnlyList<string> rightRow,
		IReadOnlyList<string> headers,
		IReadOnlyDictionary<string, int> leftColIndex,
		IReadOnlyDictionary<string, int> rightColIndex
	)
	{
		if (headers.Count == 0)
			return 1.0;

		var matches = 0;
		foreach (var header in headers)
		{
			var leftVal = GetValue(leftRow, leftColIndex, header) ?? string.Empty;
			var rightVal = GetValue(rightRow, rightColIndex, header) ?? string.Empty;
			if (string.Equals(leftVal, rightVal, StringComparison.Ordinal))
				matches++;
		}
		return (double)matches / headers.Count;
	}

	/// <summary>
	/// Builds merged headers and column indices, applying column mappings so that
	/// left/right columns designated as pairs are treated as one column.
	/// Also returns headerRenames: for each header index, the right-side name when mapped (for display "Left → Right").
	/// </summary>
	private static (
		IReadOnlyList<string> Headers,
		IReadOnlyDictionary<string, int> LeftColIndex,
		IReadOnlyDictionary<string, int> RightColIndex,
		IReadOnlyList<string?> HeaderRenames
	) BuildColumnIndices(
		IReadOnlyList<string> leftHeaders,
		IReadOnlyList<string> rightHeaders,
		IReadOnlyList<ColumnMapping>? columnMappings
	)
	{
		var leftColIndex = leftHeaders
			.Select((h, i) => (h, i))
			.Where(x => !string.IsNullOrEmpty(x.h))
			.ToDictionary(x => x.h, x => x.i, StringComparer.OrdinalIgnoreCase);

		// Right header -> canonical (left) header when mapped
		var rightToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		// Left (canonical) header -> right header name for display
		var leftToRightName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (columnMappings != null)
		{
			foreach (var m in columnMappings)
			{
				if (string.IsNullOrEmpty(m.LeftHeader) || string.IsNullOrEmpty(m.RightHeader))
					continue;
				rightToCanonical[m.RightHeader] = m.LeftHeader;
				leftToRightName[m.LeftHeader] = m.RightHeader;
			}
		}

		// Canonical header -> right column index (for right table lookups)
		var rightColIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < rightHeaders.Count; i++)
		{
			var r = rightHeaders[i];
			if (string.IsNullOrEmpty(r))
				continue;
			var canonical = rightToCanonical.TryGetValue(r, out var c) ? c : r;
			rightColIndex[canonical] = i;
		}

		// Merged headers: left headers first, then right-only (not mapped to any left)
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var headers = new List<string>();
		var headerRenames = new List<string?>();
		foreach (var h in leftHeaders)
		{
			if (string.IsNullOrEmpty(h) || !seen.Add(h))
				continue;
			headers.Add(h);
			headerRenames.Add(leftToRightName.TryGetValue(h, out var rightName) ? rightName : null);
		}
		foreach (var r in rightHeaders)
		{
			if (string.IsNullOrEmpty(r))
				continue;
			var canonical = rightToCanonical.TryGetValue(r, out var c) ? c : r;
			if (seen.Add(canonical))
			{
				headers.Add(canonical);
				headerRenames.Add(null);
			}
		}

		return (headers, leftColIndex, rightColIndex, headerRenames);
	}

	private static IReadOnlyList<DiffCell> BuildCells(
		IReadOnlyList<string> headers,
		IReadOnlyList<string>? leftRow,
		IReadOnlyList<string>? rightRow,
		IReadOnlyDictionary<string, int> colIndex,
		DiffCellStatus defaultStatus
	)
	{
		var cells = new List<DiffCell>();
		var row = defaultStatus == DiffCellStatus.Added ? rightRow : leftRow;
		foreach (var header in headers)
		{
			var value = string.Empty;
			if (row != null && colIndex.TryGetValue(header, out var idx) && idx < row.Count)
				value = row[idx] ?? string.Empty;

			cells.Add(
				new DiffCell(
					header,
					defaultStatus == DiffCellStatus.Added ? null : value,
					defaultStatus == DiffCellStatus.Removed ? null : value,
					value,
					defaultStatus
				)
			);
		}
		return cells;
	}

	private static IReadOnlyList<DiffCell> CompareCells(
		IReadOnlyList<string> headers,
		IReadOnlyList<string> leftRow,
		IReadOnlyList<string> rightRow,
		IReadOnlyDictionary<string, int> leftColIndex,
		IReadOnlyDictionary<string, int> rightColIndex
	)
	{
		var cells = new List<DiffCell>();
		foreach (var header in headers)
		{
			var leftVal = GetValue(leftRow, leftColIndex, header);
			var rightVal = GetValue(rightRow, rightColIndex, header);

			var leftStr = leftVal ?? string.Empty;
			var rightStr = rightVal ?? string.Empty;

			DiffCellStatus status;
			string display;

			if (leftStr == rightStr)
			{
				status = DiffCellStatus.Unchanged;
				display = leftStr;
			}
			else
			{
				status = DiffCellStatus.Modified;
				display = $"{leftStr} → {rightStr}";
			}

			cells.Add(new DiffCell(header, leftStr, rightStr, display, status));
		}
		return cells;
	}

	private static DiffCellStatus GetRowCellStatus(IReadOnlyList<DiffCell> cells)
	{
		var hasModified = cells.Any(c => c.Status == DiffCellStatus.Modified);
		return hasModified ? DiffCellStatus.Modified : DiffCellStatus.Unchanged;
	}

	private static string? GetValue(
		IReadOnlyList<string> row,
		IReadOnlyDictionary<string, int> colIndex,
		string header
	)
	{
		if (!colIndex.TryGetValue(header, out var idx) || idx >= row.Count)
			return null;
		return row[idx];
	}
}
