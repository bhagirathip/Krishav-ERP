using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/pharmacy/distributors")]
public class PharmacyDistributorsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PharmacyDistributorsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var rows = await _db.PharmacyDistributors
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Add(PharmacyDistributorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Distributor name is required." });
        }

        var distributor = new PharmacyDistributor
        {
            Name = request.Name.Trim(),
            Phone = request.Phone?.Trim(),
            GstNumber = request.GstNumber?.Trim(),
            Pan = request.Pan?.Trim(),
            DrugsBazaarId = request.DrugsBazaarId?.Trim(),
            Fssai = request.Fssai?.Trim(),
            Address = request.Address?.Trim()
        };

        _db.PharmacyDistributors.Add(distributor);
        await _db.SaveChangesAsync();
        return Ok(distributor);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, PharmacyDistributorRequest request)
    {
        var distributor = await _db.PharmacyDistributors.FindAsync(id);

        if (distributor == null || !distributor.IsActive)
        {
            return NotFound(new { message = "Distributor not found." });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Distributor name is required." });
        }

        distributor.Name = request.Name.Trim();
        distributor.Phone = request.Phone?.Trim();
        distributor.GstNumber = request.GstNumber?.Trim();
        distributor.Pan = request.Pan?.Trim();
        distributor.DrugsBazaarId = request.DrugsBazaarId?.Trim();
        distributor.Fssai = request.Fssai?.Trim();
        distributor.Address = request.Address?.Trim();

        await _db.SaveChangesAsync();
        return Ok(distributor);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var distributor = await _db.PharmacyDistributors.FindAsync(id);
        if (distributor == null)
        {
            return NotFound();
        }

        distributor.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }
}
