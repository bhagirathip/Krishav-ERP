using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

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
    public async Task<IActionResult> Get()
    {
        var bookings = await _db.OtBookings
            .OrderByDescending(x => x.StartAtUtc)
            .ToListAsync();

        var doctorIds = bookings.Select(x => x.DoctorId).Distinct().ToList();
        var doctors = await _db.Doctors
            .Where(x => doctorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        return Ok(bookings.Select(x => new
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
        }));
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
