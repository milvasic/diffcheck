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
