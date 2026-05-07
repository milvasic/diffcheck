namespace DiffCheck.Web;

public sealed class LongRunningDiffWarningSettings
{
	public bool Enabled { get; set; } = true;

	public double DataAmountThreshold { get; set; } = 400000;

	public double ThresholdFactor { get; set; } = 1.0;
}
