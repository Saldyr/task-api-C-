using System.ComponentModel.DataAnnotations;

namespace TasksApi.Models;

public class Category
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    // Navigation property: the "one" side of the one-to-many relationship (≈ `tasks Task[]` in Prisma).
    public List<TaskItem> Tasks { get; set; } = [];
}
