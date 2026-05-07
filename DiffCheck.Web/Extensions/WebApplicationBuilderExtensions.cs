using DiffCheck.Profiles;
using Microsoft.AspNetCore.Http.Features;

namespace DiffCheck.Web.Extensions;

public static class WebApplicationBuilderExtensions
{
	extension(WebApplicationBuilder builder)
	{
		public WebApplicationBuilder AddUploadLimits()
		{
			ArgumentNullException.ThrowIfNull(builder);

			var maxFileSizeMb = 25;
			if (
				Environment.GetEnvironmentVariable("DIFFCHECK_MAX_FILE_SIZE_MB") is { } envValue
				&& int.TryParse(envValue, out var parsed)
				&& parsed > 0
			)
				maxFileSizeMb = parsed;

			var maxFileSizeBytes = (long)maxFileSizeMb * 1024 * 1024;
			var maxRequestSizeBytes = maxFileSizeBytes * 2 + 1024 * 1024; // two files + overhead

			builder.Services.AddSingleton(new UploadLimits(maxFileSizeBytes));
			builder.Services.Configure<FormOptions>(options =>
			{
				options.MultipartBodyLengthLimit = maxRequestSizeBytes;
			});
			builder.WebHost.ConfigureKestrel(options =>
			{
				options.Limits.MaxRequestBodySize = maxRequestSizeBytes;
			});

			return builder;
		}

		public WebApplicationBuilder AddProfiles()
		{
			ArgumentNullException.ThrowIfNull(builder);

			var profilesDir =
				Environment.GetEnvironmentVariable("DIFFCHECK_PROFILES_DIR")
				?? Path.Combine(builder.Environment.ContentRootPath, "profiles");
			builder.Services.AddSingleton(new ProfileStore(profilesDir));

			return builder;
		}

		public WebApplicationBuilder AddLongRunningDiffWarning()
		{
			ArgumentNullException.ThrowIfNull(builder);

			var settings =
				builder
					.Configuration.GetSection("LongRunningDiffWarning")
					.Get<LongRunningDiffWarningSettings>()
				?? new LongRunningDiffWarningSettings();

			if (
				Environment.GetEnvironmentVariable("DIFFCHECK_LONG_RUNNING_WARNING_ENABLED")
					is { } enabled
				&& bool.TryParse(enabled, out var parsedEnabled)
			)
				settings.Enabled = parsedEnabled;

			if (
				Environment.GetEnvironmentVariable("DIFFCHECK_LONG_RUNNING_DATA_AMOUNT_THRESHOLD")
					is { } threshold
				&& double.TryParse(
					threshold,
					System.Globalization.NumberStyles.Any,
					System.Globalization.CultureInfo.InvariantCulture,
					out var parsedThreshold
				)
			)
				settings.DataAmountThreshold = parsedThreshold;

			if (
				Environment.GetEnvironmentVariable("DIFFCHECK_LONG_RUNNING_THRESHOLD_FACTOR")
					is { } factor
				&& double.TryParse(
					factor,
					System.Globalization.NumberStyles.Any,
					System.Globalization.CultureInfo.InvariantCulture,
					out var parsedFactor
				)
			)
				settings.ThresholdFactor = parsedFactor;

			builder.Services.AddSingleton(settings);

			return builder;
		}
	}
}
