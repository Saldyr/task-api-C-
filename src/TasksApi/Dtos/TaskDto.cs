using TasksApi.Models;

namespace TasksApi.Dtos;

// Read DTO: what the API returns. A `record` is an immutable class with
// value-based equality, convenient for transfer objects.
public record TaskDto(
    int Id,
    string Title,
    string? Description,
    bool IsDone,
    DateTime CreatedAt,
    DateTime? DueDate,
    int? CategoryId,
    string? CategoryName)
{
    // task.Category is only populated when the query loaded it (Include), like `include` in Prisma.
    public static TaskDto FromEntity(TaskItem task) =>
        new(task.Id, task.Title, task.Description, task.IsDone, task.CreatedAt, task.DueDate,
            task.CategoryId, task.Category?.Name);
}
