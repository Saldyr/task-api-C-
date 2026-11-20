using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TasksApi.Dtos;

// The attribute makes Swagger show names ("DueDate") instead of numbers (1).
[JsonConverter(typeof(JsonStringEnumConverter<TaskSortField>))]
public enum TaskSortField { CreatedAt, DueDate, Title }

[JsonConverter(typeof(JsonStringEnumConverter<SortDirection>))]
public enum SortDirection { Asc, Desc }

// GET /api/tasks parameters, read from the query string through [FromQuery]
// (≈ a validated query DTO in NestJS). An unknown enum value → 400.
public class TaskQuery
{
    public bool? IsDone { get; set; }

    public int? CategoryId { get; set; }

    [MaxLength(100)]
    public string? Search { get; set; }

    // true: only pending tasks whose due date has passed.
    public bool Overdue { get; set; }

    public TaskSortField SortBy { get; set; } = TaskSortField.CreatedAt;

    public SortDirection SortDir { get; set; } = SortDirection.Desc;

    [Range(1, 10_000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}
