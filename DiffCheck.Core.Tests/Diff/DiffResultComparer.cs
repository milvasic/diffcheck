using DiffCheck.Models;

namespace DiffCheck.Core.Tests.Diff;

/// <summary>
/// Compares two DiffResults for equality (used to verify optimized engine produces same output).
/// </summary>
public static class DiffResultComparer
{
	public static bool AreEqual(DiffResult? a, DiffResult? b, out string? reason)
	{
		reason = null;
		if (a == null && b == null) return true;
		if (a == null) { reason = "Left result is null"; return false; }
		if (b == null) { reason = "Right result is null"; return false; }

		if (a.Headers.Count != b.Headers.Count)
		{
			reason = $"Header count: {a.Headers.Count} vs {b.Headers.Count}";
			return false;
		}
		for (var i = 0; i < a.Headers.Count; i++)
		{
			if (!string.Equals(a.Headers[i], b.Headers[i], StringComparison.Ordinal))
			{
				reason = $"Header[{i}]: '{a.Headers[i]}' vs '{b.Headers[i]}'";
				return false;
			}
		}

		if (!SummariesEqual(a.Summary, b.Summary, out reason))
			return false;

		if (a.Rows.Count != b.Rows.Count)
		{
			reason = $"Row count: {a.Rows.Count} vs {b.Rows.Count}";
			return false;
		}

		for (var i = 0; i < a.Rows.Count; i++)
		{
			var ra = a.Rows[i];
			var rb = b.Rows[i];
			if (ra.Status != rb.Status)
			{
				reason = $"Row[{i}] Status: {ra.Status} vs {rb.Status}";
				return false;
			}
			if (ra.LeftRowIndex != rb.LeftRowIndex)
			{
				reason = $"Row[{i}] LeftRowIndex: {ra.LeftRowIndex} vs {rb.LeftRowIndex}";
				return false;
			}
			if (ra.RightRowIndex != rb.RightRowIndex)
			{
				reason = $"Row[{i}] RightRowIndex: {ra.RightRowIndex} vs {rb.RightRowIndex}";
				return false;
			}
			if (ra.Cells.Count != rb.Cells.Count)
			{
				reason = $"Row[{i}] Cells count: {ra.Cells.Count} vs {rb.Cells.Count}";
				return false;
			}
			for (var c = 0; c < ra.Cells.Count; c++)
			{
				var ca = ra.Cells[c];
				var cb = rb.Cells[c];
				if (ca.Status != cb.Status
					|| string.Equals(ca.LeftValue, cb.LeftValue, StringComparison.Ordinal) == false
					|| string.Equals(ca.RightValue, cb.RightValue, StringComparison.Ordinal) == false)
				{
					reason = $"Row[{i}] Cell[{c}] differs: Status {ca.Status} vs {cb.Status}, Left '{ca.LeftValue}' vs '{cb.LeftValue}', Right '{ca.RightValue}' vs '{cb.RightValue}'";
					return false;
				}
			}
		}

		return true;
	}

	public static bool SummariesEqual(DiffSummary a, DiffSummary b, out string? reason)
	{
		reason = null;
		if (a.AddedRows != b.AddedRows) { reason = $"AddedRows: {a.AddedRows} vs {b.AddedRows}"; return false; }
		if (a.RemovedRows != b.RemovedRows) { reason = $"RemovedRows: {a.RemovedRows} vs {b.RemovedRows}"; return false; }
		if (a.ModifiedRows != b.ModifiedRows) { reason = $"ModifiedRows: {a.ModifiedRows} vs {b.ModifiedRows}"; return false; }
		if (a.UnchangedRows != b.UnchangedRows) { reason = $"UnchangedRows: {a.UnchangedRows} vs {b.UnchangedRows}"; return false; }
		if (a.ReorderedRows != b.ReorderedRows) { reason = $"ReorderedRows: {a.ReorderedRows} vs {b.ReorderedRows}"; return false; }
		return true;
	}
}
