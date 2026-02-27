using System.CommandLine;
using DiffCheck;
using DiffCheck.Models;

var leftArg = new Argument<FileInfo>("left") { Description = "Path to the left file." };
var rightArg = new Argument<FileInfo>("right") { Description = "Path to the right file." };

var outputOption = new Option<FileInfo>("--output", ["-o"])
{
	Description = "Path for the output HTML report.",
	DefaultValueFactory = x => new FileInfo("diff-report.html"),
};

var columnMapOption = new Option<string[]>("--column-map", "Column mapping: left header and right header (e.g. \"Name:FullName\"). Can be specified multiple times.")
{
	AllowMultipleArgumentsPerToken = true,
};

var keyColumnsOption = new Option<string[]>("--key-columns", "Column name(s) to match rows by (faster). Comma-separated or multiple. E.g. \"ID\" or \"ID,Name\".")
{
	AllowMultipleArgumentsPerToken = true,
};

var rootCommand = new RootCommand("Compare two CSV or XLSX files and generate an HTML diff report.")
{
	leftArg,
	rightArg,
	outputOption,
	columnMapOption,
	keyColumnsOption,
};

rootCommand.SetAction(
	async (parseResult, token) =>
	{
		var service = new DiffCheckService();

		try
		{
			var leftFilePath = parseResult.GetValue(leftArg)!.FullName;
			var rightFilePath = parseResult.GetValue(rightArg)!.FullName;
			var outputPath = parseResult.GetValue(outputOption)!.FullName;
			var mapStrings = parseResult.GetValue(columnMapOption) ?? Array.Empty<string>();

			IReadOnlyList<ColumnMapping>? columnMappings = null;
			if (mapStrings.Length > 0)
			{
				var list = new List<ColumnMapping>();
				foreach (var s in mapStrings)
				{
					var colon = s.IndexOf(':');
					if (colon < 0)
					{
						Console.Error.WriteLine($"Invalid column map \"{s}\". Use format LeftHeader:RightHeader (e.g. Name:FullName).");
						return;
					}
					list.Add(new ColumnMapping(
						s[..colon].Trim(),
						s[(colon + 1)..].Trim()
					));
				}
				columnMappings = list;
			}

			IReadOnlyList<string>? keyColumns = null;
			var keyStrings = parseResult.GetValue(keyColumnsOption) ?? Array.Empty<string>();
			if (keyStrings.Length > 0)
			{
				var list = new List<string>();
				foreach (var s in keyStrings)
				{
					foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
					{
						if (part.Length > 0)
							list.Add(part);
					}
				}
				if (list.Count > 0)
					keyColumns = list;
			}

			await service.CompareAndSaveHtmlAsync(
				leftFilePath,
				rightFilePath,
				outputPath,
				columnMappings,
				keyColumns,
				token
			);

			Console.WriteLine($"Report saved to: {outputPath}");
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Error: {ex.Message}");
		}
	}
);

return await rootCommand.Parse(args).InvokeAsync();
