using DiffCheck;
using DiffCheck.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiffCheck.Web.Pages;

public class IndexModel : PageModel
{
	private readonly DiffCheckService _diffCheckService;

	public IndexModel(DiffCheckService diffCheckService)
	{
		_diffCheckService = diffCheckService;
	}

	public string? DiffReportHtml { get; set; }
	public string? LeftFileName { get; set; }
	public string? RightFileName { get; set; }
	public string? ErrorMessage { get; set; }

	/// <summary>Raw column mappings text (left:right per line) for repopulating the form.</summary>
	public string? ColumnMappingsRaw { get; set; }

	/// <summary>Raw key columns text (comma or newline separated) for repopulating the form.</summary>
	public string? KeyColumnsRaw { get; set; }

	public void OnGet() { }

	public async Task<IActionResult> OnPostCompareAsync(
		IFormFile? leftFile,
		IFormFile? rightFile,
		string? columnMappingsRaw,
		string? keyColumnsRaw
	)
	{
		if (leftFile == null || rightFile == null)
			return new JsonResult(new { error = "Please provide both files." });

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
			var result = await _diffCheckService.CompareAsync(
				leftPath,
				rightPath,
				columnMappings,
				keyColumns
			);
			var html = _diffCheckService.GenerateHtml(
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
		string? keyColumnsRaw
	)
	{
		if (leftFile == null || rightFile == null)
		{
			ErrorMessage = "Please provide both files.";
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

			IReadOnlyList<ColumnMapping>? columnMappings = ParseColumnMappings(columnMappingsRaw);
			IReadOnlyList<string>? keyColumns = ParseKeyColumns(keyColumnsRaw);
			var result = await _diffCheckService.CompareAsync(
				leftPath,
				rightPath,
				columnMappings,
				keyColumns
			);
			LeftFileName = leftFile.FileName;
			RightFileName = rightFile.FileName;
			DiffReportHtml = _diffCheckService.GenerateHtml(
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

	private static IReadOnlyList<string>? ParseKeyColumns(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return null;
		var list = new List<string>();
		foreach (
			var token in raw.Split(
				new[] { ',', '\n', '\r' },
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
			)
		)
		{
			if (token.Length > 0)
				list.Add(token);
		}
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
}
