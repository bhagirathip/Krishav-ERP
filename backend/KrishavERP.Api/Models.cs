namespace KrishavERP.Api;
public class AppUser
{
    public int Id
    {
        get;
        set;
    }
    public string Username
    {
        get;
        set;
    }
    = "";
    public string PasswordHash
    {
        get;
        set;
    }
    = "";
    public string DisplayName
    {
        get;
        set;
    }
    = "";
    public bool IsActive
    {
        get;
        set;
    }
    = true;
    public int? RoleId
    {
        get;
        set;
    }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
}
public class AppRole
{
    public int Id
    {
        get;
        set;
    }
    public string Name
    {
        get;
        set;
    }
    = "";
    public string Description
    {
        get;
        set;
    }
    = "";
    public bool IsActive
    {
        get;
        set;
    }
    = true;
}
public class RolePermission
{
    public int Id
    {
        get;
        set;
    }
    public int RoleId
    {
        get;
        set;
    }
    public string Module
    {
        get;
        set;
    }
    = "";
    public bool CanView
    {
        get;
        set;
    }
    public bool CanAdd
    {
        get;
        set;
    }
    public bool CanEdit
    {
        get;
        set;
    }
    public bool CanDelete
    {
        get;
        set;
    }
}
public class Doctor
{
    public int Id
    {
        get;
        set;
    }
    public string Name
    {
        get;
        set;
    }
    = "";
    public string Specialisation
    {
        get;
        set;
    }
    = "";
    public decimal ConsultationCharge
    {
        get;
        set;
    }
    public decimal IpdCharge
    {
        get;
        set;
    }
    public bool IsActive
    {
        get;
        set;
    }
    = true;
    public DateTime CreatedAtUtc
    {
        get;
        set;
    }
    = DateTime.UtcNow;
}
public class PatientSource
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Patient
{
    public int Id
    {
        get;
        set;
    }
    public string PatientCode
    {
        get;
        set;
    }
    = "";
    public string Name
    {
        get;
        set;
    }
    = "";
    public int Age
    {
        get;
        set;
    }
    public string Gender
    {
        get;
        set;
    }
    = "";
    public string Phone
    {
        get;
        set;
    }
    = "";
    public bool IsDeleted
    {
        get;
        set;
    }
    public DateTime CreatedAtUtc
    {
        get;
        set;
    }
    = DateTime.UtcNow;
    public string MarketingSource { get; set; } = "Walk-in";
    public int? ReferrerId { get; set; }
    public string? Address { get; set; }
}
public class OpdVisit
{
    public int Id
    {
        get;
        set;
    }
    public string VisitNumber
    {
        get;
        set;
    }
    = "";
    public int PatientId
    {
        get;
        set;
    }
    public int? DoctorId
    {
        get;
        set;
    }
    public bool IsEmergency
    {
        get;
        set;
    }
    public string? BloodPressure
    {
        get;
        set;
    }
    public decimal? TemperatureC
    {
        get;
        set;
    }
    public string? ChiefComplaint
    {
        get;
        set;
    }
    public decimal? WeightKg
    {
        get;
        set;
    }
    public decimal? HeightCm
    {
        get;
        set;
    }
    public decimal? Spo2
    {
        get;
        set;
    }
    public string VisitType
    {
        get;
        set;
    }
    = "OPD";
    public string MarketingSource { get; set; } = "Walk-in";
    public int? ReferrerId { get; set; }
    public DateTime VisitDateUtc
    {
        get;
        set;
    }
    = DateTime.UtcNow;
    public bool ConvertedToIpd
    {
        get;
        set;
    }
    public bool IsCancelled
    {
        get;
        set;
    }
}
public class PatientDocument
{
    public int Id
    {
        get;
        set;
    }
    public int PatientId
    {
        get;
        set;
    }
    public string Category
    {
        get;
        set;
    }
    = "Other";
    public string FileName
    {
        get;
        set;
    }
    = "";
    public string StoredPath
    {
        get;
        set;
    }
    = "";
    public DateTime UploadedAtUtc
    {
        get;
        set;
    }
    = DateTime.UtcNow;
}
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
    public int Id
    {
        get;
        set;
    }
    public string BillNumber
    {
        get;
        set;
    }
    = "";
    public int? PatientId
    {
        get;
        set;
    }
    // Free-text patient name for a bill created without picking a registered
    // patient (e.g. a name typed that doesn't match anyone). Mutually
    // exclusive with PatientId, mirroring PharmacySale.WalkInPatientName.
    public string? WalkInPatientName { get; set; }
    public int? DoctorId { get; set; }
    public int? ReferrerId { get; set; }
    public int? BulkDiscountTypeId { get; set; }
    public string? BulkDiscountName { get; set; }
    public string BillType
    {
        get;
        set;
    }
    = "OPD";
    public decimal GrossAmount
    {
        get;
        set;
    }
    public decimal DiscountPercent
    {
        get;
        set;
    }
    public decimal DiscountAmount
    {
        get;
        set;
    }
    public decimal NetAmount
    {
        get;
        set;
    }
    // Small manual rounding nudge (-9..9) folded into NetAmount so the final
    // total lands on a clean number (e.g. 448 + 2 => 450). Kept separately so
    // the app can show it as its own field, but it is never a separate line
    // on the printed bill - it just shows up as part of the total already.
    public int RoundOff
    {
        get;
        set;
    }
    public decimal PaidAmount
    {
        get;
        set;
    }
    public string Status
    {
        get;
        set;
    }
    = "Unpaid";
    public DateTime CreatedAtUtc
    {
        get;
        set;
    }
    = DateTime.UtcNow;
    // Actual date of service for the bill; defaults to now but can be backdated
    // (e.g. Patient add) to enter historical data under the correct reporting date.
    public DateTime BillDate
    {
        get;
        set;
    }
    = DateTime.UtcNow;
    public List<BillItem> Items
    {
        get;
        set;
    }
    = new();
    public List<Payment> Payments
    {
        get;
        set;
    }
    = new();
}
public class BillItem
{
    public int Id
    {
        get;
        set;
    }
    public int BillId
    {
        get;
        set;
    }
    public string Description
    {
        get;
        set;
    }
    = "";
    public int Quantity
    {
        get;
        set;
    }
    = 1;
    public decimal UnitPrice
    {
        get;
        set;
    }
    public string DiscountMode
    {
        get;
        set;
    }
    = "Percent";
    public decimal DiscountValue
    {
        get;
        set;
    }
    public decimal DiscountAmount
    {
        get;
        set;
    }
    public int? DiscountTypeId { get; set; }
    public string? DiscountName { get; set; }
    public decimal Amount
    {
        get;
        set;
    }
    public string? ReferenceType
    {
        get;
        set;
    }
    public int? ReferenceId
    {
        get;
        set;
    }
}
public class Payment
{
    public int Id
    {
        get;
        set;
    }
    public int BillId
    {
        get;
        set;
    }
    public decimal Amount
    {
        get;
        set;
    }
    public string Mode
    {
        get;
        set;
    }
    = "Cash";
    public string? Reference
    {
        get;
        set;
    }
    public DateTime PaidAtUtc
    {
        get;
        set;
    }
    = DateTime.UtcNow;
}
public class WardBed
{
    public int Id
    {
        get;
        set;
    }
    public string BedNumber
    {
        get;
        set;
    }
    = "";
    public string RoomType
    {
        get;
        set;
    }
    = "Bed";
    public decimal CashRate
    {
        get;
        set;
    }
    public decimal InsuranceRate
    {
        get;
        set;
    }
    public decimal AyushmanRate
    {
        get;
        set;
    }
    public bool IsOccupied
    {
        get;
        set;
    }
    public int? CurrentIpdAdmissionId
    {
        get;
        set;
    }
    public bool IsActive
    {
        get;
        set;
    }
    = true;
}
public class IpdAdmission
{
    public int Id
    {
        get;
        set;
    }
    public string AdmissionNumber
    {
        get;
        set;
    }
    = "";
    public int PatientId
    {
        get;
        set;
    }
    public int? SourceOpdVisitId
    {
        get;
        set;
    }
    public int DoctorId
    {
        get;
        set;
    }
    public int BedId
    {
        get;
        set;
    }
    public string PayerType
    {
        get;
        set;
    }
    = "Cash";
    public DateTime AdmittedAtUtc
    {
        get;
        set;
    }
    = DateTime.UtcNow;
    public DateTime? DischargedAtUtc
    {
        get;
        set;
    }
    public string Status
    {
        get;
        set;
    }
    = "Admitted";
}
public class IpdVital
{
    public int Id
    {
        get;
        set;
    }
    public int IpdAdmissionId
    {
        get;
        set;
    }
    public string? BloodPressure
    {
        get;
        set;
    }
    public decimal? TemperatureC
    {
        get;
        set;
    }
    public int? Pulse
    {
        get;
        set;
    }
    public decimal? Spo2
    {
        get;
        set;
    }
    public string? Notes
    {
        get;
        set;
    }
    public DateTime RecordedAtUtc
    {
        get;
        set;
    }
    = DateTime.UtcNow;
}
public class IpdDoctorNote
{
    public int Id
    {
        get;
        set;
    }
    public int IpdAdmissionId
    {
        get;
        set;
    }
    public int DoctorId
    {
        get;
        set;
    }
    public string Suggestion
    {
        get;
        set;
    }
    = "";
    public DateTime CreatedAtUtc
    {
        get;
        set;
    }
    = DateTime.UtcNow;
}
public class IpdNursingNote
{
    public int Id
    {
        get;
        set;
    }
    public int IpdAdmissionId
    {
        get;
        set;
    }
    public string ActionTaken
    {
        get;
        set;
    }
    = "";
    public DateTime CreatedAtUtc
    {
        get;
        set;
    }
    = DateTime.UtcNow;
}
public class IpdDocument
{
    public int Id
    {
        get;
        set;
    }
    public int IpdAdmissionId
    {
        get;
        set;
    }
    public string Category
    {
        get;
        set;
    }
    = "Other";
    public string FileName
    {
        get;
        set;
    }
    = "";
    public string StoredPath
    {
        get;
        set;
    }
    = "";
    public DateTime UploadedAtUtc
    {
        get;
        set;
    }
    = DateTime.UtcNow;
}
public class OtBooking
{
    public int Id
    {
        get;
        set;
    }
    public string BookingNumber
    {
        get;
        set;
    }
    = "";
    public int? PatientId
    {
        get;
        set;
    }
    public string? ExternalPatientName
    {
        get;
        set;
    }
    public string? ExternalPatientPhone
    {
        get;
        set;
    }
    public int DoctorId
    {
        get;
        set;
    }
    public string OtRoom
    {
        get;
        set;
    }
    = "OT 1";
    public DateTime StartAtUtc
    {
        get;
        set;
    }
    public DateTime EndAtUtc
    {
        get;
        set;
    }
    public string? ProcedureName
    {
        get;
        set;
    }
    public string? MedicinesUsed
    {
        get;
        set;
    }
    public string? VitalsAndLabs
    {
        get;
        set;
    }
    public decimal DoctorPayoutAmount { get; set; }
    public string Status
    {
        get;
        set;
    }
    = "Booked";
}
public class Medicine
{
    public int Id
    {
        get;
        set;
    }
    public string Name
    {
        get;
        set;
    }
    = "";
    public DateTime ExpiryDate
    {
        get;
        set;
    }
    public int QuantityTablets
    {
        get;
        set;
    }
    public int TabletsPerStrip
    {
        get;
        set;
    }
    = 10;
    public decimal PricePerStrip
    {
        get;
        set;
    }
    public string BatchNumber
    {
        get;
        set;
    }
    = "";
    public string? BoxNumber
    {
        get;
        set;
    }
    public string? Section
    {
        get;
        set;
    }
    public string? Component
    {
        get;
        set;
    }
    public bool IsActive
    {
        get;
        set;
    }
    = true;
}
public class PharmacyBillReservation
{
    public int Id
    {
        get;
        set;
    }
    public int BillId
    {
        get;
        set;
    }
    public int MedicineId
    {
        get;
        set;
    }
    public int QuantityTablets
    {
        get;
        set;
    }
    public bool StockReduced
    {
        get;
        set;
    }
}
public class LabTest
{
    public int Id
    {
        get;
        set;
    }
    public string Name
    {
        get;
        set;
    }
    = "";
    public decimal Price
    {
        get;
        set;
    }
    public string? Note
    {
        get;
        set;
    }
    public string? SchemaJson
    {
        get;
        set;
    }
    public bool IsActive
    {
        get;
        set;
    }
    = true;
    public List<LabTestComponent> Components
    {
        get;
        set;
    }
    = new();
}
public class LabTestComponent
{
    public int Id
    {
        get;
        set;
    }
    public int LabTestId
    {
        get;
        set;
    }
    public string Name
    {
        get;
        set;
    }
    = "";
    public string? RangeText
    {
        get;
        set;
    }
    public string? Unit
    {
        get;
        set;
    }
    public string? DefaultValue
    {
        get;
        set;
    }
}
public class LabOrder
{
    public int Id
    {
        get;
        set;
    }
    public string OrderNumber
    {
        get;
        set;
    }
    = "";
    public int PatientId
    {
        get;
        set;
    }
    public int? IpdAdmissionId
    {
        get;
        set;
    }
    public int? BillId
    {
        get;
        set;
    }
    public DateTime CreatedAtUtc
    {
        get;
        set;
    }
    = DateTime.UtcNow;
    public string Status
    {
        get;
        set;
    }
    = "Ordered";
    public List<LabOrderTest> Tests
    {
        get;
        set;
    }
    = new();
}
public class LabOrderTest
{
    public int Id
    {
        get;
        set;
    }
    public int LabOrderId
    {
        get;
        set;
    }
    public int LabTestId
    {
        get;
        set;
    }
    public decimal UnitPrice
    {
        get;
        set;
    }
    public int? DiscountTypeId { get; set; }
    public string? DiscountName { get; set; }
    public string DiscountMode
    {
        get;
        set;
    }
    = "Percent";
    public decimal DiscountValue
    {
        get;
        set;
    }
    public decimal DiscountAmount
    {
        get;
        set;
    }
    public decimal NetAmount
    {
        get;
        set;
    }
    public string Status
    {
        get;
        set;
    }
    = "Pending";
    public string? ResultSchemaJson
    {
        get;
        set;
    }
    public DateTime? ResultUpdatedAtUtc { get; set; }
    public List<LabResult> Results
    {
        get;
        set;
    }
    = new();
}
public class LabResult
{
    public int Id
    {
        get;
        set;
    }
    public int LabOrderTestId
    {
        get;
        set;
    }
    public int LabTestComponentId
    {
        get;
        set;
    }
    public string ComponentName
    {
        get;
        set;
    }
    = "";
    public string? ResultValue
    {
        get;
        set;
    }
    public string? RangeText
    {
        get;
        set;
    }
    public string? Unit
    {
        get;
        set;
    }
}

