namespace KrishavERP.Api.Modules.Doctors;

public class DoctorSettlementPaymentRequest
{
    public decimal PaidAmount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMode { get; set; }
    public string? ReferenceNumber { get; set; }
    public int? PaidByStaffId { get; set; }
    public string? Notes { get; set; }
}

public class DoctorSettlementBulkPaymentRequest
{
    public int DoctorId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMode { get; set; }
    public string? ReferenceNumber { get; set; }
    public int? PaidByStaffId { get; set; }
    public string? Notes { get; set; }
}
