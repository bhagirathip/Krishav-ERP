using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/reporting")]
public class ReportingController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportingController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        return Ok(await _db.BillTypes
            .Where(x => x.IsActive && x.IsBillable)
            .OrderBy(x => x.Id)
            .ToListAsync());
    }

    [HttpPost("categories")]
    public async Task<IActionResult> AddCategory(ReportCategory category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
            return BadRequest(new { message = "Category name is required." });

        if (await _db.ReportCategories.AnyAsync(x => x.Name == category.Name && x.IsActive))
            return Conflict(new { message = "Category already exists." });

        category.Name = category.Name.Trim();
        category.IsActive = true;
        _db.ReportCategories.Add(category);
        await _db.SaveChangesAsync();
        return Ok(category);
    }

    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> EditCategory(int id, ReportCategory request)
    {
        var category = await _db.ReportCategories.FindAsync(id);
        if (category == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Category name is required." });

        category.Name = request.Name.Trim();
        await _db.SaveChangesAsync();
        return Ok(category);
    }

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.ReportCategories.FindAsync(id);
        if (category == null)
            return NotFound();

        category.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("doctor-summary")]
    public async Task<IActionResult> GetDoctorSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? doctorId)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);

        var categories = await GetActiveCategoryNames();
        var doctorsQuery = _db.Doctors.Where(x => x.IsActive);

        if (doctorId.HasValue)
            doctorsQuery = doctorsQuery.Where(x => x.Id == doctorId.Value);

        var doctors = await doctorsQuery.OrderBy(x => x.Name).ToListAsync();
        var result = new List<object>();

        foreach (var doctor in doctors)
        {
            var consultationQuery = _db.OpdVisits.Where(x =>
                !x.IsCancelled &&
                x.DoctorId == doctor.Id &&
                x.VisitDateUtc >= start &&
                x.VisitDateUtc < end);

            var consultationCount = await consultationQuery.CountAsync();
            var patientCount = await consultationQuery
                .Select(x => x.PatientId)
                .Distinct()
                .CountAsync();

            var bills = await _db.Bills
                .Where(x =>
                    x.Status != "Deleted" &&
                    x.DoctorId == doctor.Id &&
                    x.BillDate >= start &&
                    x.BillDate < end)
                .ToListAsync();

            var income = categories.ToDictionary(
                category => category,
                category => bills
                    .Where(x => x.BillType.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.NetAmount));

            result.Add(new
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                PatientCount = patientCount,
                ConsultationCount = consultationCount,
                Income = income,
                Total = bills.Sum(x => x.NetAmount)
            });
        }

        return Ok(result);
    }

    [HttpGet("patient-summary")]
    public async Task<IActionResult> GetPatientSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? patientId)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);

        var categories = await GetActiveCategoryNames();
        var patientsQuery = _db.Patients.Where(x => !x.IsDeleted);

        if (patientId.HasValue)
            patientsQuery = patientsQuery.Where(x => x.Id == patientId.Value);

        var patients = await patientsQuery.OrderBy(x => x.Name).ToListAsync();
        var result = new List<object>();

        foreach (var patient in patients)
        {
            var bills = await _db.Bills
                .Where(x =>
                    x.Status != "Deleted" &&
                    x.PatientId == patient.Id &&
                    x.BillDate >= start &&
                    x.BillDate < end)
                .ToListAsync();

            if (bills.Count == 0 && !patientId.HasValue)
                continue;

            var income = categories.ToDictionary(
                category => category,
                category => bills
                    .Where(x => x.BillType.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.NetAmount));

            result.Add(new
            {
                PatientId = patient.Id,
                patient.PatientCode,
                patient.Name,
                patient.Phone,
                Income = income,
                Total = bills.Sum(x => x.NetAmount)
            });
        }

        return Ok(result);
    }

    [HttpGet("income-share")]
    public async Task<IActionResult> GetIncomeShare(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? doctorId,
        [FromQuery] int? patientId)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);
        var categories = await GetActiveCategoryNames();

        var query = _db.Bills.Where(x =>
            x.Status != "Deleted" &&
            x.BillDate >= start &&
            x.BillDate < end);

        if (doctorId.HasValue)
            query = query.Where(x => x.DoctorId == doctorId.Value);

        if (patientId.HasValue)
            query = query.Where(x => x.PatientId == patientId.Value);

        var bills = await query.ToListAsync();
        var total = bills.Sum(x => x.NetAmount);

        var result = categories.Select(category =>
        {
            var amount = bills
                .Where(x => x.BillType.Equals(category, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.NetAmount);

            return new
            {
                Category = category,
                Amount = amount,
                Percentage = total == 0
                    ? 0
                    : Math.Round(amount * 100m / total, 2)
            };
        })
        .Where(x => x.Amount > 0)
        .ToList();

        return Ok(new
        {
            Total = total,
            Items = result
        });
    }

    [HttpGet("payment-mode-share")]
    public async Task<IActionResult> GetPaymentModeShare(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? doctorId,
        [FromQuery] int? patientId)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);

        var query =
            from payment in _db.Payments
            join bill in _db.Bills on payment.BillId equals bill.Id
            where bill.Status != "Deleted" &&
                  payment.PaidAtUtc >= start &&
                  payment.PaidAtUtc < end
            select new
            {
                payment.Mode,
                payment.Amount,
                bill.DoctorId,
                bill.PatientId
            };

        if (doctorId.HasValue)
            query = query.Where(x => x.DoctorId == doctorId.Value);

        if (patientId.HasValue)
            query = query.Where(x => x.PatientId == patientId.Value);

        var payments = await query.ToListAsync();
        var total = payments.Sum(x => x.Amount);

        var items = payments
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Mode) ? "Other" : x.Mode)
            .Select(group =>
            {
                var amount = group.Sum(x => x.Amount);

                return new
                {
                    Mode = group.Key,
                    Amount = amount,
                    Percentage = total == 0
                        ? 0
                        : Math.Round(amount * 100m / total, 2)
                };
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        return Ok(new
        {
            Total = total,
            Items = items
        });
    }

    [HttpGet("payment-mode-by-bill-type")]
    public async Task<IActionResult> GetPaymentModeByBillType(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);

        var payments =
            await (from payment in _db.Payments
                   join bill in _db.Bills on payment.BillId equals bill.Id
                   where bill.Status != "Deleted" &&
                         payment.PaidAtUtc >= start &&
                         payment.PaidAtUtc < end
                   select new { bill.Id, bill.BillType, bill.NetAmount, payment.Mode, payment.Amount })
                  .ToListAsync();

        // For OPD bills specifically, split each payment between the
        // doctor's own consultation charge and any other OPD charge on the
        // same bill (hospital/registration charge etc.), pro-rated by each
        // charge's share of the bill's net amount - payments aren't tied to
        // individual line items, so this is an apportionment, not an exact
        // per-item payment trace.
        var opdBillIds = payments.Where(x => x.BillType == "OPD").Select(x => x.Id).Distinct().ToList();
        var opdItems = await _db.BillItems.Where(x => opdBillIds.Contains(x.BillId)).ToListAsync();
        var doctorChargeRatioByBill = opdBillIds.ToDictionary(billId => billId, billId =>
        {
            var doctorAmount = opdItems.Where(x => x.BillId == billId && IsDoctorConsultationItem(x)).Sum(x => x.Amount);
            var billTotal = payments.First(p => p.Id == billId).NetAmount;
            return billTotal != 0 ? doctorAmount / billTotal : 0m;
        });

        // "Online" bundles every mode besides Cash (UPI/Card/Bank Transfer/
        // Other) - this is a two-way Cash-vs-Online split per bill type, not
        // a full mode breakdown (see payment-mode-share for that).
        var buckets = payments.SelectMany(x =>
        {
            if (x.BillType == "OPD")
            {
                var ratio = doctorChargeRatioByBill.GetValueOrDefault(x.Id, 0m);
                return new[]
                {
                    new { BillType = "OPD - Doctor Consultation", x.Mode, Amount = x.Amount * ratio },
                    new { BillType = "OPD - Other Charges", x.Mode, Amount = x.Amount * (1 - ratio) }
                };
            }
            return new[] { new { BillType = x.BillType, x.Mode, x.Amount } };
        });

        var rows = buckets
            .GroupBy(x => x.BillType)
            .Select(group =>
            {
                var cash = Math.Round(group.Where(x => x.Mode == "Cash").Sum(x => x.Amount), 2);
                var online = Math.Round(group.Where(x => x.Mode != "Cash").Sum(x => x.Amount), 2);
                return new
                {
                    BillType = group.Key,
                    Cash = cash,
                    Online = online,
                    Total = cash + online
                };
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        return Ok(new
        {
            Rows = rows,
            TotalCash = rows.Sum(x => x.Cash),
            TotalOnline = rows.Sum(x => x.Online),
            GrandTotal = rows.Sum(x => x.Total)
        });
    }

    [HttpGet("executive")]
    public async Task<IActionResult> Executive([FromQuery] DateTime? from,[FromQuery] DateTime? to)
    {
        var start=(from??DateTime.Today).Date;
        var end=(to??start).Date.AddDays(1);

        var bills=_db.Bills.Where(x=>x.Status!="Deleted"&&x.BillDate>=start&&x.BillDate<end);
        var payments=_db.Payments.Where(x=>x.PaidAtUtc>=start&&x.PaidAtUtc<end);
        var visits=_db.OpdVisits.Where(x=>!x.IsCancelled&&x.VisitDateUtc>=start&&x.VisitDateUtc<end);
        var admissions=_db.IpdAdmissions.Where(x=>x.AdmittedAtUtc>=start&&x.AdmittedAtUtc<end);
        var discharges=_db.IpdAdmissions.Where(x=>x.DischargedAtUtc.HasValue&&x.DischargedAtUtc.Value>=start&&x.DischargedAtUtc.Value<end);
        var surgeries=_db.OtBookings.Where(x=>x.Status!="Cancelled"&&x.StartAtUtc>=start&&x.StartAtUtc<end);
        var labOrders=_db.LabOrders.Where(x=>x.CreatedAtUtc>=start&&x.CreatedAtUtc<end);
        var pharmacySales=_db.PharmacySales.Where(x=>x.SaleDateUtc>=start&&x.SaleDateUtc<end);
        var expiryActions=_db.PharmacyExpiryActions.Where(x=>x.ActionDateUtc>=start&&x.ActionDateUtc<end);

        var gross=await bills.SumAsync(x=>(decimal?)x.GrossAmount)??0;
        var net=await bills.SumAsync(x=>(decimal?)x.NetAmount)??0;
        var collections=await payments.SumAsync(x=>(decimal?)x.Amount)??0;
        var dailyExpenses=await _db.DailyExpenses.Where(x=>x.ExpenseDate>=start&&x.ExpenseDate<end).SumAsync(x=>(decimal?)x.Amount)??0;
        var salaryExpenses=await _db.SalaryPayments.Where(x=>x.PaymentDate.HasValue&&x.PaymentDate.Value>=start&&x.PaymentDate.Value<end).SumAsync(x=>(decimal?)x.PaidAmount)??0;
        var pharmacyExpenses=await _db.PharmacyPurchasePayments.Where(x=>x.PaymentDate>=start&&x.PaymentDate<end).SumAsync(x=>(decimal?)x.Amount)??0;
        var labExpenses=await _db.LabPurchaseExpenses.Where(x=>x.PaymentDate.HasValue&&x.PaymentDate.Value>=start&&x.PaymentDate.Value<end).SumAsync(x=>(decimal?)x.PaidAmount)??0;
        var doctorProfessionalFees=await _db.DoctorSettlements.Where(x=>x.EarnedDate>=start&&x.EarnedDate<end).SumAsync(x=>(decimal?)x.PayableAmount)??0;
        var doctorPayments=await _db.DoctorSettlements.Where(x=>x.PaymentDate.HasValue&&x.PaymentDate.Value>=start&&x.PaymentDate.Value<end).SumAsync(x=>(decimal?)x.PaidAmount)??0;
        var expenses=dailyExpenses+salaryExpenses+pharmacyExpenses+labExpenses;
        var totalBeds=await _db.WardBeds.CountAsync(x=>x.IsActive);
        var occupied=await _db.WardBeds.CountAsync(x=>x.IsActive&&x.IsOccupied);

        var activeIpdPatients=await _db.IpdAdmissions
            .Where(x=>x.AdmittedAtUtc<end&&(!x.DischargedAtUtc.HasValue||x.DischargedAtUtc.Value>=start))
            .Select(x=>x.PatientId).Distinct().CountAsync();

        return Ok(new
        {
            From=start,
            To=end.AddDays(-1),
            Opd=await visits.CountAsync(x=>!x.IsEmergency),
            Ipd=activeIpdPatients,
            Emergency=await visits.CountAsync(x=>x.IsEmergency),
            Admissions=await admissions.CountAsync(),
            Discharges=await discharges.CountAsync(),
            Surgeries=await surgeries.CountAsync(),
            BedOccupancy=new{Occupied=occupied,Total=totalBeds,Percentage=totalBeds==0?0:Math.Round(occupied*100m/totalBeds,2)},
            LabTests=await labOrders.SelectMany(x=>x.Tests).CountAsync(),
            PharmacyBills=await pharmacySales.CountAsync(),
            GrossRevenue=gross,
            Discounts=gross-net,
            NetRevenue=net,
            Collections=collections,
            Outstanding=await bills.SumAsync(x=>(decimal?)(x.NetAmount-x.PaidAmount))??0,
            Expenses=expenses,
            DoctorProfessionalFees=doctorProfessionalFees,
            DoctorPayments=doctorPayments,
            DoctorOutstanding=Math.Max(0,doctorProfessionalFees-doctorPayments),
            HospitalRevenueAfterDoctorShare=net-doctorProfessionalFees,
            ExpenseBreakdown=new{Daily=dailyExpenses,Salary=salaryExpenses,Pharmacy=pharmacyExpenses,Lab=labExpenses},
            ExpiryReturnedAmount=await expiryActions.Where(x=>x.ActionType=="Return").SumAsync(x=>(decimal?)x.Amount)??0,
            ExpiryLossAmount=await expiryActions.Where(x=>x.ActionType=="Discard").SumAsync(x=>(decimal?)x.Amount)??0,
            OperatingResult=net-doctorProfessionalFees-expenses
        });
    }

    [HttpGet("monthly-revenue")]
    public async Task<IActionResult> MonthlyRevenue([FromQuery] int? year)
    {
        var selectedYear=year??DateTime.Today.Year;
        var start=new DateTime(selectedYear,1,1);
        var end=start.AddYears(1);
        var bills=await _db.Bills.Where(x=>x.Status!="Deleted"&&x.BillDate>=start&&x.BillDate<end).ToListAsync();
        var payments=await _db.Payments.Where(x=>x.PaidAtUtc>=start&&x.PaidAtUtc<end).ToListAsync();
        var dailyExpenses=await _db.DailyExpenses.Where(x=>x.ExpenseDate>=start&&x.ExpenseDate<end).ToListAsync();
        var salaryExpenses=await _db.SalaryPayments.Where(x=>x.PaymentDate.HasValue&&x.PaymentDate.Value>=start&&x.PaymentDate.Value<end).ToListAsync();
        var pharmacyExpenses=await _db.PharmacyPurchasePayments.Where(x=>x.PaymentDate>=start&&x.PaymentDate<end).ToListAsync();
        var labExpenses=await _db.LabPurchaseExpenses.Where(x=>x.PaymentDate.HasValue&&x.PaymentDate.Value>=start&&x.PaymentDate.Value<end).ToListAsync();
        var doctorFees=await _db.DoctorSettlements.Where(x=>x.EarnedDate>=start&&x.EarnedDate<end).ToListAsync();
        var rows=new List<object>();
        decimal previousNet=0;
        foreach(var month in Enumerable.Range(1,12))
        {
            var mb=bills.Where(x=>x.BillDate.Month==month).ToList();
            var gross=mb.Sum(x=>x.GrossAmount);var net=mb.Sum(x=>x.NetAmount);
            var collection=payments.Where(x=>x.PaidAtUtc.Month==month).Sum(x=>x.Amount);
            var doctorProfessionalFees=doctorFees.Where(x=>x.EarnedDate.Month==month).Sum(x=>x.PayableAmount);
            var expense=dailyExpenses.Where(x=>x.ExpenseDate.Month==month).Sum(x=>x.Amount)
                +salaryExpenses.Where(x=>x.PaymentDate!.Value.Month==month).Sum(x=>x.PaidAmount)
                +pharmacyExpenses.Where(x=>x.PaymentDate.Month==month).Sum(x=>x.Amount)
                +labExpenses.Where(x=>x.PaymentDate!.Value.Month==month).Sum(x=>x.PaidAmount);
            rows.Add(new{Month=month,MonthName=new DateTime(selectedYear,month,1).ToString("MMM"),Gross=gross,Discount=gross-net,Net=net,DifferenceFromPreviousMonth=month==1?0:net-previousNet,DoctorProfessionalFees=doctorProfessionalFees,HospitalRevenueAfterDoctorShare=net-doctorProfessionalFees,Collections=collection,Expenses=expense,OperatingResult=net-doctorProfessionalFees-expense});
            previousNet=net;
        }
        return Ok(new{Year=selectedYear,Rows=rows});
    }

    [HttpGet("gst-filing")]
    public async Task<IActionResult> GstFiling([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date;
        if (end < start) end = start;

        var categories = await GetActiveCategoryNames();

        var bills = await _db.Bills
            .Where(x => x.Status != "Deleted" && x.BillDate >= start && x.BillDate < end.AddDays(1))
            .Select(x => new { x.BillDate, x.BillType, x.NetAmount })
            .ToListAsync();

        var rows = new List<object>();
        var columnTotals = categories.ToDictionary(c => c, c => 0m);
        decimal grandTotal = 0;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var dayBills = bills.Where(x => x.BillDate.Date == date).ToList();
            var amounts = new Dictionary<string, decimal>();
            decimal rowTotal = 0;
            foreach (var category in categories)
            {
                var amount = dayBills.Where(x => x.BillType == category).Sum(x => x.NetAmount);
                amounts[category] = amount;
                columnTotals[category] += amount;
                rowTotal += amount;
            }
            grandTotal += rowTotal;
            rows.Add(new { Date = date.ToString("yyyy-MM-dd"), Amounts = amounts, Total = rowTotal });
        }

        return Ok(new
        {
            BillTypes = categories,
            Rows = rows,
            ColumnTotals = columnTotals,
            GrandTotal = grandTotal
        });
    }

    // Matches a BillItem that represents a doctor's own consultation charge
    // (as opposed to other OPD charges like hospital/registration charge),
    // across every way this app has ever created such a line: the new
    // catalog-driven Bill flow tags it ReferenceType="DoctorConsultation",
    // the OPD-visit/registration flows tag it ReferenceType="Doctor", and
    // all three write the same "Consultation - {doctor name}" Description.
    private static bool IsDoctorConsultationItem(BillItem item)
    {
        if (item.ReferenceType == "DoctorConsultation" || item.ReferenceType == "Doctor") return true;
        return (item.Description ?? "").StartsWith("Consultation -", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<string>> GetActiveCategoryNames()
    {
        return await _db.BillTypes
            .Where(x => x.IsActive && x.IsBillable)
            .OrderBy(x => x.Id)
            .Select(x => x.Name)
            .ToListAsync();
    }
}
