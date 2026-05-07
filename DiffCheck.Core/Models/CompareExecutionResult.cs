namespace DiffCheck.Models;

/// <summary>
/// Comparison result plus optional warning assessment metadata for callers.
/// </summary>
public sealed record CompareExecutionResult(
	DiffResult DiffResult,
	LongRunningDiffWarningAssessment WarningAssessment
);
