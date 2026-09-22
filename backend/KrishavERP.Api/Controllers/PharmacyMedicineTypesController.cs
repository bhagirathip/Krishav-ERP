using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/pharmacy/medicine-types")]
public class PharmacyMedicineTypesController : ControllerBase
{
    private readonly AppDbContext _db;

    public PharmacyMedicineTypesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var rows = await _db.PharmacyMedicineTypes
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Add(PharmacyMedicineTypeRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Medicine type name is required." });
        }

        if (await _db.PharmacyMedicineTypes.AnyAsync(x => x.Name == name && x.IsActive))
        {
            return Conflict(new { message = "Medicine type already exists." });
        }

        var row = new PharmacyMedicineType { Name = name };
        _db.PharmacyMedicineTypes.Add(row);
        await _db.SaveChangesAsync();

        return Ok(row);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, PharmacyMedicineTypeRequest request)
    {
        var row = await _db.PharmacyMedicineTypes.FindAsync(id);

        if (row == null || !row.IsActive)
        {
            return NotFound();
        }

        var name = request.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Medicine type name is required." });
        }

        row.Name = name;
        await _db.SaveChangesAsync();

        return Ok(row);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.PharmacyMedicineTypes.FindAsync(id);

        if (row == null)
        {
            return NotFound();
        }

        var inUse = await _db.PharmacyPurchaseItems.AnyAsync(x => x.MedicineTypeId == id);
        if (inUse)
        {
            return BadRequest(new { message = "This medicine type is already used in purchase inventory and cannot be deleted." });
        }

        row.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }
}
