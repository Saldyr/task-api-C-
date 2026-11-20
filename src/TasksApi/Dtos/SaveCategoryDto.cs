using System.ComponentModel.DataAnnotations;

namespace TasksApi.Dtos;

// Same body for creation (POST) and renaming (PUT).
public record SaveCategoryDto
{
    [Required]
    [MaxLength(50)]
    public string Name { get; init; } = string.Empty;
}
