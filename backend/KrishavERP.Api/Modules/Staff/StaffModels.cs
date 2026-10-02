namespace KrishavERP.Api.Modules.Staff;

public class StaffDesignation
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
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
