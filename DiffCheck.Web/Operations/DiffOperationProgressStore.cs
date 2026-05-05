using System.Collections.Concurrent;
using DiffCheck.Models;

namespace DiffCheck.Web.Operations;

public sealed class DiffOperationProgressStore
{
	private static readonly TimeSpan Retention = TimeSpan.FromMinutes(10);
	private readonly ConcurrentDictionary<string, ProgressState> _states = new();

	public void Start(string operationId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
		PruneExpired();
		_states[operationId] = new ProgressState(
			new DiffOperationProgress(DiffOperationStage.Starting, 0, "Preparing comparison"),
			IsCompleted: false,
			IsFailed: false,
			Error: null,
			UpdatedAtUtc: DateTime.UtcNow
		);
	}

	public void Report(string operationId, DiffOperationProgress progress)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
		PruneExpired();
		_states[operationId] = new ProgressState(
			progress,
			IsCompleted: progress.Stage == DiffOperationStage.Completed,
			IsFailed: false,
			Error: null,
			UpdatedAtUtc: DateTime.UtcNow
		);
	}

	public void Fail(string operationId, string error)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
		ArgumentNullException.ThrowIfNull(error);
		PruneExpired();
		_states[operationId] = new ProgressState(
			new DiffOperationProgress(DiffOperationStage.Completed, 100, "Comparison failed"),
			IsCompleted: true,
			IsFailed: true,
			Error: error,
			UpdatedAtUtc: DateTime.UtcNow
		);
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
				state.Error
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
				_states.TryRemove(pair.Key, out _);
		}
	}

	private readonly record struct ProgressState(
		DiffOperationProgress Progress,
		bool IsCompleted,
		bool IsFailed,
		string? Error,
		DateTime UpdatedAtUtc
	);
}

public readonly record struct DiffOperationStatus(
	string Stage,
	int Percent,
	string Message,
	bool IsCompleted,
	bool IsFailed,
	string? Error
);
