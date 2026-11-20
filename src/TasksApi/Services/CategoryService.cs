using Microsoft.EntityFrameworkCore;
using TasksApi.Data;
using TasksApi.Dtos;
using TasksApi.Errors;
using TasksApi.Models;

namespace TasksApi.Services;

public class CategoryService(AppDbContext db) : ICategoryService
{
    // Projection with Select: EF Core only reads the needed columns and translates
    // c.Tasks.Count into a COUNT subquery (≈ `_count` in Prisma).
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync() =>
        await db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Tasks.Count))
            .ToListAsync();

    public async Task<CategoryDto?> GetByIdAsync(int id) =>
        await db.Categories
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Tasks.Count))
            .FirstOrDefaultAsync();

    public async Task<CategoryDto> CreateAsync(SaveCategoryDto dto)
    {
        var name = dto.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludeId: null);

        var category = new Category { Name = name };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return new CategoryDto(category.Id, category.Name, 0);
    }

    public async Task<bool> UpdateAsync(int id, SaveCategoryDto dto)
    {
        var category = await db.Categories.FindAsync(id);
        if (category is null)
        {
            return false;
        }

        var name = dto.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludeId: id);

        category.Name = name;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var category = await db.Categories.FindAsync(id);
        if (category is null)
        {
            return false;
        }

        // Related tasks are kept: the database sets their CategoryId to NULL (see AppDbContext).
        db.Categories.Remove(category);
        await db.SaveChangesAsync();
        return true;
    }

    // Case-insensitive comparison: "Home" and "home" are the same name.
    private async Task EnsureNameIsFreeAsync(string name, int? excludeId)
    {
        var lowered = name.ToLower();
        var taken = await db.Categories.AnyAsync(c => c.Id != excludeId && c.Name.ToLower() == lowered);
        if (taken)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "Catégorie déjà existante",
                $"Une catégorie nommée « {name} » existe déjà.");
        }
    }
}
