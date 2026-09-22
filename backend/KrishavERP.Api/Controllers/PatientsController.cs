using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KrishavERP.Api.Services;
using System.Text.RegularExpressions;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly FollowUpService _followUpService;

    public PatientsController(
        AppDbContext db,
        IWebHostEnvironment environment,
        FollowUpService followUpService)
    {
        _db = db;
        _environment = environment;
        _followUpService = followUpService;
    }

    [HttpGet("duplicates")]
    public async Task<IActionResult> GetDuplicates([FromQuery] string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return Ok(Array.Empty<object>());
        }

        var matches = await _db.Patients
            .Where(x => !x.IsDeleted && x.Phone == phone)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PatientCode,
                x.Name,
                x.Age,
                x.Gender,
                x.Phone,
                x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(matches);
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? q)
    {
        var query = _db.Patients.Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.Name.Contains(q) ||
                x.Phone.Contains(q) ||
                x.PatientCode.Contains(q));
        }

        var patients = await query
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        var result = new List<object>();

        foreach (var patient in patients)
        {
            var latestOpd = await _db.OpdVisits
                .Where(x => x.PatientId == patient.Id && !x.IsCancelled)
                .OrderByDescending(x => x.VisitDateUtc)
                .FirstOrDefaultAsync();

            var activeIpd = await _db.IpdAdmissions
                .Where(x => x.PatientId == patient.Id && x.Status == "Admitted")
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            var latestOt = await _db.OtBookings
                .Where(x => x.PatientId == patient.Id && x.Status != "Cancelled")
                .OrderByDescending(x => x.StartAtUtc)
                .FirstOrDefaultAsync();

            int? doctorId = activeIpd?.DoctorId ?? latestOpd?.DoctorId ?? latestOt?.DoctorId;
            var doctor = doctorId.HasValue
                ? await _db.Doctors.FindAsync(doctorId.Value)
                : null;

            var bed = activeIpd == null
                ? null
                : await _db.WardBeds.FindAsync(activeIpd.BedId);

            var registrationType = activeIpd != null
                ? "IPD"
                : latestOt != null && latestOpd == null
                    ? "OT"
                    : latestOpd == null
                        ? "Patient"
                        : latestOpd.IsEmergency
                            ? "Emergency"
                            : "OPD";

            var referrer = patient.ReferrerId.HasValue
                ? await _db.Referrers.FindAsync(patient.ReferrerId.Value)
                : null;

            result.Add(new
            {
                patient.Id,
                patient.PatientCode,
                patient.Name,
                patient.Age,
                patient.Gender,
                patient.Phone,
                patient.CreatedAtUtc,
                patient.MarketingSource,
                patient.ReferrerId,
                patient.Address,
                ReferrerName = referrer?.Name,
                RegistrationType = registrationType,
                DoctorId = doctorId,
                DoctorName = doctor?.Name,
                BedNumber = bed?.BedNumber,
                OtRoom = latestOt?.OtRoom,
                OtStartAtUtc = latestOt?.StartAtUtc,
                BloodPressure = latestOpd?.BloodPressure,
                TemperatureC = latestOpd?.TemperatureC,
                WeightKg = latestOpd?.WeightKg,
                HeightCm = latestOpd?.HeightCm,
                Spo2 = latestOpd?.Spo2,
                ChiefComplaint = latestOpd?.ChiefComplaint
            });
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Add(PatientCreateRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                message = "Please correct the required fields.",
                errors
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
            !await _db.Referrers.AnyAsync(x => x.Id == request.ReferrerId.Value && x.IsActive))
        {
            return BadRequest(new { message = "Selected referrer was not found." });
        }

        var duplicates = await _db.Patients
            .Where(x => !x.IsDeleted && x.Phone == request.Phone)
            .Select(x => new
            {
                x.Id,
                x.PatientCode,
                x.Name,
                x.Age,
                x.Gender,
                x.Phone
            })
            .ToListAsync();

        if (duplicates.Count > 0 && !request.ConfirmDuplicatePhone)
        {
            return Conflict(new
            {
                code = "DUPLICATE_PHONE",
                message = "This phone number already exists.",
                patients = duplicates
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var patient = new Patient
        {
            Name = request.Name.Trim(),
            Age = request.Age!.Value,
            Gender = request.Gender,
            Phone = request.Phone.Trim(),
            MarketingSource = string.IsNullOrWhiteSpace(request.MarketingSource) ? "Walk-in" : request.MarketingSource,
            ReferrerId = request.ReferrerId,
            Address = request.Address?.Trim()
        };

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        patient.PatientCode = $"KHC-P-{patient.Id:000000}";

        if (request.RegistrationType is "OPD" or "Emergency")
        {
            var doctor = await _db.Doctors.FindAsync(request.DoctorId!.Value);
            if (doctor == null || !doctor.IsActive)
            {
                return BadRequest(new { message = "Selected doctor was not found." });
            }

            var visit = new OpdVisit
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                IsEmergency = request.RegistrationType == "Emergency",
                VisitType = request.RegistrationType == "Emergency"
                    ? "Emergency Visit"
                    : "OPD",
                BloodPressure = request.BloodPressure,
                TemperatureC = request.TemperatureC,
                WeightKg = request.WeightKg,
                HeightCm = request.HeightCm,
                Spo2 = request.Spo2,
                ChiefComplaint = request.ChiefComplaint,
                MarketingSource = request.MarketingSource,
                ReferrerId = request.ReferrerId,
                VisitDateUtc = DateTime.Now
            };

            _db.OpdVisits.Add(visit);
            await _db.SaveChangesAsync();

            visit.VisitNumber = $"OPD-{DateTime.Now:yyyyMMdd}-{visit.Id:000000}";

            await _followUpService.CreateForConsultationAsync(
                patient.Id,
                visit.Id,
                visit.VisitDateUtc);

            if (request.RegistrationType == "OPD" && request.CreateBill)
            {
                await CreateOpdBill(patient.Id, doctor, request.BillDate);
            }
        }
        else if (request.RegistrationType == "Appointment")
        {
            var doctor = await _db.Doctors.FindAsync(request.DoctorId!.Value);
            if (doctor == null || !doctor.IsActive)
            {
                return BadRequest(new { message = "Selected doctor was not found." });
            }

            var visit = new OpdVisit
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                VisitType = "Appointment",
                BloodPressure = request.BloodPressure,
                TemperatureC = request.TemperatureC,
                WeightKg = request.WeightKg,
                HeightCm = request.HeightCm,
                Spo2 = request.Spo2,
                ChiefComplaint = request.ChiefComplaint,
                MarketingSource = request.MarketingSource,
                ReferrerId = request.ReferrerId,
                VisitDateUtc = request.VisitDateUtc!.Value
            };

            _db.OpdVisits.Add(visit);
            await _db.SaveChangesAsync();

            visit.VisitNumber = $"OPD-{DateTime.Now:yyyyMMdd}-{visit.Id:000000}";
        }
        else if (request.RegistrationType == "IPD")
        {
            var bed = await _db.WardBeds
                .FirstOrDefaultAsync(x =>
                    x.Id == request.BedId &&
                    x.IsActive &&
                    !x.IsOccupied);

            if (bed == null)
            {
                return BadRequest(new { message = "Selected bed/cabin is not available." });
            }

            var admission = new IpdAdmission
            {
                PatientId = patient.Id,
                DoctorId = request.DoctorId!.Value,
                BedId = bed.Id,
                PayerType = request.PayerType
            };

            _db.IpdAdmissions.Add(admission);
            await _db.SaveChangesAsync();

            admission.AdmissionNumber = $"IPD-{DateTime.Now:yyyyMMdd}-{admission.Id:000000}";
            bed.IsOccupied = true;
            bed.CurrentIpdAdmissionId = admission.Id;
        }
        else if (request.RegistrationType == "OT")
        {
            if (!request.OtStartAtUtc.HasValue || !request.OtEndAtUtc.HasValue)
            {
                return BadRequest(new { message = "OT date/time is required." });
            }

            var hasConflict = await _db.OtBookings.AnyAsync(x =>
                x.OtRoom == request.OtRoom &&
                x.Status != "Cancelled" &&
                request.OtStartAtUtc.Value < x.EndAtUtc &&
                request.OtEndAtUtc.Value > x.StartAtUtc);

            if (hasConflict)
            {
                return Conflict(new { message = "Selected OT is already booked for that time." });
            }

            var booking = new OtBooking
            {
                PatientId = patient.Id,
                DoctorId = request.DoctorId!.Value,
                OtRoom = request.OtRoom!,
                StartAtUtc = request.OtStartAtUtc.Value,
                EndAtUtc = request.OtEndAtUtc.Value
            };

            _db.OtBookings.Add(booking);
            await _db.SaveChangesAsync();

            booking.BookingNumber = $"OT-{DateTime.Now:yyyyMMdd}-{booking.Id:000000}";
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            patient.Id,
            patient.PatientCode
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, PatientEditRequest request)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null || patient.IsDeleted)
        {
            return NotFound(new { message = "Patient not found." });
        }

        var errors = Validate(request, requireType: false);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                message = "Please correct the required fields.",
                errors
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
            !await _db.Referrers.AnyAsync(x => x.Id == request.ReferrerId.Value && x.IsActive))
        {
            return BadRequest(new { message = "Selected referrer was not found." });
        }

        patient.Name = request.Name.Trim();
        patient.Age = request.Age!.Value;
        patient.Gender = request.Gender;
        patient.Phone = request.Phone.Trim();
        patient.MarketingSource = string.IsNullOrWhiteSpace(request.MarketingSource) ? "Walk-in" : request.MarketingSource;
        patient.ReferrerId = request.ReferrerId;
        patient.Address = request.Address?.Trim();

        var latestOpd = await _db.OpdVisits
            .Where(x => x.PatientId == id && !x.IsCancelled)
            .OrderByDescending(x => x.VisitDateUtc)
            .FirstOrDefaultAsync();

        if (latestOpd != null)
        {
            latestOpd.DoctorId = request.DoctorId;
            latestOpd.BloodPressure = request.BloodPressure;
            latestOpd.TemperatureC = request.TemperatureC;
            latestOpd.WeightKg = request.WeightKg;
            latestOpd.HeightCm = request.HeightCm;
            latestOpd.Spo2 = request.Spo2;
            latestOpd.ChiefComplaint = request.ChiefComplaint;
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null)
        {
            return NotFound();
        }

        patient.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("{id:int}/documents")]
    public async Task<IActionResult> GetDocuments(int id)
    {
        var documents = await _db.PatientDocuments
            .Where(x => x.PatientId == id)
            .OrderByDescending(x => x.UploadedAtUtc)
            .ToListAsync();

        return Ok(documents);
    }

    [HttpPost("{id:int}/documents")]
    public async Task<IActionResult> UploadDocuments(
        int id,
        List<IFormFile> files,
        [FromForm] string category)
    {
        if (files.Count == 0)
        {
            return BadRequest(new { message = "Select at least one document." });
        }

        if (!await _db.Patients.AnyAsync(x => x.Id == id && !x.IsDeleted))
        {
            return NotFound(new { message = "Patient not found." });
        }

        var categoryExists = await _db.DocumentCategories
            .AnyAsync(x => x.IsActive && x.Name == category);

        if (!categoryExists)
        {
            return BadRequest(new { message = "Please select a valid document category." });
        }

        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        var directory = Path.Combine(webRoot, "uploads", "patients", id.ToString());
        Directory.CreateDirectory(directory);

        var saved = new List<PatientDocument>();

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!new[] { ".pdf", ".jpg", ".jpeg", ".png" }.Contains(extension))
            {
                continue;
            }

            var storedName = $"{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(directory, storedName);

            await using (var stream = System.IO.File.Create(physicalPath))
            {
                await file.CopyToAsync(stream);
            }

            var document = new PatientDocument
            {
                PatientId = id,
                Category = category,
                FileName = file.FileName,
                StoredPath = $"/uploads/patients/{id}/{storedName}"
            };

            _db.PatientDocuments.Add(document);
            saved.Add(document);
        }

        await _db.SaveChangesAsync();
        return Ok(saved);
    }

    private List<string> Validate(PatientCreateRequest request, bool requireType = true)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("Patient name is required.");

        if (!request.Age.HasValue || request.Age < 0)
            errors.Add("Valid age is required.");

        if (string.IsNullOrWhiteSpace(request.Gender))
            errors.Add("Gender is required.");

        if (!Regex.IsMatch(request.Phone ?? string.Empty, @"^[6-9]\d{9}$"))
            errors.Add("Phone number must be a valid 10-digit Indian mobile number.");

        if (requireType && !new[] { "OPD", "Emergency", "IPD", "OT", "Appointment" }.Contains(request.RegistrationType))
            errors.Add("Patient type is required.");

        if (new[] { "OPD", "Emergency", "IPD", "OT", "Appointment" }.Contains(request.RegistrationType) && !request.DoctorId.HasValue)
            errors.Add("Doctor is required.");

        if (request.RegistrationType == "IPD" && !request.BedId.HasValue)
            errors.Add("Bed/Cabin is required.");

        if (request.RegistrationType == "OT" &&
            (string.IsNullOrWhiteSpace(request.OtRoom) ||
             !request.OtStartAtUtc.HasValue ||
             !request.OtEndAtUtc.HasValue))
        {
            errors.Add("OT number and time are required.");
        }

        if (request.RegistrationType == "Appointment")
        {
            if (!request.VisitDateUtc.HasValue)
                errors.Add("Appointment date/time is required.");
            else if (request.VisitDateUtc.Value < DateTime.Now)
                errors.Add("Appointment cannot be before current date/time.");
        }

        if (request.HeightCm.HasValue &&
            (request.HeightCm < 30 || request.HeightCm > 250))
        {
            errors.Add("Height must be between 30 cm and 250 cm.");
        }

        if (request.WeightKg.HasValue &&
            (request.WeightKg < 1 || request.WeightKg > 500))
        {
            errors.Add("Weight must be between 1 kg and 500 kg.");
        }

        if (request.Spo2.HasValue &&
            (request.Spo2 < 50 || request.Spo2 > 100))
        {
            errors.Add("SpO₂ must be between 50 and 100.");
        }

        if (request.BillDate.HasValue && request.BillDate.Value.Date > DateTime.Now.Date)
        {
            errors.Add("Bill date cannot be in the future.");
        }

        return errors;
    }

    private async Task CreateOpdBill(int patientId, Doctor doctor, DateTime? billDate = null)
    {
        var hospitalCharge = GetBillingSetting("Hospital Charge", 100);
        var gross = doctor.ConsultationCharge + hospitalCharge;
        var resolvedBillDate = billDate ?? DateTime.Now;

        var patient = await _db.Patients.FindAsync(patientId);
        var bill = new Bill
        {
            PatientId = patientId,
            DoctorId = doctor.Id,
            ReferrerId = patient?.ReferrerId,
            BillType = "OPD",
            GrossAmount = gross,
            NetAmount = gross,
            BillDate = resolvedBillDate
        };

        bill.Items.Add(new BillItem
        {
            Description = $"Consultation - {doctor.Name}",
            Quantity = 1,
            UnitPrice = doctor.ConsultationCharge,
            Amount = doctor.ConsultationCharge,
            ReferenceType = "Doctor",
            ReferenceId = doctor.Id
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

        _db.DoctorSettlements.Add(new DoctorSettlement
        {
            DoctorId = doctor.Id,
            SourceType = "Consultation",
            SourceId = bill.Id,
            Description = $"{bill.BillType} consultation · {bill.BillNumber}",
            PayableAmount = doctor.ConsultationCharge,
            EarnedDate = bill.BillDate.Date
        });

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
