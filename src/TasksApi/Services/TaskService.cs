using Microsoft.EntityFrameworkCore;
using TasksApi.Data;
using TasksApi.Dtos;
using TasksApi.Errors;
using TasksApi.Models;

namespace TasksApi.Services;

// Primary constructor (C# 12): equivalent to `constructor(private readonly db: AppDbContext)`.
public class TaskService(AppDbContext db) : ITaskService
{
    public async Task<PagedResult<TaskDto>> GetAllAsync(TaskQuery q)
    {
        // IQueryable: the SQL query is only built when it runs (CountAsync, ToListAsync).
        // Each filter adds a WHERE clause only when it is provided.
        // Include ≈ `include: { category: true }` in Prisma (LEFT JOIN).
        IQueryable<TaskItem> query = db.Tasks.AsNoTracking().Include(t => t.Category);

        if (q.IsDone is not null)
        {
            query = query.Where(t => t.IsDone == q.IsDone);
        }

        if (q.CategoryId is not null)
        {
            query = query.Where(t => t.CategoryId == q.CategoryId);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            // LIKE is case-insensitive in SQLite (for non-accented letters only).
            var pattern = $"%{q.Search.Trim()}%";
            query = query.Where(t => EF.Functions.Like(t.Title, pattern)
                || (t.Description != null && EF.Functions.Like(t.Description, pattern)));
        }

        if (q.Overdue)
        {
            var now = DateTime.UtcNow;
            query = query.Where(t => !t.IsDone && t.DueDate < now);
        }

        var totalCount = await query.CountAsync();
        var tasks = await Sort(query, q.SortBy, q.SortDir)
            .Skip((q.Page - 1) * q.PageSize) // ≈ `skip` / `take` in Prisma
            .Take(q.PageSize)
            .ToListAsync();

        return new PagedResult<TaskDto>(tasks.Select(TaskDto.FromEntity).ToList(), q.Page, q.PageSize, totalCount);
    }

    public async Task<TaskDto?> GetByIdAsync(int id)
    {
        var task = await db.Tasks.AsNoTracking().Include(t => t.Category).FirstOrDefaultAsync(t => t.Id == id);
        return task is null ? null : TaskDto.FromEntity(task);
    }

    public async Task<TaskDto> CreateAsync(CreateTaskDto dto)
    {
        await EnsureCategoryExistsAsync(dto.CategoryId);

        var task = new TaskItem
        {
            Title = dto.Title,
            Description = dto.Description,
            DueDate = ToUtc(dto.DueDate),
            CategoryId = dto.CategoryId,
            IsDone = false,
            CreatedAt = DateTime.UtcNow,
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync(); // EF Core fills in task.Id after the INSERT.

        // Load the related category so its name can be returned.
        await db.Entry(task).Reference(t => t.Category).LoadAsync();
        return TaskDto.FromEntity(task);
    }

    public async Task<bool> UpdateAsync(int id, UpdateTaskDto dto)
    {
        var task = await db.Tasks.FindAsync(id);
        if (task is null)
        {
            return false;
        }

        await EnsureCategoryExistsAsync(dto.CategoryId);

        // No explicit `update()` call as in Prisma: EF Core tracks changes on the loaded
        // entity and generates the UPDATE on SaveChanges (change tracking).
        task.Title = dto.Title;
        task.Description = dto.Description;
        task.IsDone = dto.IsDone;
        task.DueDate = ToUtc(dto.DueDate);
        task.CategoryId = dto.CategoryId;

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<TaskDto?> SetStatusAsync(int id, bool isDone)
    {
        var task = await db.Tasks.Include(t => t.Category).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return null;
        }

        task.IsDone = isDone;
        await db.SaveChangesAsync();
        return TaskDto.FromEntity(task);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var task = await db.Tasks.FindAsync(id);
        if (task is null)
        {
            return false;
        }

        db.Tasks.Remove(task);
        await db.SaveChangesAsync();
        return true;
    }

    private static IQueryable<TaskItem> Sort(IQueryable<TaskItem> query, TaskSortField sortBy, SortDirection dir)
    {
        var desc = dir == SortDirection.Desc;

        // Switch expression: it returns a value, like a lookup object in TS.
        IOrderedQueryable<TaskItem> ordered = sortBy switch
        {
            TaskSortField.Title => desc ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
            // Tasks without a due date always come last.
            TaskSortField.DueDate => desc
                ? query.OrderBy(t => t.DueDate == null).ThenByDescending(t => t.DueDate)
                : query.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate),
            _ => desc ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt),
        };

        // Tie-break on Id: without a total order, pagination could skip or repeat rows.
        return ordered.ThenBy(t => t.Id);
    }

    private async Task EnsureCategoryExistsAsync(int? categoryId)
    {
        if (categoryId is not null && !await db.Categories.AnyAsync(c => c.Id == categoryId))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Catégorie inconnue",
                $"Aucune catégorie n'a l'identifiant {categoryId}.");
        }
    }

    // JSON such as "2026-12-01T10:00:00+02:00" is deserialized in the server's local time
    // (Kind = Local): convert it to UTC. A date without a time zone is treated as UTC.
    private static DateTime? ToUtc(DateTime? date) =>
        date is null ? null
        : date.Value.Kind == DateTimeKind.Local ? date.Value.ToUniversalTime()
        : DateTime.SpecifyKind(date.Value, DateTimeKind.Utc);
}
