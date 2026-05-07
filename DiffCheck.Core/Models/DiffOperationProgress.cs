namespace DiffCheck.Models;

/// <summary>
/// Named stages reported while a diff operation runs.
/// </summary>
public enum DiffOperationStage
{
	Starting,
	ReadingLeftFile,
	ReadingRightFile,
	Comparing,
	GeneratingReport,
	Completed,
}

/// <summary>
/// Progress payload for long-running diff operations.
/// </summary>
/// <param name="Stage">Current operation stage.</param>
/// <param name="Percent">Completion percentage in range 0..100.</param>
/// <param name="Message">Human-readable progress message.</param>
/// <param name="WarningMessage">Optional warning surfaced immediately during execution.</param>
public readonly record struct DiffOperationProgress(
	DiffOperationStage Stage,
	int Percent,
	string Message,
	string? WarningMessage = null
);
