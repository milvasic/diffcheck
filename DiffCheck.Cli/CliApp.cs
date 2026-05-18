using System.CommandLine;
using System.Diagnostics;
using DiffCheck;
using DiffCheck.Models;
using DiffCheck.Profiles;

namespace DiffCheck.Cli;

internal static class CliApp
{
	internal static async Task<int> RunAsync(string[] args)
	{
		var leftArg = new Argument<FileInfo>("left") { Description = "Path to the left file." };
		var rightArg = new Argument<FileInfo>("right") { Description = "Path to the right file." };

		var outputOption = new Option<FileInfo?>("--output", "-o")
		{
			Description =
				"Path for the output report. Defaults to diff-report.html (html format) or diff-report.json (json format). Mutually exclusive with --summary.",
		};

		var formatOption = new Option<string>("--format")
		{
			Description = "Output format: html (default) or json.",
			DefaultValueFactory = _ => "html",
		};

		var columnMapOption = new Option<string[]>("--column-map")
		{
			Description =
				"Column mapping: left header and right header (e.g. \"Name:FullName\"). Can be specified multiple times.",
			AllowMultipleArgumentsPerToken = true,
		};

		var keyColumnsOption = new Option<string[]>("--key-columns")
		{
			Description =
				"Key columns for row pairing, specified as a comma-separated list (e.g. \"ID\") or "
				+ "multiple --key-columns flags (e.g. --key-columns ID --key-columns Name).",
			AllowMultipleArgumentsPerToken = true,
		};

		var profileOption = new Option<string?>("--profile")
		{
			Description =
				"Name of a saved comparison profile to use as defaults for key columns, column mappings, and comparison options.",
		};

		var saveProfileOption = new Option<string?>("--save-profile")
		{
			Description =
				"Save the current comparison settings as a profile with the given name "
				+ "(overwrites existing profile with the same name).",
		};

		var caseInsensitiveOption = new Option<bool>("--case-insensitive")
		{
			Description = "Perform case-insensitive comparisons.",
		};

		var trimWhitespaceOption = new Option<bool>("--trim-whitespace")
		{
			Description = "Trim leading and trailing whitespace from cell values before comparing.",
		};

		var numericToleranceOption = new Option<double?>("--numeric-tolerance")
		{
			Description =
				"Numeric tolerance for comparing numeric values. If specified, numeric columns will be "
				+ "compared using this tolerance instead of exact equality (e.g. 0.01).",
		};

		var matchThresholdOption = new Option<double?>("--match-threshold")
		{
			Description =
				"Match threshold (0 to 1) for determining whether rows are considered a match based on "
				+ "similarity of key column values. Only applicable when key columns are specified. Default is 0.8.",
		};

		var openOption = new Option<bool>("--open")
		{
			Description =
				"Open the generated HTML report in the system default browser after generation.",
		};

		var summaryOption = new Option<bool>("--summary")
		{
			Description =
				"Print diff counts to stdout (added=N removed=N modified=N reordered=N) without writing "
				+ "an HTML report. Mutually exclusive with --output/-o.",
		};

		var failOnDiffOption = new Option<bool>("--fail-on-diff")
		{
			Description = "Exit with code 1 if any differences are found.",
		};

		var maxAddedOption = new Option<int?>("--max-added")
		{
			Description = "Exit with code 1 if the number of added rows exceeds this value.",
		};

		var maxRemovedOption = new Option<int?>("--max-removed")
		{
			Description = "Exit with code 1 if the number of removed rows exceeds this value.",
		};

		var maxModifiedOption = new Option<int?>("--max-modified")
		{
			Description = "Exit with code 1 if the number of modified rows exceeds this value.",
		};

		var rootCommand = new RootCommand(
			"Compare two CSV or XLSX files and generate a diff report."
		)
		{
			leftArg,
			rightArg,
			outputOption,
			formatOption,
			columnMapOption,
			keyColumnsOption,
			profileOption,
			saveProfileOption,
			caseInsensitiveOption,
			trimWhitespaceOption,
			numericToleranceOption,
			matchThresholdOption,
			openOption,
			summaryOption,
			failOnDiffOption,
			maxAddedOption,
			maxRemovedOption,
			maxModifiedOption,
		};

		var listProfilesCommand = new Command(
			"list-profiles",
			"List all saved comparison profiles."
		);
		listProfilesCommand.SetAction(
			(_) =>
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
					var outputFile = parseResult.GetValue(outputOption);
					var format = (parseResult.GetValue(formatOption) ?? "html").ToLowerInvariant();
					var mapStrings = parseResult.GetValue(columnMapOption) ?? [];
					var keyStrings = parseResult.GetValue(keyColumnsOption) ?? [];
					var profileName = parseResult.GetValue(profileOption);
					var saveProfileName = parseResult.GetValue(saveProfileOption);
					var caseInsensitive = parseResult.GetValue(caseInsensitiveOption);
					var trimWhitespace = parseResult.GetValue(trimWhitespaceOption);
					var numericTolerance = parseResult.GetValue(numericToleranceOption);
					var matchThreshold = parseResult.GetValue(matchThresholdOption);
					var openInBrowser = parseResult.GetValue(openOption);
					var summary = parseResult.GetValue(summaryOption);
					var failOnDiff = parseResult.GetValue(failOnDiffOption);
					var maxAdded = parseResult.GetValue(maxAddedOption);
					var maxRemoved = parseResult.GetValue(maxRemovedOption);
					var maxModified = parseResult.GetValue(maxModifiedOption);

					if (format is not ("html" or "json"))
					{
						Console.Error.WriteLine(
							$"Error: unsupported --format value \"{format}\". Supported: html, json."
						);
						return 1;
					}

					if (summary && outputFile != null)
					{
						Console.Error.WriteLine(
							"Error: --summary and --output/-o are mutually exclusive."
						);
						return 2;
					}

					var defaultOutputPath = format == "json" ? "diff-report.json" : "diff-report.html";
					var outputPath = outputFile?.FullName ?? defaultOutputPath;

					IReadOnlyList<ColumnMapping>? profileMappings = null;
					IReadOnlyList<string>? profileKeyColumns = null;
					ComparisonOptions? profileOptions = null;
					if (profileName != null)
					{
						var profile = await profileStore.LoadAsync(profileName);
						if (profile == null)
						{
							Console.Error.WriteLine($"Profile \"{profileName}\" not found.");
							return 2;
						}
						profileMappings = profile.ColumnMappings;
						profileKeyColumns = profile.KeyColumns;
						profileOptions = profile.Options;
					}

					IReadOnlyList<ColumnMapping>? columnMappings;
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
								return 2;
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
							list.AddRange(
								s.Split(
										',',
										StringSplitOptions.RemoveEmptyEntries
											| StringSplitOptions.TrimEntries
									)
									.Where(part => part.Length > 0)
							);
						}
						if (list.Count > 0)
							keyColumns = list;
					}
					else
					{
						keyColumns = profileKeyColumns;
					}

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
							MatchThreshold =
								matchThreshold ?? ComparisonOptions.Default.MatchThreshold,
						};
					}
					else
					{
						comparisonOptions = profileOptions;
					}

					if (summary)
					{
						var result = await service.CompareAsync(
							leftFilePath,
							rightFilePath,
							columnMappings,
							keyColumns,
							comparisonOptions,
							cancellationToken: token
						);
						Console.WriteLine(
							$"added={result.Summary.AddedRows} removed={result.Summary.RemovedRows} "
								+ $"modified={result.Summary.ModifiedRows} reordered={result.Summary.ReorderedRows}"
						);
						return ThresholdExceeded(
							result.Summary,
							failOnDiff,
							maxAdded,
							maxRemoved,
							maxModified
						)
							? 1
							: 0;
					}

					DiffResult diffResult;
					if (format == "json")
					{
						diffResult = await service.CompareAndSaveJsonAsync(
							leftFilePath,
							rightFilePath,
							outputPath,
							columnMappings,
							keyColumns,
							comparisonOptions,
							cancellationToken: token
						);
					}
					else
					{
						diffResult = await service.CompareAndSaveHtmlAsync(
							leftFilePath,
							rightFilePath,
							outputPath,
							columnMappings,
							keyColumns,
							comparisonOptions,
							cancellationToken: token
						);
					}

					Console.WriteLine($"Report saved to: {outputPath}");

					if (openInBrowser)
					{
						try
						{
							Process.Start(
								new ProcessStartInfo
								{
									FileName = outputPath,
									UseShellExecute = true,
								}
							);
						}
						catch (Exception ex)
							when (ex
									is System.ComponentModel.Win32Exception
										or InvalidOperationException
							)
						{
							Console.WriteLine(
								"Warning: could not open browser — no default handler registered."
							);
						}
					}

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

					return ThresholdExceeded(
						diffResult.Summary,
						failOnDiff,
						maxAdded,
						maxRemoved,
						maxModified
					)
						? 1
						: 0;
				}
				catch (ArgumentException ex)
				{
					Console.Error.WriteLine($"Error: {ex.Message}");
					return 2;
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine($"Error: {ex.Message}");
					return 1;
				}
			}
		);

		return await rootCommand.Parse(args).InvokeAsync();
	}

	private static bool ThresholdExceeded(
		DiffSummary summary,
		bool failOnDiff,
		int? maxAdded,
		int? maxRemoved,
		int? maxModified
	) =>
		(failOnDiff && summary.HasDifferences)
		|| (maxAdded.HasValue && summary.AddedRows > maxAdded.Value)
		|| (maxRemoved.HasValue && summary.RemovedRows > maxRemoved.Value)
		|| (maxModified.HasValue && summary.ModifiedRows > maxModified.Value);
}
