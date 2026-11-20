using Microsoft.AspNetCore.Mvc;
using TasksApi.Dtos;
using TasksApi.Services;

namespace TasksApi.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll()
    {
        return Ok(await categoryService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> GetById(int id)
    {
        var category = await categoryService.GetByIdAsync(id);
        return category is null ? NotFound() : Ok(category);
    }

    // A name already in use throws an ApiException → 409 Conflict, via ApiExceptionHandler.
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(SaveCategoryDto dto)
    {
        var category = await categoryService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveCategoryDto dto)
    {
        return await categoryService.UpdateAsync(id, dto) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        return await categoryService.DeleteAsync(id) ? NoContent() : NotFound();
    }
}
