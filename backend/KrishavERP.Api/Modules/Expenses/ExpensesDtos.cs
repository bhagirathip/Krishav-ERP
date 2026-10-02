namespace KrishavERP.Api.Modules.Expenses;

public class DailyExpenseRequest
{
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = "General";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string PaymentMode { get; set; } = "Cash";
    public int? PaidByStaffId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}

public class PharmacyPurchasePaymentRequest
{
    public int PurchaseInvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMode { get; set; } = "Cash";
    public string? ReferenceNumber { get; set; }
    public int? PaidByStaffId { get; set; }
    public string? Notes { get; set; }
}

public class LabPurchaseExpenseRequest
{
    public DateTime ExpenseDate { get; set; }
    public string? VendorName { get; set; }
    public string? InvoiceNumber { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMode { get; set; }
    public string? ReferenceNumber { get; set; }
    public int? PaidByStaffId { get; set; }
    public string? Notes { get; set; }
}
