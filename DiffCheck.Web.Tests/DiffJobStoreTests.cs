using DiffCheck.Web.Operations;

namespace DiffCheck.Web.Tests;

[TestClass]
public class DiffJobStoreTests
{
	[TestMethod]
	public void TryCreate_StoresExplicitLeftAndRightFilenames()
	{
		var store = new DiffJobStore();

		Assert.IsTrue(
			store.TryCreate("left.csv vs right.csv", "left.csv", "right.csv", out var jobId)
		);
		Assert.IsTrue(store.TryGetStatus(jobId, out var status));
		Assert.IsNotNull(status);
		Assert.AreEqual("left.csv", status!.LeftFileName);
		Assert.AreEqual("right.csv", status.RightFileName);
	}

	[TestMethod]
	public void TryCreate_EnforcesMaxConcurrentJobsUnderConcurrentCallers()
	{
		const int maxConcurrent = 5;
		const int callers = 50;

		var store = new DiffJobStore();
		var created = 0;
		var ready = new ManualResetEventSlim();
		var threads = new List<Thread>();

		for (int i = 0; i < callers; i++)
		{
			var t = new Thread(() =>
			{
				ready.Wait();
				if (store.TryCreate("a vs b", "a", "b", out _))
					Interlocked.Increment(ref created);
			});
			threads.Add(t);
			t.Start();
		}

		ready.Set();
		foreach (var t in threads)
			t.Join();

		Assert.AreEqual(
			maxConcurrent,
			created,
			"TryCreate must not let concurrent callers exceed MaxConcurrentJobs."
		);
	}

	[TestMethod]
	public void Complete_FreesSlotForNewJob()
	{
		var store = new DiffJobStore();
		var ids = new List<string>();
		for (int i = 0; i < 5; i++)
		{
			Assert.IsTrue(store.TryCreate("a vs b", "a", "b", out var id));
			ids.Add(id);
		}
		Assert.IsFalse(store.TryCreate("c vs d", "c", "d", out _));

		store.Complete(ids[0], "<html/>", null);

		Assert.IsTrue(store.TryCreate("c vs d", "c", "d", out _));
	}

	[TestMethod]
	public void Fail_FreesSlotForNewJob()
	{
		var store = new DiffJobStore();
		var ids = new List<string>();
		for (int i = 0; i < 5; i++)
		{
			Assert.IsTrue(store.TryCreate("a vs b", "a", "b", out var id));
			ids.Add(id);
		}
		Assert.IsFalse(store.TryCreate("c vs d", "c", "d", out _));

		store.Fail(ids[0], "boom");

		Assert.IsTrue(store.TryCreate("c vs d", "c", "d", out _));
	}

	[TestMethod]
	public void TryGetStatus_ReturnsHtmlOnFirstReadOnly()
	{
		var store = new DiffJobStore();
		Assert.IsTrue(store.TryCreate("a vs b", "a", "b", out var jobId));
		store.Start(jobId);
		store.Complete(jobId, "<html>diff</html>", null);

		Assert.IsTrue(store.TryGetStatus(jobId, out var first));
		Assert.AreEqual("<html>diff</html>", first!.Html);
		Assert.AreEqual("done", first.Status);

		Assert.IsTrue(store.TryGetStatus(jobId, out var second));
		Assert.IsNull(second!.Html);
		Assert.AreEqual("done", second.Status);
	}

	[TestMethod]
	public void Fail_ExposesProvidedErrorMessage()
	{
		var store = new DiffJobStore();
		Assert.IsTrue(store.TryCreate("a vs b", "a", "b", out var jobId));

		store.Fail(jobId, "Comparison failed. Check server logs for details.");

		Assert.IsTrue(store.TryGetStatus(jobId, out var status));
		Assert.AreEqual("failed", status!.Status);
		Assert.AreEqual("Comparison failed. Check server logs for details.", status.Error);
	}
}
