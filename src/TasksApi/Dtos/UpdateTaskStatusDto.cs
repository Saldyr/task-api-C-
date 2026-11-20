using System.ComponentModel.DataAnnotations;

namespace TasksApi.Dtos;

public record UpdateTaskStatusDto
{
    // `bool?` + [Required]: a plain `bool` would default to false when the field is missing,
    // so [Required] could never fail. With `bool?`, an empty body correctly returns 400.
    [Required]
    public bool? IsDone { get; init; }
}
