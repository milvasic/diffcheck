using System.Globalization;
using DiffCheck.Models;

namespace DiffCheck.Core.Tests.TestData;

/// <summary>
/// Deterministic test fixture data generator used by snapshot tests and benchmarks.
/// The benchmark project compiles this source file directly via a linked Compile item.
/// </summary>
public static class MockTestData
{
	private static readonly string[] Headers = [.. Enumerable.Range(1, 10).Select(i => $"C{i}")];

	public static (DataTable Left, DataTable Right) BuildDataSet(int rowCount)
	{
		var leftRows = new List<IReadOnlyList<string>>(rowCount);
		var rightRows = new List<IReadOnlyList<string>>(rowCount);

		var removedStart = rowCount * 9 / 10;
		for (var i = 0; i < rowCount; i++)
		{
			var id = i + 1;
			var leftRow = CreateBaseRow(id);
			leftRows.Add(leftRow);

			if (i >= removedStart)
				continue;

			var rightRow = i % 5 == 0 ? CreateModifiedRow(leftRow) : leftRow;
			rightRows.Add(rightRow);
		}

		var addedCount = rowCount - removedStart;
		for (var i = 0; i < addedCount; i++)
		{
			var id = rowCount + i + 1;
			rightRows.Add(CreateBaseRow(id));
		}

		return (new DataTable(Headers, leftRows), new DataTable(Headers, rightRows));
	}

	private static string[] CreateBaseRow(int id)
	{
		return
		[
			id.ToString(CultureInfo.InvariantCulture),
			$"Name_{id % 1000}",
			$"Category_{id % 50}",
			$"Region_{id % 20}",
			(id * 13 % 10000).ToString(CultureInfo.InvariantCulture),
			(id * 0.125).ToString("0.000", CultureInfo.InvariantCulture),
			$"Status_{id % 7}",
			$"Code_{id % 97}",
			$"Flag_{id % 2}",
			$"Notes_{id % 23}",
		];
	}

	private static string[] CreateModifiedRow(string[] baseRow)
	{
		return
		[
			baseRow[0],
			baseRow[1],
			baseRow[2],
			baseRow[3],
			baseRow[4],
			baseRow[5],
			baseRow[6],
			baseRow[7],
			baseRow[8],
			baseRow[9] + "_changed",
		];
	}
}
