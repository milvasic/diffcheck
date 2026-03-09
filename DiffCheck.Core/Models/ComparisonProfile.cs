namespace DiffCheck.Models;

/// <summary>
/// A named preset of comparison settings (key columns and column mappings).
/// </summary>
/// <param name="Name">Profile identifier. Must contain only letters, digits, hyphens, or underscores.</param>
/// <param name="KeyColumns">Column names used to match rows by key.</param>
/// <param name="ColumnMappings">Column header pairs treated as the same column.</param>
public sealed record ComparisonProfile(
	string Name,
	IReadOnlyList<string>? KeyColumns,
	IReadOnlyList<ColumnMapping>? ColumnMappings
);
