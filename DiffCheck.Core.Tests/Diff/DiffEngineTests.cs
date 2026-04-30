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
	public void Compare_NullLeft_ThrowsArgumentNullException()
	{
		var right = new DataTable(new[] { "A" }, new List<IReadOnlyList<string>>());
		var engine = new DiffEngine();
		Assert.ThrowsExactly<ArgumentNullException>(() =>
		{
			engine.Compare(null!, right);
		});
	}

	[TestMethod]
	public void Compare_NullRight_ThrowsArgumentNullException()
	{
		var left = new DataTable(new[] { "A" }, new List<IReadOnlyList<string>>());
		var engine = new DiffEngine();
		Assert.ThrowsExactly<ArgumentNullException>(() =>
		{
			engine.Compare(left, null!);
		});
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

	// ── ComparisonOptions tests ───────────────────────────────────────────────

	[TestMethod]
	public void Compare_DefaultOptions_SameResultAsNoOptions()
	{
		var left = new DataTable(
			new[] { "A", "B" },
			new List<IReadOnlyList<string>> { new[] { "1", "hello" }, new[] { "2", "world" } }
		);
		var right = new DataTable(
			new[] { "A", "B" },
			new List<IReadOnlyList<string>> { new[] { "1", "hello" }, new[] { "2", "changed" } }
		);

		var engine = new DiffEngine();
		var withDefault = engine.Compare(left, right, options: ComparisonOptions.Default);
		var withNull = engine.Compare(left, right);

		Assert.AreEqual(withNull.Summary.UnchangedRows, withDefault.Summary.UnchangedRows);
		Assert.AreEqual(withNull.Summary.ModifiedRows, withDefault.Summary.ModifiedRows);
	}

	[TestMethod]
	public void Compare_CaseInsensitive_TreatsValuesAsEqual()
	{
		// Use 2 columns so match score for case-sensitive row is 1/2 = 0.5 (still paired)
		var left = new DataTable(
			new[] { "ID", "Name" },
			new List<IReadOnlyList<string>> { new[] { "1", "Hello" } }
		);
		var right = new DataTable(
			new[] { "ID", "Name" },
			new List<IReadOnlyList<string>> { new[] { "1", "HELLO" } }
		);

		var engine = new DiffEngine();
		var sensitive = engine.Compare(left, right);
		var insensitive = engine.Compare(
			left,
			right,
			options: new ComparisonOptions { CaseSensitive = false }
		);

		Assert.AreEqual(1, sensitive.Summary.ModifiedRows);
		Assert.AreEqual(0, insensitive.Summary.ModifiedRows);
		Assert.AreEqual(1, insensitive.Summary.UnchangedRows);
	}

	[TestMethod]
	public void Compare_CaseSensitive_DefaultBehaviorPreserved()
	{
		// ID column matches (1==1) giving 50% score, so row is paired; Name column differs in case
		var left = new DataTable(
			new[] { "ID", "Name" },
			new List<IReadOnlyList<string>> { new[] { "1", "abc" } }
		);
		var right = new DataTable(
			new[] { "ID", "Name" },
			new List<IReadOnlyList<string>> { new[] { "1", "ABC" } }
		);

		var engine = new DiffEngine();
		var result = engine.Compare(left, right); // default: case-sensitive

		Assert.AreEqual(1, result.Summary.ModifiedRows);
	}

	[TestMethod]
	public void Compare_TrimWhitespace_TreatsValuesAsEqual()
	{
		// ID column provides stable 50% match so row is always paired
		var left = new DataTable(
			new[] { "ID", "Name" },
			new List<IReadOnlyList<string>> { new[] { "1", "  hello  " } }
		);
		var right = new DataTable(
			new[] { "ID", "Name" },
			new List<IReadOnlyList<string>> { new[] { "1", "hello" } }
		);

		var engine = new DiffEngine();
		var withTrim = engine.Compare(
			left,
			right,
			options: new ComparisonOptions { TrimWhitespace = true }
		);
		var withoutTrim = engine.Compare(left, right);

		Assert.AreEqual(0, withTrim.Summary.ModifiedRows);
		Assert.AreEqual(1, withoutTrim.Summary.ModifiedRows);
	}

	[TestMethod]
	public void Compare_NumericTolerance_TreatsCloseValuesAsEqual()
	{
		// ID column provides stable 50%+ match so rows are always paired
		var left = new DataTable(
			new[] { "ID", "Price" },
			new List<IReadOnlyList<string>> { new[] { "1", "1.0" } }
		);
		var right = new DataTable(
			new[] { "ID", "Price" },
			new List<IReadOnlyList<string>> { new[] { "1", "1.0009" } } // diff = 0.0009
		);

		var engine = new DiffEngine();
		var withinTolerance = engine.Compare(
			left,
			right,
			options: new ComparisonOptions { NumericTolerance = 0.001 } // 0.0009 ≤ 0.001
		);
		var outsideTolerance = engine.Compare(
			left,
			right,
			options: new ComparisonOptions { NumericTolerance = 0.0001 } // 0.0009 > 0.0001
		);

		Assert.AreEqual(
			0,
			withinTolerance.Summary.ModifiedRows,
			"Should be unchanged within tolerance"
		);
		Assert.AreEqual(
			1,
			outsideTolerance.Summary.ModifiedRows,
			"Should be modified outside tolerance"
		);
	}

	[TestMethod]
	public void Compare_NumericTolerance_NonNumericValuesUseStringCompare()
	{
		var left = new DataTable(
			new[] { "Tag" },
			new List<IReadOnlyList<string>> { new[] { "alpha" } }
		);
		var right = new DataTable(
			new[] { "Tag" },
			new List<IReadOnlyList<string>> { new[] { "alpha" } }
		);

		var engine = new DiffEngine();
		var result = engine.Compare(
			left,
			right,
			options: new ComparisonOptions { NumericTolerance = 0.5 }
		);

		Assert.AreEqual(1, result.Summary.UnchangedRows);
	}

	[TestMethod]
	public void Compare_CustomMatchThreshold_AffectsRowMatching()
	{
		// Row with 1 of 4 columns matching = 25% match score.
		// Default threshold 0.5 would NOT match them; lowered to 0.2 should match.
		var left = new DataTable(
			new[] { "A", "B", "C", "D" },
			new List<IReadOnlyList<string>> { new[] { "same", "x", "y", "z" } }
		);
		var right = new DataTable(
			new[] { "A", "B", "C", "D" },
			new List<IReadOnlyList<string>> { new[] { "same", "1", "2", "3" } }
		);

		var engine = new DiffEngine();
		var defaultThreshold = engine.Compare(left, right); // 1/4 = 25% < 50% → not matched
		var lowThreshold = engine.Compare(
			left,
			right,
			options: new ComparisonOptions { MatchThreshold = 0.2 } // 25% ≥ 20% → matched (modified)
		);

		Assert.AreEqual(1, defaultThreshold.Summary.AddedRows);
		Assert.AreEqual(1, defaultThreshold.Summary.RemovedRows);
		Assert.AreEqual(1, lowThreshold.Summary.ModifiedRows);
	}

	[TestMethod]
	public void Compare_CaseInsensitiveKeyColumns_MatchesRowsRegardlessOfCase()
	{
		var left = new DataTable(
			new[] { "ID", "Value" },
			new List<IReadOnlyList<string>> { new[] { "abc", "100" }, new[] { "def", "200" } }
		);
		var right = new DataTable(
			new[] { "ID", "Value" },
			new List<IReadOnlyList<string>>
			{
				new[] { "ABC", "100" }, // key differs in case only
				new[] { "DEF", "999" }, // key differs in case, value changed
			}
		);
		var keyColumns = new[] { "ID" };

		var engine = new DiffEngine();
		var sensitive = engine.Compare(left, right, keyColumns: keyColumns);
		var insensitive = engine.Compare(
			left,
			right,
			keyColumns: keyColumns,
			options: new ComparisonOptions { CaseSensitive = false }
		);

		// Case-sensitive: no key matches → all added/removed
		Assert.AreEqual(2, sensitive.Summary.AddedRows);
		Assert.AreEqual(2, sensitive.Summary.RemovedRows);

		// Case-insensitive: keys match; abc+ABC unchanged on value, def+DEF value changed
		Assert.AreEqual(1, insensitive.Summary.UnchangedRows);
		Assert.AreEqual(1, insensitive.Summary.ModifiedRows);
	}

	// ── Inverted content-index path ───────────────────────────────────────────

	[TestMethod]
	public void Compare_ContentIndex_SameResultAsLinearScan_DefaultOptions()
	{
		// Large-ish table exercised without key columns so the inverted-index path
		// (NumericTolerance == 0.0, the default) is taken.
		var headers = new[] { "ID", "Name", "Score" };
		var leftRows = new List<IReadOnlyList<string>>
		{
			new[] { "1", "Alice", "90" },
			new[] { "2", "Bob", "80" },
			new[] { "3", "Carol", "70" },
			new[] { "4", "Dave", "60" },
		};
		var rightRows = new List<IReadOnlyList<string>>
		{
			new[] { "1", "Alice", "90" }, // unchanged
			new[] { "2", "Bob", "85" }, // modified Score
			new[] { "5", "Eve", "55" }, // added (new ID)
		};

		var left = new DataTable(headers, leftRows);
		var right = new DataTable(headers, rightRows);

		var engine = new DiffEngine();
		// Without key columns → inverted-index path (default options: NumericTolerance = 0.0)
		var result = engine.Compare(left, right);

		Assert.AreEqual(1, result.Summary.UnchangedRows); // ID 1
		Assert.AreEqual(1, result.Summary.ModifiedRows); // ID 2
		Assert.AreEqual(1, result.Summary.AddedRows); // ID 5
		// IDs 3 & 4 are unmatched left rows → Removed
		Assert.AreEqual(2, result.Summary.RemovedRows);
	}

	[TestMethod]
	public void Compare_ContentIndex_NormalizesNumericStrings()
	{
		// "1.0" and "1" are numerically equal (default NumericTolerance = 0.0).
		// The inverted index must hash them to the same key so the rows are matched.
		var left = new DataTable(
			new[] { "ID", "Val" },
			new List<IReadOnlyList<string>> { new[] { "1.0", "hello" } }
		);
		var right = new DataTable(
			new[] { "ID", "Val" },
			new List<IReadOnlyList<string>> { new[] { "1", "hello" } }
		);

		var engine = new DiffEngine();
		var result = engine.Compare(left, right); // no key columns → inverted-index path

		// Both cells compare as equal under NumericTolerance=0.0 → row should be Unchanged
		Assert.AreEqual(1, result.Summary.UnchangedRows);
		Assert.AreEqual(0, result.Summary.AddedRows);
		Assert.AreEqual(0, result.Summary.RemovedRows);
	}

	[TestMethod]
	public void Compare_PositiveNumericTolerance_FallsBackToLinearScan()
	{
		// With NumericTolerance > 0 the inverted-index cannot be used; the engine
		// must fall back to the O(n²) scan and still produce the correct result.
		var left = new DataTable(
			new[] { "ID", "Price" },
			new List<IReadOnlyList<string>> { new[] { "1", "10.00" }, new[] { "2", "20.00" } }
		);
		var right = new DataTable(
			new[] { "ID", "Price" },
			new List<IReadOnlyList<string>>
			{
				new[] { "1", "10.005" }, // within 0.01 tolerance → unchanged
				new[] { "2", "21.00" }, // outside 0.01 tolerance → modified
			}
		);

		var engine = new DiffEngine();
		var result = engine.Compare(
			left,
			right,
			options: new ComparisonOptions { NumericTolerance = 0.01 }
		);

		Assert.AreEqual(1, result.Summary.UnchangedRows); // row 1
		Assert.AreEqual(1, result.Summary.ModifiedRows); // row 2
	}
}
