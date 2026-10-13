namespace KrishavERP.Api.Modules.Expenses;

public class DailyExpense
{
    public int Id { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = "General";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string PaymentMode { get; set; } = "Cash";
    public int? PaidByStaffId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // "Paid" on screen means "settled" - once settled (see
    // ExpensesController.SettleOneDaily) this row is locked: no further
    // Edit/Delete. Null until then, same lock-flag shape as
    // HospitalIssue.SettlementId.
    public bool IsSettled { get; set; }
    public int? SettlementId { get; set; }

    // Role of whoever recorded this expense, captured once at creation
    // (ExpensesController.AddDaily) - only an Administrator can settle
    // expenses, but never one they added themselves (see SettleOneDaily),
    // so this is what that self-settlement guard checks against.
    public string CreatedByRole { get; set; } = "";
}

// One expense, settled - clicking Settle on a single DailyExpense row locks
// it (see ExpensesController.SettleOneDaily). Only an Administrator can
// settle, and never an expense they added themselves. A row paid
// out-of-pocket by a staff member (PaidByStaffId set) becomes reimbursable
// on that staff member's salary for the month it falls in once settled.
public class ExpenseSettlement
{
    public int Id { get; set; }
    public string SettlementNumber { get; set; } = "";
    public DateTime SettlementDateUtc { get; set; } = DateTime.Now;
    public decimal TotalAmount { get; set; }
    public decimal ReimbursedAmount { get; set; }
    public string SettledByRole { get; set; } = "";
}

public class PharmacyPurchasePayment
{
    public int Id { get; set; }
    public int PurchaseInvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMode { get; set; } = "Cash";
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public int? PaidByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class LabPurchaseExpense
{
    public int Id { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? VendorName { get; set; }
    public string? InvoiceNumber { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMode { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public int? PaidByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
