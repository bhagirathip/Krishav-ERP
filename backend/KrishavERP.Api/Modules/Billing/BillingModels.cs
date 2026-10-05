namespace KrishavERP.Api.Modules.Billing;

public class BillTypeMaster
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsBillable { get; set; } = true;
    public bool IsOpdType { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Bill
{
    public int Id { get; set; }
    public string BillNumber { get; set; } = "";
    public int? PatientId { get; set; }

    // Free-text patient name for a bill created without picking a registered
    // patient (e.g. a name typed that doesn't match anyone). Mutually
    // exclusive with PatientId, mirroring PharmacySale.WalkInPatientName.
    public string? WalkInPatientName { get; set; }
    public int? DoctorId { get; set; }
    public int? ReferrerId { get; set; }
    public int? BulkDiscountTypeId { get; set; }
    public string? BulkDiscountName { get; set; }
    public string BillType { get; set; } = "OPD";
    public decimal GrossAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }

    // Small manual rounding nudge (-9..9, decimals like 5.05 allowed) folded
    // into NetAmount so the final total lands on a clean number (e.g.
    // 448 + 2 => 450). Kept separately so the app can show it as its own
    // field, but it is never a separate line on the printed bill - it just
    // shows up as part of the total already.
    public decimal RoundOff { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = "Unpaid";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Actual date of service for the bill; defaults to now but can be backdated
    // (e.g. Patient add) to enter historical data under the correct reporting date.
    public DateTime BillDate { get; set; } = DateTime.UtcNow;
    public List<BillItem> Items { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}

public class BillItem
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string DiscountMode { get; set; } = "Percent";
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public int? DiscountTypeId { get; set; }
    public string? DiscountName { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public decimal Amount { get; set; }
    public string Mode { get; set; } = "Cash";
    public string? Reference { get; set; }
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
}

public class DiscountType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string DiscountMode { get; set; } = "Percent";
    public decimal Value { get; set; }
    public string Scope { get; set; } = "Individual";
    public bool IsActive { get; set; } = true;
}

// A price list for miscellaneous hospital charges (Dressing Charge, Injection
// Charge, Saline Charge, Emergency Charge, etc.) that don't belong to the
// Lab/Pharmacy test or product catalogs - a flat name+price lookup, the same
// shape as those catalogs, so it can be billed the same way.
public class ServiceCharge
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }

    // Matches a BillTypeMaster.Name (e.g. "OPD", "Emergency") - only bills of
    // this type offer the charge in their Description catalog dropdown.
    public string BillType { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
