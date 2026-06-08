using DiffCheck.Web.Extensions;
using DiffCheck.Web.Operations;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddUploadLimits().AddLongRunningDiffWarning().AddProfiles();
builder.Services.AddRazorPages();
builder.Services.AddSingleton<DiffCheck.DiffCheckService>();
builder.Services.AddSingleton<DiffOperationProgressStore>();
builder.Services.AddSingleton<DiffJobStore>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
	app.UseExceptionHandler("/Error");

app.UseRouting().UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();

public partial class Program { }
