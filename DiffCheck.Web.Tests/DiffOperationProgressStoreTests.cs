using DiffCheck.Models;
using DiffCheck.Web.Operations;

namespace DiffCheck.Web.Tests;

[TestClass]
public class DiffOperationProgressStoreTests
{
	[TestMethod]
	public void Start_ReturnsNonCancelledToken()
	{
		var store = new DiffOperationProgressStore();

		var token = store.Start("op1");

		Assert.IsFalse(token.IsCancellationRequested);
	}

	[TestMethod]
	public void RequestCancel_KnownOperation_CancelsTokenAndReturnsTrue()
	{
		var store = new DiffOperationProgressStore();
		var token = store.Start("op1");

		var result = store.RequestCancel("op1");

		Assert.IsTrue(result);
		Assert.IsTrue(token.IsCancellationRequested);
	}

	[TestMethod]
	public void RequestCancel_UnknownOperation_ReturnsFalse()
	{
		var store = new DiffOperationProgressStore();

		var result = store.RequestCancel("unknown");

		Assert.IsFalse(result);
	}

	[TestMethod]
	public void Cancel_SetsIsCancelledAndIsCompleted()
	{
		var store = new DiffOperationProgressStore();
		store.Start("op1");

		store.Cancel("op1");

		Assert.IsTrue(store.TryGet("op1", out var status));
		Assert.IsTrue(status.IsCancelled);
		Assert.IsTrue(status.IsCompleted);
		Assert.IsFalse(status.IsFailed);
		Assert.IsNull(status.Error);
	}

	[TestMethod]
	public void Cancel_PreservesWarningMessageFromPriorReport()
	{
		var store = new DiffOperationProgressStore();
		store.Start("op1");
		store.Report(
			"op1",
			new DiffOperationProgress(DiffOperationStage.Comparing, 50, "Working", "Large dataset")
		);

		store.Cancel("op1");

		Assert.IsTrue(store.TryGet("op1", out var status));
		Assert.IsTrue(status.IsCancelled);
		Assert.AreEqual("Large dataset", status.WarningMessage);
	}

	[TestMethod]
	public void Start_ReplacesExistingOperation_NewTokenIsUnCancelled()
	{
		var store = new DiffOperationProgressStore();
		var firstToken = store.Start("op1");
		store.RequestCancel("op1");
		Assert.IsTrue(firstToken.IsCancellationRequested);

		var secondToken = store.Start("op1");

		Assert.IsFalse(secondToken.IsCancellationRequested);
	}

	[TestMethod]
	public void TryGet_UnknownOperation_ReturnsFalse()
	{
		var store = new DiffOperationProgressStore();

		var found = store.TryGet("unknown", out _);

		Assert.IsFalse(found);
	}

	[TestMethod]
	public void TryGet_AfterStart_ReturnsStartingState()
	{
		var store = new DiffOperationProgressStore();
		store.Start("op1");

		Assert.IsTrue(store.TryGet("op1", out var status));
		Assert.AreEqual("Starting", status.Stage);
		Assert.IsFalse(status.IsCompleted);
		Assert.IsFalse(status.IsFailed);
		Assert.IsFalse(status.IsCancelled);
	}

	[TestMethod]
	public void Report_UpdatesProgressState()
	{
		var store = new DiffOperationProgressStore();
		store.Start("op1");

		store.Report(
			"op1",
			new DiffOperationProgress(DiffOperationStage.Comparing, 60, "Comparing rows")
		);

		Assert.IsTrue(store.TryGet("op1", out var status));
		Assert.AreEqual("Comparing", status.Stage);
		Assert.AreEqual(60, status.Percent);
		Assert.AreEqual("Comparing rows", status.Message);
		Assert.IsFalse(status.IsCompleted);
		Assert.IsFalse(status.IsCancelled);
	}

	[TestMethod]
	public void Fail_SetsIsFailedWithError()
	{
		var store = new DiffOperationProgressStore();
		store.Start("op1");

		store.Fail("op1", "Something went wrong");

		Assert.IsTrue(store.TryGet("op1", out var status));
		Assert.IsTrue(status.IsFailed);
		Assert.IsTrue(status.IsCompleted);
		Assert.IsFalse(status.IsCancelled);
		Assert.AreEqual("Something went wrong", status.Error);
	}
}
