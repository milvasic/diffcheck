using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using DiffCheck.Models;
using DiffCheck.Readers;
using DiffCheck.Web.Operations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DiffCheck.Web.Tests;

[TestClass]
public class IndexPageTests
{
	private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
	{
		var response = await client.GetAsync("/");
		response.EnsureSuccessStatusCode();
		var html = await response.Content.ReadAsStringAsync();
		var match = Regex.Match(
			html,
			@"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"""
		);
		return match.Groups[1].Value;
	}

	[TestMethod]
	public async Task OnPostCancel_UnknownOperationId_ReturnsCancelledFalse()
	{
		await using var factory = new WebApplicationFactory<Program>();
		var client = factory.CreateClient();

		var token = await GetAntiforgeryTokenAsync(client);

		var form = new MultipartFormDataContent();
		form.Add(new StringContent("unknown-op-id"), "operationId");
		form.Add(new StringContent(token), "__RequestVerificationToken");

		var response = await client.PostAsync("/?handler=Cancel", form);
		var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.IsFalse(json.RootElement.GetProperty("cancelled").GetBoolean());
	}

	[TestMethod]
	public async Task OnPostCancel_KnownOperationId_ReturnsCancelledTrue()
	{
		await using var factory = new WebApplicationFactory<Program>();
		var client = factory.CreateClient();

		var progressStore = factory.Services.GetRequiredService<DiffOperationProgressStore>();
		progressStore.Start("test-op-cancel");

		var token = await GetAntiforgeryTokenAsync(client);

		var form = new MultipartFormDataContent();
		form.Add(new StringContent("test-op-cancel"), "operationId");
		form.Add(new StringContent(token), "__RequestVerificationToken");

		var response = await client.PostAsync("/?handler=Cancel", form);
		var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.IsTrue(json.RootElement.GetProperty("cancelled").GetBoolean());
	}

	[TestMethod]
	public async Task OnPostCompare_WhenServiceThrowsOperationCanceled_ReturnsCancelledTrue()
	{
		await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
		{
			builder.ConfigureServices(services =>
			{
				var descriptor = services.SingleOrDefault(d =>
					d.ServiceType == typeof(DiffCheck.DiffCheckService)
				);
				if (descriptor != null)
					services.Remove(descriptor);
				services.AddScoped(_ => new DiffCheck.DiffCheckService(
					new CancellingFileReader(),
					new CancellingFileReader()
				));
			});
		});

		var client = factory.CreateClient();
		var token = await GetAntiforgeryTokenAsync(client);

		using var leftContent = new ByteArrayContent(
			System.Text.Encoding.UTF8.GetBytes("a,b\n1,2\n")
		);
		using var rightContent = new ByteArrayContent(
			System.Text.Encoding.UTF8.GetBytes("a,b\n1,2\n")
		);
		leftContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
		rightContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");

		var form = new MultipartFormDataContent();
		form.Add(leftContent, "leftFile", "left.csv");
		form.Add(rightContent, "rightFile", "right.csv");
		form.Add(new StringContent(token), "__RequestVerificationToken");

		var response = await client.PostAsync("/?handler=Compare", form);
		var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.IsTrue(json.RootElement.GetProperty("cancelled").GetBoolean());
	}

	[TestMethod]
	public async Task OnPostStartJob_SinglePair_CreatesJobAndReturnsJobId()
	{
		await using var factory = new WebApplicationFactory<Program>();
		var client = factory.CreateClient();
		var token = await GetAntiforgeryTokenAsync(client);

		using var leftContent = new ByteArrayContent(
			System.Text.Encoding.UTF8.GetBytes("a,b\n1,2\n")
		);
		using var rightContent = new ByteArrayContent(
			System.Text.Encoding.UTF8.GetBytes("a,b\n1,2\n")
		);
		leftContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
		rightContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");

		var form = new MultipartFormDataContent();
		form.Add(leftContent, "leftFile", "left.csv");
		form.Add(rightContent, "rightFile", "right.csv");
		form.Add(new StringContent(token), "__RequestVerificationToken");

		var response = await client.PostAsync("/?handler=StartJob", form);
		var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.IsTrue(json.RootElement.TryGetProperty("jobId", out var jobIdProp));
		Assert.IsFalse(string.IsNullOrWhiteSpace(jobIdProp.GetString()));
		Assert.AreEqual("left.csv vs right.csv", json.RootElement.GetProperty("label").GetString());
	}

	[TestMethod]
	public async Task OnPostStartJob_MultipleBulkPairs_EachPairCreatesOwnJob()
	{
		await using var factory = new WebApplicationFactory<Program>();
		var client = factory.CreateClient();
		var token = await GetAntiforgeryTokenAsync(client);

		var jobIds = new List<string>();

		for (int i = 1; i <= 3; i++)
		{
			using var leftContent = new ByteArrayContent(
				System.Text.Encoding.UTF8.GetBytes("a,b\n1,2\n")
			);
			using var rightContent = new ByteArrayContent(
				System.Text.Encoding.UTF8.GetBytes("a,b\n3,4\n")
			);
			leftContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
			rightContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");

			var form = new MultipartFormDataContent();
			form.Add(leftContent, "leftFile", $"left{i}.csv");
			form.Add(rightContent, "rightFile", $"right{i}.csv");
			form.Add(new StringContent(token), "__RequestVerificationToken");

			var response = await client.PostAsync("/?handler=StartJob", form);
			var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

			Assert.IsTrue(json.RootElement.TryGetProperty("jobId", out var jobIdProp));
			var jobId = jobIdProp.GetString()!;
			Assert.IsFalse(string.IsNullOrWhiteSpace(jobId));
			Assert.IsFalse(jobIds.Contains(jobId), "Each pair should receive a unique job ID");
			jobIds.Add(jobId);
		}

		Assert.AreEqual(3, jobIds.Distinct().Count());
	}

	[TestMethod]
	public async Task OnPostStartJob_ExceedsMaxConcurrentJobs_ReturnsError()
	{
		await using var factory = new WebApplicationFactory<Program>();
		var client = factory.CreateClient();
		var token = await GetAntiforgeryTokenAsync(client);

		// Pre-fill the job store to its limit (MaxConcurrentJobs = 5)
		var jobStore = factory.Services.GetRequiredService<DiffJobStore>();
		for (int i = 0; i < 5; i++)
			jobStore.TryCreate($"pre-filled job {i}", out _);

		using var leftContent = new ByteArrayContent(
			System.Text.Encoding.UTF8.GetBytes("a,b\n1,2\n")
		);
		using var rightContent = new ByteArrayContent(
			System.Text.Encoding.UTF8.GetBytes("a,b\n1,2\n")
		);
		leftContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
		rightContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");

		var form = new MultipartFormDataContent();
		form.Add(leftContent, "leftFile", "left.csv");
		form.Add(rightContent, "rightFile", "right.csv");
		form.Add(new StringContent(token), "__RequestVerificationToken");

		var response = await client.PostAsync("/?handler=StartJob", form);
		var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.IsTrue(json.RootElement.TryGetProperty("error", out _));
	}

	private sealed class CancellingFileReader : IFileReader
	{
		public IEnumerable<string> SupportedExtensions => [".csv", ".txt", ".xlsx", ".xlsm"];

		public Task<DataTable> ReadAsync(
			string filePath,
			Action<int>? progressCallback = null,
			CancellationToken cancellationToken = default
		)
		{
			throw new OperationCanceledException();
		}
	}
}
