using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Modules.Lab.Controllers;

// Read-only API over the CBC analyzer results captured by
// CbcAnalyzerListenerService (BM500 LIS protocol). Entirely separate from
// LabController - this never reads or writes LabOrder/LabOrderTest/LabResult.
[Authorize]
[ApiController]
[Route("api/cbc-analyzer")]
public class CbcAnalyzerController : ControllerBase
{
    private readonly AppDbContext _db;

    public CbcAnalyzerController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("results")]
    public async Task<IActionResult> GetResults(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search)
    {
        var query = _db.CbcAnalyzerResults.AsQueryable();

        if (from.HasValue)
        {
            var start = from.Value.Date;
            query = query.Where(x => x.ReceivedAtUtc >= start);
        }

        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            query = query.Where(x => x.ReceivedAtUtc < end);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.PatientName.Contains(search) ||
                x.PatientIdentifier.Contains(search) ||
                x.SampleId.Contains(search));
        }

        var rows = await query
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.SampleId,
                x.ProcessingId,
                x.ResultTypeName,
                x.PatientIdentifier,
                x.PatientName,
                x.Gender,
                x.AgeText,
                x.TestMode,
                x.ObservationAtUtc,
                x.ReceivedAtUtc,
                ItemCount = x.Items.Count,
                ImageCount = x.Images.Count
            })
            .Take(500)
            .ToListAsync();

        return Ok(rows);
    }

    [HttpGet("results/{id:int}")]
    public async Task<IActionResult> GetResult(int id)
    {
        var result = await _db.CbcAnalyzerResults
            .Include(x => x.Items)
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            result.Id,
            result.SampleId,
            result.ProcessingId,
            result.ResultTypeCode,
            result.ResultTypeName,
            result.PatientIdentifier,
            result.PatientName,
            result.Gender,
            result.AgeText,
            result.PatientClass,
            result.PatientLocation,
            result.Tester,
            result.Interpreter,
            result.RequestedAtUtc,
            result.ObservationAtUtc,
            result.SpecimenReceivedAtUtc,
            result.LoadingMode,
            result.BloodMode,
            result.TestMode,
            result.RefGroup,
            result.Remark,
            result.ReceivedAtUtc,
            Items = result.Items.OrderBy(x => x.SortOrder).Select(x => new
            {
                x.Code,
                x.Name,
                x.Value,
                x.Unit,
                x.ReferenceRange,
                x.AbnormalFlag
            }),
            Images = result.Images.OrderBy(x => x.SortOrder).Select(x => new
            {
                x.Id,
                x.Name,
                x.ImagePath
            })
        });
    }

    [HttpDelete("results/{id:int}")]
    public async Task<IActionResult> DeleteResult(int id)
    {
        var result = await _db.CbcAnalyzerResults
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (result == null)
        {
            return NotFound();
        }

        foreach (var image in result.Images)
        {
            var path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                image.ImagePath.TrimStart('/'));

            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        _db.CbcAnalyzerResults.Remove(result);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
