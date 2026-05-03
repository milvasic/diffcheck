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
	}
}
