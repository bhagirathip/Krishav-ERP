namespace KrishavERP.Api;
public record LoginRequest(string Username,string Password);
public class PatientCreateRequest
{
    public string Name
    {
        get;
        set;
    }
    = "";
    public int? Age
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
    public string RegistrationType
    {
        get;
        set;
    }
    = "OPD";
    public int? DoctorId
    {
        get;
        set;
    }
    public int? BedId
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
    public string? OtRoom
    {
        get;
        set;
    }
    public DateTime? OtStartAtUtc
    {
        get;
        set;
    }
    public DateTime? OtEndAtUtc
    {
        get;
        set;
    }
    public DateTime? VisitDateUtc
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
    public bool ConfirmDuplicatePhone
    {
        get;
        set;
    }
    public string MarketingSource { get; set; } = "Walk-in";
    public int? ReferrerId { get; set; }
    public bool CreateBill { get; set; } = true;
    public DateTime? BillDate { get; set; }
    public string? Address { get; set; }
}
public class PatientEditRequest : PatientCreateRequest
{
}
public class OpdCreateRequest
{
    public string VisitType
    {
        get;
        set;
    }
    = "OPD";
    public int PatientId
    {
        get;
        set;
    }
    public int DoctorId
    {
        get;
        set;
    }
    public bool IsEmergency
    {
        get;
        set;
    }
    public DateTime VisitDateUtc
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
    public bool CreateBill
    {
        get;
        set;
    }
    = true;
    public string MarketingSource { get; set; } = "Walk-in";
    public int? ReferrerId { get; set; }
    public DateTime? FollowUpDate { get; set; }
}
public class PaymentRequest
{
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
}
public class BillCreateRequest
{
    public int? PatientId
    {
        get;
        set;
    }
    public string? WalkInPatientName { get; set; }
    public int? DoctorId
    {
        get;
        set;
    }
    public string BillType
    {
        get;
        set;
    }
    = "OPD";
    public int? BulkDiscountTypeId { get; set; }
    public int RoundOff { get; set; }
    public List<BillCreateItem> Items
    {
        get;
        set;
    }
    = new();
}
public class BillCreateItem
{
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
    public int? DiscountTypeId { get; set; }
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
public class ConvertIpdRequest
{
    public int BedId
    {
        get;
        set;
    }
    public int DoctorId
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
}
public class IpdVitalRequest
{
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
}
public class IpdDoctorNoteRequest
{
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
}
public class IpdNursingNoteRequest
{
    public string ActionTaken
    {
        get;
        set;
    }
    = "";
}
public class OtBookingRequest
{
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
    public decimal DoctorPayoutAmount { get; set; }
}
public class PharmacyBillRequest
{
    public int? PatientId
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
    public List<PharmacyBillLine> Items
    {
        get;
        set;
    }
    = new();
}
public class PharmacyBillLine
{
    public int MedicineId
    {
        get;
        set;
    }
    public string UnitType
    {
        get;
        set;
    }
    = "Tablet";
    public int Quantity
    {
        get;
        set;
    }
}
public class LabOrderCreateRequest
{
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
    public int RoundOff { get; set; }
    public List<LabOrderLine> Tests
    {
        get;
        set;
    }
    = new();
}
public class LabOrderLine
{
    public int LabTestId { get; set; }
    public int? DiscountTypeId { get; set; }
}
public class LabDynamicResultSaveRequest
{
    public string SchemaJson
    {
        get;
        set;
    }
    = "";
}

public class LabResultSaveRequest
{
    public List<LabResultValue> Results
    {
        get;
        set;
    }
    = new();
}
public class LabResultValue
{
    public int LabResultId
    {
        get;
        set;
    }
    public string? ResultValue
    {
        get;
        set;
    }
}
public class RoleSaveRequest
{
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
    public List<RolePermission> Permissions
    {
        get;
        set;
    }
    = new();
}
public class UserCreateRequest
{
    public string Username
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
    public string Password
    {
        get;
        set;
    }
    = "";
    public int RoleId
    {
        get;
        set;
    }
}
public class UserUpdateRequest
{
    public string DisplayName
    {
        get;
        set;
    }
    = "";
    // Leave blank to keep the current password unchanged.
    public string? Password
    {
        get;
        set;
    }
    public int RoleId
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

public class DiscountTypeRequest
{
    public string Name { get; set; } = "";
    public string DiscountMode { get; set; } = "Percent";
    public decimal Value { get; set; }
    public string Scope { get; set; } = "Individual";
    public bool IsActive { get; set; } = true;
}

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


public class FollowUpCompleteRequest
{
    public string Comment { get; set; } = "";
    public bool FollowUpNeeded { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
}


public class FollowUpCreateRequest
{
    public int PatientId { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string? Comment { get; set; }
}


public class BillTypeMasterRequest
{
    public string Name { get; set; } = "";
    public bool IsBillable { get; set; } = true;
    public bool IsOpdType { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PharmacyExpiryActionRequest
{
    public int PurchaseItemId { get; set; }
    public string ActionType { get; set; } = "Discard";
    public string UnitType { get; set; } = "Pack";
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}


public class StaffRequest
{
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
}

public class StaffDesignationRequest
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class PatientSourceRequest
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class AttendanceSaveRequest
{
    public DateTime AttendanceDate { get; set; }
    public List<AttendanceLineRequest> Items { get; set; } = new();
}

public class AttendanceLineRequest
{
    public int StaffId { get; set; }
    public string Status { get; set; } = "Present";
    public string? Comment { get; set; }
}

public class MonthlyAttendanceSaveRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<MonthlyAttendanceLineRequest> Items { get; set; } = new();
}

public class MonthlyAttendanceLineRequest
{
    public int StaffId { get; set; }
    public int Day { get; set; }
    public string Status { get; set; } = "Present";
}

public class SalaryPaymentRequest
{
    public int StaffId { get; set; }
    public int SalaryYear { get; set; }
    public int SalaryMonth { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMode { get; set; }
    public string? ReferenceNumber { get; set; }
    public int? PaidByStaffId { get; set; }
    public string? Notes { get; set; }
}

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

public class DoctorSettlementPaymentRequest
{
    public decimal PaidAmount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentMode { get; set; }
    public string? ReferenceNumber { get; set; }
    public int? PaidByStaffId { get; set; }
    public string? Notes { get; set; }
}
