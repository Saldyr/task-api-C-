using TasksApi.Dtos;

namespace TasksApi.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync();
    Task<CategoryDto?> GetByIdAsync(int id);
    Task<CategoryDto> CreateAsync(SaveCategoryDto dto);
    Task<bool> UpdateAsync(int id, SaveCategoryDto dto);
    Task<bool> DeleteAsync(int id);
}
