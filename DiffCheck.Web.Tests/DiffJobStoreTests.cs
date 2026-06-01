using DiffCheck.Web.Operations;

namespace DiffCheck.Web.Tests;

[TestClass]
public class DiffJobStoreTests
{
	[TestMethod]
	public void TryCreate_WhenUnderLimit_ReturnsTrueWithJobId()
	{
		var store = new DiffJobStore();

		var result = store.TryCreate("label", out var jobId);

		Assert.IsTrue(result);
		Assert.IsFalse(string.IsNullOrWhiteSpace(jobId));
	}

	[TestMethod]
	public void TryCreate_WhenAtMaxConcurrentJobs_ReturnsFalse()
	{
		var store = new DiffJobStore();
		for (int i = 0; i < 5; i++)
			store.TryCreate($"job {i}", out _);

		var result = store.TryCreate("overflow", out var jobId);

		Assert.IsFalse(result);
		Assert.AreEqual(string.Empty, jobId);
	}

	[TestMethod]
	public void TryCreate_AfterJobCompletes_AllowsNewJob()
	{
		var store = new DiffJobStore();
		store.TryCreate("first", out var firstJobId);
		for (int i = 0; i < 4; i++)
			store.TryCreate($"filler {i}", out _);
		Assert.IsFalse(store.TryCreate("overflow", out _), "Should be at limit");

		store.Complete(firstJobId, "<html/>", null);

		Assert.IsTrue(store.TryCreate("new job", out var newJobId));
		Assert.IsFalse(string.IsNullOrWhiteSpace(newJobId));
	}

	[TestMethod]
	public void TryCreate_AfterJobFails_AllowsNewJob()
	{
		var store = new DiffJobStore();
		store.TryCreate("first", out var firstJobId);
		for (int i = 0; i < 4; i++)
			store.TryCreate($"filler {i}", out _);
		Assert.IsFalse(store.TryCreate("overflow", out _), "Should be at limit");

		store.Fail(firstJobId, "error");

		Assert.IsTrue(store.TryCreate("new job", out var newJobId));
		Assert.IsFalse(string.IsNullOrWhiteSpace(newJobId));
	}

	[TestMethod]
	public void TryCreate_EachJob_ReceivesUniqueId()
	{
		var store = new DiffJobStore();
		var ids = new HashSet<string>();

		for (int i = 0; i < 5; i++)
		{
			store.TryCreate($"job {i}", out var id);
			Assert.IsTrue(ids.Add(id), "Duplicate job ID generated");
		}
	}

	[TestMethod]
	public void TryCreate_NewJob_HasPendingStatusAndQueuedMessage()
	{
		var store = new DiffJobStore();
		store.TryCreate("my label", out var jobId);

		Assert.IsTrue(store.TryGetStatus(jobId, out var result));
		Assert.AreEqual("pending", result!.Status);
		Assert.AreEqual("my label", result.Label);
		Assert.AreEqual("Queued", result.Message);
		Assert.AreEqual(0, result.Percent);
		Assert.IsNull(result.Error);
		Assert.IsNull(result.Html);
	}

	[TestMethod]
	public void Start_UpdatesStatusToRunning()
	{
		var store = new DiffJobStore();
		store.TryCreate("label", out var jobId);

		store.Start(jobId);

		Assert.IsTrue(store.TryGetStatus(jobId, out var result));
		Assert.AreEqual("running", result!.Status);
		Assert.AreEqual("Starting...", result.Message);
		Assert.AreEqual(0, result.Percent);
	}

	[TestMethod]
	public void UpdateProgress_UpdatesPercentAndMessage()
	{
		var store = new DiffJobStore();
		store.TryCreate("label", out var jobId);
		store.Start(jobId);

		store.UpdateProgress(jobId, 50, "Halfway there");

		Assert.IsTrue(store.TryGetStatus(jobId, out var result));
		Assert.AreEqual(50, result!.Percent);
		Assert.AreEqual("Halfway there", result.Message);
	}

	[TestMethod]
	public void Complete_SetsStatusToDoneWith100PercentAndHtml()
	{
		var store = new DiffJobStore();
		store.TryCreate("label", out var jobId);
		store.Start(jobId);

		store.Complete(jobId, "<html>result</html>", null);

		Assert.IsTrue(store.TryGetStatus(jobId, out var result));
		Assert.AreEqual("done", result!.Status);
		Assert.AreEqual(100, result.Percent);
		Assert.AreEqual("<html>result</html>", result.Html);
		Assert.IsNull(result.Error);
	}

