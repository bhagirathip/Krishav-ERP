namespace KrishavERP.Api.Modules.Pharmacy;

public class PharmacyBillRequest
{
    public int? PatientId { get; set; }
    public string DiscountMode { get; set; } = "Percent";
    public decimal DiscountValue { get; set; }
    public List<PharmacyBillLine> Items { get; set; } = new();
}

public class PharmacyBillLine
{
    public int MedicineId { get; set; }
    public string UnitType { get; set; } = "Tablet";
    public int Quantity { get; set; }
}

public class PharmacyDistributorRequest
{
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? GstNumber { get; set; }
    public string? Pan { get; set; }
    public string? DrugsBazaarId { get; set; }
    public string? Fssai { get; set; }
    public string? Address { get; set; }
}

public class PharmacyPurchaseInvoiceRequest
{
    public int DistributorId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public string PaymentType { get; set; } = "Cash";
    public List<PharmacyPurchaseItemRequest> Items { get; set; } = new();
}

public class PharmacyPurchaseItemRequest
{
    public int? Id { get; set; }
    public int SlNo { get; set; }
    public int MedicineTypeId { get; set; }
    public string Manufacturer { get; set; } = "";
    public string Hsn { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Packing { get; set; } = "";
    public string BatchNo { get; set; } = "";
    public DateTime ExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal Rate { get; set; }
    public int Quantity { get; set; }
    public int Bonus { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal CgstPercent { get; set; }
    public decimal SgstPercent { get; set; }
    public int TabletsPerStrip { get; set; } = 1;
    public bool IsReturnableOnExpiry { get; set; } = true;
}

public class PharmacySaleRequest
{
    public int? PatientId { get; set; }
    public int? DoctorId { get; set; }
    public string? OutsideDoctorName { get; set; }
    public string? WalkInPatientName { get; set; }
    public string? WalkInPhone { get; set; }
    public string PaymentMode { get; set; } = "Cash";
    public int? IpdAdmissionId { get; set; }
    public decimal RoundOff { get; set; }
    public List<PharmacySaleLineRequest> Items { get; set; } = new();
}

public class PharmacySaleLineRequest
{
    public int PurchaseItemId { get; set; }
    public string UnitType { get; set; } = "Strip";
    public int Quantity { get; set; }
    public int? DiscountTypeId { get; set; }
}

public class PharmacyMedicineTypeRequest
{
    public string Name { get; set; } = "";
}

public class PharmacyExpiryActionRequest
{
    public int PurchaseItemId { get; set; }
    public string ActionType { get; set; } = "Discard";
    public string UnitType { get; set; } = "Pack";
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}
