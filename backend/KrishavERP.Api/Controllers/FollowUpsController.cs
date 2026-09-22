using System.Security.Claims;
using KrishavERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/followups")]
public class FollowUpsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly FollowUpService _followUpService;

    public FollowUpsController(
        AppDbContext db,
        FollowUpService followUpService)
    {
        _db = db;
        _followUpService = followUpService;
    }

    [HttpPost]
    public async Task<IActionResult> Add(FollowUpCreateRequest request)
    {
        if (!await _db.Patients.AnyAsync(x =>
            x.Id == request.PatientId &&
            !x.IsDeleted))
        {
            return BadRequest(new
            {
                message = "Please select a valid patient."
            });
        }

        if (request.FollowUpDate.Date < DateTime.Today)
        {
            return BadRequest(new
            {
                message = "Follow-up date cannot be before today."
            });
        }

        var row = new PatientFollowUp
        {
            PatientId = request.PatientId,
            FollowUpDate = request.FollowUpDate.Date,
            Status = "Pending",
            Comment = string.IsNullOrWhiteSpace(request.Comment)
                ? "Manual follow-up."
                : request.Comment.Trim()
        };

        _db.PatientFollowUps.Add(row);
        await _db.SaveChangesAsync();

        return Ok(row);
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? date,
        [FromQuery] int? patientId)
    {
        var selectedDate = (date ?? DateTime.Today).Date;
        var nextDate = selectedDate.AddDays(1);

        var followUps = await _db.PatientFollowUps
            .Where(x =>
                x.FollowUpDate >= selectedDate &&
                x.FollowUpDate < nextDate &&
                (!patientId.HasValue ||
                 x.PatientId == patientId.Value))
            .OrderBy(x => x.Status)
            .ThenBy(x => x.FollowUpDate)
            .ToListAsync();

        var result = new List<object>();

        foreach (var followUp in followUps)
        {
            var patient = await _db.Patients
                .FirstOrDefaultAsync(x =>
                    x.Id == followUp.PatientId);

            OpdVisit? visit = null;
            Doctor? doctor = null;

            if (followUp.OpdVisitId.HasValue)
            {
                visit = await _db.OpdVisits
                    .FirstOrDefaultAsync(x =>
                        x.Id == followUp.OpdVisitId.Value);

                if (visit?.DoctorId != null)
                {
                    doctor = await _db.Doctors
                        .FirstOrDefaultAsync(x =>
                            x.Id == visit.DoctorId.Value);
                }
            }

            result.Add(new
            {
                followUp.Id,
                followUp.PatientId,
                PatientCode = patient?.PatientCode ?? "",
                PatientName = patient?.Name ?? "",
                Phone = patient?.Phone ?? "",
                followUp.OpdVisitId,
                VisitNumber = visit?.VisitNumber,
                DoctorName = doctor?.Name,
                followUp.FollowUpDate,
                followUp.Status,
                followUp.Comment,
                followUp.FollowUpNeeded,
                followUp.NextFollowUpDate,
                followUp.CompletedAtUtc
            });
        }

        return Ok(result);
    }

    [HttpGet("patient/{patientId:int}")]
    public async Task<IActionResult> PatientHistory(int patientId)
    {
        var patient = await _db.Patients
            .FirstOrDefaultAsync(x =>
                x.Id == patientId &&
                !x.IsDeleted);

        if (patient == null)
        {
            return NotFound(new { message = "Patient not found." });
        }

        var rows = await _db.PatientFollowUps
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.FollowUpDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        return Ok(new
        {
            patient = new
            {
                patient.Id,
                patient.PatientCode,
                patient.Name,
                patient.Phone
            },
            followUps = rows
        });
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(
        int id,
        FollowUpCompleteRequest request)
    {
        var row = await _db.PatientFollowUps.FindAsync(id);

        if (row == null)
        {
            return NotFound(new { message = "Follow-up not found." });
        }

        if (row.Status == "Completed")
        {
            return BadRequest(new
            {
                message = "This follow-up is already completed."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            return BadRequest(new
            {
                message = "Follow-up comment is required."
            });
        }

        int? userId = null;

        if (int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var parsedUserId))
        {
            userId = parsedUserId;
        }

        try
        {
            await _followUpService.CompleteAsync(
                row,
                request,
                userId);

            await _db.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok();
    }
}
