using DiffCheck;
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

	public void OnGet() { }

	public async Task<IActionResult> OnPostAsync(IFormFile? leftFile, IFormFile? rightFile)
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

			var result = await _diffCheckService.CompareAsync(leftPath, rightPath);
			LeftFileName = leftFile.FileName;
			RightFileName = rightFile.FileName;
			DiffReportHtml = _diffCheckService.GenerateHtml(
				result,
				leftFile.FileName,
				rightFile.FileName,
				leftFile.Length,
				rightFile.Length,
				theme
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

		return Page();
	}
}
