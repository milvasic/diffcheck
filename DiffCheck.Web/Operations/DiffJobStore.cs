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

	public bool TryCreate(string label, out string jobId)
	{
		PruneExpired();
		var activeCount = _jobs.Values.Count(j =>
			j.Status is DiffJobStatus.Pending or DiffJobStatus.Running
		);
		if (activeCount >= MaxConcurrentJobs)
		{
			jobId = string.Empty;
			return false;
		}

		jobId = Guid.NewGuid().ToString("N");
		_jobs[jobId] = new JobEntry(
			jobId,
			label,
			DiffJobStatus.Pending,
			null,
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
			_jobs[jobId] = entry with
			{
				Status = DiffJobStatus.Done,
				Result = html,
				WarningMessage = warningMessage,
				Percent = 100,
				Message = "Complete",
				UpdatedAt = DateTime.UtcNow,
			};
	}

	public void Fail(string jobId, string error)
	{
		if (_jobs.TryGetValue(jobId, out var entry))
			_jobs[jobId] = entry with
			{
				Status = DiffJobStatus.Failed,
				Error = error,
				Percent = 100,
				Message = "Failed",
				UpdatedAt = DateTime.UtcNow,
			};
	}

	public bool TryGetStatus(string jobId, out JobStatusResult? result)
	{
		PruneExpired();
		if (!_jobs.TryGetValue(jobId, out var entry))
		{
			result = null;
			return false;
		}
		result = new JobStatusResult(
			entry.Id,
			entry.Label,
			entry.Status.ToString().ToLowerInvariant(),
			entry.Error,
			entry.WarningMessage,
			entry.Percent,
			entry.Message,
			entry.Status == DiffJobStatus.Done ? entry.Result : null
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
			if (pair.Value.UpdatedAt < cutoff)
				_jobs.TryRemove(pair.Key, out _);
		}
	}

	private sealed record JobEntry(
		string Id,
		string Label,
		DiffJobStatus Status,
		string? Result,
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
	string Status,
	string? Error,
	int Percent,
	string Message,
	string CreatedAtIso
);
