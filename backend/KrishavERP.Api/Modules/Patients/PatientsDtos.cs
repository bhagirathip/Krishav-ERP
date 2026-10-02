namespace KrishavERP.Api.Modules.Patients;

public class PatientCreateRequest
{
    public string Name { get; set; } = "";
    public int? Age { get; set; }
    public string? AgeUnit { get; set; }
    public string Gender { get; set; } = "";
    public string Phone { get; set; } = "";
    public string RegistrationType { get; set; } = "OPD";
    public int? DoctorId { get; set; }
    public int? BedId { get; set; }
    public string PayerType { get; set; } = "Cash";
    public string? OtRoom { get; set; }
    public DateTime? OtStartAtUtc { get; set; }
    public DateTime? OtEndAtUtc { get; set; }
    public DateTime? VisitDateUtc { get; set; }
    public string? BloodPressure { get; set; }
    public decimal? TemperatureC { get; set; }
    public int? Pulse { get; set; }
    public string? ChiefComplaint { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? Spo2 { get; set; }
    public bool ConfirmDuplicatePhone { get; set; }
    public string MarketingSource { get; set; } = "Walk-in";
    public int? ReferrerId { get; set; }
    public bool CreateBill { get; set; } = true;
    public DateTime? BillDate { get; set; }
    public string? Address { get; set; }
}

public class PatientEditRequest : PatientCreateRequest
{
}

public class FollowUpCompleteRequest
{
    public string Comment { get; set; } = "";
    public bool FollowUpNeeded { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
}

public class FollowUpCreateRequest
{
    public int PatientId { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string? Comment { get; set; }
}

public class PatientSourceRequest
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
