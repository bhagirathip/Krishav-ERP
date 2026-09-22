using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Services;

public class FollowUpService
{
    private readonly AppDbContext _db;

    public FollowUpService(AppDbContext db)
    {
        _db = db;
    }

    public async Task CreateForConsultationAsync(
        int patientId,
        int opdVisitId,
        DateTime consultationDate,
        DateTime? requestedFollowUpDate = null)
    {
        var exists = await _db.PatientFollowUps.AnyAsync(x =>
            x.OpdVisitId == opdVisitId);

        if (exists)
        {
            return;
        }

        _db.PatientFollowUps.Add(new PatientFollowUp
        {
            PatientId = patientId,
            OpdVisitId = opdVisitId,
            FollowUpDate =
                requestedFollowUpDate?.Date ??
                consultationDate.Date.AddDays(2),
            Status = "Pending",
            Comment = "Automatic follow-up after consultation."
        });
    }

    public async Task CompleteAsync(
        PatientFollowUp followUp,
        FollowUpCompleteRequest request,
        int? completedByUserId)
    {
        followUp.Status = "Completed";
        followUp.Comment = request.Comment?.Trim();
        followUp.FollowUpNeeded = request.FollowUpNeeded;
        followUp.NextFollowUpDate = request.FollowUpNeeded
            ? request.NextFollowUpDate?.Date
            : null;
        followUp.CompletedAtUtc = DateTime.UtcNow;
        followUp.CompletedByUserId = completedByUserId;

        if (!request.FollowUpNeeded)
        {
            return;
        }

        if (!request.NextFollowUpDate.HasValue)
        {
            throw new InvalidOperationException(
                "Next follow-up date is required when follow-up is needed.");
        }

        if (request.NextFollowUpDate.Value.Date <= DateTime.Today)
        {
            throw new InvalidOperationException(
                "Next follow-up date must be after today.");
        }

        _db.PatientFollowUps.Add(new PatientFollowUp
        {
            PatientId = followUp.PatientId,
            ParentFollowUpId = followUp.Id,
            FollowUpDate = request.NextFollowUpDate.Value.Date,
            Status = "Pending",
            Comment = "Scheduled from previous follow-up."
        });
    }
}
