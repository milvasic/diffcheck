using DiffCheck.Models;

namespace DiffCheck;

/// <summary>
/// Evaluates whether a comparison is likely to be long-running when no keys are provided.
/// </summary>
public static class LongRunningDiffWarningEvaluator
{
	public static LongRunningDiffWarningAssessment Evaluate(
		DataTable left,
		DataTable right,
		IReadOnlyList<string>? keyColumns,
		LongRunningDiffWarningOptions? options = null
	)
	{
		ArgumentNullException.ThrowIfNull(left);
		ArgumentNullException.ThrowIfNull(right);

		var hasDefinedKeyColumns = keyColumns is { Count: > 0 };
		var totalRows = left.RowCount + right.RowCount;
		var maxColumns = Math.Max(left.ColumnCount, right.ColumnCount);
		var configured = options ?? LongRunningDiffWarningOptions.Default;
		var threshold = configured.DataAmountThreshold;
		var factor = configured.ThresholdFactor;
		if (threshold < 0)
			threshold = 0;
		if (factor < 0)
			factor = 0;

		var estimatedDataAmount = totalRows * maxColumns * factor;
		var shouldWarn = !hasDefinedKeyColumns && estimatedDataAmount > threshold;

		return new LongRunningDiffWarningAssessment(
			ShouldWarn: shouldWarn,
			EstimatedDataAmount: estimatedDataAmount,
			EffectiveThreshold: threshold,
			TotalRows: totalRows,
			MaxColumns: maxColumns,
			HasDefinedKeyColumns: hasDefinedKeyColumns
		);
	}
}
