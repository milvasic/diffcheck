using DiffCheck.Models;
using DiffCheck.Profiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiffCheck.Web.Pages;

public class IndexModel(
	DiffCheckService diffCheckService,
	UploadLimits uploadLimits,
	ProfileStore profileStore
) : PageModel
{
	public long MaxFileSizeMb => uploadLimits.MaxFileSizeBytes / (1024 * 1024);

	public string? DiffReportHtml { get; set; }
	public string? LeftFileName { get; set; }
	public string? RightFileName { get; set; }
	public string? ErrorMessage { get; set; }

	/// <summary>Raw column mappings text (left:right per line) for repopulating the form.</summary>
	public string? ColumnMappingsRaw { get; set; }

	/// <summary>Raw key columns text (comma or newline separated) for repopulating the form.</summary>
	public string? KeyColumnsRaw { get; set; }

	/// <summary>Whether case-insensitive comparison was selected.</summary>
	public bool CaseInsensitive { get; set; }

	/// <summary>Whether trim-whitespace was selected.</summary>
	public bool TrimWhitespace { get; set; }

	/// <summary>Numeric tolerance value as raw string for repopulating the form.</summary>
	public string? NumericToleranceRaw { get; set; }

	/// <summary>Match threshold value as raw string for repopulating the form.</summary>
	public string? MatchThresholdRaw { get; set; }

	public void OnGet() { }

	public async Task<IActionResult> OnPostCompareAsync(
		IFormFile? leftFile,
		IFormFile? rightFile,
		string? columnMappingsRaw,
		string? keyColumnsRaw,
		bool caseInsensitive = false,
		bool trimWhitespace = false,
		string? numericToleranceRaw = null,
		string? matchThresholdRaw = null
	)
	{
		if (leftFile == null || rightFile == null)
			return new JsonResult(new { error = "Please provide both files." });

		if (leftFile.Length == 0 || rightFile.Length == 0)
			return new JsonResult(new { error = "One or both files are empty." });

		var maxBytes = uploadLimits.MaxFileSizeBytes;
		if (leftFile.Length > maxBytes || rightFile.Length > maxBytes)
			return new JsonResult(new { error = $"Each file must be under {MaxFileSizeMb} MB." });

		var leftExt = Path.GetExtension(leftFile.FileName).ToLowerInvariant();
		var rightExt = Path.GetExtension(rightFile.FileName).ToLowerInvariant();
		var supported = new[] { ".csv", ".txt", ".xlsx", ".xlsm" };
		if (!supported.Contains(leftExt) || !supported.Contains(rightExt))
			return new JsonResult(
				new
				{
					error = $"Unsupported file format. Supported: {string.Join(", ", supported)}",
				}
			);

		string? leftPath = null;
		string? rightPath = null;

		try
		{
			leftPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + leftExt);
			rightPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + rightExt);

			await using (var leftStream = System.IO.File.Create(leftPath))
			await using (var rightStream = System.IO.File.Create(rightPath))
			{
				await leftFile.CopyToAsync(leftStream);
				await rightFile.CopyToAsync(rightStream);
			}

			var theme = Request.Headers["X-Theme"].FirstOrDefault() ?? "light";
			var viewPref = Request.Headers["X-View"].FirstOrDefault() ?? "table";

			IReadOnlyList<ColumnMapping>? columnMappings = ParseColumnMappings(columnMappingsRaw);
			IReadOnlyList<string>? keyColumns = ParseKeyColumns(keyColumnsRaw);
			var comparisonOptions = BuildComparisonOptions(
				caseInsensitive,
				trimWhitespace,
				numericToleranceRaw,
				matchThresholdRaw
			);
			var result = await diffCheckService.CompareAsync(
				leftPath,
				rightPath,
				columnMappings,
				keyColumns,
				comparisonOptions
			);
			var html = diffCheckService.GenerateHtml(
				result,
				leftFile.FileName,
				rightFile.FileName,
				leftFile.Length,
				rightFile.Length,
				theme,
				viewPref
			);

			return new JsonResult(
				new
				{
					html,
					leftFileName = leftFile.FileName,
					rightFileName = rightFile.FileName,
				}
			);
		}
		catch (Exception ex)
		{
			return new JsonResult(new { error = ex.Message });
		}
		finally
		{
			if (leftPath != null && System.IO.File.Exists(leftPath))
				System.IO.File.Delete(leftPath);
			if (rightPath != null && System.IO.File.Exists(rightPath))
				System.IO.File.Delete(rightPath);
		}
	}

	public async Task<IActionResult> OnPostAsync(
		IFormFile? leftFile,
		IFormFile? rightFile,
		string? columnMappingsRaw,
		string? keyColumnsRaw,
		bool caseInsensitive = false,
		bool trimWhitespace = false,
		string? numericToleranceRaw = null,
		string? matchThresholdRaw = null
	)
	{
		if (leftFile == null || rightFile == null)
		{
			ErrorMessage = "Please provide both files.";
			return Page();
		}

		if (leftFile.Length == 0 || rightFile.Length == 0)
		{
			ErrorMessage = "One or both files are empty.";
			return Page();
		}

		var maxBytes = uploadLimits.MaxFileSizeBytes;
		if (leftFile.Length > maxBytes || rightFile.Length > maxBytes)
		{
			ErrorMessage = $"Each file must be under {MaxFileSizeMb} MB.";
			return Page();
		}

		var leftExt = Path.GetExtension(leftFile.FileName).ToLowerInvariant();
		var rightExt = Path.GetExtension(rightFile.FileName).ToLowerInvariant();
		var supported = new[] { ".csv", ".txt", ".xlsx", ".xlsm" };
		if (!supported.Contains(leftExt) || !supported.Contains(rightExt))
		{
			ErrorMessage = $"Unsupported file format. Supported: {string.Join(", ", supported)}";
			return Page();
		}

		string? leftPath = null;
		string? rightPath = null;

		try
		{
			leftPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + leftExt);
			rightPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + rightExt);

			await using (var leftStream = System.IO.File.Create(leftPath))
			await using (var rightStream = System.IO.File.Create(rightPath))
			{
				await leftFile.CopyToAsync(leftStream);
				await rightFile.CopyToAsync(rightStream);
			}

			var theme = Request.Headers["X-Theme"].FirstOrDefault() ?? "light";
			var viewPref = Request.Headers["X-View"].FirstOrDefault() ?? "table";

			var columnMappings = ParseColumnMappings(columnMappingsRaw);
			var keyColumns = ParseKeyColumns(keyColumnsRaw);
			var comparisonOptions = BuildComparisonOptions(
				caseInsensitive,
				trimWhitespace,
				numericToleranceRaw,
				matchThresholdRaw
			);
			var result = await diffCheckService.CompareAsync(
				leftPath,
				rightPath,
				columnMappings,
				keyColumns,
				comparisonOptions
			);
			LeftFileName = leftFile.FileName;
			RightFileName = rightFile.FileName;
			DiffReportHtml = diffCheckService.GenerateHtml(
				result,
				leftFile.FileName,
				rightFile.FileName,
				leftFile.Length,
				rightFile.Length,
				theme,
				viewPref
			);
		}
		catch (Exception ex)
		{
			ErrorMessage = ex.Message;
		}
		finally
		{
			if (leftPath != null && System.IO.File.Exists(leftPath))
				System.IO.File.Delete(leftPath);
			if (rightPath != null && System.IO.File.Exists(rightPath))
				System.IO.File.Delete(rightPath);
		}

		ColumnMappingsRaw = columnMappingsRaw;
		KeyColumnsRaw = keyColumnsRaw;
		return Page();
	}

	public IActionResult OnGetProfiles()
	{
		var names = profileStore.List();
		var profiles = names
			.Select(name => profileStore.LoadAsync(name).GetAwaiter().GetResult())
			.OfType<ComparisonProfile>()
			.Select(p => new
			{
				name = p.Name,
				keyColumns = p.KeyColumns,
				columnMappings = p.ColumnMappings?.Select(m => new
				{
					leftHeader = m.LeftHeader,
					rightHeader = m.RightHeader,
				}),
				options = p.Options != null
					? new
					{
						caseSensitive = p.Options.CaseSensitive,
						trimWhitespace = p.Options.TrimWhitespace,
						numericTolerance = p.Options.NumericTolerance,
						matchThreshold = p.Options.MatchThreshold,
					}
					: null,
			});
		return new JsonResult(profiles);
	}

	public async Task<IActionResult> OnPostSaveProfileAsync(
		string? name,
		string? keyColumnsRaw,
		string? columnMappingsRaw,
		bool caseInsensitive = false,
		bool trimWhitespace = false,
		string? numericToleranceRaw = null,
		string? matchThresholdRaw = null
	)
	{
		if (string.IsNullOrWhiteSpace(name))
			return new JsonResult(new { error = "Profile name is required." });

		try
		{
			var options = BuildComparisonOptions(
				caseInsensitive,
				trimWhitespace,
				numericToleranceRaw,
				matchThresholdRaw
			);
			var profile = new ComparisonProfile(
				name.Trim(),
				ParseKeyColumns(keyColumnsRaw),
				ParseColumnMappings(columnMappingsRaw),
				options
			);
			await profileStore.SaveAsync(profile);
			return new JsonResult(new { success = true });
		}
		catch (ArgumentException ex)
		{
			return new JsonResult(new { error = ex.Message });
		}
	}

	public async Task<IActionResult> OnPostDeleteProfileAsync(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return new JsonResult(new { error = "Profile name is required." });

		try
		{
			await profileStore.DeleteAsync(name.Trim());
			return new JsonResult(new { success = true });
		}
		catch (ArgumentException ex)
		{
			return new JsonResult(new { error = ex.Message });
		}
	}

	private static IReadOnlyList<string>? ParseKeyColumns(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return null;
		var list = raw.Split(
				[',', '\n', '\r'],
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
			)
			.Where(token => token.Length > 0)
			.ToList();
		return list.Count == 0 ? null : list;
	}

	private static IReadOnlyList<ColumnMapping>? ParseColumnMappings(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return null;
		var list = new List<ColumnMapping>();
		foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
		{
			var trimmed = line.Trim();
			if (trimmed.Length == 0)
				continue;
			var sep = trimmed.IndexOf(':') >= 0 ? ':' : ',';
			var idx = trimmed.IndexOf(sep);
			if (idx < 0)
				continue;
			var left = trimmed[..idx].Trim();
			var right = trimmed[(idx + 1)..].Trim();
			if (left.Length > 0 && right.Length > 0)
				list.Add(new ColumnMapping(left, right));
		}
		return list.Count == 0 ? null : list;
	}

	private static ComparisonOptions? BuildComparisonOptions(
		bool caseInsensitive,
		bool trimWhitespace,
		string? numericToleranceRaw,
		string? matchThresholdRaw
	)
	{
		var tolerance = double.TryParse(
			numericToleranceRaw,
			System.Globalization.NumberStyles.Any,
			System.Globalization.CultureInfo.InvariantCulture,
			out var t
		)
			? t
			: (double?)null;

		var threshold = double.TryParse(
			matchThresholdRaw,
			System.Globalization.NumberStyles.Any,
			System.Globalization.CultureInfo.InvariantCulture,
			out var th
		)
			? th
			: (double?)null;

		if (!caseInsensitive && !trimWhitespace && tolerance == null && threshold == null)
			return null; // defaults — no need to allocate

		return new ComparisonOptions
		{
			CaseSensitive = !caseInsensitive,
			TrimWhitespace = trimWhitespace,
			NumericTolerance = tolerance,
			MatchThreshold = threshold ?? ComparisonOptions.Default.MatchThreshold,
		};
	}
}
