namespace DiffCheck.Html;

/// <summary>
/// Options for HTML report generation.
/// </summary>
public sealed class HtmlReportOptions
{
	/// <summary>
	/// Font family for the report. Default: system UI font stack.
	/// </summary>
	public string FontFamily { get; set; } =
		"system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif";

	/// <summary>
	/// Background color for added rows/cells. Default: green.
	/// </summary>
	public string AddedColor { get; set; } = "#22c55e";

	/// <summary>
	/// Background color for removed rows/cells. Default: red.
	/// </summary>
	public string RemovedColor { get; set; } = "#ef4444";

	/// <summary>
	/// Background color for modified rows/cells. Default: amber/yellow.
	/// </summary>
	public string ModifiedColor { get; set; } = "#f59e0b";

	/// <summary>
	/// Background color for reordered rows/cells. Default: blue.
	/// </summary>
	public string ReorderedColor { get; set; } = "#3b82f6";
}
