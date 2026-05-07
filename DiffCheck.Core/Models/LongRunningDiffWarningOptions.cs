namespace DiffCheck.Models;

/// <summary>
/// Controls the heuristic used to warn about potentially long-running comparisons
/// when no key columns are defined.
/// </summary>
public sealed record LongRunningDiffWarningOptions
{
	/// <summary>
	/// Baseline threshold for estimated data amount.
	/// </summary>
	public double DataAmountThreshold { get; init; } = 400000;

	/// <summary>
	/// Multiplier applied to rows * columns before threshold comparison.
	/// </summary>
	public double ThresholdFactor { get; init; } = 1.0;

	/// <summary>Default warning options.</summary>
	public static LongRunningDiffWarningOptions Default { get; } = new();
}
