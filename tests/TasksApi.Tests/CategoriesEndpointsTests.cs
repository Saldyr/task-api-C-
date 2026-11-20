using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TasksApi.Dtos;

namespace TasksApi.Tests;

public class CategoriesEndpointsTests(TasksApiFactory factory) : IClassFixture<TasksApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_ValidCategory_Returns201WithLocation()
    {
        var response = await _client.PostAsJsonAsync("/api/categories", new { name = "  Travail  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.Equal("Travail", category!.Name); // whitespace trimmed by the service
        Assert.EndsWith($"/api/categories/{category.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Post_DuplicateNameIgnoringCase_Returns409ProblemDetails()
    {
        await _client.PostAsJsonAsync("/api/categories", new { name = "Maison" });

        var response = await _client.PostAsJsonAsync("/api/categories", new { name = "MAISON" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(409, problem?.Status);
    }

    [Fact]
    public async Task Put_RenameToExistingName_Returns409()
    {
        await _client.PostAsJsonAsync("/api/categories", new { name = "Sport" });
        var other = await (await _client.PostAsJsonAsync("/api/categories", new { name = "Lecture" }))
            .Content.ReadFromJsonAsync<CategoryDto>();

        var response = await _client.PutAsJsonAsync($"/api/categories/{other!.Id}", new { name = "sport" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Category_KeepsItsTasksWithoutCategory()
    {
        var category = await (await _client.PostAsJsonAsync("/api/categories", new { name = "Temporaire" }))
            .Content.ReadFromJsonAsync<CategoryDto>();
        var task = await (await _client.PostAsJsonAsync("/api/tasks", new { title = "Survivante", categoryId = category!.Id }))
            .Content.ReadFromJsonAsync<TaskDto>();
        Assert.Equal("Temporaire", task!.CategoryName);

        var delete = await _client.DeleteAsync($"/api/categories/{category.Id}");
        var reloaded = await _client.GetFromJsonAsync<TaskDto>($"/api/tasks/{task.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Null(reloaded!.CategoryId);
        Assert.Null(reloaded.CategoryName);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/categories/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
