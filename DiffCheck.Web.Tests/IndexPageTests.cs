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

	[TestMethod]
	public async Task OnGetJobStatus_MissingJobId_ReturnsError()
	{
		await using var factory = new WebApplicationFactory<Program>();
		var client = factory.CreateClient();

		var response = await client.GetAsync("/?handler=JobStatus");
		var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.IsTrue(json.RootElement.TryGetProperty("error", out _));
	}

	[TestMethod]
	public async Task OnGetJobStatus_UnknownJobId_ReturnsFoundFalse()
	{
		await using var factory = new WebApplicationFactory<Program>();
		var client = factory.CreateClient();

		var response = await client.GetAsync("/?handler=JobStatus&jobId=unknown-id");
		var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.IsFalse(json.RootElement.GetProperty("found").GetBoolean());
	}

	[TestMethod]
	public async Task OnGetJobStatus_AfterStartJob_ReturnsFoundTrue()
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

		var startJson = JsonDocument.Parse(
			await (await client.PostAsync("/?handler=StartJob", form)).Content.ReadAsStringAsync()
		);
		var jobId = startJson.RootElement.GetProperty("jobId").GetString();

		var statusJson = JsonDocument.Parse(
			await (await client.GetAsync($"/?handler=JobStatus&jobId={jobId}")).Content.ReadAsStringAsync()
		);

		Assert.IsTrue(statusJson.RootElement.GetProperty("found").GetBoolean());
		var status = statusJson.RootElement.GetProperty("status").GetString();
		Assert.IsTrue(
			status is "pending" or "running" or "done",
			$"Unexpected status: {status}"
		);
	}

	[TestMethod]
	public async Task OnGetJobs_WhenNoJobs_ReturnsEmptyArray()
	{
		await using var factory = new WebApplicationFactory<Program>();
		var client = factory.CreateClient();

		var response = await client.GetAsync("/?handler=Jobs");
		var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.AreEqual(JsonValueKind.Array, json.RootElement.ValueKind);
	}

	[TestMethod]
	public async Task OnGetJobs_AfterStartJob_ReturnsJobInList()
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

		var startJson = JsonDocument.Parse(
			await (await client.PostAsync("/?handler=StartJob", form)).Content.ReadAsStringAsync()
		);
		var jobId = startJson.RootElement.GetProperty("jobId").GetString();

		var jobs = JsonDocument.Parse(
			await (await client.GetAsync("/?handler=Jobs")).Content.ReadAsStringAsync()
		);

		Assert.AreEqual(JsonValueKind.Array, jobs.RootElement.ValueKind);
		var found = jobs.RootElement.EnumerateArray().Any(j =>
			j.GetProperty("id").GetString() == jobId
		);
		Assert.IsTrue(found, "Started job should appear in the jobs list");
	}

	[TestMethod]
	public async Task OnPostStartJob_NullFiles_ReturnsError()
	{
		await using var factory = new WebApplicationFactory<Program>();
		var client = factory.CreateClient();
		var token = await GetAntiforgeryTokenAsync(client);

		var form = new MultipartFormDataContent();
		form.Add(new StringContent(token), "__RequestVerificationToken");

		var response = await client.PostAsync("/?handler=StartJob", form);
		var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		Assert.IsTrue(json.RootElement.TryGetProperty("error", out _));
	}

	[TestMethod]
	public async Task OnPostStartJob_EmptyFile_ReturnsError()
	{
		await using var factory = new WebApplicationFactory<Program>();
		var client = factory.CreateClient();
		var token = await GetAntiforgeryTokenAsync(client);

		using var leftContent = new ByteArrayContent(Array.Empty<byte>());
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

	[TestMethod]
	public async Task OnPostStartJob_UnsupportedFileFormat_ReturnsError()
	{
		await using var factory = new WebApplicationFactory<Program>();
		var client = factory.CreateClient();
		var token = await GetAntiforgeryTokenAsync(client);

		using var leftContent = new ByteArrayContent(
			System.Text.Encoding.UTF8.GetBytes("some,data")
		);
		using var rightContent = new ByteArrayContent(
			System.Text.Encoding.UTF8.GetBytes("some,data")
		);
		leftContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
		rightContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

		var form = new MultipartFormDataContent();
		form.Add(leftContent, "leftFile", "left.pdf");
		form.Add(rightContent, "rightFile", "right.pdf");
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
