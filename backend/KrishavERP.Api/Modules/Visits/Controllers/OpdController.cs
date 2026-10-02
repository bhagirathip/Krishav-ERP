using KrishavERP.Api;
using KrishavERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KrishavERP.Api.Modules.Doctors;
using KrishavERP.Api.Modules.Billing;

namespace KrishavERP.Api.Modules.Visits.Controllers;

[Authorize]
[ApiController]
[Route("api/opd")]
public class OpdController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly FollowUpService _followUpService;
    private readonly IWebHostEnvironment _env;

    public OpdController(
        AppDbContext db,
        FollowUpService followUpService,
        IWebHostEnvironment env)
    {
        _db = db;
        _followUpService = followUpService;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? q,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query =
            from visit in _db.OpdVisits
            where !visit.IsCancelled
            join patient in _db.Patients on visit.PatientId equals patient.Id
            join doctorJoin in _db.Doctors on visit.DoctorId equals doctorJoin.Id into doctors
            from doctor in doctors.DefaultIfEmpty()
            select new
            {
                visit.Id,
                visit.VisitNumber,
                visit.PatientId,
                PatientCode = patient.PatientCode,
                PatientName = patient.Name,
                patient.Phone,
                patient.Age,
                patient.Gender,
                visit.DoctorId,
                DoctorName = doctor == null ? null : doctor.Name,
                visit.IsEmergency,
                visit.BloodPressure,
                visit.TemperatureC,
                visit.Pulse,
                visit.ChiefComplaint,
                visit.WeightKg,
                visit.HeightCm,
                visit.Spo2,
                visit.VisitType,
                visit.MarketingSource,
                visit.ReferrerId,
                ReferrerName = visit.ReferrerId.HasValue
                    ? _db.Referrers
                        .Where(r => r.Id == visit.ReferrerId.Value)
                        .Select(r => r.Name)
                        .FirstOrDefault()
                    : null,
                visit.VisitDateUtc,
                visit.ConvertedToIpd
            };

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.PatientName.Contains(q) ||
                x.Phone.Contains(q) ||
                x.PatientCode.Contains(q) ||
                (x.DoctorName != null && x.DoctorName.Contains(q)));
        }

        if (from.HasValue) query = query.Where(x => x.VisitDateUtc >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.VisitDateUtc < to.Value.Date.AddDays(1));

        return Ok(await query
            .OrderByDescending(x => x.VisitDateUtc)
            .ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Add(OpdCreateRequest request)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(x =>
            x.Id == request.PatientId &&
            !x.IsDeleted);

        if (patient == null)
        {
            return BadRequest(new
            {
                message = "Please select a valid patient."
            });
        }

        if (!await _db.PatientSources.AnyAsync(x =>
            x.IsActive &&
            x.Name == request.MarketingSource))
        {
            return BadRequest(new
            {
                message = "Please select a valid patient source."
            });
        }

        if (request.ReferrerId.HasValue &&
            !await _db.Referrers.AnyAsync(x =>
                x.Id == request.ReferrerId.Value &&
                x.IsActive))
        {
            return BadRequest(new
            {
                message = "Selected referrer was not found."
            });
        }

        var doctor = await _db.Doctors.FindAsync(request.DoctorId);
        if (doctor == null || !doctor.IsActive)
            return BadRequest(new { message = "Please select a valid doctor." });

        var visitType = string.IsNullOrWhiteSpace(request.VisitType)
            ? "OPD"
            : request.VisitType;

        var visitTypeMaster = await _db.BillTypes.FirstOrDefaultAsync(x =>
            x.IsActive &&
            x.IsOpdType &&
            x.Name == visitType);

        if (visitTypeMaster == null)
            return BadRequest(new { message = "Please select a valid OPD visit type." });

        var now = DateTime.Now;
        DateTime visitDate;

        if (visitType == "Appointment")
        {
            if (request.VisitDateUtc == default || request.VisitDateUtc < now)
            {
                return BadRequest(new
                {
                    message = "Appointment date/time cannot be before current date/time."
                });
            }

            visitDate = request.VisitDateUtc;
        }
        else
        {
            visitDate = now;
        }

        var isEmergency = visitType.Equals("Emergency", StringComparison.OrdinalIgnoreCase) ||
            visitType.Equals("Emergency Visit", StringComparison.OrdinalIgnoreCase);

        if (visitType != "Appointment" &&
            request.FollowUpDate.HasValue &&
            request.FollowUpDate.Value.Date <= visitDate.Date)
        {
            return BadRequest(new
            {
                message =
                    "Follow-up date must be after the consultation date."
            });
        }

        var visit = new OpdVisit
        {
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            IsEmergency = isEmergency,
            VisitType = visitType,
            BloodPressure = request.BloodPressure,
            TemperatureC = request.TemperatureC,
            Pulse = request.Pulse,
            ChiefComplaint = request.ChiefComplaint,
            WeightKg = request.WeightKg,
            HeightCm = request.HeightCm,
            Spo2 = request.Spo2,
            MarketingSource = request.MarketingSource,
            ReferrerId = request.ReferrerId,
            VisitDateUtc = visitDate
        };

        _db.OpdVisits.Add(visit);
        await _db.SaveChangesAsync();

        visit.VisitNumber = $"OPD-{visitDate:yyyyMMdd}-{visit.Id:000000}";

        patient.MarketingSource = request.MarketingSource;
        patient.ReferrerId = request.ReferrerId;

        if (visitType != "Appointment")
        {
            await _followUpService.CreateForConsultationAsync(
                visit.PatientId,
                visit.Id,
                visit.VisitDateUtc,
                request.FollowUpDate);
        }

        if (request.CreateBill && visitTypeMaster.IsBillable)
        {
            await CreateVisitBill(
                request.PatientId,
                doctor,
                visitType,
                request.ReferrerId);
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            visit.Id,
            visit.VisitNumber
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, OpdEditRequest request)
    {
        var visit = await _db.OpdVisits.FirstOrDefaultAsync(x => x.Id == id && !x.IsCancelled);
        if (visit == null) return NotFound();

        var doctor = await _db.Doctors.FindAsync(request.DoctorId);
        if (doctor == null || !doctor.IsActive)
            return BadRequest(new { message = "Please select a valid doctor." });

        if (!await _db.PatientSources.AnyAsync(x => x.IsActive && x.Name == request.MarketingSource))
        {
            return BadRequest(new { message = "Please select a valid patient source." });
        }

        if (request.ReferrerId.HasValue &&
            !await _db.Referrers.AnyAsync(x => x.Id == request.ReferrerId.Value && x.IsActive))
        {
            return BadRequest(new { message = "Selected referrer was not found." });
        }

        if (request.WeightKg is < 1 or > 500) return BadRequest(new { message = "Weight must be 1-500 kg." });
        if (request.HeightCm is < 30 or > 250) return BadRequest(new { message = "Height must be 30-250 cm." });
        if (request.Spo2 is < 50 or > 100) return BadRequest(new { message = "SpO2 must be 50-100%." });
        if (request.Pulse is < 30 or > 250) return BadRequest(new { message = "Pulse must be 30-250 bpm." });

        visit.DoctorId = request.DoctorId;
        visit.BloodPressure = request.BloodPressure;
        visit.TemperatureC = request.TemperatureC;
        visit.Pulse = request.Pulse;
        visit.ChiefComplaint = request.ChiefComplaint;
        visit.WeightKg = request.WeightKg;
        visit.HeightCm = request.HeightCm;
        visit.Spo2 = request.Spo2;
        visit.MarketingSource = request.MarketingSource;
        visit.ReferrerId = request.ReferrerId;

        await _db.SaveChangesAsync();
        return Ok();
    }

    // Soft delete (matches Get()'s "where !visit.IsCancelled" filter) - a
    // visit already billed keeps its own Bill row untouched, since bills are
    // managed independently on the Bill page rather than cascaded from here.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var visit = await _db.OpdVisits.FirstOrDefaultAsync(x => x.Id == id && !x.IsCancelled);
        if (visit == null) return NotFound();

        if (visit.ConvertedToIpd)
        {
            return BadRequest(new { message = "Cannot delete: this visit has already been converted to an IPD admission." });
        }

        visit.IsCancelled = true;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("{id:int}/prescription")]
    public async Task<IActionResult> Prescription(int id)
    {
        var visit = await _db.OpdVisits.FindAsync(id);
        if (visit == null)
            return NotFound();

        var patient = await _db.Patients.FindAsync(visit.PatientId);
        var doctor = visit.DoctorId.HasValue
            ? await _db.Doctors.FindAsync(visit.DoctorId.Value)
            : null;

        string Setting(string name) => AssetVersioning.Stamp(_env, _db.AppSettings
            .FirstOrDefault(x => x.Name == name && x.IsActive)?.Value ?? string.Empty);

        return Ok(new
        {
            patient,
            visit,
            doctor,
            hospital = new
            {
                name = Setting("Hospital Name"),
                address = Setting("Hospital Address"),
                phone = Setting("Hospital Phone"),
                logo = Setting("Hospital Logo"),
                opdHeader = Setting("OPD Header"),
                header = Setting("Blank Prescription Header")
            }
        });
    }

    private async Task CreateVisitBill(
        int patientId,
        Doctor doctor,
        string visitType,
        int? referrerId)
    {
        var hospitalCharge = GetBillingSetting("Hospital Charge", 100);
        var emergencyRate = GetBillingSetting("Emergency Rate", 500);

        var isEmergency =
            visitType == "Emergency Visit";

        var consultationCharge = isEmergency
            ? emergencyRate
            : doctor.ConsultationCharge;

        var billType = isEmergency
            ? "Emergency"
            : visitType;

        var bill = new Bill
        {
            PatientId = patientId,
            DoctorId = doctor.Id,
            ReferrerId = referrerId,
            BillType = billType,
            GrossAmount = consultationCharge + hospitalCharge,
            NetAmount = consultationCharge + hospitalCharge
        };

        bill.Items.Add(new BillItem
        {
            Description = isEmergency
                ? "Emergency Rate"
                : $"Consultation - {doctor.Name}",
            Quantity = 1,
            UnitPrice = consultationCharge,
            Amount = consultationCharge,
            ReferenceType = isEmergency ? "Setting" : "Doctor",
            ReferenceId = isEmergency ? null : doctor.Id
        });

        bill.Items.Add(new BillItem
        {
            Description = "Hospital Charge",
            Quantity = 1,
            UnitPrice = hospitalCharge,
            Amount = hospitalCharge,
            ReferenceType = "Setting"
        });

        _db.Bills.Add(bill);
        await _db.SaveChangesAsync();

        bill.BillNumber = $"BILL-{DateTime.Now:yyyyMMdd}-{bill.Id:000000}";

        if (!isEmergency && consultationCharge > 0)
        {
            _db.DoctorSettlements.Add(new DoctorSettlement
            {
                DoctorId = doctor.Id,
                SourceType = "Consultation",
                SourceId = bill.Id,
                Description = $"{billType} consultation · {bill.BillNumber}",
                PayableAmount = consultationCharge,
                EarnedDate = bill.BillDate.Date
            });
        }

        await _db.SaveChangesAsync();
    }

    private decimal GetBillingSetting(string name, decimal fallback)
    {
        var setting = _db.AppSettings
            .FirstOrDefault(x => x.Name == name && x.IsActive);

        return setting != null && decimal.TryParse(setting.Value, out var value)
            ? value
            : fallback;
    }
}
