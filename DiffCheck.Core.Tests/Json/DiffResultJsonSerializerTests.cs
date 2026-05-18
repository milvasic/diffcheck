using System.Text.Json;
using DiffCheck.Json;
using DiffCheck.Models;

namespace DiffCheck.Core.Tests.Json;

[TestClass]
public class DiffResultJsonSerializerTests
{
    [TestMethod]
    public void Serialize_ValidResult_ReturnsValidJson()
    {
        var result = CreateDiffResult();

        var json = DiffResultJsonSerializer.Serialize(result);

        using var doc = JsonDocument.Parse(json);
        Assert.IsNotNull(doc);
    }

    [TestMethod]
    public void Serialize_ContainsSummaryBlock()
    {
        var result = CreateDiffResult();

        var json = DiffResultJsonSerializer.Serialize(result);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.IsTrue(root.TryGetProperty("summary", out var summary));
        Assert.AreEqual(1, summary.GetProperty("addedRows").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("removedRows").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("modifiedRows").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("unchangedRows").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("reorderedRows").GetInt32());
    }

    [TestMethod]
    public void Serialize_ContainsColumnsArray()
    {
        var result = CreateDiffResult();

        var json = DiffResultJsonSerializer.Serialize(result);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.IsTrue(root.TryGetProperty("columns", out var columns));
        Assert.AreEqual(2, columns.GetArrayLength());
        Assert.AreEqual("Id", columns[0].GetString());
        Assert.AreEqual("Name", columns[1].GetString());
    }

    [TestMethod]
    public void Serialize_ContainsRowsWithStatus()
    {
        var result = CreateDiffResult();

        var json = DiffResultJsonSerializer.Serialize(result);

        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.GetProperty("rows");
        Assert.AreEqual(4, rows.GetArrayLength());

        var statuses = Enumerable
            .Range(0, rows.GetArrayLength())
            .Select(i => rows[i].GetProperty("status").GetString())
            .ToList();

        Assert.IsTrue(statuses.Contains("added"));
        Assert.IsTrue(statuses.Contains("removed"));
        Assert.IsTrue(statuses.Contains("modified"));
        Assert.IsTrue(statuses.Contains("unchanged"));
    }

    [TestMethod]
    public void Serialize_ModifiedRow_ContainsCellDiff()
    {
        var result = CreateDiffResult();

        var json = DiffResultJsonSerializer.Serialize(result);

        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.GetProperty("rows");
        var modifiedRow = Enumerable
            .Range(0, rows.GetArrayLength())
            .Select(i => rows[i])
            .First(r => r.GetProperty("status").GetString() == "modified");

        var cells = modifiedRow.GetProperty("cells");
        Assert.AreEqual(2, cells.GetArrayLength());

        var nameCell = Enumerable
            .Range(0, cells.GetArrayLength())
            .Select(i => cells[i])
            .First(c => c.GetProperty("column").GetString() == "Name");

        Assert.AreEqual("modified", nameCell.GetProperty("status").GetString());
        Assert.AreEqual("Alice", nameCell.GetProperty("leftValue").GetString());
        Assert.AreEqual("Alicia", nameCell.GetProperty("rightValue").GetString());
    }

    [TestMethod]
    public void Serialize_AddedRow_HasNullLeftRowIndex()
    {
        var result = CreateDiffResult();

        var json = DiffResultJsonSerializer.Serialize(result);

        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.GetProperty("rows");
        var addedRow = Enumerable
            .Range(0, rows.GetArrayLength())
            .Select(i => rows[i])
            .First(r => r.GetProperty("status").GetString() == "added");

        Assert.AreEqual(JsonValueKind.Null, addedRow.GetProperty("leftRowIndex").ValueKind);
        Assert.AreEqual(3, addedRow.GetProperty("rightRowIndex").GetInt32());
    }

    [TestMethod]
    public async Task WriteToFileAsync_WritesJsonFile()
    {
        var result = CreateDiffResult();
        var path = Path.GetTempFileName();
        try
        {
            await DiffResultJsonSerializer.WriteToFileAsync(result, path);

            var content = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(content);
            Assert.IsTrue(doc.RootElement.TryGetProperty("summary", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static DiffResult CreateDiffResult() =>
        new(
            ["Id", "Name"],
            [
                new DiffRow(
                    1,
                    DiffRowStatus.Unchanged,
                    [
                        new DiffCell("Id", "1", "1", "1", DiffCellStatus.Unchanged),
                        new DiffCell("Name", "Alice", "Alice", "Alice", DiffCellStatus.Unchanged),
                    ],
                    leftRowIndex: 1,
                    rightRowIndex: 1
                ),
                new DiffRow(
                    2,
                    DiffRowStatus.Modified,
                    [
                        new DiffCell("Id", "2", "2", "2", DiffCellStatus.Unchanged),
                        new DiffCell("Name", "Alice", "Alicia", "Alice → Alicia", DiffCellStatus.Modified),
                    ],
                    leftRowIndex: 2,
                    rightRowIndex: 2
                ),
                new DiffRow(
                    3,
                    DiffRowStatus.Removed,
                    [
                        new DiffCell("Id", "3", null, "3", DiffCellStatus.Removed),
                        new DiffCell("Name", "Bob", null, "Bob", DiffCellStatus.Removed),
                    ],
                    leftRowIndex: 3,
                    rightRowIndex: null
                ),
                new DiffRow(
                    4,
                    DiffRowStatus.Added,
                    [
                        new DiffCell("Id", null, "4", "4", DiffCellStatus.Added),
                        new DiffCell("Name", null, "Carol", "Carol", DiffCellStatus.Added),
                    ],
                    leftRowIndex: null,
                    rightRowIndex: 3
                ),
            ],
            new DiffSummary(addedRows: 1, removedRows: 1, modifiedRows: 1, unchangedRows: 1),
            leftRowCount: 3,
            leftColumnCount: 2,
            rightRowCount: 3,
            rightColumnCount: 2
        );
}
