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
	/// <param name="options">Optional normalization and matching options. Defaults preserve the original behavior.</param>
	/// <param name="progressCallback">Optional callback invoked as operation stages complete.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The diff result.</returns>
	/// <exception cref="ArgumentException">Thrown when file format is not supported.</exception>
	public async Task<DiffResult> CompareAsync(
		string leftFilePath,
		string rightFilePath,
		IReadOnlyList<ColumnMapping>? columnMappings = null,
		IReadOnlyList<string>? keyColumns = null,
		ComparisonOptions? options = null,
		Action<DiffOperationProgress>? progressCallback = null,
		CancellationToken cancellationToken = default
	)
	{
		progressCallback?.Invoke(
			new DiffOperationProgress(DiffOperationStage.Starting, 0, "Preparing comparison")
		);

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

		progressCallback?.Invoke(
			new DiffOperationProgress(
				DiffOperationStage.ReadingLeftFile,
				0,
				"Reading left file: 0%"
			)
		);
		var left = await leftReader.ReadAsync(
			leftFilePath,
			percent =>
				progressCallback?.Invoke(
					new DiffOperationProgress(
						DiffOperationStage.ReadingLeftFile,
						percent,
						$"Reading left file: {percent}%"
					)
				),
			cancellationToken
		);

		progressCallback?.Invoke(
			new DiffOperationProgress(
				DiffOperationStage.ReadingRightFile,
				0,
				"Reading right file: 0%"
			)
		);
		var right = await rightReader.ReadAsync(
			rightFilePath,
			percent =>
				progressCallback?.Invoke(
					new DiffOperationProgress(
						DiffOperationStage.ReadingRightFile,
						percent,
						$"Reading right file: {percent}%"
					)
				),
			cancellationToken
		);

		progressCallback?.Invoke(
			new DiffOperationProgress(
				DiffOperationStage.Comparing,
				0,
				"Comparing rows and cells: 0%"
			)
		);
		var result = _diffEngine.Compare(
			left,
			right,
			columnMappings,
			keyColumns,
			options,
			percent =>
				progressCallback?.Invoke(
					new DiffOperationProgress(
						DiffOperationStage.Comparing,
						percent,
						$"Comparing rows and cells: {percent}%"
					)
				)
		);

		progressCallback?.Invoke(
			new DiffOperationProgress(DiffOperationStage.Completed, 100, "Comparison complete")
		);

		return result;
	}

	/// <summary>
	/// Compares two data tables and returns the diff result.
	/// </summary>
	/// <param name="left">The first (original) table.</param>
	/// <param name="right">The second (modified) table.</param>
	/// <param name="columnMappings">Optional column pairs (left header, right header) to treat as the same column (e.g. renames).</param>
	/// <param name="keyColumns">Optional column names to match rows by (faster than content-based matching).</param>
	/// <param name="options">Optional normalization and matching options. Defaults preserve the original behavior.</param>
	public DiffResult Compare(
		DataTable left,
		DataTable right,
		IReadOnlyList<ColumnMapping>? columnMappings = null,
		IReadOnlyList<string>? keyColumns = null,
		ComparisonOptions? options = null
	)
	{
		return _diffEngine.Compare(left, right, columnMappings, keyColumns, options);
	}

	/// <summary>
	/// Compares two files and generates an HTML report.
	/// </summary>
	/// <param name="leftFilePath">Path to the first file.</param>
	/// <param name="rightFilePath">Path to the second file.</param>
	/// <param name="outputPath">Path for the output HTML file.</param>
	/// <param name="columnMappings">Optional column pairs (left header, right header) to treat as the same column (e.g. renames).</param>
	/// <param name="keyColumns">Optional column names to match rows by (faster than content-based matching).</param>
	/// <param name="options">Optional normalization and matching options. Defaults preserve the original behavior.</param>
	/// <param name="progressCallback">Optional callback invoked as operation stages complete.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The diff result (for further use if needed).</returns>
	public async Task<DiffResult> CompareAndSaveHtmlAsync(
		string leftFilePath,
		string rightFilePath,
		string outputPath,
		IReadOnlyList<ColumnMapping>? columnMappings = null,
		IReadOnlyList<string>? keyColumns = null,
		ComparisonOptions? options = null,
		Action<DiffOperationProgress>? progressCallback = null,
		CancellationToken cancellationToken = default
	)
	{
		var result = await CompareAsync(
			leftFilePath,
			rightFilePath,
			columnMappings,
			keyColumns,
			options,
			ReportFromCompare,
			cancellationToken
		);
		progressCallback?.Invoke(
			new DiffOperationProgress(
				DiffOperationStage.GeneratingReport,
				0,
				"Generating HTML report: 0%"
			)
		);
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
			progressCallback: percent =>
				progressCallback?.Invoke(
					new DiffOperationProgress(
						DiffOperationStage.GeneratingReport,
						percent,
						$"Generating HTML report: {percent}%"
					)
				),
			cancellationToken
		);
		progressCallback?.Invoke(
			new DiffOperationProgress(DiffOperationStage.Completed, 100, "Report saved")
		);
		return result;

		void ReportFromCompare(DiffOperationProgress progress)
		{
			if (progress.Stage != DiffOperationStage.Completed)
				progressCallback?.Invoke(progress);
		}
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
	/// <param name="progressCallback">Optional callback for report-generation progress in range 0..100.</param>
	/// <param name="requestStartedAtUtc">Optional request start timestamp used to compute end-to-end request duration.</param>
	/// <returns>HTML string.</returns>
	public string GenerateHtml(
		DiffResult result,
		string? leftFilePath = null,
		string? rightFilePath = null,
		long? leftFileSize = null,
		long? rightFileSize = null,
		string? theme = null,
		string? initialView = null,
		Action<DiffOperationProgress>? progressCallback = null,
		DateTimeOffset? requestStartedAtUtc = null
	)
	{
		progressCallback?.Invoke(
			new DiffOperationProgress(
				DiffOperationStage.GeneratingReport,
				0,
				"Generating HTML report: 0%"
			)
		);

		return _htmlGenerator.Generate(
			result,
			leftFilePath,
			rightFilePath,
			leftFileSize,
			rightFileSize,
			theme,
			initialView,
			percent =>
				progressCallback?.Invoke(
					new DiffOperationProgress(
						DiffOperationStage.GeneratingReport,
						percent,
						$"Generating HTML report: {percent}%"
					)
				),
			requestStartedAtUtc
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
			progressCallback: null,
			cancellationToken
		);
	}
}
