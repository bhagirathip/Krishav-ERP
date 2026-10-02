namespace KrishavERP.Api.Modules.Referrals;

public class ReferrerRequest
{
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string ReferrerType { get; set; } = "Person";
    public int? DoctorId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ReferralPayoutRequest
{
    public int ReferrerId { get; set; }
    public int PatientId { get; set; }
    public decimal Amount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMode { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}
