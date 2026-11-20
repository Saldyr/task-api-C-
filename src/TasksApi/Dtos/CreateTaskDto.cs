using System.ComponentModel.DataAnnotations;

namespace TasksApi.Dtos;

// [Required] and [MaxLength] play the role of class-validator decorators
// (@IsNotEmpty, @MaxLength). No ValidationPipe needed: [ApiController]
// validates automatically and returns 400 when the input is invalid.
public record CreateTaskDto
{
    [Required]
    [MaxLength(100)]
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateTime? DueDate { get; init; }

    public int? CategoryId { get; init; }
}
