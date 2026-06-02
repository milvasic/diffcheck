using System.Collections.Concurrent;

namespace DiffCheck.Web.Operations;

public enum DiffJobStatus
{
	Pending,
	Running,
	Done,
	Failed,
}

public sealed class DiffJobStore
{
	private static readonly TimeSpan Retention = TimeSpan.FromMinutes(30);
	private const int MaxConcurrentJobs = 5;

	private readonly ConcurrentDictionary<string, JobEntry> _jobs = new();
	private int _activeCount;

	public bool TryCreate(string label, string leftFileName, string rightFileName, out string jobId)
	{
		PruneExpired();

		var next = Interlocked.Increment(ref _activeCount);
		if (next > MaxConcurrentJobs)
		{
			Interlocked.Decrement(ref _activeCount);
			jobId = string.Empty;
			return false;
		}

		jobId = Guid.NewGuid().ToString("N");
		_jobs[jobId] = new JobEntry(
			jobId,
			label,
			leftFileName,
			rightFileName,
			DiffJobStatus.Pending,
			null,
			false,
			null,
			null,
			0,
			"Queued",
			DateTime.UtcNow,
			DateTime.UtcNow
		);
		return true;
	}

	public void Start(string jobId)
	{
		if (_jobs.TryGetValue(jobId, out var entry))
			_jobs[jobId] = entry with
			{
				Status = DiffJobStatus.Running,
				Percent = 0,
				Message = "Starting...",
				UpdatedAt = DateTime.UtcNow,
			};
	}

	public void UpdateProgress(string jobId, int percent, string message)
	{
		if (_jobs.TryGetValue(jobId, out var entry))
			_jobs[jobId] = entry with
			{
				Percent = percent,
				Message = message,
				UpdatedAt = DateTime.UtcNow,
			};
	}

	public void Complete(string jobId, string html, string? warningMessage)
	{
		if (_jobs.TryGetValue(jobId, out var entry))
		{
			_jobs[jobId] = entry with
			{
				Status = DiffJobStatus.Done,
				Result = html,
				ResultRetrieved = false,
				WarningMessage = warningMessage,
				Percent = 100,
				Message = "Complete",
				UpdatedAt = DateTime.UtcNow,
			};
			Interlocked.Decrement(ref _activeCount);
		}
	}

	public void Fail(string jobId, string error)
	{
		if (_jobs.TryGetValue(jobId, out var entry))
		{
			_jobs[jobId] = entry with
			{
				Status = DiffJobStatus.Failed,
				Error = error,
				Percent = 100,
				Message = "Failed",
				UpdatedAt = DateTime.UtcNow,
			};
			Interlocked.Decrement(ref _activeCount);
		}
	}

	public bool TryGetStatus(string jobId, out JobStatusResult? result)
	{
		PruneExpired();
		if (!_jobs.TryGetValue(jobId, out var entry))
		{
			result = null;
			return false;
		}

		string? html = null;
		if (entry.Status == DiffJobStatus.Done && !entry.ResultRetrieved)
		{
			html = entry.Result;
			_jobs[jobId] = entry with
			{
				Result = null,
				ResultRetrieved = true,
				UpdatedAt = DateTime.UtcNow,
			};
		}

		result = new JobStatusResult(
			entry.Id,
			entry.Label,
			entry.LeftFileName,
			entry.RightFileName,
			entry.Status.ToString().ToLowerInvariant(),
			entry.Error,
			entry.WarningMessage,
			entry.Percent,
			entry.Message,
			html
		);
		return true;
	}

	public IReadOnlyList<JobListItem> GetAll()
	{
		PruneExpired();
		return
		[
			.. _jobs
				.Values.OrderByDescending(e => e.CreatedAt)
				.Select(e => new JobListItem(
					e.Id,
					e.Label,
					e.LeftFileName,
					e.RightFileName,
					e.Status.ToString().ToLowerInvariant(),
					e.Error,
					e.Percent,
					e.Message,
					e.CreatedAt.ToString("o")
				)),
		];
	}

	private void PruneExpired()
	{
		var cutoff = DateTime.UtcNow - Retention;
		foreach (var pair in _jobs)
		{
			if (pair.Value.UpdatedAt >= cutoff)
				continue;
			if (!_jobs.TryRemove(pair.Key, out var removed))
				continue;
			if (removed.Status is DiffJobStatus.Pending or DiffJobStatus.Running)
				Interlocked.Decrement(ref _activeCount);
		}
	}

	private sealed record JobEntry(
		string Id,
		string Label,
		string LeftFileName,
		string RightFileName,
		DiffJobStatus Status,
		string? Result,
		bool ResultRetrieved,
		string? Error,
		string? WarningMessage,
		int Percent,
		string Message,
		DateTime CreatedAt,
		DateTime UpdatedAt
	);
}

public sealed record JobStatusResult(
	string Id,
	string Label,
	string LeftFileName,
	string RightFileName,
	string Status,
	string? Error,
	string? WarningMessage,
	int Percent,
	string Message,
	string? Html
);

public sealed record JobListItem(
	string Id,
	string Label,
	string LeftFileName,
	string RightFileName,
	string Status,
	string? Error,
	int Percent,
	string Message,
	string CreatedAtIso
);
