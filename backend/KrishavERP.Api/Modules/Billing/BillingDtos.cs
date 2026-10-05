namespace KrishavERP.Api.Modules.Billing;

public class PaymentRequest
{
    public decimal Amount { get; set; }
    public string Mode { get; set; } = "Cash";
    public string? Reference { get; set; }
}

public class BillCreateRequest
{
    public int? PatientId { get; set; }
    public string? WalkInPatientName { get; set; }
    public int? DoctorId { get; set; }
    public string BillType { get; set; } = "OPD";
    public int? BulkDiscountTypeId { get; set; }

    // User-entered value for a variable-rate Bulk discount (Manager
    // Discount/Admin Discount, where BulkDiscountTypeId is set but the type's
    // own Value isn't the real cap) or for a General Manager's direct entry
    // (no BulkDiscountTypeId at all). BulkDiscountMode only matters for the
    // latter case - Manager/Admin Discount are always Percent.
    public decimal? BulkDiscountValue { get; set; }
    public string? BulkDiscountMode { get; set; }
    public decimal RoundOff { get; set; }
    public List<BillCreateItem> Items { get; set; } = new();
}

public class BillCreateItem
{
    public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public int? DiscountTypeId { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
}

public class DiscountTypeRequest
{
    public string Name { get; set; } = "";
    public string DiscountMode { get; set; } = "Percent";
    public decimal Value { get; set; }
    public string Scope { get; set; } = "Individual";
    public bool IsActive { get; set; } = true;
}

public class ServiceChargeRequest
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string BillType { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class BillTypeMasterRequest
{
    public string Name { get; set; } = "";
    public bool IsBillable { get; set; } = true;
    public bool IsOpdType { get; set; }
    public bool IsActive { get; set; } = true;
}
