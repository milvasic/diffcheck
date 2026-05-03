using DiffCheck.Html;
using DiffCheck.Models;

namespace DiffCheck.Core.Tests.Html;

[TestClass]
public class HtmlReportGeneratorTests
{
	[TestMethod]
	public void Generate_ValidResult_ReturnsHtmlWithDoctype()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result);

		Assert.Contains("<!DOCTYPE html>", html);
		Assert.Contains("<html", html);
		Assert.Contains("Diff Report", html);
		Assert.Contains("</body>", html);
	}

	[TestMethod]
	public void Generate_WithFilePaths_IncludesFileInfo()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result, "left.csv", "right.csv");

		Assert.Contains("left.csv", html);
		Assert.Contains("right.csv", html);
	}

	[TestMethod]
	public void Generate_WithFileSizes_IncludesFileStats()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(
			result,
			"left.csv",
			"right.csv",
			leftFileSize: 1024,
			rightFileSize: 2048
		);

		Assert.Contains("1 KB", html);
		Assert.Contains("2 KB", html);
		Assert.Contains("rows", html);
		Assert.Contains("columns", html);
		Assert.Contains("cells", html);
	}

	[TestMethod]
	public void Generate_DarkTheme_SetsDataTheme()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result, theme: "dark");

		Assert.Contains("data-theme=\"dark\"", html);
	}

	[TestMethod]
	public void Generate_ContainsToolsCurtainAndOptions()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result);

		Assert.Contains("tools-curtain", html);
		Assert.Contains("tools-toggle", html);
		Assert.Contains("Hide unchanged columns", html);
		Assert.Contains("hide-unchanged-cols", html);
		Assert.Contains("Highlight changed rows", html);
		Assert.Contains("highlight-rows", html);
		Assert.Contains("Highlight changed cells", html);
		Assert.Contains("highlight-cells", html);
		Assert.Contains("Whole value cell diff", html);
		Assert.Contains("whole-value-diff", html);
	}

	[TestMethod]
	public void Generate_ContainsViewSwitcherAndTextViewModel()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result);

		Assert.Contains("view-btn", html);
		Assert.Contains("data-view=\"table\"", html);
		Assert.Contains("data-view=\"text\"", html);
		Assert.Contains("id=\"view-table\"", html);
		Assert.Contains("id=\"view-text\"", html);
		Assert.Contains("text-diff", html);
	}

	[TestMethod]
	public void Generate_TableHasColumnMetadataAndRowClasses()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result);

		Assert.Contains("diff-grid", html);
		Assert.Contains("window.diffData", html);
		Assert.Contains("\"c\":", html);
		Assert.Contains("row-unchanged", html);
	}

	[TestMethod]
	public void Generate_TextViewContainsUnifiedDiffLines()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result);

		Assert.Contains("text-line-unchanged", html);
		Assert.Contains("text-line-added", html);
		Assert.Contains("text-line-removed", html);
		Assert.Contains("text-line-modified", html);
	}

	[TestMethod]
	public void Generate_ContainsStableDomIds()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result);

		Assert.Contains("id=\"tools-curtain\"", html);
		Assert.Contains("id=\"tools-panel\"", html);
		Assert.Contains("id=\"diff-grid\"", html);
		Assert.Contains("id=\"view-table\"", html);
		Assert.Contains("id=\"view-text\"", html);
		Assert.Contains("id=\"text-diff-content\"", html);
		Assert.Contains("id=\"autosize-columns-btn\"", html);
		Assert.Contains("id=\"hide-unchanged-cols\"", html);
		Assert.Contains("id=\"highlight-rows\"", html);
		Assert.Contains("id=\"highlight-cells\"", html);
		Assert.Contains("id=\"whole-value-diff\"", html);
	}

	private static DiffResult CreateSimpleDiffResult()
	{
		var summary = new DiffSummary(0, 0, 0, 1);
		return new DiffResult(
			["A", "B"],
			[
				new DiffRow(
					1,
					DiffRowStatus.Unchanged,
					[
						new DiffCell("A", "1", "1", "1", DiffCellStatus.Unchanged),
						new DiffCell("B", "2", "2", "2", DiffCellStatus.Unchanged),
					],
					1,
					1
				),
			],
			summary,
			1,
			2,
			1,
			2
		);
	}
}
