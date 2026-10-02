namespace KrishavERP.Api.Modules.Referrals;

public class Referrer
{
    public int Id { get; set; }
    public string ReferrerCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string ReferrerType { get; set; } = "Person";
    public int? DoctorId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ReferralPayout
{
    public int Id { get; set; }
    public int ReferrerId { get; set; }
    public int PatientId { get; set; }
    public decimal Amount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMode { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
