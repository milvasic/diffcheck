using DiffCheck.Models;

namespace DiffCheck.Core.Tests;

[TestClass]
public class LongRunningDiffWarningEvaluatorTests
{
	[TestMethod]
	public void Evaluate_NoKeyColumns_AboveThreshold_ReturnsWarning()
	{
		var left = new DataTable(
			["A", "B", "C"],
			[
				["1", "2", "3"],
				["4", "5", "6"],
			]
		);
		var right = new DataTable(
			["A", "B", "C"],
			[
				["1", "2", "3"],
				["4", "5", "6"],
			]
		);
		var options = new LongRunningDiffWarningOptions
		{
			DataAmountThreshold = 10,
			ThresholdFactor = 1.0,
		};

		var assessment = LongRunningDiffWarningEvaluator.Evaluate(
			left,
			right,
			keyColumns: null,
			options
		);

		Assert.IsTrue(assessment.ShouldWarn);
		Assert.AreEqual(12, assessment.EstimatedDataAmount);
		Assert.AreEqual(4, assessment.TotalRows);
		Assert.AreEqual(3, assessment.MaxColumns);
		Assert.IsFalse(assessment.HasDefinedKeyColumns);
	}

	[TestMethod]
	public void Evaluate_KeyColumnsDefined_AboveThreshold_DoesNotWarn()
	{
		var left = new DataTable(
			["ID", "A"],
			[
				["1", "x"],
				["2", "y"],
				["3", "z"],
			]
		);
		var right = new DataTable(
			["ID", "A"],
			[
				["1", "x"],
				["2", "y"],
				["4", "z"],
			]
		);
		var options = new LongRunningDiffWarningOptions
		{
			DataAmountThreshold = 5,
			ThresholdFactor = 1.0,
		};

		var assessment = LongRunningDiffWarningEvaluator.Evaluate(left, right, ["ID"], options);

		Assert.IsFalse(assessment.ShouldWarn);
		Assert.IsTrue(assessment.HasDefinedKeyColumns);
	}

	[TestMethod]
	public void Evaluate_NoKeyColumns_AtOrBelowThreshold_DoesNotWarn()
	{
		var left = new DataTable(
			["A", "B"],
			[
				["1", "2"],
			]
		);
		var right = new DataTable(
			["A", "B"],
			[
				["1", "2"],
			]
		);
		var options = new LongRunningDiffWarningOptions
		{
			DataAmountThreshold = 4,
			ThresholdFactor = 1.0,
		};

		var assessment = LongRunningDiffWarningEvaluator.Evaluate(
			left,
			right,
			keyColumns: null,
			options
		);

		Assert.IsFalse(assessment.ShouldWarn);
		Assert.AreEqual(4, assessment.EstimatedDataAmount);
	}
}
