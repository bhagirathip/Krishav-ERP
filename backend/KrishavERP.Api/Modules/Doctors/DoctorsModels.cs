namespace KrishavERP.Api.Modules.Doctors;

public class Doctor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Specialisation { get; set; } = "";
    public decimal ConsultationCharge { get; set; }
    public decimal IpdCharge { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DoctorSettlement
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string SourceType { get; set; } = "Consultation";
    public int SourceId { get; set; }
    public string? Description { get; set; }
    public decimal PayableAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime EarnedDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMode { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public int? PaidByStaffId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
