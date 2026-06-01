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
	private readonly ConcurrentDictionary<string, CancellationTokenSource> _cts = new();
	private readonly object _createLock = new();

	public bool TryCreate(string label, out string jobId)
	{
		PruneExpired();
		lock (_createLock)
		{
			var activeCount = _jobs.Values.Count(j =>
				j.Status is DiffJobStatus.Pending or DiffJobStatus.Running
			);
			if (activeCount >= MaxConcurrentJobs)
			{
				jobId = string.Empty;
				return false;
			}

			jobId = Guid.NewGuid().ToString("N");
			var ownerToken = Guid.NewGuid().ToString("N");
			var cts = new CancellationTokenSource();
			_cts[jobId] = cts;
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
				DateTime.UtcNow,
				ownerToken
			);
			return true;
		}
	}

	public CancellationToken GetCancellationToken(string jobId) =>
		_cts.TryGetValue(jobId, out var cts) ? cts.Token : CancellationToken.None;

	public string? GetOwnerToken(string jobId) =>
		_jobs.TryGetValue(jobId, out var entry) ? entry.OwnerToken : null;

	public bool TryRemoveOwned(string jobId, string? ownerToken)
	{
		if (string.IsNullOrWhiteSpace(ownerToken)) return false;
		if (!_jobs.TryGetValue(jobId, out var entry)) return false;
		if (entry.OwnerToken != ownerToken) return false;
		Remove(jobId);
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
		if (_cts.TryRemove(jobId, out var cts))
			cts.Dispose();
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

	public void Remove(string jobId)
	{
		if (_cts.TryRemove(jobId, out var cts))
		{
			cts.Cancel();
			cts.Dispose();
		}
		_jobs.TryRemove(jobId, out _);
	}

	public void Fail(string jobId, string error)
	{
		if (_cts.TryRemove(jobId, out var cts))
			cts.Dispose();
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
			{
				if (_jobs.TryRemove(pair.Key, out _) && _cts.TryRemove(pair.Key, out var cts))
				{
					cts.Cancel();
					cts.Dispose();
				}
			}
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
		DateTime UpdatedAt,
		string OwnerToken
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
