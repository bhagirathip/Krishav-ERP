namespace KrishavERP.Api.Modules.Staff;

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
