namespace KrishavERP.Api.Modules.Visits;

public class OpdVisit
{
    public int Id { get; set; }
    public string VisitNumber { get; set; } = "";
    public int PatientId { get; set; }
    public int? DoctorId { get; set; }
    public bool IsEmergency { get; set; }
    public string? BloodPressure { get; set; }
    public decimal? TemperatureC { get; set; }
    public int? Pulse { get; set; }
    public string? ChiefComplaint { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? Spo2 { get; set; }
    public string VisitType { get; set; } = "OPD";
    public string MarketingSource { get; set; } = "Walk-in";
    public int? ReferrerId { get; set; }
    public DateTime VisitDateUtc { get; set; } = DateTime.UtcNow;
    public bool ConvertedToIpd { get; set; }
    public bool IsCancelled { get; set; }
}

public class WardBed
{
    public int Id { get; set; }
    public string BedNumber { get; set; } = "";
    public string RoomType { get; set; } = "Bed";
    public decimal CashRate { get; set; }
    public decimal InsuranceRate { get; set; }
    public decimal AyushmanRate { get; set; }
    public bool IsOccupied { get; set; }
    public int? CurrentIpdAdmissionId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class IpdAdmission
{
    public int Id { get; set; }
    public string AdmissionNumber { get; set; } = "";
    public int PatientId { get; set; }
    public int? SourceOpdVisitId { get; set; }
    public int DoctorId { get; set; }
    public int BedId { get; set; }
    public string PayerType { get; set; } = "Cash";
    public DateTime AdmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DischargedAtUtc { get; set; }
    public string Status { get; set; } = "Admitted";
}

public class IpdVital
{
    public int Id { get; set; }
    public int IpdAdmissionId { get; set; }
    public string? BloodPressure { get; set; }
    public decimal? TemperatureC { get; set; }
    public int? Pulse { get; set; }
    public decimal? Spo2 { get; set; }
    public string? Notes { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}

public class IpdDoctorNote
{
    public int Id { get; set; }
    public int IpdAdmissionId { get; set; }
    public int DoctorId { get; set; }
    public string Suggestion { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class IpdNursingNote
{
    public int Id { get; set; }
    public int IpdAdmissionId { get; set; }
    public string ActionTaken { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class IpdDocument
{
    public int Id { get; set; }
    public int IpdAdmissionId { get; set; }
    public string Category { get; set; } = "Other";
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}

public class OtBooking
{
    public int Id { get; set; }
    public string BookingNumber { get; set; } = "";
    public int? PatientId { get; set; }
    public string? ExternalPatientName { get; set; }
    public string? ExternalPatientPhone { get; set; }
    public int DoctorId { get; set; }
    public string OtRoom { get; set; } = "OT 1";
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public string? ProcedureName { get; set; }
    public string? MedicinesUsed { get; set; }
    public string? VitalsAndLabs { get; set; }
    public decimal DoctorPayoutAmount { get; set; }
    public string Status { get; set; } = "Booked";
}
