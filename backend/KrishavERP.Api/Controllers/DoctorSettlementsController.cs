using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/doctor-settlements")]
public class DoctorSettlementsController : ControllerBase
{
    private readonly AppDbContext _db;

    public DoctorSettlementsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? doctorId,
        [FromQuery] string? sourceType)
    {
        var start = (from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);

        var query = _db.DoctorSettlements
            .Where(x => x.EarnedDate >= start && x.EarnedDate < end);

        if (doctorId.HasValue)
        {
            query = query.Where(x => x.DoctorId == doctorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(sourceType) && sourceType != "All")
        {
            query = query.Where(x => x.SourceType == sourceType);
        }

        var rows = await query
            .OrderByDescending(x => x.EarnedDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        var doctorIds = rows.Select(x => x.DoctorId).Distinct().ToList();
        var doctors = await _db.Doctors
            .Where(x => doctorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        return Ok(new
        {
            rows = rows.Select(x => new
            {
                x.Id,
                x.DoctorId,
                DoctorName = doctors.TryGetValue(x.DoctorId, out var name) ? name : "",
                x.SourceType,
                x.SourceId,
                x.Description,
                x.PayableAmount,
                x.PaidAmount,
                Outstanding = Math.Max(0, x.PayableAmount - x.PaidAmount),
                x.EarnedDate,
                x.PaymentDate,
                x.PaymentMode,
                x.ReferenceNumber,
                x.Notes,
                x.PaidByStaffId
            }),
            totalPayable = rows.Sum(x => x.PayableAmount),
            totalPaid = rows.Sum(x => x.PaidAmount),
            totalOutstanding = rows.Sum(x => Math.Max(0, x.PayableAmount - x.PaidAmount))
        });
    }

    [HttpPost("{id:int}/payment")]
    public async Task<IActionResult> Payment(
        int id,
        DoctorSettlementPaymentRequest request)
    {
        var row = await _db.DoctorSettlements.FindAsync(id);
        if (row == null)
        {
            return NotFound(new { message = "Doctor settlement not found." });
        }

        if (request.PaidAmount < 0 || request.PaidAmount > row.PayableAmount)
        {
            return BadRequest(new
            {
                message = "Paid amount cannot exceed doctor payable amount."
            });
        }

        row.PaidAmount = request.PaidAmount;
        row.PaymentDate = request.PaymentDate;
        row.PaymentMode = request.PaymentMode?.Trim();
        if (request.PaidByStaffId.HasValue &&
            !await _db.StaffMembers.AnyAsync(x => x.Id == request.PaidByStaffId.Value && x.IsActive))
        {
            return BadRequest(new { message = "Please select a valid staff member who paid." });
        }

        row.ReferenceNumber = request.ReferenceNumber?.Trim();
        row.PaidByStaffId = request.PaidByStaffId;
        row.Notes = request.Notes?.Trim();
        row.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(row);
    }
}