public class DocumentCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class ReportCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
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


public class PatientFollowUp
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int? OpdVisitId { get; set; }
    public int? ParentFollowUpId { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Comment { get; set; }
    public bool FollowUpNeeded { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int? CompletedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class AppSetting
{
    public int Id
    {
        get;
        set;
    }
    public string Name
    {
        get;
        set;
    }
    = "";
    public string Value
    {
        get;
        set;
    }
    = "";
    public string Type
    {
        get;
        set;
    }
    = "Billing";
    // Billing, Branding, Generic
    public bool IsActive
    {
        get;
        set;
    }
    = true;
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
    public List<PharmacySaleItem> Items { get; set; } = new();
}

public class PharmacySaleItem
{
    public int Id { get; set; }
    public int PharmacySaleId { get; set; }
    public int PurchaseItemId { get; set; }
    public string Manufacturer { get; set; } = "";
    public string Hsn { get; set; } = "";
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



public class StaffDesignation
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
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
public class StaffMember
{
    public int Id { get; set; }
    public string StaffCode { get; set; } = "";
    public string Name { get; set; } = "";
    public int? DesignationId { get; set; }
    public string? Designation { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? AccountNumber { get; set; }
    public decimal MonthlySalary { get; set; }
    public DateTime? DateOfJoining { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class StaffAttendance
{
    public int Id { get; set; }
    public int StaffId { get; set; }
    public DateTime AttendanceDate { get; set; }
    public string Status { get; set; } = "Present";
    public string? Comment { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SalaryPayment
{
    public int Id { get; set; }
    public int StaffId { get; set; }
    public int SalaryYear { get; set; }
    public int SalaryMonth { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal AbsentDays { get; set; }
    public decimal HalfDays { get; set; }
    public decimal DeductionAmount { get; set; }
    public decimal PayableAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMode { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public int? PaidByStaffId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

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
