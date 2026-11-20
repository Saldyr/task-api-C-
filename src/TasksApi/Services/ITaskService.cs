using TasksApi.Dtos;

namespace TasksApi.Services;

// NestJS usually injects the class directly. In C#, the convention is to inject
// an interface: the DI container picks the implementation (see Program.cs).
// Task<T> ≈ Promise<T>.
public interface ITaskService
{
    Task<PagedResult<TaskDto>> GetAllAsync(TaskQuery query);
    Task<TaskDto?> GetByIdAsync(int id);
    Task<TaskDto> CreateAsync(CreateTaskDto dto);
    Task<bool> UpdateAsync(int id, UpdateTaskDto dto);
    Task<TaskDto?> SetStatusAsync(int id, bool isDone);
    Task<bool> DeleteAsync(int id);
    Task<TaskStatsDto> GetStatsAsync();
}
