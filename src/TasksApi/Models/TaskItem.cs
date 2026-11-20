using System.ComponentModel.DataAnnotations;

namespace TasksApi.Models;

// EF Core entity (≈ a Prisma `model`). Named TaskItem rather than Task to avoid
// clashing with System.Threading.Tasks.Task, which async/await relies on.
public class TaskItem
{
    public int Id { get; set; } // "Id" is picked up by convention as the auto-increment primary key.

    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    // "?" makes the column nullable, like `String?` in Prisma.
    public string? Description { get; set; }

    public bool IsDone { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DueDate { get; set; }

    // Optional foreign key + navigation property (≈ `categoryId Int?` and `category Category?` in Prisma).
    public int? CategoryId { get; set; }

    public Category? Category { get; set; }
}
