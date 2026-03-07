namespace DiffCheck.Web;

public sealed class UploadLimits(long maxFileSizeBytes)
{
	public long MaxFileSizeBytes { get; } = maxFileSizeBytes;
}
