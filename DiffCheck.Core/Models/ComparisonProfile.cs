namespace DiffCheck.Models;

/// <summary>
/// A named preset of comparison settings (key columns, column mappings, and normalization options).
/// </summary>
/// <param name="Name">Profile identifier. Must contain only letters, digits, hyphens, or underscores.</param>
/// <param name="KeyColumns">Column names used to match rows by key.</param>
/// <param name="ColumnMappings">Column header pairs treated as the same column.</param>
/// <param name="Options">Optional normalization and matching options.</param>
public sealed record ComparisonProfile(
	string Name,
	IReadOnlyList<string>? KeyColumns,
	IReadOnlyList<ColumnMapping>? ColumnMappings,
	ComparisonOptions? Options = null
);
