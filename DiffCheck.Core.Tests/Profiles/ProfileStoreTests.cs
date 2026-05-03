using DiffCheck.Models;
using DiffCheck.Profiles;

namespace DiffCheck.Core.Tests.Profiles;

[TestClass]
public class ProfileStoreTests
{
	private string _dir = null!;
	private ProfileStore _store = null!;

	[TestInitialize]
	public void Setup()
	{
		_dir = Path.Combine(Path.GetTempPath(), "diffcheck-tests-" + Guid.NewGuid());
		_store = new ProfileStore(_dir);
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_dir))
			Directory.Delete(_dir, recursive: true);
	}

	[TestMethod]
	public async Task SaveAndLoad_RoundTrip()
	{
		var profile = new ComparisonProfile(
			"my-profile",
			["ID", "Name"],
			[new ColumnMapping("Name", "FullName"), new ColumnMapping("Dept", "Department")]
		);

		await _store.SaveAsync(profile);
		var loaded = await _store.LoadAsync("my-profile");

		Assert.IsNotNull(loaded);
		Assert.AreEqual(profile.Name, loaded.Name);
		CollectionAssert.AreEqual(profile.KeyColumns!.ToList(), loaded.KeyColumns!.ToList());
		Assert.HasCount(2, loaded.ColumnMappings!);
		Assert.AreEqual("Name", loaded.ColumnMappings![0].LeftHeader);
		Assert.AreEqual("FullName", loaded.ColumnMappings![0].RightHeader);
		Assert.AreEqual("Dept", loaded.ColumnMappings![1].LeftHeader);
		Assert.AreEqual("Department", loaded.ColumnMappings![1].RightHeader);
	}

	[TestMethod]
	public async Task SaveAndLoad_NullCollections()
	{
		var profile = new ComparisonProfile("minimal", null, null);

		await _store.SaveAsync(profile);
		var loaded = await _store.LoadAsync("minimal");

		Assert.IsNotNull(loaded);
		Assert.AreEqual("minimal", loaded.Name);
		Assert.IsNull(loaded.KeyColumns);
		Assert.IsNull(loaded.ColumnMappings);
	}

	[TestMethod]
	public async Task List_ReturnsSavedProfileNames()
	{
		await _store.SaveAsync(new ComparisonProfile("beta", null, null));
		await _store.SaveAsync(new ComparisonProfile("alpha", null, null));

		var names = _store.List();

		Assert.HasCount(2, names);
		Assert.AreEqual("alpha", names[0]);
		Assert.AreEqual("beta", names[1]);
	}

	[TestMethod]
	public void List_EmptyWhenDirectoryDoesNotExist()
	{
		var names = _store.List();
		Assert.IsEmpty(names);
	}

	[TestMethod]
	public async Task Delete_RemovesProfile()
	{
		await _store.SaveAsync(new ComparisonProfile("to-delete", null, null));
		Assert.HasCount(1, _store.List());

		await _store.DeleteAsync("to-delete");

		Assert.IsEmpty(_store.List());
		Assert.IsNull(await _store.LoadAsync("to-delete"));
	}

	[TestMethod]
	public async Task Delete_NoOpWhenProfileDoesNotExist()
	{
		// Should not throw
		await _store.DeleteAsync("nonexistent");
	}

	[TestMethod]
	public async Task Load_ReturnsNullForMissingProfile()
	{
		var result = await _store.LoadAsync("missing");
		Assert.IsNull(result);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("   ")]
	[DataRow("has space")]
	[DataRow("has/slash")]
	[DataRow("has\\backslash")]
	[DataRow("../traversal")]
	[DataRow("has.dot")]
	public async Task InvalidName_ThrowsArgumentException(string name)
	{
		await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
			await _store.LoadAsync(name)
		);
	}

	[TestMethod]
	public async Task Save_InvalidName_ThrowsArgumentException()
	{
		var profile = new ComparisonProfile("bad name!", null, null);
		await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
			await _store.SaveAsync(profile)
		);
	}

	[TestMethod]
	public async Task Save_OverwritesExistingProfile()
	{
		await _store.SaveAsync(new ComparisonProfile("p", ["OldKey"], null));
		await _store.SaveAsync(new ComparisonProfile("p", ["NewKey"], null));

		var loaded = await _store.LoadAsync("p");

		Assert.IsNotNull(loaded);
		CollectionAssert.AreEqual((string[])["NewKey"], loaded.KeyColumns!.ToList());
	}

	[TestMethod]
	public async Task SaveAndLoad_WithComparisonOptions_RoundTrip()
	{
		var options = new ComparisonOptions
		{
			CaseSensitive = false,
			TrimWhitespace = true,
			NumericTolerance = 0.001,
			MatchThreshold = 0.7,
		};
		var profile = new ComparisonProfile("opts-profile", ["ID"], null, options);

		await _store.SaveAsync(profile);
		var loaded = await _store.LoadAsync("opts-profile");

		Assert.IsNotNull(loaded);
		Assert.IsNotNull(loaded.Options);
		Assert.IsFalse(loaded.Options.CaseSensitive);
		Assert.IsTrue(loaded.Options.TrimWhitespace);
		Assert.AreEqual(0.001, loaded.Options.NumericTolerance);
		Assert.AreEqual(0.7, loaded.Options.MatchThreshold);
	}

	[TestMethod]
	public async Task SaveAndLoad_WithNullOptions_RoundTrip()
	{
		var profile = new ComparisonProfile("no-opts", null, null);

		await _store.SaveAsync(profile);
		var loaded = await _store.LoadAsync("no-opts");

		Assert.IsNotNull(loaded);
		Assert.IsNull(loaded.Options);
	}
}
