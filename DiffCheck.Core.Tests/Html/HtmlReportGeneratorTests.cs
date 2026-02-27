using DiffCheck.Html;
using DiffCheck.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

		Assert.IsTrue(html.Contains("<!DOCTYPE html>"));
		Assert.IsTrue(html.Contains("<html"));
		Assert.IsTrue(html.Contains("Diff Report"));
		Assert.IsTrue(html.Contains("</body>"));
	}

	[TestMethod]
	public void Generate_WithFilePaths_IncludesFileInfo()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result, "left.csv", "right.csv");

		Assert.IsTrue(html.Contains("left.csv"));
		Assert.IsTrue(html.Contains("right.csv"));
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

		Assert.IsTrue(html.Contains("1 KB"));
		Assert.IsTrue(html.Contains("2 KB"));
		Assert.IsTrue(html.Contains("rows"));
		Assert.IsTrue(html.Contains("columns"));
		Assert.IsTrue(html.Contains("cells"));
	}

	[TestMethod]
	public void Generate_DarkTheme_SetsDataTheme()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result, theme: "dark");

		Assert.IsTrue(html.Contains("data-theme=\"dark\""));
	}

	[TestMethod]
	public void Generate_ContainsToolsCurtainAndOptions()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result);

		Assert.IsTrue(html.Contains("tools-curtain"));
		Assert.IsTrue(html.Contains("tools-toggle"));
		Assert.IsTrue(html.Contains("Hide unchanged rows"));
		Assert.IsTrue(html.Contains("Hide unchanged columns"));
		Assert.IsTrue(html.Contains("hide-unchanged-rows"));
		Assert.IsTrue(html.Contains("hide-unchanged-cols"));
	}

	[TestMethod]
	public void Generate_ContainsViewSwitcherAndTextViewModel()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result);

		Assert.IsTrue(html.Contains("view-btn"));
		Assert.IsTrue(html.Contains("data-view=\"table\""));
		Assert.IsTrue(html.Contains("data-view=\"text\""));
		Assert.IsTrue(html.Contains("id=\"view-table\""));
		Assert.IsTrue(html.Contains("id=\"view-text\""));
		Assert.IsTrue(html.Contains("text-diff"));
	}

	[TestMethod]
	public void Generate_TableHasColumnMetadataAndRowClasses()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result);

		Assert.IsTrue(html.Contains("diff-grid"));
		Assert.IsTrue(html.Contains("diffGridRowData"));
		Assert.IsTrue(html.Contains("diffGridColumnHasChanges"));
		Assert.IsTrue(html.Contains("row-unchanged"));
	}

	[TestMethod]
	public void Generate_TextViewContainsUnifiedDiffLines()
	{
		var result = CreateSimpleDiffResult();
		var generator = new HtmlReportGenerator();

		var html = generator.Generate(result);

		Assert.IsTrue(html.Contains("text-line-unchanged") || html.Contains("text-line-added") || html.Contains("text-line-removed") || html.Contains("text-line-modified"));
	}

	private static DiffResult CreateSimpleDiffResult()
	{
		var headers = new List<string> { "A", "B" };
		var cells = new List<DiffCell>
		{
			new("A", "1", "1", "1", DiffCellStatus.Unchanged),
			new("B", "2", "2", "2", DiffCellStatus.Unchanged),
		};
		var row = new DiffRow(1, DiffRowStatus.Unchanged, cells, 1, 1);
		var summary = new DiffSummary(0, 0, 0, 1, 0);
		return new DiffResult(headers, new List<DiffRow> { row }, summary, 1, 2, 1, 2);
	}
}
