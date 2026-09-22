using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/staff")]
public class StaffController : ControllerBase
{
    private readonly AppDbContext _db;

    public StaffController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool includeInactive = false)
    {
        var query = _db.StaffMembers.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var rows = await query
            .OrderBy(x => x.Name)
            .ToListAsync();

        var designationIds = rows
            .Where(x => x.DesignationId.HasValue)
            .Select(x => x.DesignationId!.Value)
            .Distinct()
            .ToList();

        var designationMap = await _db.StaffDesignations
            .Where(x => designationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        return Ok(rows.Select(x => new
        {
            x.Id,
            x.StaffCode,
            x.Name,
            x.DesignationId,
            Designation = x.DesignationId.HasValue && designationMap.TryGetValue(x.DesignationId.Value, out var name)
                ? name
                : x.Designation,
            x.DateOfBirth,
            x.Phone,
            x.Address,
            x.AccountNumber,
            x.MonthlySalary,
            x.DateOfJoining,
            x.IsActive,
            x.CreatedAtUtc
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Add(StaffRequest request)
    {
        var validation = await ValidateStaff(request);
        if (validation != null)
        {
            return BadRequest(new { message = validation });
        }

        var nextId = (await _db.StaffMembers.MaxAsync(x => (int?)x.Id) ?? 0) + 1;
        var designation = request.DesignationId.HasValue
            ? await _db.StaffDesignations.FindAsync(request.DesignationId.Value)
            : null;

        var row = new StaffMember
        {
            StaffCode = $"KHC-S-{nextId:0000}",
            Name = request.Name.Trim(),
            DesignationId = request.DesignationId,
            Designation = designation?.Name ?? request.Designation?.Trim(),
            DateOfBirth = request.DateOfBirth,
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            AccountNumber = request.AccountNumber?.Trim(),
            MonthlySalary = request.MonthlySalary,
            DateOfJoining = request.DateOfJoining,
            IsActive = request.IsActive
        };

        _db.StaffMembers.Add(row);
        await _db.SaveChangesAsync();

        return Ok(row);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, StaffRequest request)
    {
        var row = await _db.StaffMembers.FindAsync(id);
        if (row == null)
        {
            return NotFound();
        }

        var validation = await ValidateStaff(request);
        if (validation != null)
        {
            return BadRequest(new { message = validation });
        }

        var designation = request.DesignationId.HasValue
            ? await _db.StaffDesignations.FindAsync(request.DesignationId.Value)
            : null;

        row.Name = request.Name.Trim();
        row.DesignationId = request.DesignationId;
        row.Designation = designation?.Name ?? request.Designation?.Trim();
        row.DateOfBirth = request.DateOfBirth;
        row.Phone = request.Phone?.Trim();
        row.Address = request.Address?.Trim();
        row.AccountNumber = request.AccountNumber?.Trim();
        row.MonthlySalary = request.MonthlySalary;
        row.DateOfJoining = request.DateOfJoining;
        row.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.StaffMembers.FindAsync(id);
        if (row == null)
        {
            return NotFound();
        }

        row.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("attendance-month")]
    public async Task<IActionResult> AttendanceMonth(
        [FromQuery] int year,
        [FromQuery] int month)
    {
        if (year < 2000 || month < 1 || month > 12)
        {
            return BadRequest(new { message = "Valid year and month are required." });
        }

        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var staff = await _db.StaffMembers
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var attendance = await _db.StaffAttendances
            .Where(x => x.AttendanceDate >= start && x.AttendanceDate < end)
            .ToListAsync();

        return Ok(new
        {
            year,
            month,
            daysInMonth,
            rows = staff.Select(member => new
            {
                member.Id,
                member.StaffCode,
                member.Name,
                member.Designation,
                days = Enumerable.Range(1, daysInMonth).Select(day =>
                {
                    var record = attendance.FirstOrDefault(x =>
                        x.StaffId == member.Id &&
                        x.AttendanceDate.Day == day);

                    return new
                    {
                        day,
                        status = record?.Status ?? "",
                        comment = record?.Comment ?? ""
                    };
                })
            })
        });
    }

    [HttpPost("attendance-month")]
    public async Task<IActionResult> SaveAttendanceMonth(MonthlyAttendanceSaveRequest request)
    {
        if (request.Year < 2000 || request.Month < 1 || request.Month > 12)
        {
            return BadRequest(new { message = "Valid year and month are required." });
        }

        var allowed = new[] { "", "Present", "Absent", "WeekOff", "Leave", "Half Day" };
        var daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);

        foreach (var item in request.Items)
        {
            if (item.Day < 1 || item.Day > daysInMonth)
            {
                return BadRequest(new { message = $"Invalid day {item.Day}." });
            }

            if (!allowed.Contains(item.Status))
            {
                return BadRequest(new { message = $"Invalid attendance status for staff ID {item.StaffId}." });
            }

            var date = new DateTime(request.Year, request.Month, item.Day);
            var row = await _db.StaffAttendances.FirstOrDefaultAsync(x =>
                x.StaffId == item.StaffId &&
                x.AttendanceDate == date);

            if (string.IsNullOrWhiteSpace(item.Status))
            {
                if (row != null)
                {
                    _db.StaffAttendances.Remove(row);
                }
                continue;
            }

            if (row == null)
            {
                row = new StaffAttendance
                {
                    StaffId = item.StaffId,
                    AttendanceDate = date
                };
                _db.StaffAttendances.Add(row);
            }

            row.Status = item.Status;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("payroll")]
    public async Task<IActionResult> Payroll([FromQuery] int year, [FromQuery] int month)
    {
        if (year < 2000 || month < 1 || month > 12)
        {
            return BadRequest(new { message = "Valid year and month are required." });
        }

        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var prevMonthStart = start.AddMonths(-1);
        var prevYear = prevMonthStart.Year;
        var prevMonth = prevMonthStart.Month;
        var prevDaysInMonth = DateTime.DaysInMonth(prevYear, prevMonth);
        var prevEnd = start;

        var staff = await _db.StaffMembers
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var attendance = await _db.StaffAttendances
            .Where(x => x.AttendanceDate >= start && x.AttendanceDate < end)
            .ToListAsync();

        var payments = await _db.SalaryPayments
            .Where(x => x.SalaryYear == year && x.SalaryMonth == month)
            .ToListAsync();

        var prevAttendance = await _db.StaffAttendances
            .Where(x => x.AttendanceDate >= prevMonthStart && x.AttendanceDate < prevEnd)
            .ToListAsync();

        var prevPayments = await _db.SalaryPayments
            .Where(x => x.SalaryYear == prevYear && x.SalaryMonth == prevMonth)
            .ToListAsync();

        var result = staff.Select(x =>
        {
            var items = attendance.Where(a => a.StaffId == x.Id).ToList();
            var calc = CalculateSalary(items, x.MonthlySalary, daysInMonth);
            var payment = payments.FirstOrDefault(p => p.StaffId == x.Id);

            var prevItems = prevAttendance.Where(a => a.StaffId == x.Id).ToList();
            var prevCalc = CalculateSalary(prevItems, x.MonthlySalary, prevDaysInMonth);
            var prevPayment = prevPayments.FirstOrDefault(p => p.StaffId == x.Id);
            var previousOutstanding = x.DateOfJoining.HasValue && x.DateOfJoining.Value >= prevEnd
                ? 0
                : Math.Max(0, prevCalc.PayableAmount - (prevPayment?.PaidAmount ?? 0));

            return new
            {
                x.Id,
                x.StaffCode,
                x.Name,
                x.Designation,
                x.AccountNumber,
                x.MonthlySalary,
                DaysInMonth = daysInMonth,
                PresentDays = calc.PresentDays,
                AbsentDays = calc.AbsentDays,
                HalfDays = calc.HalfDays,
                WeekOffDays = calc.WeekOffDays,
                LeaveDays = calc.LeaveDays,
                UnmarkedDays = calc.UnmarkedDays,
                DailyRate = calc.DailyRate,
                DeductionAmount = calc.DeductionAmount,
                PayableAmount = calc.PayableAmount,
                PaidAmount = payment?.PaidAmount ?? 0,
                Outstanding = Math.Max(0, calc.PayableAmount - (payment?.PaidAmount ?? 0)),
                PreviousOutstanding = previousOutstanding,
                payment?.PaymentDate,
                payment?.PaymentMode,
                payment?.ReferenceNumber,
                payment?.Notes
            };
        });

        return Ok(result);
    }

    [HttpPost("salary-payment")]
    public async Task<IActionResult> SalaryPayment(SalaryPaymentRequest request)
    {
        if (request.SalaryYear < 2000 || request.SalaryMonth < 1 || request.SalaryMonth > 12)
        {
            return BadRequest(new { message = "Valid salary year and month are required." });
        }

        var staff = await _db.StaffMembers.FindAsync(request.StaffId);
        if (staff == null)
        {
            return BadRequest(new { message = "Staff not found." });
        }

        if (request.PaidByStaffId.HasValue &&
            !await _db.StaffMembers.AnyAsync(x => x.Id == request.PaidByStaffId.Value && x.IsActive))
        {
            return BadRequest(new { message = "Please select a valid staff member who paid." });
        }

        var start = new DateTime(request.SalaryYear, request.SalaryMonth, 1);
        var end = start.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(request.SalaryYear, request.SalaryMonth);

        var items = await _db.StaffAttendances
            .Where(x => x.StaffId == staff.Id && x.AttendanceDate >= start && x.AttendanceDate < end)
            .ToListAsync();

        var calc = CalculateSalary(items, staff.MonthlySalary, daysInMonth);

        if (request.PaidAmount < 0 || request.PaidAmount > calc.PayableAmount)
        {
            return BadRequest(new { message = "Paid amount cannot be more than payable salary." });
        }

        var row = await _db.SalaryPayments.FirstOrDefaultAsync(x =>
            x.StaffId == request.StaffId &&
            x.SalaryYear == request.SalaryYear &&
            x.SalaryMonth == request.SalaryMonth);

        if (row == null)
        {
            row = new SalaryPayment
            {
                StaffId = request.StaffId,
                SalaryYear = request.SalaryYear,
                SalaryMonth = request.SalaryMonth
            };
            _db.SalaryPayments.Add(row);
        }

        row.GrossSalary = staff.MonthlySalary;
        row.AbsentDays = calc.AbsentDays + calc.UnmarkedDays;
        row.HalfDays = calc.HalfDays;
        row.DeductionAmount = calc.DeductionAmount;
        row.PayableAmount = calc.PayableAmount;
        row.PaidAmount = request.PaidAmount;
        row.PaymentDate = request.PaymentDate;
        row.PaymentMode = request.PaymentMode?.Trim();
        row.ReferenceNumber = request.ReferenceNumber?.Trim();
        row.PaidByStaffId = request.PaidByStaffId;
        row.Notes = request.Notes?.Trim();
        row.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(row);
    }

    private const decimal SalaryDaysPerMonth = 30m;

    private static (int PresentDays, int AbsentDays, int HalfDays, int WeekOffDays, int LeaveDays, int UnmarkedDays, decimal DailyRate, decimal DeductionAmount, decimal PayableAmount) CalculateSalary(
        List<StaffAttendance> items, decimal monthlySalary, int daysInMonth)
    {
        var presentDays = items.Count(a => a.Status == "Present");
        var absentDays = items.Count(a => a.Status == "Absent");
        var halfDays = items.Count(a => a.Status == "Half Day");
        var weekOffDays = items.Count(a => a.Status == "WeekOff");
        var leaveDays = items.Count(a => a.Status == "Leave");
        var markedDays = presentDays + absentDays + halfDays + weekOffDays + leaveDays;
        var unmarkedDays = Math.Max(0, daysInMonth - markedDays);

        var dailyRate = monthlySalary / SalaryDaysPerMonth;
        var rawDeduction = dailyRate * (absentDays + unmarkedDays) + dailyRate * 0.5m * halfDays;
        var deduction = Math.Round(Math.Min(rawDeduction, monthlySalary), 0, MidpointRounding.AwayFromZero);
        var payable = Math.Max(0, Math.Round(monthlySalary - deduction, 0, MidpointRounding.AwayFromZero));

        return (presentDays, absentDays, halfDays, weekOffDays, leaveDays, unmarkedDays, Math.Round(dailyRate, 0, MidpointRounding.AwayFromZero), deduction, payable);
    }

    private async Task<string?> ValidateStaff(StaffRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Staff name is required.";
        }

        if (request.MonthlySalary < 0)
        {
            return "Monthly salary cannot be negative.";
        }

        if (request.DesignationId.HasValue &&
            !await _db.StaffDesignations.AnyAsync(x =>
                x.Id == request.DesignationId.Value &&
                x.IsActive))
        {
            return "Please select a valid designation.";
        }

        return null;
    }
}
