using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/service-charges")]
public class ServiceChargesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ServiceChargesController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool activeOnly = false)
    {
        var query = _db.ServiceCharges.AsQueryable();
        if (activeOnly) query = query.Where(x => x.IsActive);
        return Ok(await query.OrderBy(x => x.Name).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Add(ServiceChargeRequest request)
    {
        var error = Validate(request);
        if (error != null) return BadRequest(new { message = error });
        if (await _db.ServiceCharges.AnyAsync(x => x.Name == request.Name.Trim()))
            return Conflict(new { message = "A charge with this name already exists." });

        var row = new ServiceCharge { Name = request.Name.Trim(), Price = request.Price, BillType = request.BillType.Trim(), IsActive = true };
        _db.ServiceCharges.Add(row);
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, ServiceChargeRequest request)
    {
        var row = await _db.ServiceCharges.FindAsync(id);
        if (row == null) return NotFound();
        var error = Validate(request);
        if (error != null) return BadRequest(new { message = error });
        if (await _db.ServiceCharges.AnyAsync(x => x.Id != id && x.Name == request.Name.Trim()))
            return Conflict(new { message = "A charge with this name already exists." });

        row.Name = request.Name.Trim();
        row.Price = request.Price;
        row.BillType = request.BillType.Trim();
        row.IsActive = request.IsActive;
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.ServiceCharges.FindAsync(id);
        if (row == null) return NotFound();
        row.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }

    private static string? Validate(ServiceChargeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Charge name is required.";
        if (request.Price < 0) return "Price cannot be negative.";
        if (string.IsNullOrWhiteSpace(request.BillType)) return "Bill type is required.";
        return null;
    }
}
