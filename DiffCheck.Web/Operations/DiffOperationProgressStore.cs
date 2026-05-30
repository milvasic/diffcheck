using System.Collections.Concurrent;
using DiffCheck.Models;

namespace DiffCheck.Web.Operations;

public sealed class DiffOperationProgressStore
{
	private static readonly TimeSpan Retention = TimeSpan.FromMinutes(10);
	private readonly ConcurrentDictionary<string, ProgressState> _states = new();
	private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();

	public CancellationToken Start(string operationId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
		PruneExpired();
		var cts = new CancellationTokenSource();
		if (_cancellations.TryRemove(operationId, out var old))
			old.Dispose();
		_cancellations[operationId] = cts;
		_states[operationId] = new ProgressState(
			new DiffOperationProgress(DiffOperationStage.Starting, 0, "Preparing comparison"),
			false,
			false,
			false,
			null,
			null,
			DateTime.UtcNow
		);
		return cts.Token;
	}

	public bool RequestCancel(string operationId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
		if (_cancellations.TryGetValue(operationId, out var cts))
		{
			try { cts.Cancel(); }
			catch (ObjectDisposedException) { return false; }
			return true;
		}
		return false;
	}

	public void Cancel(string operationId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
		PruneExpired();
		var warningMessage = _states.TryGetValue(operationId, out var existing)
			? existing.WarningMessage
			: null;
		_states[operationId] = new ProgressState(
			new DiffOperationProgress(DiffOperationStage.Completed, 0, "Cancelled"),
			true,
			false,
			true,
			null,
			warningMessage,
			DateTime.UtcNow
		);
		if (_cancellations.TryRemove(operationId, out var cts))
			cts.Dispose();
	}

	public void Report(string operationId, DiffOperationProgress progress)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
		PruneExpired();
		var existingWarning = _states.TryGetValue(operationId, out var existing)
			? existing.WarningMessage
			: null;
		var warningMessage = progress.WarningMessage ?? existingWarning;
		_states[operationId] = new ProgressState(
			progress,
			progress.Stage == DiffOperationStage.Completed,
			false,
			false,
			null,
			warningMessage,
			DateTime.UtcNow
		);
		if (progress.Stage == DiffOperationStage.Completed &&
			_cancellations.TryRemove(operationId, out var cts))
			cts.Dispose();
	}

	public void Fail(string operationId, string error)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
		ArgumentNullException.ThrowIfNull(error);
		PruneExpired();
		var warningMessage = _states.TryGetValue(operationId, out var existing)
			? existing.WarningMessage
			: null;
		_states[operationId] = new ProgressState(
			new DiffOperationProgress(DiffOperationStage.Completed, 100, "Comparison failed"),
			true,
			true,
			false,
			error,
			warningMessage,
			DateTime.UtcNow
		);
		if (_cancellations.TryRemove(operationId, out var cts))
			cts.Dispose();
	}

	public bool TryGet(string operationId, out DiffOperationStatus status)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
		PruneExpired();

		if (_states.TryGetValue(operationId, out var state))
		{
			status = new DiffOperationStatus(
				state.Progress.Stage.ToString(),
				state.Progress.Percent,
				state.Progress.Message,
				state.IsCompleted,
				state.IsFailed,
				state.IsCancelled,
				state.Error,
				state.WarningMessage
			);
			return true;
		}

		status = default;
		return false;
	}

	private void PruneExpired()
	{
		var cutoff = DateTime.UtcNow - Retention;
		foreach (var pair in _states)
		{
			if (pair.Value.UpdatedAtUtc < cutoff)
			{
				_states.TryRemove(pair.Key, out _);
				if (_cancellations.TryRemove(pair.Key, out var cts))
					cts.Dispose();
			}
		}
	}

	private readonly record struct ProgressState(
		DiffOperationProgress Progress,
		bool IsCompleted,
		bool IsFailed,
		bool IsCancelled,
		string? Error,
		string? WarningMessage,
		DateTime UpdatedAtUtc
	);
}

public readonly record struct DiffOperationStatus(
	string Stage,
	int Percent,
	string Message,
	bool IsCompleted,
	bool IsFailed,
	bool IsCancelled,
	string? Error,
	string? WarningMessage
);
