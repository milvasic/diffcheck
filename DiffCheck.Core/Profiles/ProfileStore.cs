using System.Text.Json;
using System.Text.RegularExpressions;
using DiffCheck.Models;

namespace DiffCheck.Profiles;

/// <summary>
/// Persists and retrieves <see cref="ComparisonProfile"/> instances as JSON files
/// in a directory. One file per profile; the filename (without .json) is the profile name.
/// </summary>
public sealed class ProfileStore
{
	private static readonly Regex ValidName = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
	};

	private readonly string _directory;

	public ProfileStore(string profilesDirectory)
	{
		ArgumentNullException.ThrowIfNull(profilesDirectory);
		_directory = profilesDirectory;
	}

	/// <summary>Default profile directory for the CLI: <c>~/.diffcheck/profiles</c>.</summary>
	public static string DefaultCliDirectory =>
		Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
			".diffcheck",
			"profiles"
		);

	/// <summary>Returns profile names sorted alphabetically. Returns an empty list if the directory does not exist.</summary>
	public IReadOnlyList<string> List()
	{
		if (!Directory.Exists(_directory))
			return [];

		return
		[
			.. Directory
				.EnumerateFiles(_directory, "*.json")
				.Select(Path.GetFileNameWithoutExtension)
				.OfType<string>()
				.Where(n => ValidName.IsMatch(n))
				.OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
		];
	}

	/// <summary>Loads a profile by name. Returns <c>null</c> if the profile does not exist.</summary>
	public async Task<ComparisonProfile?> LoadAsync(string name)
	{
		ValidateName(name);
		var path = GetPath(name);
		if (!File.Exists(path))
			return null;
		await using var stream = File.OpenRead(path);
		return await JsonSerializer.DeserializeAsync<ComparisonProfile>(stream, JsonOptions);
	}

	/// <summary>Saves (creates or overwrites) a profile.</summary>
	public async Task SaveAsync(ComparisonProfile profile)
	{
		ArgumentNullException.ThrowIfNull(profile);
		ValidateName(profile.Name);
		Directory.CreateDirectory(_directory);
		var path = GetPath(profile.Name);
		await using var stream = File.Create(path);
		await JsonSerializer.SerializeAsync(stream, profile, JsonOptions);
	}

	/// <summary>Deletes a profile. No-op if the profile does not exist.</summary>
	public Task DeleteAsync(string name)
	{
		ValidateName(name);
		var path = GetPath(name);
		if (File.Exists(path))
			File.Delete(path);
		return Task.CompletedTask;
	}

	private string GetPath(string name) => Path.Combine(_directory, name + ".json");

	private static void ValidateName(string name)
	{
		if (string.IsNullOrWhiteSpace(name) || !ValidName.IsMatch(name))
			throw new ArgumentException(
				"Profile name must contain only letters, digits, hyphens, or underscores.",
				nameof(name)
			);
	}
}
