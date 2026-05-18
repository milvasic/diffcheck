using DiffCheck.Diff;
using DiffCheck.Html;
using DiffCheck.Json;
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
		return (
			await CompareWithWarningAssessmentAsync(
				leftFilePath,
				rightFilePath,
				columnMappings,
				keyColumns,
				options,
				progressCallback,
				null,
				cancellationToken
			)
		).DiffResult;
	}

	/// <summary>
	/// Compares two files and returns both diff result and a long-running warning assessment.
	/// </summary>
	public async Task<CompareExecutionResult> CompareWithWarningAssessmentAsync(
		string leftFilePath,
		string rightFilePath,
		IReadOnlyList<ColumnMapping>? columnMappings = null,
		IReadOnlyList<string>? keyColumns = null,
		ComparisonOptions? options = null,
		Action<DiffOperationProgress>? progressCallback = null,
		LongRunningDiffWarningOptions? warningOptions = null,
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

		ValidateProvidedKeysAndMappings(left, right, keyColumns, columnMappings);

		var warningAssessment =
			warningOptions == null
				? LongRunningDiffWarningAssessment.None
				: LongRunningDiffWarningEvaluator.Evaluate(left, right, keyColumns, warningOptions);
		var warningMessage = BuildLongRunningWarningMessage(warningAssessment);
		if (!string.IsNullOrWhiteSpace(warningMessage))
			progressCallback?.Invoke(
				new DiffOperationProgress(
					DiffOperationStage.Comparing,
					0,
					"Comparing rows and cells: 0%",
					warningMessage
				)
			);

		var diffResult = _diffEngine.Compare(
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

		return new CompareExecutionResult(diffResult, warningAssessment);
	}

	private static string? BuildLongRunningWarningMessage(
		LongRunningDiffWarningAssessment warningAssessment
	)
	{
		if (!warningAssessment.ShouldWarn)
			return null;

		return "Large dataset without key columns detected. This comparison may take longer. "
			+ "Add one or more key columns to improve row matching performance.";
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
		ValidateProvidedKeysAndMappings(left, right, keyColumns, columnMappings);
		return _diffEngine.Compare(left, right, columnMappings, keyColumns, options);
	}

	private static void ValidateProvidedKeysAndMappings(
		DataTable left,
		DataTable right,
		IReadOnlyList<string>? keyColumns,
		IReadOnlyList<ColumnMapping>? columnMappings
	)
	{
		var keyError = ValidateProvidedKeyColumns(left, right, columnMappings, keyColumns);
		var mappingError = ValidateProvidedColumnMappings(left, right, columnMappings);

		if (mappingError == null && keyError == null)
			return;

		if (mappingError != null && keyError != null)
			throw new ArgumentException(
				"Input validation failed with multiple issues:"
					+ Environment.NewLine
					+ Environment.NewLine
					+ mappingError
					+ Environment.NewLine
					+ Environment.NewLine
					+ keyError
			);

		throw new ArgumentException(mappingError ?? keyError);
	}

	private static string? ValidateProvidedColumnMappings(
		DataTable left,
		DataTable right,
		IReadOnlyList<ColumnMapping>? columnMappings
	)
	{
		if (columnMappings == null || columnMappings.Count == 0)
			return null;

		var leftHeaders = left
			.Headers.Where(header => !string.IsNullOrWhiteSpace(header))
			.Select(header => header.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		var rightHeaders = right
			.Headers.Where(header => !string.IsNullOrWhiteSpace(header))
			.Select(header => header.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var leftHeaderSet = new HashSet<string>(leftHeaders, StringComparer.OrdinalIgnoreCase);
		var rightHeaderSet = new HashSet<string>(rightHeaders, StringComparer.OrdinalIgnoreCase);

		var invalidMappings = new List<string>();
		var seenLeft = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var seenRight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var duplicateLeft = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var duplicateRight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var mapping in columnMappings)
		{
			var leftHeader = mapping.LeftHeader.Trim();
			var rightHeader = mapping.RightHeader.Trim();

			if (leftHeader.Length == 0 || rightHeader.Length == 0)
			{
				invalidMappings.Add(
					$"- {FormatMapping(leftHeader, rightHeader)} (both mapped column names are required)"
				);
				continue;
			}

			var reasons = new List<string>();
			if (!leftHeaderSet.Contains(leftHeader))
				reasons.Add("left column not found");
			if (!rightHeaderSet.Contains(rightHeader))
				reasons.Add("right column not found");

			if (reasons.Count > 0)
			{
				invalidMappings.Add(
					$"- {FormatMapping(leftHeader, rightHeader)} ({string.Join(", ", reasons)})"
				);
				continue;
			}

			if (!seenLeft.Add(leftHeader))
				duplicateLeft.Add(leftHeader);
			if (!seenRight.Add(rightHeader))
				duplicateRight.Add(rightHeader);
		}

		if (invalidMappings.Count == 0 && duplicateLeft.Count == 0 && duplicateRight.Count == 0)
			return null;

		var duplicateLines = new List<string>();
		if (duplicateLeft.Count > 0)
			duplicateLines.Add(
				"- Duplicate left columns in mappings: "
					+ string.Join(
						", ",
						duplicateLeft.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
					)
			);
		if (duplicateRight.Count > 0)
			duplicateLines.Add(
				"- Duplicate right columns in mappings: "
					+ string.Join(
						", ",
						duplicateRight.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
					)
			);

		var validationDetails = invalidMappings.Concat(duplicateLines).ToList();

		return "One or more provided column mappings are invalid:"
			+ Environment.NewLine
			+ string.Join(Environment.NewLine, validationDetails)
			+ Environment.NewLine
			+ Environment.NewLine
			+ "Each mapping must reference existing columns and each side can be mapped only once."
			+ Environment.NewLine
			+ Environment.NewLine
			+ "Detected left columns:"
			+ Environment.NewLine
			+ (leftHeaders.Count == 0 ? "(none)" : string.Join(", ", leftHeaders))
			+ Environment.NewLine
			+ Environment.NewLine
			+ "Detected right columns:"
			+ Environment.NewLine
			+ (rightHeaders.Count == 0 ? "(none)" : string.Join(", ", rightHeaders));
	}

	private static string FormatMapping(string leftHeader, string rightHeader)
	{
		var leftValue = leftHeader.Length == 0 ? "<empty>" : leftHeader;
		var rightValue = rightHeader.Length == 0 ? "<empty>" : rightHeader;
		return $"{leftValue}:{rightValue}";
	}

	private static string? ValidateProvidedKeyColumns(
		DataTable left,
		DataTable right,
		IReadOnlyList<ColumnMapping>? columnMappings,
		IReadOnlyList<string>? keyColumns
	)
	{
		if (keyColumns == null || keyColumns.Count == 0)
			return null;

		var leftHeaderSet = new HashSet<string>(
			left.Headers.Where(header => !string.IsNullOrWhiteSpace(header)),
			StringComparer.OrdinalIgnoreCase
		);

		var rightToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (columnMappings != null)
			foreach (var mapping in columnMappings)
			{
				if (
					string.IsNullOrWhiteSpace(mapping.LeftHeader)
					|| string.IsNullOrWhiteSpace(mapping.RightHeader)
				)
					continue;

				rightToCanonical[mapping.RightHeader.Trim()] = mapping.LeftHeader.Trim();
			}

		var rightCanonicalHeaderSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var rightHeader in right.Headers)
		{
			if (string.IsNullOrWhiteSpace(rightHeader))
				continue;

			var normalizedRightHeader = rightHeader.Trim();
			var canonical = rightToCanonical.GetValueOrDefault(
				normalizedRightHeader,
				normalizedRightHeader
			);
			rightCanonicalHeaderSet.Add(canonical);
		}

		var unusable = new List<string>();
		foreach (var keyColumn in keyColumns)
		{
			if (string.IsNullOrWhiteSpace(keyColumn))
			{
				unusable.Add("<empty>");
				continue;
			}

			var key = keyColumn.Trim();
			if (!leftHeaderSet.Contains(key) || !rightCanonicalHeaderSet.Contains(key))
				unusable.Add(key);
		}

		if (unusable.Count == 0)
			return null;

		var unusableDistinct = unusable.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		var unusableList = string.Join(
			Environment.NewLine,
			unusableDistinct.Select(column => $"- {column}")
		);

		var detectedUsableColumns = left
			.Headers.Where(header => !string.IsNullOrWhiteSpace(header))
			.Select(header => header.Trim())
			.Where(rightCanonicalHeaderSet.Contains)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		var detectedList =
			detectedUsableColumns.Count == 0 ? "(none)" : string.Join(", ", detectedUsableColumns);

		return "One or more provided key columns are unusable:"
			+ Environment.NewLine
			+ unusableList
			+ Environment.NewLine
			+ Environment.NewLine
			+ "Each provided key column must exist in both files after applying column mappings."
			+ Environment.NewLine
			+ Environment.NewLine
			+ "Detected columns (usable in both files):"
			+ Environment.NewLine
			+ detectedList;
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
	/// Compares two files and generates a JSON report.
	/// </summary>
	/// <param name="leftFilePath">Path to the first file.</param>
	/// <param name="rightFilePath">Path to the second file.</param>
	/// <param name="outputPath">Path for the output JSON file.</param>
	/// <param name="columnMappings">Optional column pairs (left header, right header) to treat as the same column (e.g. renames).</param>
	/// <param name="keyColumns">Optional column names to match rows by (faster than content-based matching).</param>
	/// <param name="options">Optional normalization and matching options.</param>
	/// <param name="progressCallback">Optional callback invoked as operation stages complete.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The diff result.</returns>
	public async Task<DiffResult> CompareAndSaveJsonAsync(
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
			progressCallback,
			cancellationToken
		);
		await DiffResultJsonSerializer.WriteToFileAsync(result, outputPath, cancellationToken);
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
