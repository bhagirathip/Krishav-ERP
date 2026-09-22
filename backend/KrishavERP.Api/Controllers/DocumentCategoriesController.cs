using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/document-categories")]
public class DocumentCategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public DocumentCategoriesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(await _db.DocumentCategories
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Add(DocumentCategory category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
            return BadRequest(new { message = "Category name is required." });

        if (await _db.DocumentCategories.AnyAsync(x => x.Name == category.Name && x.IsActive))
            return Conflict(new { message = "This category already exists." });

        category.Name = category.Name.Trim();
        category.IsActive = true;
        _db.DocumentCategories.Add(category);
        await _db.SaveChangesAsync();
        return Ok(category);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, DocumentCategory request)
    {
        var category = await _db.DocumentCategories.FindAsync(id);
        if (category == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Category name is required." });

        category.Name = request.Name.Trim();
        await _db.SaveChangesAsync();
        return Ok(category);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.DocumentCategories.FindAsync(id);
        if (category == null)
            return NotFound();

        category.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }
}
