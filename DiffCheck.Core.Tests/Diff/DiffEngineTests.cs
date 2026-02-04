using DiffCheck.Diff;
using DiffCheck.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffCheck.Core.Tests.Diff;

[TestClass]
public class DiffEngineTests
{
	[TestMethod]
	public void Compare_IdenticalTables_ReturnsUnchangedRows()
	{
		var left = new DataTable(
			new[] { "A", "B" },
			new List<IReadOnlyList<string>> { new[] { "1", "2" }, new[] { "3", "4" } }
		);
		var right = new DataTable(
			new[] { "A", "B" },
			new List<IReadOnlyList<string>> { new[] { "1", "2" }, new[] { "3", "4" } }
		);

		var engine = new DiffEngine();
		var result = engine.Compare(left, right);

		Assert.AreEqual(2, result.Summary.UnchangedRows);
		Assert.AreEqual(0, result.Summary.AddedRows);
		Assert.AreEqual(0, result.Summary.RemovedRows);
		Assert.AreEqual(0, result.Summary.ModifiedRows);
		Assert.IsFalse(result.Summary.HasDifferences);
		Assert.AreEqual(2, result.LeftRowCount);
		Assert.AreEqual(2, result.RightRowCount);
		Assert.AreEqual(2, result.LeftColumnCount);
		Assert.AreEqual(2, result.RightColumnCount);
	}

	[TestMethod]
	public void Compare_AddedRow_DetectsAdded()
	{
		var left = new DataTable(
			new[] { "A", "B" },
			new List<IReadOnlyList<string>> { new[] { "1", "2" } }
		);
		var right = new DataTable(
			new[] { "A", "B" },
			new List<IReadOnlyList<string>> { new[] { "1", "2" }, new[] { "3", "4" } }
		);

		var engine = new DiffEngine();
		var result = engine.Compare(left, right);

		Assert.AreEqual(1, result.Summary.AddedRows);
		Assert.AreEqual(1, result.Summary.UnchangedRows);
		Assert.IsTrue(result.Summary.HasDifferences);
	}

	[TestMethod]
	public void Compare_RemovedRow_DetectsRemoved()
	{
		var left = new DataTable(
			new[] { "A", "B" },
			new List<IReadOnlyList<string>> { new[] { "1", "2" }, new[] { "3", "4" } }
		);
		var right = new DataTable(
			new[] { "A", "B" },
			new List<IReadOnlyList<string>> { new[] { "1", "2" } }
		);

		var engine = new DiffEngine();
		var result = engine.Compare(left, right);

		Assert.AreEqual(1, result.Summary.RemovedRows);
		Assert.AreEqual(1, result.Summary.UnchangedRows);
		Assert.IsTrue(result.Summary.HasDifferences);
	}

	[TestMethod]
	public void Compare_ModifiedCell_DetectsModified()
	{
		var left = new DataTable(
			new[] { "A", "B" },
			new List<IReadOnlyList<string>> { new[] { "1", "2" } }
		);
		var right = new DataTable(
			new[] { "A", "B" },
			new List<IReadOnlyList<string>> { new[] { "1", "99" } }
		);

		var engine = new DiffEngine();
		var result = engine.Compare(left, right);

		Assert.AreEqual(1, result.Summary.ModifiedRows);
		Assert.IsTrue(result.Summary.HasDifferences);
	}

	[TestMethod]
	[ExpectedException(typeof(ArgumentNullException))]
	public void Compare_NullLeft_ThrowsArgumentNullException()
	{
		var right = new DataTable(new[] { "A" }, new List<IReadOnlyList<string>>());
		var engine = new DiffEngine();
		engine.Compare(null!, right);
	}

	[TestMethod]
	[ExpectedException(typeof(ArgumentNullException))]
	public void Compare_NullRight_ThrowsArgumentNullException()
	{
		var left = new DataTable(new[] { "A" }, new List<IReadOnlyList<string>>());
		var engine = new DiffEngine();
		engine.Compare(left, null!);
	}
}
