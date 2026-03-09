using System.CommandLine;
using DiffCheck;
using DiffCheck.Models;
using DiffCheck.Profiles;

var leftArg = new Argument<FileInfo>("left") { Description = "Path to the left file." };
var rightArg = new Argument<FileInfo>("right") { Description = "Path to the right file." };

var outputOption = new Option<FileInfo>("--output", ["-o"])
{
	Description = "Path for the output HTML report.",
	DefaultValueFactory = x => new FileInfo("diff-report.html"),
};

var columnMapOption = new Option<string[]>(
	"--column-map",
	"Column mapping: left header and right header (e.g. \"Name:FullName\"). Can be specified multiple times."
)
{
	AllowMultipleArgumentsPerToken = true,
};

var keyColumnsOption = new Option<string[]>(
	"--key-columns",
	"Column name(s) to match rows by (faster). Comma-separated or multiple. E.g. \"ID\" or \"ID,Name\"."
)
{
	AllowMultipleArgumentsPerToken = true,
};

var profileOption = new Option<string?>(
	"--profile",
	"Load a saved profile by name. Explicit --key-columns and --column-map flags override the profile values."
);

var saveProfileOption = new Option<string?>(
	"--save-profile",
	"Save the effective key columns and column mappings as a named profile after a successful run."
);

var caseInsensitiveOption = new Option<bool>(
	"--case-insensitive",
	"Compare values case-insensitively."
);

var trimWhitespaceOption = new Option<bool>(
	"--trim-whitespace",
	"Trim leading and trailing whitespace before comparing."
);

var numericToleranceOption = new Option<double?>(
	"--numeric-tolerance",
	"Treat numeric values as equal when their absolute difference is within this tolerance (e.g. 0.001)."
);

var matchThresholdOption = new Option<double?>(
	"--match-threshold",
	"Fraction of columns that must match for content-based row pairing (default: 0.5)."
);

var rootCommand = new RootCommand("Compare two CSV or XLSX files and generate an HTML diff report.")
{
	leftArg,
	rightArg,
	outputOption,
	columnMapOption,
	keyColumnsOption,
	profileOption,
	saveProfileOption,
	caseInsensitiveOption,
	trimWhitespaceOption,
	numericToleranceOption,
	matchThresholdOption,
};

// Subcommand: list-profiles
var listProfilesCommand = new Command("list-profiles", "List all saved comparison profiles.");
listProfilesCommand.SetAction(
	(parseResult) =>
	{
		var store = new ProfileStore(ProfileStore.DefaultCliDirectory);
		var profiles = store.List();
		if (profiles.Count == 0)
			Console.WriteLine("No saved profiles.");
		else
			foreach (var name in profiles)
				Console.WriteLine(name);
	}
);
rootCommand.Add(listProfilesCommand);

rootCommand.SetAction(
	async (parseResult, token) =>
	{
		var service = new DiffCheckService();
		var profileStore = new ProfileStore(ProfileStore.DefaultCliDirectory);

		try
		{
			var leftFilePath = parseResult.GetValue(leftArg)!.FullName;
			var rightFilePath = parseResult.GetValue(rightArg)!.FullName;
			var outputPath = parseResult.GetValue(outputOption)!.FullName;
			var mapStrings = parseResult.GetValue(columnMapOption) ?? Array.Empty<string>();
			var keyStrings = parseResult.GetValue(keyColumnsOption) ?? Array.Empty<string>();
			var profileName = parseResult.GetValue(profileOption);
			var saveProfileName = parseResult.GetValue(saveProfileOption);
			var caseInsensitive = parseResult.GetValue(caseInsensitiveOption);
			var trimWhitespace = parseResult.GetValue(trimWhitespaceOption);
			var numericTolerance = parseResult.GetValue(numericToleranceOption);
			var matchThreshold = parseResult.GetValue(matchThresholdOption);

			// Load profile defaults (explicit CLI flags take precedence)
			IReadOnlyList<ColumnMapping>? profileMappings = null;
			IReadOnlyList<string>? profileKeyColumns = null;
			ComparisonOptions? profileOptions = null;
			if (profileName != null)
			{
				var profile = await profileStore.LoadAsync(profileName);
				if (profile == null)
				{
					Console.Error.WriteLine($"Profile \"{profileName}\" not found.");
					return;
				}
				profileMappings = profile.ColumnMappings;
				profileKeyColumns = profile.KeyColumns;
				profileOptions = profile.Options;
			}

			IReadOnlyList<ColumnMapping>? columnMappings = null;
			if (mapStrings.Length > 0)
			{
				var list = new List<ColumnMapping>();
				foreach (var s in mapStrings)
				{
					var colon = s.IndexOf(':');
					if (colon < 0)
					{
						Console.Error.WriteLine(
							$"Invalid column map \"{s}\". Use format LeftHeader:RightHeader (e.g. Name:FullName)."
						);
						return;
					}
					list.Add(new ColumnMapping(s[..colon].Trim(), s[(colon + 1)..].Trim()));
				}
				columnMappings = list;
			}
			else
			{
				columnMappings = profileMappings;
			}

			IReadOnlyList<string>? keyColumns = null;
			if (keyStrings.Length > 0)
			{
				var list = new List<string>();
				foreach (var s in keyStrings)
				{
					foreach (
						var part in s.Split(
							',',
							StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
						)
					)
					{
						if (part.Length > 0)
							list.Add(part);
					}
				}
				if (list.Count > 0)
					keyColumns = list;
			}
			else
			{
				keyColumns = profileKeyColumns;
			}

			// Build ComparisonOptions: explicit CLI flags override profile options
			var anyOptionFlag =
				parseResult.GetResult(caseInsensitiveOption) != null
				|| parseResult.GetResult(trimWhitespaceOption) != null
				|| parseResult.GetResult(numericToleranceOption) != null
				|| parseResult.GetResult(matchThresholdOption) != null;
			ComparisonOptions? comparisonOptions;
			if (anyOptionFlag)
			{
				comparisonOptions = new ComparisonOptions
				{
					CaseSensitive = !caseInsensitive,
					TrimWhitespace = trimWhitespace,
					NumericTolerance = numericTolerance,
					MatchThreshold = matchThreshold ?? ComparisonOptions.Default.MatchThreshold,
				};
			}
			else
			{
				comparisonOptions = profileOptions;
			}

			await service.CompareAndSaveHtmlAsync(
				leftFilePath,
				rightFilePath,
				outputPath,
				columnMappings,
				keyColumns,
				comparisonOptions,
				token
			);

			Console.WriteLine($"Report saved to: {outputPath}");

			if (saveProfileName != null)
			{
				var profile = new ComparisonProfile(
					saveProfileName,
					keyColumns,
					columnMappings,
					comparisonOptions
				);
				await profileStore.SaveAsync(profile);
				Console.WriteLine($"Profile \"{saveProfileName}\" saved.");
			}
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Error: {ex.Message}");
		}
	}
);

return await rootCommand.Parse(args).InvokeAsync();
