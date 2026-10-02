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

        // Patient/bill detail for the collapsed-by-default per-doctor view -
        // Consultation rows point at a Bill, OT rows point at an OtBooking,
        // each of which may reference a registered patient or carry a
        // free-text walk-in/external name instead.
        var billIds = rows.Where(x => x.SourceType == "Consultation").Select(x => x.SourceId).Distinct().ToList();
        var bookingIds = rows.Where(x => x.SourceType == "OT").Select(x => x.SourceId).Distinct().ToList();

        var bills = await _db.Bills
            .Where(x => billIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x);
        var bookings = await _db.OtBookings
            .Where(x => bookingIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x);

        // A deleted consultation bill's settlement is removed as part of
        // deleting the bill (see BillsController.Delete), but this is a
        // safety net for any row from before that cleanup existed, or any
        // other path that might soft-delete a bill - an owed amount
        // shouldn't still show up for a bill that no longer exists.
        rows = rows.Where(x => x.SourceType != "Consultation"
            || !bills.TryGetValue(x.SourceId, out var sourceBill)
            || sourceBill.Status != "Deleted").ToList();

        var patientIds = bills.Values.Where(x => x.PatientId.HasValue).Select(x => x.PatientId!.Value)
            .Concat(bookings.Values.Where(x => x.PatientId.HasValue).Select(x => x.PatientId!.Value))
            .Distinct().ToList();
        var patients = await _db.Patients
            .Where(x => patientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        string PatientNameFor(DoctorSettlement x) => x.SourceType switch
        {
            "Consultation" when bills.TryGetValue(x.SourceId, out var bill) =>
                (bill.PatientId.HasValue && patients.TryGetValue(bill.PatientId.Value, out var pn) ? pn : bill.WalkInPatientName) ?? "",
            "OT" when bookings.TryGetValue(x.SourceId, out var booking) =>
                (booking.PatientId.HasValue && patients.TryGetValue(booking.PatientId.Value, out var pn) ? pn : booking.ExternalPatientName) ?? "",
            _ => ""
        };

        string ReferenceCodeFor(DoctorSettlement x) => x.SourceType switch
        {
            "Consultation" when bills.TryGetValue(x.SourceId, out var bill) => bill.BillNumber,
            "OT" when bookings.TryGetValue(x.SourceId, out var booking) => booking.BookingNumber,
            _ => ""
        };

        var detailRows = rows.Select(x => new
        {
            x.Id,
            x.DoctorId,
            DoctorName = doctors.TryGetValue(x.DoctorId, out var name) ? name : "",
            x.SourceType,
            x.SourceId,
            ReferenceCode = ReferenceCodeFor(x),
            PatientName = PatientNameFor(x),
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
        }).ToList();

        var doctorGroups = rows
            .GroupBy(x => x.DoctorId)
            .Select(g => new
            {
                DoctorId = g.Key,
                DoctorName = doctors.TryGetValue(g.Key, out var name) ? name : "",
                Payable = g.Sum(x => x.PayableAmount),
                Paid = g.Sum(x => x.PaidAmount),
                Outstanding = g.Sum(x => Math.Max(0, x.PayableAmount - x.PaidAmount))
            })
            .OrderByDescending(x => x.Outstanding)
            .ToList();

        return Ok(new
        {
            rows = detailRows,
            doctorGroups,
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

    // Settles every outstanding row for one doctor (Consultation and OT
    // together) within the given date range in a single action, rather than
    // paying each bill/OT booking one at a time.
    [HttpPost("settle-doctor")]
    public async Task<IActionResult> SettleDoctor(DoctorSettlementBulkPaymentRequest request)
    {
        if (request.PaidByStaffId.HasValue &&
            !await _db.StaffMembers.AnyAsync(x => x.Id == request.PaidByStaffId.Value && x.IsActive))
        {
            return BadRequest(new { message = "Please select a valid staff member who paid." });
        }

        var start = request.From.Date;
        var end = request.To.Date.AddDays(1);

        var rows = await _db.DoctorSettlements
            .Where(x => x.DoctorId == request.DoctorId && x.EarnedDate >= start && x.EarnedDate < end)
            .ToListAsync();

        var outstandingRows = rows.Where(x => x.PaidAmount < x.PayableAmount).ToList();
        if (outstandingRows.Count == 0)
        {
            return BadRequest(new { message = "No outstanding settlement for this doctor in the selected range." });
        }

        var settledAmount = 0m;
        foreach (var row in outstandingRows)
        {
            settledAmount += row.PayableAmount - row.PaidAmount;
            row.PaidAmount = row.PayableAmount;
            row.PaymentDate = request.PaymentDate;
            row.PaymentMode = request.PaymentMode?.Trim();
            row.ReferenceNumber = request.ReferenceNumber?.Trim();
            row.PaidByStaffId = request.PaidByStaffId;
            row.Notes = request.Notes?.Trim();
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(new { settledCount = outstandingRows.Count, settledAmount });
    }
}
