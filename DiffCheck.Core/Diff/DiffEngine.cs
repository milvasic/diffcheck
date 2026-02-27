using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using DiffCheck.Models;

namespace DiffCheck.Diff;

/// <summary>
/// Compares two data tables and produces a diff result.
/// Uses content-based row matching to detect reordered rows.
/// Optimized for large tables (100k+ rows) via exact-match hashing, bucketing, and parallel index building.
/// </summary>
public sealed class DiffEngine
{
	private const double MatchThreshold = 0.5;

	// Separator for row content key; chosen to avoid collision with typical cell data
	private const char KeySeparator = '\u200b';

	/// <summary>
	/// Compares two data tables.
	/// </summary>
	/// <param name="left">The first (original) table.</param>
	/// <param name="right">The second (modified) table.</param>
	/// <returns>The diff result.</returns>
	public DiffResult Compare(DataTable left, DataTable right)
	{
		ArgumentNullException.ThrowIfNull(left);
		ArgumentNullException.ThrowIfNull(right);

		var headers = MergeHeaders(left.Headers, right.Headers);
		var leftColIndex = BuildColumnIndex(left.Headers);
		var rightColIndex = BuildColumnIndex(right.Headers);

		var leftIndexed = new (IReadOnlyList<string> Row, int OriginalIndex)[left.Rows.Count];
		for (var i = 0; i < left.Rows.Count; i++)
			leftIndexed[i] = (left.Rows[i], i + 1);

		var rightIndexed = new (IReadOnlyList<string> Row, int OriginalIndex)[right.Rows.Count];
		for (var i = 0; i < right.Rows.Count; i++)
			rightIndexed[i] = (right.Rows[i], i + 1);

		// Build indexes in parallel for fast lookup
		var (exactMatchIndex, bucketIndex, leftRowKeys) = BuildLeftIndexes(
			leftIndexed, headers, leftColIndex
		);

		var leftMatched = new HashSet<int>();
		var diffRows = new List<DiffRow>(right.Rows.Count + left.Rows.Count);
		var added = 0;
		var removed = 0;
		var modified = 0;
		var unchanged = 0;
		var reordered = 0;
		var displayIndex = 1;

		// Preallocate reusable buffer for right row key (avoid per-row allocation in hot path)
		var rightKeyBuffer = new string[headers.Count];

		for (var rightIdx = 0; rightIdx < rightIndexed.Length; rightIdx++)
		{
			var (rightRow, rightOrigIdx) = rightIndexed[rightIdx];

			var bestLeft = FindBestMatchOptimized(
				rightRow,
				rightIdx,
				leftIndexed,
				leftRowKeys,
				leftMatched,
				headers,
				leftColIndex,
				rightColIndex,
				exactMatchIndex,
				bucketIndex,
				rightKeyBuffer
			);

			if (bestLeft.HasValue)
			{
				var leftIdx = bestLeft.Value;
				var (leftRow, leftOrigIdx) = leftIndexed[leftIdx];
				leftMatched.Add(leftIdx);

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

				if (status == DiffRowStatus.Reordered)
				{
					cells = RemapCellsToReordered(cells);
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

		for (var leftIdx = 0; leftIdx < leftIndexed.Length; leftIdx++)
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

		var orderedRows = OrderDiffRows(diffRows);
		var summary = new DiffSummary(added, removed, modified, unchanged, reordered);
		return new DiffResult(
			headers,
			orderedRows,
			summary,
			left.Rows.Count,
			left.Headers.Count,
			right.Rows.Count,
			right.Headers.Count
		);
	}

	private static IReadOnlyDictionary<string, int> BuildColumnIndex(IReadOnlyList<string> headers)
	{
		var d = new Dictionary<string, int>(headers.Count, StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < headers.Count; i++)
			d[headers[i]] = i;
		return d;
	}

	private static (
		Dictionary<string, List<int>> ExactMatchIndex,
		Dictionary<string, List<int>> BucketIndex,
		string[] LeftRowKeys
	) BuildLeftIndexes(
		(IReadOnlyList<string> Row, int OriginalIndex)[] leftIndexed,
		IReadOnlyList<string> headers,
		IReadOnlyDictionary<string, int> leftColIndex
	)
	{
		var n = leftIndexed.Length;
		var leftRowKeys = new string[n];

		// Build content keys in parallel
		Parallel.For(0, n, i =>
		{
			leftRowKeys[i] = BuildRowContentKey(leftIndexed[i].Row, headers, leftColIndex);
		});

		var exactConcurrent = new ConcurrentDictionary<string, List<int>>();
		var bucketConcurrent = new ConcurrentDictionary<string, List<int>>();

		Parallel.For(0, n, i =>
		{
			var key = leftRowKeys[i];
			exactConcurrent.AddOrUpdate(key, _ => new List<int> { i }, (_, list) =>
			{
				lock (list) list.Add(i);
				return list;
			});

			var bucketKey = BuildBucketKey(leftIndexed[i].Row, headers, leftColIndex);
			bucketConcurrent.AddOrUpdate(bucketKey, _ => new List<int> { i }, (_, list) =>
			{
				lock (list) list.Add(i);
				return list;
			});
		});

		// Convert to regular dictionary (values already populated)
		var exactMatchIndex = new Dictionary<string, List<int>>(exactConcurrent.Count, StringComparer.Ordinal);
		foreach (var kv in exactConcurrent)
			exactMatchIndex[kv.Key] = kv.Value;

		var bucketIndex = new Dictionary<string, List<int>>(bucketConcurrent.Count, StringComparer.Ordinal);
		foreach (var kv in bucketConcurrent)
			bucketIndex[kv.Key] = kv.Value;

		return (exactMatchIndex, bucketIndex, leftRowKeys);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string BuildRowContentKey(
		IReadOnlyList<string> row,
		IReadOnlyList<string> headers,
		IReadOnlyDictionary<string, int> colIndex
	)
	{
		if (headers.Count == 0) return string.Empty;
		var sb = new StringBuilder(headers.Count * 16);
		for (var i = 0; i < headers.Count; i++)
		{
			if (i > 0) sb.Append(KeySeparator);
			var v = GetValue(row, colIndex, headers[i]);
			sb.Append(v ?? string.Empty);
		}
		return sb.ToString();
	}

	private static string BuildBucketKey(
		IReadOnlyList<string> row,
		IReadOnlyList<string> headers,
		IReadOnlyDictionary<string, int> colIndex
	)
	{
		if (headers.Count == 0) return string.Empty;
		var first = GetValue(row, colIndex, headers[0]) ?? string.Empty;
		if (headers.Count < 2) return first;
		var second = GetValue(row, colIndex, headers[1]) ?? string.Empty;
		return first + KeySeparator + second;
	}

	private static int? FindBestMatchOptimized(
		IReadOnlyList<string> rightRow,
		int rightIdx,
		(IReadOnlyList<string> Row, int OriginalIndex)[] leftIndexed,
		string[] leftRowKeys,
		HashSet<int> leftMatched,
		IReadOnlyList<string> headers,
		IReadOnlyDictionary<string, int> leftColIndex,
		IReadOnlyDictionary<string, int> rightColIndex,
		Dictionary<string, List<int>> exactMatchIndex,
		Dictionary<string, List<int>> bucketIndex,
		string[] rightKeyBuffer
	)
	{
		// 1) Exact match via content key
		BuildRowContentKeyInto(rightRow, headers, rightColIndex, rightKeyBuffer);
		var contentKey = string.Join(KeySeparator, rightKeyBuffer);
		if (exactMatchIndex.TryGetValue(contentKey, out var exactList))
		{
			foreach (var leftIdx in exactList)
			{
				if (leftMatched.Contains(leftIdx))
					continue;
				return leftIdx;
			}
		}

		// 2) Fuzzy match: try bucket first, then full scan
		var bucketKey = BuildBucketKey(rightRow, headers, rightColIndex);
		if (bucketIndex.TryGetValue(bucketKey, out var bucketList))
		{
			double bestScore = 0;
			int? bestIdx = null;
			foreach (var leftIdx in bucketList)
			{
				if (leftMatched.Contains(leftIdx))
					continue;
				var score = GetMatchScoreFast(
					leftIndexed[leftIdx].Row,
					rightRow,
					headers,
					leftColIndex,
					rightColIndex
				);
				if (score >= MatchThreshold && score > bestScore)
				{
					bestScore = score;
					bestIdx = leftIdx;
				}
			}
			if (bestIdx.HasValue)
				return bestIdx;
		}

		// 3) Fallback: full scan over unmatched left rows
		double bestScore2 = 0;
		int? bestIdx2 = null;
		for (var i = 0; i < leftIndexed.Length; i++)
		{
			if (leftMatched.Contains(i))
				continue;
			var score = GetMatchScoreFast(
				leftIndexed[i].Row,
				rightRow,
				headers,
				leftColIndex,
				rightColIndex
			);
			if (score >= MatchThreshold && score > bestScore2)
			{
				bestScore2 = score;
				bestIdx2 = i;
			}
		}
		return bestIdx2;
	}

	private static void BuildRowContentKeyInto(
		IReadOnlyList<string> row,
		IReadOnlyList<string> headers,
		IReadOnlyDictionary<string, int> rightColIndex,
		string[] buffer
	)
	{
		for (var i = 0; i < headers.Count && i < buffer.Length; i++)
		{
			var v = GetValue(row, rightColIndex, headers[i]);
			buffer[i] = v ?? string.Empty;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double GetMatchScoreFast(
		IReadOnlyList<string> leftRow,
		IReadOnlyList<string> rightRow,
		IReadOnlyList<string> headers,
		IReadOnlyDictionary<string, int> leftColIndex,
		IReadOnlyDictionary<string, int> rightColIndex
	)
	{
		if (headers.Count == 0) return 1.0;
		var matches = 0;
		for (var i = 0; i < headers.Count; i++)
		{
			var h = headers[i];
			var leftVal = GetValue(leftRow, leftColIndex, h) ?? string.Empty;
			var rightVal = GetValue(rightRow, rightColIndex, h) ?? string.Empty;
			if (string.Equals(leftVal, rightVal, StringComparison.Ordinal))
				matches++;
		}
		return (double)matches / headers.Count;
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
			_ => 1,
		};
	}

	private static IReadOnlyList<DiffRow> OrderDiffRows(List<DiffRow> diffRows)
	{
		var ordered = new DiffRow[diffRows.Count];
		var idx = 0;
		foreach (var r in diffRows.OrderBy(GetSortPosition).ThenBy(GetSortType))
			ordered[idx++] = new DiffRow(idx, r.Status, r.Cells, r.LeftRowIndex, r.RightRowIndex);
		return ordered;
	}

	private static IReadOnlyList<DiffCell> RemapCellsToReordered(IReadOnlyList<DiffCell> cells)
	{
		var list = new List<DiffCell>(cells.Count);
		foreach (var c in cells)
			list.Add(new DiffCell(c.Header, c.LeftValue, c.RightValue, c.DisplayValue, DiffCellStatus.Reordered));
		return list;
	}

	private static IReadOnlyList<string> MergeHeaders(
		IReadOnlyList<string> left,
		IReadOnlyList<string> right
	)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var result = new List<string>();
		foreach (var h in left.Concat(right))
		{
			if (string.IsNullOrEmpty(h)) continue;
			if (seen.Add(h)) result.Add(h);
		}
		return result;
	}

	private static IReadOnlyList<DiffCell> BuildCells(
		IReadOnlyList<string> headers,
		IReadOnlyList<string>? leftRow,
		IReadOnlyList<string>? rightRow,
		IReadOnlyDictionary<string, int> colIndex,
		DiffCellStatus defaultStatus
	)
	{
		var cells = new List<DiffCell>(headers.Count);
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
		var cells = new List<DiffCell>(headers.Count);
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
		foreach (var c in cells)
			if (c.Status == DiffCellStatus.Modified) return DiffCellStatus.Modified;
		return DiffCellStatus.Unchanged;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
