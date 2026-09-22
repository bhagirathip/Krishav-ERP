using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/patient-sources")]
public class PatientSourcesController : ControllerBase
{
    private readonly AppDbContext _db;

    public PatientSourcesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] bool includeInactive = false)
    {
        var query = _db.PatientSources.AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return Ok(await query
            .OrderBy(x => x.Name)
            .ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Add(PatientSourceRequest request)
    {
        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new
            {
                message = "Patient source name is required."
            });
        }

        if (await _db.PatientSources.AnyAsync(x => x.Name == name))
        {
            return Conflict(new
            {
                message = "Patient source already exists."
            });
        }

        var row = new PatientSource
        {
            Name = name,
            IsActive = request.IsActive
        };

        _db.PatientSources.Add(row);
        await _db.SaveChangesAsync();

        return Ok(row);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(
        int id,
        PatientSourceRequest request)
    {
        var row = await _db.PatientSources.FindAsync(id);

        if (row == null)
        {
            return NotFound();
        }

        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new
            {
                message = "Patient source name is required."
            });
        }

        if (await _db.PatientSources.AnyAsync(x =>
            x.Id != id &&
            x.Name == name))
        {
            return Conflict(new
            {
                message = "Patient source already exists."
            });
        }

        row.Name = name;
        row.IsActive = request.IsActive;

        await _db.SaveChangesAsync();

        return Ok(row);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.PatientSources.FindAsync(id);

        if (row == null)
        {
            return NotFound();
        }

        row.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok();
    }
}
