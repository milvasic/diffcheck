using DiffCheck.Diff;
using DiffCheck.Html;
using DiffCheck.Models;
using DiffCheck.Readers;

namespace DiffCheck;

/// <summary>
/// Main service for comparing CSV and XLSX files and generating HTML reports.
/// </summary>
public sealed class DiffCheckService
{
	private readonly IFileReader? _leftReader;
	private readonly IFileReader? _rightReader;
	private readonly DiffEngine _diffEngine;
	private readonly HtmlReportGenerator _htmlGenerator;

	/// <summary>
	/// Creates a new DiffCheckService with default readers and options.
	/// </summary>
	public DiffCheckService(HtmlReportOptions? htmlOptions = null)
	{
		_leftReader = null;
		_rightReader = null;
		_diffEngine = new DiffEngine();
		_htmlGenerator = new HtmlReportGenerator(htmlOptions);
	}

	/// <summary>
	/// Creates a new DiffCheckService with custom readers.
	/// </summary>
	public DiffCheckService(
		IFileReader leftReader,
		IFileReader rightReader,
		HtmlReportOptions? htmlOptions = null
	)
	{
		_leftReader = leftReader ?? throw new ArgumentNullException(nameof(leftReader));
		_rightReader = rightReader ?? throw new ArgumentNullException(nameof(rightReader));
		_diffEngine = new DiffEngine();
		_htmlGenerator = new HtmlReportGenerator(htmlOptions);
	}

	/// <summary>
	/// Compares two files and returns the diff result.
	/// File format is auto-detected from extension (.csv, .xlsx, .xlsm).
	/// </summary>
	/// <param name="leftFilePath">Path to the first file.</param>
	/// <param name="rightFilePath">Path to the second file.</param>
	/// <param name="columnMappings">Optional column pairs (left header, right header) to treat as the same column (e.g. renames).</param>
	/// <param name="keyColumns">Optional column names to match rows by (faster than content-based matching).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The diff result.</returns>
	/// <exception cref="ArgumentException">Thrown when file format is not supported.</exception>
	public async Task<DiffResult> CompareAsync(
		string leftFilePath,
		string rightFilePath,
		IReadOnlyList<ColumnMapping>? columnMappings = null,
		IReadOnlyList<string>? keyColumns = null,
		CancellationToken cancellationToken = default
	)
	{
		var leftReader =
			_leftReader
			?? FileReaderFactory.GetReader(leftFilePath)
			?? throw new ArgumentException(
				$"Unsupported file format: {Path.GetExtension(leftFilePath)}. Supported: {string.Join(", ", FileReaderFactory.SupportedExtensions)}",
				nameof(leftFilePath)
			);

		var rightReader =
			_rightReader
			?? FileReaderFactory.GetReader(rightFilePath)
			?? throw new ArgumentException(
				$"Unsupported file format: {Path.GetExtension(rightFilePath)}. Supported: {string.Join(", ", FileReaderFactory.SupportedExtensions)}",
				nameof(rightFilePath)
			);

		var left = await leftReader.ReadAsync(leftFilePath, cancellationToken);
		var right = await rightReader.ReadAsync(rightFilePath, cancellationToken);

		return _diffEngine.Compare(left, right, columnMappings, keyColumns);
	}

	/// <summary>
	/// Compares two data tables and returns the diff result.
	/// </summary>
	/// <param name="left">The first (original) table.</param>
	/// <param name="right">The second (modified) table.</param>
	/// <param name="columnMappings">Optional column pairs (left header, right header) to treat as the same column (e.g. renames).</param>
	/// <param name="keyColumns">Optional column names to match rows by (faster than content-based matching).</param>
	public DiffResult Compare(
		DataTable left,
		DataTable right,
		IReadOnlyList<ColumnMapping>? columnMappings = null,
		IReadOnlyList<string>? keyColumns = null
	)
	{
		return _diffEngine.Compare(left, right, columnMappings, keyColumns);
	}

	/// <summary>
	/// Compares two files and generates an HTML report.
	/// </summary>
	/// <param name="leftFilePath">Path to the first file.</param>
	/// <param name="rightFilePath">Path to the second file.</param>
	/// <param name="outputPath">Path for the output HTML file.</param>
	/// <param name="columnMappings">Optional column pairs (left header, right header) to treat as the same column (e.g. renames).</param>
	/// <param name="keyColumns">Optional column names to match rows by (faster than content-based matching).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The diff result (for further use if needed).</returns>
	public async Task<DiffResult> CompareAndSaveHtmlAsync(
		string leftFilePath,
		string rightFilePath,
		string outputPath,
		IReadOnlyList<ColumnMapping>? columnMappings = null,
		IReadOnlyList<string>? keyColumns = null,
		CancellationToken cancellationToken = default
	)
	{
		var result = await CompareAsync(leftFilePath, rightFilePath, columnMappings, keyColumns, cancellationToken);
		var leftSize = new FileInfo(leftFilePath).Length;
		var rightSize = new FileInfo(rightFilePath).Length;
		await _htmlGenerator.WriteToFileAsync(
			result,
			outputPath,
			leftFilePath,
			rightFilePath,
			leftSize,
			rightSize,
			theme: null,
			cancellationToken
		);
		return result;
	}

	/// <summary>
	/// Generates an HTML report from a diff result.
	/// </summary>
	/// <param name="result">The diff result.</param>
	/// <param name="leftFilePath">Optional path of the first file (for display).</param>
	/// <param name="rightFilePath">Optional path of the second file (for display).</param>
	/// <param name="leftFileSize">Optional file size in bytes of the first file.</param>
	/// <param name="rightFileSize">Optional file size in bytes of the second file.</param>
	/// <param name="theme">Theme for the report: "light" or "dark". Default: "light".</param>
	/// <param name="initialView">
	/// Initial active view in the report: "table" or "text". Default: "table".
	/// </param>
	/// <returns>HTML string.</returns>
	public string GenerateHtml(
		DiffResult result,
		string? leftFilePath = null,
		string? rightFilePath = null,
		long? leftFileSize = null,
		long? rightFileSize = null,
		string? theme = null,
		string? initialView = null
	)
	{
		return _htmlGenerator.Generate(
			result,
			leftFilePath,
			rightFilePath,
			leftFileSize,
			rightFileSize,
			theme,
			initialView
		);
	}

	/// <summary>
	/// Writes an HTML report to a file.
	/// </summary>
	public Task WriteHtmlToFileAsync(
		DiffResult result,
		string outputPath,
		string? leftFilePath = null,
		string? rightFilePath = null,
		long? leftFileSize = null,
		long? rightFileSize = null,
		string? theme = null,
		CancellationToken cancellationToken = default
	)
	{
		return _htmlGenerator.WriteToFileAsync(
			result,
			outputPath,
			leftFilePath,
			rightFilePath,
			leftFileSize,
			rightFileSize,
			theme,
			cancellationToken
		);
	}
}
