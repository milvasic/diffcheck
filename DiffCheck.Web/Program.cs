using DiffCheck.Profiles;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

var maxFileSizeMb = 25;
if (
	Environment.GetEnvironmentVariable("DIFFCHECK_MAX_FILE_SIZE_MB") is { } envValue
	&& int.TryParse(envValue, out var parsed)
	&& parsed > 0
)
{
	maxFileSizeMb = parsed;
}

var maxFileSizeBytes = (long)maxFileSizeMb * 1024 * 1024;
var maxRequestSizeBytes = maxFileSizeBytes * 2 + 1024 * 1024; // two files + overhead

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddScoped<DiffCheck.DiffCheckService>();
builder.Services.AddSingleton(new DiffCheck.Web.UploadLimits(maxFileSizeBytes));

var profilesDir =
	Environment.GetEnvironmentVariable("DIFFCHECK_PROFILES_DIR")
	?? Path.Combine(builder.Environment.ContentRootPath, "profiles");
builder.Services.AddSingleton(new ProfileStore(profilesDir));
builder.Services.Configure<FormOptions>(options =>
{
	options.MultipartBodyLengthLimit = maxRequestSizeBytes;
});
builder.WebHost.ConfigureKestrel(options =>
{
	options.Limits.MaxRequestBodySize = maxRequestSizeBytes;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
