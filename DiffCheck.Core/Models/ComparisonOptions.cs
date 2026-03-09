namespace DiffCheck.Models;

/// <summary>
/// Controls how cell values are normalized and compared during diffing.
/// All properties default to the original behavior, so omitting this object is a no-op.
/// </summary>
public sealed record ComparisonOptions
{
	/// <summary>
	/// When <c>false</c>, value comparisons are case-insensitive.
	/// Default: <c>true</c> (case-sensitive).
	/// </summary>
	public bool CaseSensitive { get; init; } = true;

	/// <summary>
	/// When <c>true</c>, leading and trailing whitespace is stripped before comparing.
	/// Default: <c>false</c>.
	/// </summary>
	public bool TrimWhitespace { get; init; } = false;

	/// <summary>
	/// When set, two values that both parse as numbers are considered equal if their
	/// absolute difference is within this tolerance.
	/// Default: <c>null</c> (exact string comparison).
	/// </summary>
	public double? NumericTolerance { get; init; } = null;

	/// <summary>
	/// Fraction of columns that must match for two rows to be considered a candidate pair
	/// during content-based matching (used when no key columns are specified). Range: 0.0–1.0.
	/// Default: <c>0.5</c>.
	/// </summary>
	public double MatchThreshold { get; init; } = 0.5;

	/// <summary>Default instance — reproduces the original comparison behavior.</summary>
	public static ComparisonOptions Default { get; } = new();
}
