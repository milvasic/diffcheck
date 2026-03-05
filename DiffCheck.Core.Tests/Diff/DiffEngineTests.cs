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

	[TestMethod]
	public void Compare_WithColumnMapping_RenamedColumnTreatedAsOne()
	{
		// Left has "A", right has "B" - same data. Without mapping: 2 columns (A, B), both sides differ.
		// With mapping (A, B): one column "A", values compared from left A and right B -> unchanged.
		var left = new DataTable(
			new[] { "A" },
			new List<IReadOnlyList<string>> { new[] { "1" }, new[] { "2" } }
		);
		var right = new DataTable(
			new[] { "B" },
			new List<IReadOnlyList<string>> { new[] { "1" }, new[] { "2" } }
		);
		var mappings = new[] { new ColumnMapping("A", "B") };

		var engine = new DiffEngine();
		var result = engine.Compare(left, right, mappings);

		Assert.AreEqual(1, result.Headers.Count);
		Assert.AreEqual("A", result.Headers[0]);
		Assert.AreEqual(2, result.Summary.UnchangedRows);
		Assert.AreEqual(0, result.Summary.ModifiedRows);
		Assert.IsFalse(result.Summary.HasDifferences);
	}

	[TestMethod]
	public void Compare_WithColumnMapping_WithoutMapping_ShowsTwoColumns()
	{
		var left = new DataTable(new[] { "A" }, new List<IReadOnlyList<string>> { new[] { "1" } });
		var right = new DataTable(new[] { "B" }, new List<IReadOnlyList<string>> { new[] { "1" } });

		var engine = new DiffEngine();
		var result = engine.Compare(left, right);

		Assert.AreEqual(2, result.Headers.Count);
		Assert.IsTrue(result.Headers.Contains("A"));
		Assert.IsTrue(result.Headers.Contains("B"));
		// No mapping: A and B are different columns, so no column values match between rows -> row is not matched (Added + Removed)
		Assert.AreEqual(1, result.Summary.AddedRows);
		Assert.AreEqual(1, result.Summary.RemovedRows);
	}

	[TestMethod]
	public void Compare_WithKeyColumns_MatchesRowsByKey()
	{
		var left = new DataTable(
			new[] { "ID", "Name" },
			new List<IReadOnlyList<string>>
			{
				new[] { "1", "Alice" },
				new[] { "2", "Bob" },
				new[] { "3", "Carol" },
			}
		);
		var right = new DataTable(
			new[] { "ID", "Name" },
			new List<IReadOnlyList<string>>
			{
				new[] { "1", "Alice" },
				new[] { "2", "Robert" }, // modified
				new[] { "4", "Dave" }, // added
			}
		);
		var keyColumns = new[] { "ID" };

		var engine = new DiffEngine();
		var result = engine.Compare(left, right, keyColumns: keyColumns);

		Assert.AreEqual(4, result.Rows.Count); // removed, unchanged, modified, added
		Assert.AreEqual(1, result.Summary.UnchangedRows); // ID 1
		Assert.AreEqual(1, result.Summary.ModifiedRows); // ID 2
		Assert.AreEqual(1, result.Summary.AddedRows); // ID 4
		Assert.AreEqual(1, result.Summary.RemovedRows); // ID 3
	}

	[TestMethod]
	public void Compare_WithKeyColumns_SameResultAsContentMatch_WhenKeysUnique()
	{
		var left = new DataTable(
			new[] { "ID", "V" },
			new List<IReadOnlyList<string>> { new[] { "1", "a" }, new[] { "2", "b" } }
		);
		var right = new DataTable(
			new[] { "ID", "V" },
			new List<IReadOnlyList<string>> { new[] { "1", "a" }, new[] { "2", "b" } }
		);

		var engine = new DiffEngine();
		var withKeys = engine.Compare(left, right, keyColumns: new[] { "ID" });
		var withoutKeys = engine.Compare(left, right);

		Assert.AreEqual(withoutKeys.Summary.UnchangedRows, withKeys.Summary.UnchangedRows);
		Assert.AreEqual(withoutKeys.Summary.AddedRows, withKeys.Summary.AddedRows);
		Assert.AreEqual(withoutKeys.Summary.RemovedRows, withKeys.Summary.RemovedRows);
	}
}
