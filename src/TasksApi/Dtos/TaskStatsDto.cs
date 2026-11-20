namespace TasksApi.Dtos;

public record TaskStatsDto(
    int Total,
    int Done,
    int Pending,
    int Overdue,
    int Uncategorized,
    IReadOnlyList<CategoryStatsDto> ByCategory);

public record CategoryStatsDto(int CategoryId, string CategoryName, int Total, int Done);
