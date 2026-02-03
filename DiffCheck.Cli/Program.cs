using System.CommandLine;
using DiffCheck;

var leftArg = new Argument<FileInfo>("left") { Description = "Path to the left file." };
var rightArg = new Argument<FileInfo>("right") { Description = "Path to the right file." };

var outputOption = new Option<FileInfo>("--output", ["-o"])
{
	Description = "Path for the output HTML report.",
	DefaultValueFactory = x => new FileInfo("./files/diff-report.html"),
};

var rootCommand = new RootCommand("Compare two CSV or XLSX files and generate an HTML diff report.")
{
	leftArg,
	rightArg,
	outputOption,
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
			await service.CompareAndSaveHtmlAsync(leftFilePath, rightFilePath, outputPath, token);

			Console.WriteLine($"Report saved to: {outputPath}");
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Error: {ex.Message}");
		}
	}
);

return await rootCommand.Parse(args).InvokeAsync();
