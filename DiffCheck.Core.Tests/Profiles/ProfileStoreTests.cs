using DiffCheck.Models;
using DiffCheck.Profiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
		Assert.AreEqual(2, loaded.ColumnMappings!.Count);
		Assert.AreEqual("Name", loaded.ColumnMappings[0].LeftHeader);
		Assert.AreEqual("FullName", loaded.ColumnMappings[0].RightHeader);
		Assert.AreEqual("Dept", loaded.ColumnMappings[1].LeftHeader);
		Assert.AreEqual("Department", loaded.ColumnMappings[1].RightHeader);
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

		Assert.AreEqual(2, names.Count);
		Assert.AreEqual("alpha", names[0]);
		Assert.AreEqual("beta", names[1]);
	}

	[TestMethod]
	public void List_EmptyWhenDirectoryDoesNotExist()
	{
		var names = _store.List();
		Assert.AreEqual(0, names.Count);
	}

	[TestMethod]
	public async Task Delete_RemovesProfile()
	{
		await _store.SaveAsync(new ComparisonProfile("to-delete", null, null));
		Assert.AreEqual(1, _store.List().Count);

		await _store.DeleteAsync("to-delete");

		Assert.AreEqual(0, _store.List().Count);
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
		CollectionAssert.AreEqual(new[] { "NewKey" }, loaded.KeyColumns!.ToList());
	}
}