	[TestMethod]
	public void Complete_WithWarning_SetsWarningMessage()
	{
		var store = new DiffJobStore();
		store.TryCreate("label", out var jobId);

		store.Complete(jobId, "<html/>", "Large dataset warning");

		Assert.IsTrue(store.TryGetStatus(jobId, out var result));
		Assert.AreEqual("Large dataset warning", result!.WarningMessage);
	}

	[TestMethod]
	public void Fail_SetsStatusToFailedWithError()
	{
		var store = new DiffJobStore();
		store.TryCreate("label", out var jobId);
		store.Start(jobId);

		store.Fail(jobId, "Comparison failed. Reference: abc123");

		Assert.IsTrue(store.TryGetStatus(jobId, out var result));
		Assert.AreEqual("failed", result!.Status);
		Assert.AreEqual(100, result.Percent);
		Assert.AreEqual("Comparison failed. Reference: abc123", result.Error);
		Assert.IsNull(result.Html);
	}

	[TestMethod]
	public void TryGetStatus_HtmlNotReturnedForPendingOrRunning()
	{
		var store = new DiffJobStore();
		store.TryCreate("label", out var pendingId);
		store.TryCreate("label2", out var runningId);
		store.Start(runningId);

		Assert.IsTrue(store.TryGetStatus(pendingId, out var pending));
		Assert.IsNull(pending!.Html);

		Assert.IsTrue(store.TryGetStatus(runningId, out var running));
		Assert.IsNull(running!.Html);
	}

	[TestMethod]
	public void TryGetStatus_UnknownJobId_ReturnsFalse()
	{
		var store = new DiffJobStore();

		Assert.IsFalse(store.TryGetStatus("unknown-id", out var result));
		Assert.IsNull(result);
	}

	[TestMethod]
	public void GetAll_ReturnsAllCreatedJobs()
	{
		var store = new DiffJobStore();
		store.TryCreate("first", out var id1);
		store.TryCreate("second", out var id2);
		store.TryCreate("third", out var id3);

		var all = store.GetAll();
		var ids = all.Select(j => j.Id).ToHashSet();

		Assert.AreEqual(3, all.Count);
		Assert.IsTrue(ids.Contains(id1));
		Assert.IsTrue(ids.Contains(id2));
		Assert.IsTrue(ids.Contains(id3));
	}

	[TestMethod]
	public void GetAll_WhenEmpty_ReturnsEmptyList()
	{
		var store = new DiffJobStore();

		Assert.AreEqual(0, store.GetAll().Count);
	}

	[TestMethod]
	public void GetOwnerToken_ReturnsNonEmptyTokenForNewJob()
	{
		var store = new DiffJobStore();
		store.TryCreate("label", out var jobId);

		var token = store.GetOwnerToken(jobId);

		Assert.IsFalse(string.IsNullOrWhiteSpace(token));
	}

	[TestMethod]
	public void TryRemoveOwned_WithCorrectToken_RemovesJob()
	{
		var store = new DiffJobStore();
		store.TryCreate("label", out var jobId);
		var ownerToken = store.GetOwnerToken(jobId);

		var removed = store.TryRemoveOwned(jobId, ownerToken);

		Assert.IsTrue(removed);
		Assert.IsFalse(store.TryGetStatus(jobId, out _));
	}

	[TestMethod]
	public void TryRemoveOwned_WithWrongToken_DoesNotRemoveJob()
	{
		var store = new DiffJobStore();
		store.TryCreate("label", out var jobId);

		var removed = store.TryRemoveOwned(jobId, "wrong-token");

		Assert.IsFalse(removed);
		Assert.IsTrue(store.TryGetStatus(jobId, out _));
	}

	[TestMethod]
	public void TryRemoveOwned_WithNullToken_DoesNotRemoveJob()
	{
		var store = new DiffJobStore();
		store.TryCreate("label", out var jobId);

		var removed = store.TryRemoveOwned(jobId, null);

		Assert.IsFalse(removed);
		Assert.IsTrue(store.TryGetStatus(jobId, out _));
	}

	[TestMethod]
	public void GetCancellationToken_CanceledByRemove_TokenIsCanceled()
	{
		var store = new DiffJobStore();
		store.TryCreate("label", out var jobId);
		var ct = store.GetCancellationToken(jobId);

		store.Remove(jobId);

		Assert.IsTrue(ct.IsCancellationRequested);
	}

	[TestMethod]
	public void GetCancellationToken_NotCanceledBeforeRemove_TokenIsNotCanceled()
	{
		var store = new DiffJobStore();
		store.TryCreate("label", out var jobId);

		var ct = store.GetCancellationToken(jobId);

		Assert.IsFalse(ct.IsCancellationRequested);
	}
}
