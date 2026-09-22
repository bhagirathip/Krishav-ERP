using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/staff-designations")]
public class StaffDesignationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public StaffDesignationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool includeInactive = false)
    {
        var query = _db.StaffDesignations.AsQueryable();
        if (!includeInactive) query = query.Where(x => x.IsActive);
        return Ok(await query.OrderBy(x => x.Name).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Add(StaffDesignationRequest request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Designation name is required." });
        if (await _db.StaffDesignations.AnyAsync(x => x.Name == name)) return Conflict(new { message = "Designation already exists." });
        var row = new StaffDesignation { Name = name, IsActive = request.IsActive };
        _db.StaffDesignations.Add(row);
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, StaffDesignationRequest request)
    {
        var row = await _db.StaffDesignations.FindAsync(id);
        if (row == null) return NotFound();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Designation name is required." });
        if (await _db.StaffDesignations.AnyAsync(x => x.Id != id && x.Name == name)) return Conflict(new { message = "Designation already exists." });
        row.Name = name; row.IsActive = request.IsActive;
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.StaffDesignations.FindAsync(id);
        if (row == null) return NotFound();
        row.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }
}
