using System.Globalization;
using DiffCheck.Models;

namespace DiffCheck.Diff;

/// <summary>
/// Compares two data tables and produces a diff result.
/// Uses content-based row matching to detect reordered rows.
/// </summary>
public sealed class DiffEngine
{
	// Bounded parallelism: cap at the logical CPU count so we don't over-subscribe.
	private static readonly ParallelOptions s_parallelOptions = new()
	{
		MaxDegreeOfParallelism = Environment.ProcessorCount,
	};

	/// <summary>
	/// Compares two data tables.
	/// </summary>
	/// <param name="left">The first (original) table.</param>
	/// <param name="right">The second (modified) table.</param>
	/// <param name="columnMappings">Optional pairs (left header, right header) to treat as the same column (e.g. renames).</param>
	/// <param name="keyColumns">Optional list of column names to match rows by (faster than content-based matching).</param>
	/// <param name="options">Optional normalization and matching options. Defaults preserve the original behavior.</param>
	/// <returns>The diff result.</returns>
	public DiffResult Compare(
		DataTable left,
		DataTable right,
		IReadOnlyList<ColumnMapping>? columnMappings = null,
		IReadOnlyList<string>? keyColumns = null,
		ComparisonOptions? options = null
	)
	{
		ArgumentNullException.ThrowIfNull(left);
		ArgumentNullException.ThrowIfNull(right);

		var opts = options ?? ComparisonOptions.Default;

		var (headers, leftColIndex, rightColIndex, headerRenames) = BuildColumnIndices(
			left.Headers,
			right.Headers,
			columnMappings
		);

		// Build indexed rows: (row, originalIndex)
		var leftIndexed = left.Rows.Select((row, i) => (Row: row, OriginalIndex: i + 1)).ToList();
		var rightIndexed = right.Rows.Select((row, i) => (Row: row, OriginalIndex: i + 1)).ToList();

		// When key columns are specified, match by key (O(1) lookup per row).
		var keyColumnsFiltered = FilterKeyColumns(keyColumns, headers, leftColIndex, rightColIndex);
		Dictionary<string, Queue<int>>? leftKeyToIndices =
			keyColumnsFiltered.Count > 0
				? BuildLeftKeyMultimap(leftIndexed, keyColumnsFiltered, leftColIndex, opts)
				: null;

		// When no key columns, build an inverted content index for O(n·cols) matching
		// instead of the O(n²·cols) linear scan — provided NumericTolerance is not
		// positive (positive tolerance requires range-based matching).
		Dictionary<string, List<int>>? contentIndex =
			leftKeyToIndices == null && CanUseContentIndex(opts)
				? BuildContentIndex(leftIndexed, headers, leftColIndex, opts)
				: null;

		// ── Phase 1: Sequential matching ─────────────────────────────────────
		// Determine the left-row index paired with each right row.  Sequential
		// order is required so that duplicate keys are consumed in document order.
		var pairings = new int?[rightIndexed.Count]; // rightIdx → leftIdx (null = unmatched)
		var leftMatched = new HashSet<int>(rightIndexed.Count);

		for (var rightIdx = 0; rightIdx < rightIndexed.Count; rightIdx++)
		{
			var (rightRow, _) = rightIndexed[rightIdx];

			int? bestLeft;
			if (leftKeyToIndices != null)
			{
				// O(1) hash-map lookup on key columns.
				var rightKey = GetRowKey(rightRow, keyColumnsFiltered, rightColIndex, opts);
				if (leftKeyToIndices.TryGetValue(rightKey, out var queue) && queue.Count > 0)
				{
					bestLeft = queue.Dequeue();
					if (queue.Count == 0)
						leftKeyToIndices.Remove(rightKey);
				}
				else
					bestLeft = null;
			}
			else if (contentIndex != null)
			{
				// Inverted-index accelerated content match.
				bestLeft = FindBestMatchWithIndex(
					rightRow,
					contentIndex,
					leftMatched,
					headers,
					rightColIndex,
					opts
				);
			}
			else
			{
				// Fallback: O(n²·cols) linear scan (only when NumericTolerance > 0).
				bestLeft = FindBestMatch(
					rightRow,
					leftIndexed,
					leftMatched,
					headers,
					leftColIndex,
					rightColIndex,
					opts
				);
			}

			if (bestLeft.HasValue)
				leftMatched.Add(bestLeft.Value);
			pairings[rightIdx] = bestLeft;
		}

		// Collect unmatched left row indices for the Removed pass.
		var unmatchedLeft = new List<int>(Math.Max(0, leftIndexed.Count - leftMatched.Count));
		for (var i = 0; i < leftIndexed.Count; i++)
			if (!leftMatched.Contains(i))
				unmatchedLeft.Add(i);

		// ── Phase 2: Parallel cell comparisons ───────────────────────────────
		// Each row comparison is data-independent, so all rows can be processed
		// in parallel.  Results are stored at pre-assigned array slots to avoid
		// any shared mutable state.
		var totalRows = rightIndexed.Count + unmatchedLeft.Count;
		var diffRows = new DiffRow[totalRows];

		Parallel.For(
			0,
			rightIndexed.Count,
			s_parallelOptions,
			rightIdx =>
			{
				var (rightRow, rightOrigIdx) = rightIndexed[rightIdx];
				var leftIdx = pairings[rightIdx];

				DiffRow row;
				if (leftIdx.HasValue)
				{
					var (leftRow, leftOrigIdx) = leftIndexed[leftIdx.Value];
					var cells = CompareCells(
						headers,
						leftRow,
						rightRow,
						leftColIndex,
						rightColIndex,
						opts
					);
					var isModified = HasModifiedCell(cells);
					var isReordered = leftOrigIdx != rightOrigIdx;

					DiffRowStatus status;
					if (!isModified && isReordered)
					{
						status = DiffRowStatus.Reordered;
						cells = MarkCellsReordered(cells);
					}
					else if (isModified)
						status = DiffRowStatus.Modified;
					else
						status = DiffRowStatus.Unchanged;

					row = new DiffRow(0, status, cells, leftOrigIdx, rightOrigIdx);
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
					row = new DiffRow(0, DiffRowStatus.Added, cells, null, rightOrigIdx);
				}

				diffRows[rightIdx] = row;
			}
		);

		Parallel.For(
			0,
			unmatchedLeft.Count,
			s_parallelOptions,
			j =>
			{
				var i = unmatchedLeft[j];
				var (leftRow, leftOrigIdx) = leftIndexed[i];
				var cells = BuildCells(
					headers,
					leftRow,
					null,
					leftColIndex,
					DiffCellStatus.Removed
				);
				diffRows[rightIndexed.Count + j] = new DiffRow(
					0,
					DiffRowStatus.Removed,
					cells,
					leftOrigIdx,
					null
				);
			}
		);

		// ── Phase 3: Count, sort, and assemble the result ────────────────────
		var added = 0;
		var removed = 0;
		var modified = 0;
		var unchanged = 0;
		var reordered = 0;
		foreach (var r in diffRows)
		{
			switch (r.Status)
			{
				case DiffRowStatus.Added:
					added++;
					break;
				case DiffRowStatus.Removed:
					removed++;
					break;
				case DiffRowStatus.Modified:
					modified++;
					break;
				case DiffRowStatus.Unchanged:
					unchanged++;
					break;
				case DiffRowStatus.Reordered:
					reordered++;
					break;
			}
		}

		// Order so added rows appear at their right index, removed at their left index.
		// At same position: removed first, then matched, then added.
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
		IReadOnlyDictionary<string, int> leftColIndex,
		ComparisonOptions options
	)
	{
		var map = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
		for (var i = 0; i < leftIndexed.Count; i++)
		{
			var key = GetRowKey(leftIndexed[i].Row, keyColumns, leftColIndex, options);
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
		IReadOnlyDictionary<string, int> colIndex,
		ComparisonOptions options
	)
	{
		var parts = new string[keyColumns.Count];
		for (var i = 0; i < keyColumns.Count; i++)
		{
			var val = GetValue(row, colIndex, keyColumns[i]) ?? string.Empty;
			if (options.TrimWhitespace)
				val = val.Trim();
			if (!options.CaseSensitive)
				val = val.ToUpperInvariant();
			parts[i] = val;
		}
		return string.Join(KeySeparator, parts);
	}

	/// <summary>
	/// Returns true when the inverted content index can be used for matching.
	/// It cannot be used when <paramref name="options"/> has a positive numeric tolerance,
	/// because approximate numeric matching requires range-based queries rather than exact hash lookups.
	/// </summary>
	private static bool CanUseContentIndex(ComparisonOptions options) =>
		!(options.NumericTolerance.HasValue && options.NumericTolerance.Value > 0.0);

	/// <summary>
	/// Normalizes a cell value to the canonical form used as the content-index key.
	/// Must produce the same string for any two values that <see cref="ValuesAreEqual"/> considers equal
	/// under the given options (trim, case, exact-numeric).
	/// </summary>
	private static string NormalizeForIndex(string value, ComparisonOptions options)
	{
		var v = options.TrimWhitespace ? value.Trim() : value;
		if (!options.CaseSensitive)
			v = v.ToUpperInvariant();
		// When exact numeric tolerance is active (0.0), normalize numeric strings so that
		// "1.0" and "1" (which both parse to the same double) hash to the same key.
		if (
			options.NumericTolerance is 0.0
			&& double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
			&& !double.IsNaN(d)
			&& !double.IsInfinity(d)
		)
			v = d.ToString("R", CultureInfo.InvariantCulture);
		return v;
	}

	/// <summary>
	/// Builds an inverted index mapping each (header, normalizedValue) pair to the list of
	/// left-row indices that contain that value.  Enables O(cols) candidate lookup per right
	/// row instead of an O(n) linear scan over all left rows.
	/// </summary>
	private static Dictionary<string, List<int>> BuildContentIndex(
		IReadOnlyList<(IReadOnlyList<string> Row, int OriginalIndex)> leftIndexed,
		IReadOnlyList<string> headers,
		IReadOnlyDictionary<string, int> leftColIndex,
		ComparisonOptions options
	)
	{
		// Capacity hint: at most one distinct key per left row (common case where values are unique).
		// The dictionary will grow automatically for high-cardinality data.
		var index = new Dictionary<string, List<int>>(leftIndexed.Count, StringComparer.Ordinal);
		for (var i = 0; i < leftIndexed.Count; i++)
		{
			var row = leftIndexed[i].Row;
			foreach (var header in headers)
			{
				var val = GetValue(row, leftColIndex, header) ?? string.Empty;
				var key = header + KeySeparator + NormalizeForIndex(val, options);
				if (!index.TryGetValue(key, out var list))
					index[key] = list = [];
				list.Add(i);
			}
		}
		return index;
	}

	/// <summary>
	/// Finds the best-scoring unmatched left row for <paramref name="rightRow"/> using the
	/// pre-built inverted content index.  Falls back to null when no candidate meets
	/// <see cref="ComparisonOptions.MatchThreshold"/>.
	/// Tie-breaking preserves the same first-lowest-index semantics as <see cref="FindBestMatch"/>.
	/// </summary>
	private static int? FindBestMatchWithIndex(
		IReadOnlyList<string> rightRow,
		Dictionary<string, List<int>> contentIndex,
		HashSet<int> leftMatched,
		IReadOnlyList<string> headers,
		IReadOnlyDictionary<string, int> rightColIndex,
		ComparisonOptions options
	)
	{
		if (headers.Count == 0)
			return null;

		// Count how many headers agree between the right row and each candidate left row.
		var hitCounts = new Dictionary<int, int>();
		foreach (var header in headers)
		{
			var val = GetValue(rightRow, rightColIndex, header) ?? string.Empty;
			var key = header + KeySeparator + NormalizeForIndex(val, options);
			if (!contentIndex.TryGetValue(key, out var candidates))
				continue;
			foreach (var leftIdx in candidates)
			{
				if (!leftMatched.Contains(leftIdx))
				{
					hitCounts.TryGetValue(leftIdx, out var c);
					hitCounts[leftIdx] = c + 1;
				}
			}
		}

		if (hitCounts.Count == 0)
			return null;

		// Iterate in ascending left-index order so ties resolve to the lowest index,
		// matching the behaviour of the linear-scan FindBestMatch.
		double bestScore = 0;
		int? bestIdx = null;
		var total = headers.Count;
		foreach (var leftIdx in hitCounts.Keys.Order())
		{
			var score = (double)hitCounts[leftIdx] / total;
			if (score >= options.MatchThreshold && score > bestScore)
			{
				bestScore = score;
				bestIdx = leftIdx;
			}
		}
		return bestIdx;
	}

	private static int? FindBestMatch(
		IReadOnlyList<string> rightRow,
		IReadOnlyList<(IReadOnlyList<string> Row, int OriginalIndex)> leftIndexed,
		HashSet<int> leftMatched,
		IReadOnlyList<string> headers,
		IReadOnlyDictionary<string, int> leftColIndex,
		IReadOnlyDictionary<string, int> rightColIndex,
		ComparisonOptions options
	)
	{
		double bestScore = 0;
		int? bestIdx = null;

		for (var i = 0; i < leftIndexed.Count; i++)
		{
			if (leftMatched.Contains(i))
				continue;

			var leftRow = leftIndexed[i].Row;
			var score = GetMatchScore(
				leftRow,
				rightRow,
				headers,
				leftColIndex,
				rightColIndex,
				options
			);

			if (score >= options.MatchThreshold && score > bestScore)
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
		IReadOnlyDictionary<string, int> rightColIndex,
		ComparisonOptions options
	)
	{
		if (headers.Count == 0)
			return 1.0;

		var matches = 0;
		foreach (var header in headers)
		{
			var leftVal = GetValue(leftRow, leftColIndex, header) ?? string.Empty;
			var rightVal = GetValue(rightRow, rightColIndex, header) ?? string.Empty;
			if (ValuesAreEqual(leftVal, rightVal, options))
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

	/// <summary>
	/// Builds a uniform-status cell array for a row that exists only on one side
	/// (Added or Removed).
	/// </summary>
	private static DiffCell[] BuildCells(
		IReadOnlyList<string> headers,
		IReadOnlyList<string>? leftRow,
		IReadOnlyList<string>? rightRow,
		IReadOnlyDictionary<string, int> colIndex,
		DiffCellStatus defaultStatus
	)
	{
		var cells = new DiffCell[headers.Count];
		var row = defaultStatus == DiffCellStatus.Added ? rightRow : leftRow;
		for (var i = 0; i < headers.Count; i++)
		{
			var header = headers[i];
			var value = string.Empty;
			if (row != null && colIndex.TryGetValue(header, out var idx) && idx < row.Count)
				value = row[idx] ?? string.Empty;

			cells[i] = new DiffCell(
				header,
				defaultStatus == DiffCellStatus.Added ? null : value,
				defaultStatus == DiffCellStatus.Removed ? null : value,
				value,
				defaultStatus
			);
		}
		return cells;
	}

	/// <summary>
	/// Compares corresponding cells from the left and right rows and returns a
	/// fixed-size array of <see cref="DiffCell"/> objects.
	/// </summary>
	private static DiffCell[] CompareCells(
		IReadOnlyList<string> headers,
		IReadOnlyList<string> leftRow,
		IReadOnlyList<string> rightRow,
		IReadOnlyDictionary<string, int> leftColIndex,
		IReadOnlyDictionary<string, int> rightColIndex,
		ComparisonOptions options
	)
	{
		var cells = new DiffCell[headers.Count];
		for (var i = 0; i < headers.Count; i++)
		{
			var header = headers[i];
			var leftVal = GetValue(leftRow, leftColIndex, header);
			var rightVal = GetValue(rightRow, rightColIndex, header);

			var leftStr = leftVal ?? string.Empty;
			var rightStr = rightVal ?? string.Empty;

			DiffCellStatus status;
			string display;

			if (ValuesAreEqual(leftStr, rightStr, options))
			{
				status = DiffCellStatus.Unchanged;
				display = leftStr;
			}
			else
			{
				status = DiffCellStatus.Modified;
				display = $"{leftStr} → {rightStr}";
			}

			cells[i] = new DiffCell(header, leftStr, rightStr, display, status);
		}
		return cells;
	}

	/// <summary>Returns true when at least one cell in the array has Modified status.</summary>
	private static bool HasModifiedCell(DiffCell[] cells)
	{
		for (var i = 0; i < cells.Length; i++)
			if (cells[i].Status == DiffCellStatus.Modified)
				return true;
		return false;
	}

	/// <summary>
	/// Returns a new array where every cell's status is set to <see cref="DiffCellStatus.Reordered"/>.
	/// </summary>
	private static DiffCell[] MarkCellsReordered(DiffCell[] cells)
	{
		var result = new DiffCell[cells.Length];
		for (var i = 0; i < cells.Length; i++)
		{
			var c = cells[i];
			result[i] = new DiffCell(
				c.Header,
				c.LeftValue,
				c.RightValue,
				c.DisplayValue,
				DiffCellStatus.Reordered
			);
		}
		return result;
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

	/// <summary>
	/// Returns true when <paramref name="left"/> and <paramref name="right"/> are considered equal
	/// under the given <paramref name="options"/> (trim, case, numeric tolerance).
	/// Raw values are compared; normalization is applied only for the equality check.
	/// </summary>
	private static bool ValuesAreEqual(string left, string right, ComparisonOptions options)
	{
		var l = options.TrimWhitespace ? left.Trim() : left;
		var r = options.TrimWhitespace ? right.Trim() : right;

		if (
			options.NumericTolerance.HasValue
			&& double.TryParse(l, NumberStyles.Any, CultureInfo.InvariantCulture, out var lNum)
			&& double.TryParse(r, NumberStyles.Any, CultureInfo.InvariantCulture, out var rNum)
		)
		{
			return Math.Abs(lNum - rNum) <= options.NumericTolerance.Value;
		}

		var comparison = options.CaseSensitive
			? StringComparison.Ordinal
			: StringComparison.OrdinalIgnoreCase;
		return string.Equals(l, r, comparison);
	}
}
