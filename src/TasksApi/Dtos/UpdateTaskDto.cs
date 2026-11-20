using System.ComponentModel.DataAnnotations;

namespace TasksApi.Dtos;

// PUT replaces the whole resource: every editable field is expected.
public record UpdateTaskDto
{
    [Required]
    [MaxLength(100)]
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsDone { get; init; }

    public DateTime? DueDate { get; init; }

    public int? CategoryId { get; init; }
}
