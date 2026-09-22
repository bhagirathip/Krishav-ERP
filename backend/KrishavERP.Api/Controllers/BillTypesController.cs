using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/bill-types")]
public class BillTypesController : ControllerBase
{
    private readonly AppDbContext _db;
    public BillTypesController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool opdOnly = false,
        [FromQuery] bool billableOnly = false)
    {
        var query = _db.BillTypes.AsQueryable();
        if (!includeInactive) query = query.Where(x => x.IsActive);
        if (opdOnly) query = query.Where(x => x.IsOpdType);
        if (billableOnly) query = query.Where(x => x.IsBillable);
        return Ok(await query.OrderBy(x => x.Name).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Add(BillTypeMasterRequest request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Bill type name is required." });
        if (await _db.BillTypes.AnyAsync(x => x.Name == name)) return Conflict(new { message = "Bill type already exists." });
        var row = new BillTypeMaster { Name = name, IsBillable = request.IsBillable, IsOpdType = request.IsOpdType, IsActive = request.IsActive };
        _db.BillTypes.Add(row); await _db.SaveChangesAsync(); return Ok(row);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, BillTypeMasterRequest request)
    {
        var row = await _db.BillTypes.FindAsync(id); if (row == null) return NotFound();
        var name = request.Name?.Trim(); if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Bill type name is required." });
        if (await _db.BillTypes.AnyAsync(x => x.Id != id && x.Name == name)) return Conflict(new { message = "Bill type already exists." });
        row.Name = name; row.IsBillable = request.IsBillable; row.IsOpdType = request.IsOpdType; row.IsActive = request.IsActive;
        await _db.SaveChangesAsync(); return Ok(row);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.BillTypes.FindAsync(id); if (row == null) return NotFound();
        row.IsActive = false; await _db.SaveChangesAsync(); return Ok();
    }
}
