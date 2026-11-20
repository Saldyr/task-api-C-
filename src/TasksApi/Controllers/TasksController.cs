using Microsoft.AspNetCore.Mvc;
using TasksApi.Dtos;
using TasksApi.Services;

namespace TasksApi.Controllers;

// [ApiController] ≈ @Controller() + a global ValidationPipe: JSON body binding,
// automatic validation (400) and ProblemDetails error responses.
// [Route] ≈ @Controller('api/tasks').
[ApiController]
[Route("api/tasks")]
public class TasksController(ITaskService taskService) : ControllerBase
{
    // GET /api/tasks?search=...&isDone=false&categoryId=1&overdue=true&sortBy=DueDate&sortDir=Asc&page=1&pageSize=10
    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskDto>>> GetAll([FromQuery] TaskQuery query)
    {
        return Ok(await taskService.GetAllAsync(query));
    }

    // Literal "stats" route: no conflict with {id:int}, which only matches integers.
    [HttpGet("stats")]
    public async Task<ActionResult<TaskStatsDto>> GetStats()
    {
        return Ok(await taskService.GetStatsAsync());
    }

    // {id:int}: route constraint, a non-integer id does not match → 404.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskDto>> GetById(int id)
    {
        var task = await taskService.GetByIdAsync(id);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(CreateTaskDto dto)
    {
        var task = await taskService.CreateAsync(dto);
        // 201 Created + a Location: /api/tasks/{id} header, built from the GetById route.
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateTaskDto dto)
    {
        return await taskService.UpdateAsync(id, dto) ? NoContent() : NotFound();
    }

    // PATCH: partial update, here only the status (check / uncheck).
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<TaskDto>> SetStatus(int id, UpdateTaskStatusDto dto)
    {
        // [Required] guarantees IsDone is not null at this point.
        var task = await taskService.SetStatusAsync(id, dto.IsDone!.Value);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        return await taskService.DeleteAsync(id) ? NoContent() : NotFound();
    }
}
