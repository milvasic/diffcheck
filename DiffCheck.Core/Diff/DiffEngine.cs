using DiffCheck.Models;

namespace DiffCheck.Diff;

/// <summary>
/// Compares two data tables and produces a diff result.
/// Uses row-by-row comparison (same row index = same logical row).
/// </summary>
public sealed class DiffEngine
{
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
		var headerSet = headers.ToHashSet();
		var leftColIndex = left
			.Headers.Select((h, i) => (h, i))
			.ToDictionary(x => x.h, x => x.i, StringComparer.OrdinalIgnoreCase);
		var rightColIndex = right
			.Headers.Select((h, i) => (h, i))
			.ToDictionary(x => x.h, x => x.i, StringComparer.OrdinalIgnoreCase);

		var diffRows = new List<DiffRow>();
		var maxRows = Math.Max(left.RowCount, right.RowCount);
		var added = 0;
		var removed = 0;
		var modified = 0;
		var unchanged = 0;

		for (var i = 0; i < maxRows; i++)
		{
			var leftRow = i < left.RowCount ? left.Rows[i] : null;
			var rightRow = i < right.RowCount ? right.Rows[i] : null;

			if (leftRow == null && rightRow != null)
			{
				var cells = BuildCells(
					headers,
					null,
					rightRow,
					rightColIndex,
					DiffCellStatus.Added
				);
				diffRows.Add(new DiffRow(i + 1, DiffRowStatus.Added, cells));
				added++;
			}
			else if (leftRow != null && rightRow == null)
			{
				var cells = BuildCells(
					headers,
					leftRow,
					null,
					leftColIndex,
					DiffCellStatus.Removed
				);
				diffRows.Add(new DiffRow(i + 1, DiffRowStatus.Removed, cells));
				removed++;
			}
			else
			{
				var cells = CompareCells(headers, leftRow!, rightRow!, leftColIndex, rightColIndex);
				var status = GetRowStatus(cells);
				diffRows.Add(new DiffRow(i + 1, status, cells));

				switch (status)
				{
					case DiffRowStatus.Modified:
						modified++;
						break;
					case DiffRowStatus.Unchanged:
						unchanged++;
						break;
				}
			}
		}

		var summary = new DiffSummary(added, removed, modified, unchanged);
		return new DiffResult(headers, diffRows, summary);
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
			if (string.IsNullOrEmpty(h))
				continue;
			if (seen.Add(h))
				result.Add(h);
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

	private static DiffRowStatus GetRowStatus(IReadOnlyList<DiffCell> cells)
	{
		var hasModified = cells.Any(c => c.Status == DiffCellStatus.Modified);
		return hasModified ? DiffRowStatus.Modified : DiffRowStatus.Unchanged;
	}
}
