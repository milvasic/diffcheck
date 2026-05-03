using DiffCheck.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddUploadLimits().AddProfiles();
builder.Services.AddRazorPages();
builder.Services.AddScoped<DiffCheck.DiffCheckService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
	app.UseExceptionHandler("/Error");

app.UseRouting().UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
