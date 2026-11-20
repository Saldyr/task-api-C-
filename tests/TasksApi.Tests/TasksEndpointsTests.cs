using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TasksApi.Dtos;

namespace TasksApi.Tests;

// IClassFixture: xUnit creates a single factory (one API, one database)
// shared by the tests of this class. Each test creates its own data.
public class TasksEndpointsTests(TasksApiFactory factory) : IClassFixture<TasksApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_ValidTask_Returns201WithLocation()
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Écrire le README",
            dueDate = "2026-12-01T10:00:00+02:00",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<TaskDto>();
        Assert.NotNull(created);
        Assert.Equal("Écrire le README", created.Title);
        Assert.False(created.IsDone);
        Assert.Equal(new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc), created.DueDate);
        Assert.Equal(DateTimeKind.Utc, created.DueDate!.Value.Kind);
        Assert.EndsWith($"/api/tasks/{created.Id}", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task GetById_ExistingTask_ReturnsTask()
    {
        var post = await _client.PostAsJsonAsync("/api/tasks", new { title = "Lire la doc EF Core" });
        var created = await post.Content.ReadFromJsonAsync<TaskDto>();

        var response = await _client.GetAsync(post.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskDto>();
        Assert.Equal(created, task); // Value equality, thanks to the record.
        // Read back from SQLite, the date must stay UTC (JSON ending with "Z").
        Assert.Equal(DateTimeKind.Utc, task!.CreatedAt.Kind);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/tasks/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Post_MissingTitle_Returns400(string? title)
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", new { title });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Title", problem.Errors.Keys);
    }

    [Fact]
    public async Task Post_TitleLongerThan100_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", new { title = new string('a', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Title", problem.Errors.Keys);
    }

    [Fact]
    public async Task Post_UnknownCategory_Returns400ProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", new { title = "Orpheline", categoryId = 999999 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Catégorie inconnue", problem?.Title);
    }

    [Fact]
    public async Task Patch_Status_MarksTaskDone()
    {
        var task = await CreateTaskAsync("À cocher");

        var response = await _client.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", new { isDone = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<TaskDto>();
        Assert.True(updated!.IsDone);
    }

    [Fact]
    public async Task Patch_StatusWithoutIsDone_Returns400()
    {
        var task = await CreateTaskAsync("Sans statut");

        var response = await _client.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_SearchWithPagination_ReturnsRequestedPage()
    {
        var tag = UniqueTag();
        await CreateTaskAsync($"Un {tag}");
        await CreateTaskAsync($"Deux {tag}");
        await CreateTaskAsync($"Trois {tag}");

        var page1 = await _client.GetFromJsonAsync<PagedResult<TaskDto>>($"/api/tasks?search={tag}&pageSize=2&page=1");
        var page2 = await _client.GetFromJsonAsync<PagedResult<TaskDto>>($"/api/tasks?search={tag}&pageSize=2&page=2");

        Assert.Equal(3, page1!.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);
        Assert.Single(page2!.Items);
    }

    [Fact]
    public async Task GetAll_SortByTitleAsc_ReturnsAlphabeticalOrder()
    {
        var tag = UniqueTag();
        await CreateTaskAsync($"b {tag}");
        await CreateTaskAsync($"c {tag}");
        await CreateTaskAsync($"a {tag}");

        var result = await _client.GetFromJsonAsync<PagedResult<TaskDto>>($"/api/tasks?search={tag}&sortBy=Title&sortDir=Asc");

        Assert.Equal([$"a {tag}", $"b {tag}", $"c {tag}"], result!.Items.Select(t => t.Title));
    }

    [Fact]
    public async Task GetAll_Overdue_ReturnsOnlyPendingTasksPastDueDate()
    {
        var tag = UniqueTag();
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var late = await CreateTaskAsync($"En retard {tag}", dueDate: yesterday);
        var lateButDone = await CreateTaskAsync($"Faite {tag}", dueDate: yesterday);
        await _client.PatchAsJsonAsync($"/api/tasks/{lateButDone.Id}/status", new { isDone = true });
        await CreateTaskAsync($"À venir {tag}", dueDate: DateTime.UtcNow.AddDays(1));

        var result = await _client.GetFromJsonAsync<PagedResult<TaskDto>>($"/api/tasks?search={tag}&overdue=true");

        Assert.Equal(late.Id, Assert.Single(result!.Items).Id);
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("sortBy=Inconnu")]
    public async Task GetAll_InvalidQuery_Returns400(string queryString)
    {
        var response = await _client.GetAsync($"/api/tasks?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_CountsTasksPerCategory()
    {
        var categoryResponse = await _client.PostAsJsonAsync("/api/categories", new { name = $"Stats {UniqueTag()}" });
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
        var first = await CreateTaskAsync("Stat 1", categoryId: category!.Id);
        await CreateTaskAsync("Stat 2", categoryId: category.Id);
        await _client.PatchAsJsonAsync($"/api/tasks/{first.Id}/status", new { isDone = true });

        var stats = await _client.GetFromJsonAsync<TaskStatsDto>("/api/tasks/stats");

        var forCategory = Assert.Single(stats!.ByCategory, c => c.CategoryId == category.Id);
        Assert.Equal(2, forCategory.Total);
        Assert.Equal(1, forCategory.Done);
        Assert.Equal(stats.Total, stats.Done + stats.Pending);
    }

    // Tests share the same database: a unique tag isolates each test's data.
    private static string UniqueTag() => Guid.NewGuid().ToString("N")[..8];

    private async Task<TaskDto> CreateTaskAsync(string title, DateTime? dueDate = null, int? categoryId = null)
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", new { title, dueDate, categoryId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskDto>())!;
    }
}
