namespace KrishavERP.Api.Modules.Visits;

public class OpdCreateRequest
{
    public string VisitType { get; set; } = "OPD";
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public bool IsEmergency { get; set; }
    public DateTime VisitDateUtc { get; set; }
    public string? BloodPressure { get; set; }
    public decimal? TemperatureC { get; set; }
    public int? Pulse { get; set; }
    public string? ChiefComplaint { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? Spo2 { get; set; }
    public bool CreateBill { get; set; } = true;
    public string MarketingSource { get; set; } = "Walk-in";
    public int? ReferrerId { get; set; }
    public DateTime? FollowUpDate { get; set; }
}

// Deliberately narrower than OpdCreateRequest: PatientId, VisitType and
// VisitDateUtc drive billing/emergency logic already applied when the visit
// was created, so editing only touches the doctor/vitals/complaint/referral
// fields a receptionist would realistically need to correct afterwards.
public class OpdEditRequest
{
    public int DoctorId { get; set; }
    public string? BloodPressure { get; set; }
    public decimal? TemperatureC { get; set; }
    public int? Pulse { get; set; }
    public string? ChiefComplaint { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? Spo2 { get; set; }
    public string MarketingSource { get; set; } = "Walk-in";
    public int? ReferrerId { get; set; }
}

public class ConvertIpdRequest
{
    public int BedId { get; set; }
    public int DoctorId { get; set; }
    public string PayerType { get; set; } = "Cash";
}

public class IpdVitalRequest
{
    public string? BloodPressure { get; set; }
    public decimal? TemperatureC { get; set; }
    public int? Pulse { get; set; }
    public decimal? Spo2 { get; set; }
    public string? Notes { get; set; }
}

public class IpdDoctorNoteRequest
{
    public int DoctorId { get; set; }
    public string Suggestion { get; set; } = "";
}

public class IpdNursingNoteRequest
{
    public string ActionTaken { get; set; } = "";
}

public class OtBookingRequest
{
    public int? PatientId { get; set; }
    public string? ExternalPatientName { get; set; }
    public string? ExternalPatientPhone { get; set; }
    public int DoctorId { get; set; }
    public string OtRoom { get; set; } = "OT 1";
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public string? ProcedureName { get; set; }
    public decimal DoctorPayoutAmount { get; set; }
}
