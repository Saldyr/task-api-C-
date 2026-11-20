using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TasksApi.Models;

namespace TasksApi.Data;

// ≈ PrismaClient: the entry point to the database. One instance per HTTP request (scoped).
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Each DbSet maps to a table.
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<Category> Categories => Set<Category>();

    // Model configuration (≈ what schema.prisma expresses beyond plain fields).
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasIndex(c => c.Name).IsUnique();

        // Deleting a category keeps its tasks: their CategoryId is set back to NULL
        // (≈ `onDelete: SetNull` in Prisma).
        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.Category)
            .WithMany(c => c.Tasks)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // SQLite stores dates as text without a time zone: read back, they have
        // DateTime.Kind = Unspecified and the JSON loses its "Z". The service always
        // writes UTC (TaskService.ToUtc), so this converter marks values read back as UTC.
        var utc = new ValueConverter<DateTime, DateTime>(
            toDb => toDb,
            fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc));

        modelBuilder.Entity<TaskItem>().Property(t => t.CreatedAt).HasConversion(utc);
        modelBuilder.Entity<TaskItem>().Property(t => t.DueDate).HasConversion(utc);
    }
}
