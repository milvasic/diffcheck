namespace DiffCheck.Models;

/// <summary>
/// Result of evaluating whether a comparison is likely to be long-running.
/// </summary>
public sealed record LongRunningDiffWarningAssessment(
	bool ShouldWarn,
	double EstimatedDataAmount,
	double EffectiveThreshold,
	int TotalRows,
	int MaxColumns,
	bool HasDefinedKeyColumns
)
{
	/// <summary>An assessment indicating no warning was triggered.</summary>
	public static LongRunningDiffWarningAssessment None { get; } =
		new(
			ShouldWarn: false,
			EstimatedDataAmount: 0,
			EffectiveThreshold: 0,
			TotalRows: 0,
			MaxColumns: 0,
			HasDefinedKeyColumns: false
		);
}
