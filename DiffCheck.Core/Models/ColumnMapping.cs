namespace DiffCheck.Models;

/// <summary>
/// Maps a column in the left (first) file to a column in the right (second) file.
/// Both columns are treated as one for diffing (e.g. when a column was renamed).
/// </summary>
/// <param name="LeftHeader">Column header name in the left file.</param>
/// <param name="RightHeader">Column header name in the right file.</param>
public sealed record ColumnMapping(string LeftHeader, string RightHeader);
