using KrishavERP.Api;
using KrishavERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KrishavERP.Api.Modules.Doctors;

namespace KrishavERP.Api.Modules.Visits.Controllers;

[Authorize]
[ApiController]
[Route("api/ot")]
public class OtController : ControllerBase
{
    private readonly AppDbContext _db;

    public OtController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? searchColumn = null,
        [FromQuery] string? searchText = null)
    {
        var query = _db.OtBookings.AsQueryable();

        // DoctorName isn't an OtBooking column (it's resolved via a bulk
        // dictionary lookup below, same as Bills does for patient/doctor
        // names), so searching/sorting by it runs as a correlated subquery
        // against Doctors instead of a plain .Where/.OrderBy on the entity -
        // same trick Bills uses for patientName.
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = searchColumn switch
            {
                "otRoom" => query.Where(x => x.OtRoom.Contains(searchText)),
                "doctorName" => query.Where(x => _db.Doctors.Any(d => d.Id == x.DoctorId && d.Name.Contains(searchText))),
                "procedureName" => query.Where(x => x.ProcedureName != null && x.ProcedureName.Contains(searchText)),
                "status" => query.Where(x => x.Status.Contains(searchText)),
                // No column specified - the OT grid uses a single plain
                // search box, not a column picker, so match any of the
                // fields a user would plausibly be searching by.
                _ => query.Where(x =>
                    x.BookingNumber.Contains(searchText)
                    || x.OtRoom.Contains(searchText)
                    || _db.Doctors.Any(d => d.Id == x.DoctorId && d.Name.Contains(searchText))
                    || (x.ProcedureName != null && x.ProcedureName.Contains(searchText))
                    || x.Status.Contains(searchText))
            };
        }

        var descending = sortDirection == "desc";
        query = sortBy switch
        {
            "otRoom" => descending ? query.OrderByDescending(x => x.OtRoom) : query.OrderBy(x => x.OtRoom),
            "doctorName" => descending
                ? query.OrderByDescending(x => _db.Doctors.Where(d => d.Id == x.DoctorId).Select(d => d.Name).FirstOrDefault())
                : query.OrderBy(x => _db.Doctors.Where(d => d.Id == x.DoctorId).Select(d => d.Name).FirstOrDefault()),
            "startAtUtc" => descending ? query.OrderByDescending(x => x.StartAtUtc) : query.OrderBy(x => x.StartAtUtc),
            "endAtUtc" => descending ? query.OrderByDescending(x => x.EndAtUtc) : query.OrderBy(x => x.EndAtUtc),
            "procedureName" => descending ? query.OrderByDescending(x => x.ProcedureName) : query.OrderBy(x => x.ProcedureName),
            "doctorPayoutAmount" => descending ? query.OrderByDescending(x => x.DoctorPayoutAmount) : query.OrderBy(x => x.DoctorPayoutAmount),
            "status" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            _ => query.OrderByDescending(x => x.StartAtUtc)
        };

        var paged = await query.ToPagedResultAsync(page, pageSize ?? 10);

        var doctorIds = paged.Rows.Select(x => x.DoctorId).Distinct().ToList();
        var doctors = await _db.Doctors
            .Where(x => doctorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        var shaped = paged.Rows.Select(x => new
        {
            x.Id,
            x.BookingNumber,
            x.PatientId,
            x.ExternalPatientName,
            x.ExternalPatientPhone,
            x.DoctorId,
            DoctorName = doctors.TryGetValue(x.DoctorId, out var doctorName) ? doctorName : "",
            x.OtRoom,
            x.StartAtUtc,
            x.EndAtUtc,
            x.ProcedureName,
            x.DoctorPayoutAmount,
            x.Status
        }).ToList();

        return Ok(new { rows = shaped, totalCount = paged.TotalCount });
    }

    [HttpPost]
    public async Task<IActionResult> Add(OtBookingRequest request)
    {
        if (request.EndAtUtc <= request.StartAtUtc)
        {
            return BadRequest(new { message = "End time must be after start time." });
        }

        if (request.OtRoom != "OT 1" && request.OtRoom != "OT 2")
        {
            return BadRequest(new { message = "Select OT 1 or OT 2." });
        }

        if (request.DoctorPayoutAmount < 0)
        {
            return BadRequest(new { message = "Doctor OT payout cannot be negative." });
        }

        if (!await _db.Doctors.AnyAsync(x => x.Id == request.DoctorId && x.IsActive))
        {
            return BadRequest(new { message = "Select a valid doctor." });
        }

        var clash = await _db.OtBookings.AnyAsync(x =>
            x.OtRoom == request.OtRoom &&
            x.Status != "Cancelled" &&
            request.StartAtUtc < x.EndAtUtc &&
            request.EndAtUtc > x.StartAtUtc);

        if (clash)
        {
            return Conflict(new { message = "This OT is already booked during the selected time." });
        }

        if (request.PatientId == null && string.IsNullOrWhiteSpace(request.ExternalPatientName))
        {
            return BadRequest(new { message = "Patient name is required." });
        }

        var booking = new OtBooking
        {
            PatientId = request.PatientId,
            ExternalPatientName = request.ExternalPatientName,
            ExternalPatientPhone = request.ExternalPatientPhone,
            DoctorId = request.DoctorId,
            OtRoom = request.OtRoom,
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            ProcedureName = request.ProcedureName,
            DoctorPayoutAmount = request.DoctorPayoutAmount
        };

        _db.OtBookings.Add(booking);
        await _db.SaveChangesAsync();

        booking.BookingNumber = $"OT-{DateTime.UtcNow:yyyyMMdd}-{booking.Id:000000}";

        if (booking.DoctorPayoutAmount > 0)
        {
            _db.DoctorSettlements.Add(new DoctorSettlement
            {
                DoctorId = booking.DoctorId,
                SourceType = "OT",
                SourceId = booking.Id,
                Description = $"OT payout · {booking.BookingNumber} · {booking.ProcedureName}",
                PayableAmount = booking.DoctorPayoutAmount,
                EarnedDate = booking.StartAtUtc.Date
            });
        }

        await _db.SaveChangesAsync();
        return Ok(booking);
    }
}
