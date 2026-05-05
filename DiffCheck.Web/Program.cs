using DiffCheck.Web.Extensions;
using DiffCheck.Web.Operations;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddUploadLimits().AddProfiles();
builder.Services.AddRazorPages();
builder.Services.AddScoped<DiffCheck.DiffCheckService>();
builder.Services.AddSingleton<DiffOperationProgressStore>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
	app.UseExceptionHandler("/Error");

app.UseRouting().UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
