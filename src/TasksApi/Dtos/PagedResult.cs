namespace TasksApi.Dtos;

// Generic type: PagedResult<TaskDto> ≈ PagedResult<T> in TypeScript.
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    // Computed property: serialized to JSON like the others.
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
