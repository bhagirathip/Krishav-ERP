namespace KrishavERP.Api.Modules.Pharmacy;

// Legacy flat medicine master, used only by the unrouted Pharmacy.vue screen.
public class Medicine
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime ExpiryDate { get; set; }
    public int QuantityTablets { get; set; }
    public int TabletsPerStrip { get; set; } = 10;
    public decimal PricePerStrip { get; set; }
    public string BatchNumber { get; set; } = "";
    public string? BoxNumber { get; set; }
    public string? Section { get; set; }
    public string? Component { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PharmacyBillReservation
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public int MedicineId { get; set; }
    public int QuantityTablets { get; set; }
    public bool StockReduced { get; set; }
}

public class PharmacyDistributor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? GstNumber { get; set; }
    public string? Pan { get; set; }
    public string? DrugsBazaarId { get; set; }
    public string? Fssai { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PharmacyMedicineType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class PharmacyPurchaseInvoice
{
    public int Id { get; set; }
    public int DistributorId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public string PaymentType { get; set; } = "Cash";
    public string? DistributorDocumentName { get; set; }
    public string? DistributorDocumentPath { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTaxableAmount { get; set; }
    public decimal TotalCgst { get; set; }
    public decimal TotalSgst { get; set; }
    public decimal PreRoundTotalAmount { get; set; }
    public decimal RoundOffAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public PharmacyDistributor? Distributor { get; set; }
    public List<PharmacyPurchaseItem> Items { get; set; } = new();
}

public class PharmacyPurchaseItem
{
    public int Id { get; set; }
    public int PurchaseInvoiceId { get; set; }
    public int MedicineTypeId { get; set; }
    public int SlNo { get; set; }
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
    public decimal TaxableAmount { get; set; }
    public decimal CgstPercent { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstPercent { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int TabletsPerStrip { get; set; } = 1;
    public int RemainingTablets { get; set; }
    public bool IsReturnableOnExpiry { get; set; } = true;
    public PharmacyPurchaseInvoice? PurchaseInvoice { get; set; }
    public PharmacyMedicineType? MedicineType { get; set; }
}

public class PharmacyExpiryAction
{
    public int Id { get; set; }
    public int PurchaseItemId { get; set; }
    public string ActionType { get; set; } = "Discard";
    public int QuantityInBaseUnits { get; set; }
    public string UnitType { get; set; } = "BaseUnit";
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public DateTime ActionDateUtc { get; set; } = DateTime.UtcNow;
}

public class PharmacySale
{
    public int Id { get; set; }
    public string SaleNumber { get; set; } = "";
    public int? PatientId { get; set; }
    public int BillId { get; set; }
    public int? DoctorId { get; set; }
    public string? OutsideDoctorName { get; set; }
    public string? WalkInPatientName { get; set; }
    public string? WalkInPhone { get; set; }
    public string PaymentMode { get; set; } = "Cash";
    public decimal TotalAmount { get; set; }
    public DateTime SaleDateUtc { get; set; } = DateTime.Now;

    // Set when this sale is medicine dispensed against an ongoing IPD stay
    // rather than a walk-in/OPD counter sale - see PharmacySalesController.Create.
    public int? IpdAdmissionId { get; set; }
    public List<PharmacySaleItem> Items { get; set; } = new();
}

public class PharmacySaleItem
{
    public int Id { get; set; }
    public int PharmacySaleId { get; set; }
    public int PurchaseItemId { get; set; }
    public string Manufacturer { get; set; } = "";
    public string Hsn { get; set; } = "";

    // Copied from the purchase batch at sale time (same as Hsn above) so the
    // printed invoice can show a real tax breakdown of the MRP - MRP is
    // retail-standard tax-inclusive, so this doesn't change what the patient
    // pays, it's extracted from the existing amount.
    public decimal CgstPercent { get; set; }
    public decimal SgstPercent { get; set; }
    public string ProductName { get; set; } = "";
    public string BatchNo { get; set; } = "";
    public string Packing { get; set; } = "";
    public decimal Mrp { get; set; }
    public string UnitType { get; set; } = "Strip";
    public int Quantity { get; set; }
    public int QuantityInTablets { get; set; }
    public decimal UnitPrice { get; set; }
    public int? DiscountTypeId { get; set; }
    public string? DiscountName { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

// A patient returning medicine (fully or partially) from a past sale. Kept
// as its own linked record rather than editing the original PharmacySale/
// PharmacySaleItem rows, so the original sale stays an untouched audit
// trail of what was actually sold - this is what actually happened, the
// return is a separate event layered on top of it. The financial effect
// (reducing the Bill's NetAmount, and recording a refund Payment if the
// returned portion was already paid) is applied directly to the existing
// Bill/Payment records, so every report that already reads from those
// tables reflects the return automatically with no separate reporting code.
public class PharmacyReturn
{
    public int Id { get; set; }
    public string ReturnNumber { get; set; } = "";
    public int PharmacySaleId { get; set; }
    public int BillId { get; set; }
    public DateTime ReturnDateUtc { get; set; } = DateTime.Now;
    public decimal TotalAmount { get; set; }

    // Only meaningful amounts actually refunded in cash/UPI/etc - a return
    // against a still-unpaid bill just reduces what's owed, nothing to
    // refund, so these stay null in that case.
    public decimal RefundedAmount { get; set; }
    public string? RefundMode { get; set; }
    public string? Reference { get; set; }
    public List<PharmacyReturnItem> Items { get; set; } = new();
}

public class PharmacyReturnItem
{
    public int Id { get; set; }
    public int PharmacyReturnId { get; set; }
    public int PharmacySaleItemId { get; set; }
    public int PurchaseItemId { get; set; }
    public string ProductName { get; set; } = "";
    public string BatchNo { get; set; } = "";
    public string UnitType { get; set; } = "Strip";
    public int Quantity { get; set; }
    public int QuantityInTablets { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal CgstPercent { get; set; }
    public decimal SgstPercent { get; set; }
    public decimal TotalAmount { get; set; }
}

// Stock issued to an internal hospital department (Lab/Dental/OT/Others),
// not to a patient - no Bill/Payment is created for these, since nobody is
// being charged at the point of dispensing. The hospital settles its running
// tab with the pharmacy periodically instead (see HospitalSettlement below).
public class HospitalIssue
{
    public int Id { get; set; }
    public string IssueNumber { get; set; } = "";
    public string Department { get; set; } = ""; // "Lab" | "Dental" | "OT" | "Others"
    public DateTime IssueDateUtc { get; set; } = DateTime.Now;
    public decimal TotalAmount { get; set; }

    // Null until this issue is bundled into a settlement - settling always
    // sweeps up every unsettled issue at once (see PharmacyHospitalController),
    // so this is really just "settled or not", not a partial-settle marker.
    public int? SettlementId { get; set; }
    public List<HospitalIssueItem> Items { get; set; } = new();
}

public class HospitalIssueItem
{
    public int Id { get; set; }
    public int HospitalIssueId { get; set; }
    public int PurchaseItemId { get; set; }
    public string ProductName { get; set; } = "";
    public string BatchNo { get; set; } = "";
    public string UnitType { get; set; } = "Strip";
    public int Quantity { get; set; }
    public int QuantityInTablets { get; set; }
    public decimal Mrp { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
}

// One lump-sum settlement of every HospitalIssue that was still unsettled at
// the moment "Settle Now" was clicked. The settlement itself is a single
// payable total, but its linked issues keep their own IssueDateUtc, so the
// settlement detail view can still show a date-wise breakdown of what's
// being paid for.
public class HospitalSettlement
{
    public int Id { get; set; }
    public string SettlementNumber { get; set; } = "";
    public DateTime SettlementDateUtc { get; set; } = DateTime.Now;
    public decimal TotalAmount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? PaymentMode { get; set; }
    public string? ReferenceNumber { get; set; }
}
