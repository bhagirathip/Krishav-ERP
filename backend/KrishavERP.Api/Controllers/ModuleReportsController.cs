using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/module-reports")]
public class ModuleReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ModuleReportsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("pharmacy")]
    public async Task<IActionResult> Pharmacy(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? year)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? start).Date.AddDays(1);
        var selectedYear = year ?? DateTime.Today.Year;

        var invoiceRows = await _db.PharmacyPurchaseInvoices
            .Where(x => !x.IsDeleted && x.InvoiceDate >= start && x.InvoiceDate < end)
            .Include(x => x.Distributor)
            .ToListAsync();

        var purchaseAmount = invoiceRows.Sum(x => x.TotalAmount);
        var cashPurchase = invoiceRows.Where(x => x.PaymentType == "Cash").Sum(x => x.TotalAmount);
        var creditPurchase = invoiceRows.Where(x => x.PaymentType == "Credit").Sum(x => x.TotalAmount);

        var distributorBreakdown = invoiceRows
            .GroupBy(x => x.Distributor?.Name ?? "Unknown")
            .Select(g => new
            {
                Distributor = g.Key,
                InvoiceCount = g.Count(),
                PurchaseAmount = g.Sum(x => x.TotalAmount),
                CashAmount = g.Where(x => x.PaymentType == "Cash").Sum(x => x.TotalAmount),
                CreditAmount = g.Where(x => x.PaymentType == "Credit").Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.PurchaseAmount)
            .ToList();

        var payments = await _db.PharmacyPurchasePayments
            .Where(x => x.PaymentDate >= start && x.PaymentDate < end)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var sales = await _db.PharmacySales
            .Where(x => x.SaleDateUtc >= start && x.SaleDateUtc < end)
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        var returnAmount = await _db.PharmacyExpiryActions
            .Where(x => x.ActionDateUtc >= start && x.ActionDateUtc < end && x.ActionType == "Return")
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var discardLoss = await _db.PharmacyExpiryActions
            .Where(x => x.ActionDateUtc >= start && x.ActionDateUtc < end && x.ActionType == "Discard")
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var yearStart = new DateTime(selectedYear, 1, 1);
        var yearEnd = yearStart.AddYears(1);
        var yearlyInvoices = await _db.PharmacyPurchaseInvoices
            .Where(x => !x.IsDeleted && x.InvoiceDate >= yearStart && x.InvoiceDate < yearEnd)
            .ToListAsync();
        var yearlySales = await _db.PharmacySales
            .Where(x => x.SaleDateUtc >= yearStart && x.SaleDateUtc < yearEnd)
            .ToListAsync();

        var monthly = new List<object>();
        decimal previousRevenue = 0;
        for (var month = 1; month <= 12; month++)
        {
            var purchase = yearlyInvoices.Where(x => x.InvoiceDate.Month == month).Sum(x => x.TotalAmount);
            var revenue = yearlySales.Where(x => x.SaleDateUtc.Month == month).Sum(x => x.TotalAmount);
            monthly.Add(new
            {
                Month = month,
                MonthName = new DateTime(selectedYear, month, 1).ToString("MMM"),
                PurchaseAmount = purchase,
                Revenue = revenue,
                DifferenceFromPreviousMonth = month == 1 ? 0 : revenue - previousRevenue
            });
            previousRevenue = revenue;
        }

        return Ok(new
        {
            PurchaseAmount = purchaseAmount,
            CashPurchaseAmount = cashPurchase,
            CreditPurchaseAmount = creditPurchase,
            SupplierPayments = payments,
            SalesAmount = sales,
            ExpiryReturnAmount = returnAmount,
            ExpiryDiscardLoss = discardLoss,
            GrossMarginBeforeOtherCosts = sales - purchaseAmount + returnAmount - discardLoss,
            DistributorBreakdown = distributorBreakdown,
            Year = selectedYear,
            Monthly = monthly
        });
    }

    [HttpGet("lab")]
    public async Task<IActionResult> Lab(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? year)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? start).Date.AddDays(1);
        var selectedYear = year ?? DateTime.Today.Year;

        var orders = await _db.LabOrders
            .Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end)
            .CountAsync();

        var billIds = await _db.LabOrders
            .Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end && x.BillId != null)
            .Select(x => x.BillId!.Value)
            .ToListAsync();

        var income = await _db.Bills
            .Where(x => billIds.Contains(x.Id))
            .SumAsync(x => (decimal?)x.NetAmount) ?? 0;

        var purchases = await _db.LabPurchaseExpenses
            .Where(x => x.ExpenseDate >= start && x.ExpenseDate < end)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        var paid = await _db.LabPurchaseExpenses
            .Where(x => x.PaymentDate.HasValue && x.PaymentDate.Value >= start && x.PaymentDate.Value < end)
            .SumAsync(x => (decimal?)x.PaidAmount) ?? 0;

        var yearStart = new DateTime(selectedYear, 1, 1);
        var yearEnd = yearStart.AddYears(1);
        var bills = await _db.Bills
            .Where(x => x.Status != "Deleted" && x.BillType == "Lab" && x.CreatedAtUtc >= yearStart && x.CreatedAtUtc < yearEnd)
            .ToListAsync();
        var labPurchases = await _db.LabPurchaseExpenses
            .Where(x => x.ExpenseDate >= yearStart && x.ExpenseDate < yearEnd)
            .ToListAsync();

        var monthly = new List<object>();
        decimal previous = 0;
        for (var month = 1; month <= 12; month++)
        {
            var revenue = bills.Where(x => x.CreatedAtUtc.Month == month).Sum(x => x.NetAmount);
            var expense = labPurchases.Where(x => x.ExpenseDate.Month == month).Sum(x => x.Amount);
            monthly.Add(new
            {
                Month = month,
                MonthName = new DateTime(selectedYear, month, 1).ToString("MMM"),
                Revenue = revenue,
                Expense = expense,
                DifferenceFromPreviousMonth = month == 1 ? 0 : revenue - previous
            });
            previous = revenue;
        }

        return Ok(new
        {
            LabOrders = orders,
            LabIncome = income,
            LabPurchaseExpense = purchases,
            LabExpensePaid = paid,
            LabOperatingContribution = income - purchases,
            Year = selectedYear,
            Monthly = monthly
        });
    }

    [HttpGet("expenses")]
    public async Task<IActionResult> Expenses(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? year)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? start).Date.AddDays(1);
        var selectedYear = year ?? DateTime.Today.Year;

        var daily = await _db.DailyExpenses
            .Where(x => x.ExpenseDate >= start && x.ExpenseDate < end)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;
        var pharmacy = await _db.PharmacyPurchasePayments
            .Where(x => x.PaymentDate >= start && x.PaymentDate < end)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;
        var lab = await _db.LabPurchaseExpenses
            .Where(x => x.PaymentDate.HasValue && x.PaymentDate.Value >= start && x.PaymentDate.Value < end)
            .SumAsync(x => (decimal?)x.PaidAmount) ?? 0;
        var salaries = await _db.SalaryPayments
            .Where(x => x.PaymentDate.HasValue && x.PaymentDate.Value >= start && x.PaymentDate.Value < end)
            .SumAsync(x => (decimal?)x.PaidAmount) ?? 0;
        var doctorPaid = await _db.DoctorSettlements
            .Where(x => x.PaymentDate.HasValue && x.PaymentDate.Value >= start && x.PaymentDate.Value < end)
            .SumAsync(x => (decimal?)x.PaidAmount) ?? 0;

        var yearStart = new DateTime(selectedYear,1,1);
        var yearEnd = yearStart.AddYears(1);
        var dailyYear = await _db.DailyExpenses.Where(x=>x.ExpenseDate>=yearStart&&x.ExpenseDate<yearEnd).ToListAsync();
        var salaryYear = await _db.SalaryPayments.Where(x=>x.PaymentDate.HasValue&&x.PaymentDate.Value>=yearStart&&x.PaymentDate.Value<yearEnd).ToListAsync();
        var pharmacyYear = await _db.PharmacyPurchasePayments.Where(x=>x.PaymentDate>=yearStart&&x.PaymentDate<yearEnd).ToListAsync();
        var labYear = await _db.LabPurchaseExpenses.Where(x=>x.PaymentDate.HasValue&&x.PaymentDate.Value>=yearStart&&x.PaymentDate.Value<yearEnd).ToListAsync();
        var doctorYear = await _db.DoctorSettlements.Where(x=>x.PaymentDate.HasValue&&x.PaymentDate.Value>=yearStart&&x.PaymentDate.Value<yearEnd).ToListAsync();

        var monthly = new List<object>();
        decimal previous = 0;
        for(var month=1;month<=12;month++)
        {
            var amount = dailyYear.Where(x=>x.ExpenseDate.Month==month).Sum(x=>x.Amount)
                + salaryYear.Where(x=>x.PaymentDate!.Value.Month==month).Sum(x=>x.PaidAmount)
                + pharmacyYear.Where(x=>x.PaymentDate.Month==month).Sum(x=>x.Amount)
                + labYear.Where(x=>x.PaymentDate!.Value.Month==month).Sum(x=>x.PaidAmount)
                + doctorYear.Where(x=>x.PaymentDate!.Value.Month==month).Sum(x=>x.PaidAmount);
            monthly.Add(new{Month=month,MonthName=new DateTime(selectedYear,month,1).ToString("MMM"),Amount=amount,DifferenceFromPreviousMonth=month==1?0:amount-previous});
            previous=amount;
        }

        return Ok(new
        {
            DailyExpenses = daily,
            SalaryPayments = salaries,
            PharmacyPayments = pharmacy,
            LabPayments = lab,
            DoctorPayments = doctorPaid,
            TotalExpenses = daily + salaries + pharmacy + lab + doctorPaid,
            Year = selectedYear,
            Monthly = monthly
        });
    }

    [HttpGet("staff")]
    public async Task<IActionResult> Staff([FromQuery] int year, [FromQuery] int month)
    {
        var totalStaff = await _db.StaffMembers.CountAsync(x => x.IsActive);
        var salary = await _db.SalaryPayments
            .Where(x => x.SalaryYear == year && x.SalaryMonth == month)
            .SumAsync(x => (decimal?)x.PayableAmount) ?? 0;
        var paid = await _db.SalaryPayments
            .Where(x => x.SalaryYear == year && x.SalaryMonth == month)
            .SumAsync(x => (decimal?)x.PaidAmount) ?? 0;
        var absent = await _db.StaffAttendances
            .Where(x => x.AttendanceDate.Year == year && x.AttendanceDate.Month == month && x.Status == "Absent")
            .CountAsync();
        var half = await _db.StaffAttendances
            .Where(x => x.AttendanceDate.Year == year && x.AttendanceDate.Month == month && x.Status == "Half Day")
            .CountAsync();

        var salaryYear = await _db.SalaryPayments.Where(x=>x.SalaryYear==year).ToListAsync();
        var monthly = new List<object>();
        decimal previous=0;
        for(var m=1;m<=12;m++)
        {
            var payable=salaryYear.Where(x=>x.SalaryMonth==m).Sum(x=>x.PayableAmount);
            var paidMonth=salaryYear.Where(x=>x.SalaryMonth==m).Sum(x=>x.PaidAmount);
            monthly.Add(new{Month=m,MonthName=new DateTime(year,m,1).ToString("MMM"),Payable=payable,Paid=paidMonth,DifferenceFromPreviousMonth=m==1?0:payable-previous});
            previous=payable;
        }

        return Ok(new
        {
            ActiveStaff = totalStaff,
            SalaryPayable = salary,
            SalaryPaid = paid,
            SalaryOutstanding = Math.Max(0, salary - paid),
            AbsentEntries = absent,
            HalfDayEntries = half,
            Year=year,
            Monthly=monthly
        });
    }
}
