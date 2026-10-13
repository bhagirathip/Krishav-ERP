using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KrishavERP.Api.Modules.Billing;
using KrishavERP.Api.Modules.Doctors;
using KrishavERP.Api.Modules.Patients;
using KrishavERP.Api.Services;

namespace KrishavERP.Api.Modules.Reporting.Controllers;

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
        [FromQuery] int? doctorId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] string? searchColumn,
        [FromQuery] string? searchText)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);

        var categories = await GetActiveCategoryNames();
        var doctorsQuery = _db.Doctors.Where(x => x.IsActive);

        if (doctorId.HasValue)
            doctorsQuery = doctorsQuery.Where(x => x.Id == doctorId.Value);

        // doctorName is the only free-text column on this grid - patientCount/
        // consultationCount/total are aggregates handled below, not something
        // a plain Doctor-table WHERE can filter on. No column picker on the
        // frontend anymore either - the Doctor-wise grid uses a single plain
        // search box, so this default branch is the only one that ever runs.
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            doctorsQuery = searchColumn switch
            {
                _ => doctorsQuery.Where(x => x.Name.Contains(searchText))
            };
        }

        // patientCount/consultationCount/total are correlated per-doctor
        // aggregates, but they're bounded by the (small, non-growing) active
        // doctor count rather than by the transaction volume behind them, so
        // it's still cheap to let SQL sort on them via subqueries instead of
        // falling back to an in-memory sort.
        //
        // Total is cash-basis: it sums actual Payments received in the
        // period (joined to this doctor's bills), not bills issued in the
        // period - a bill created last month but paid today counts today; a
        // bill created today but not yet paid doesn't count until it is
        // (see Unpaid in ShapeDoctorRows for that remainder).
        var projected = doctorsQuery.Select(d => new DoctorAggregateRow
        {
            Doctor = d,
            ConsultationCount = _db.OpdVisits.Count(v =>
                !v.IsCancelled && v.DoctorId == d.Id && v.VisitDateUtc >= start && v.VisitDateUtc < end),
            PatientCount = _db.OpdVisits
                .Where(v => !v.IsCancelled && v.DoctorId == d.Id && v.VisitDateUtc >= start && v.VisitDateUtc < end)
                .Select(v => v.PatientId)
                .Distinct()
                .Count(),
            Total = (from p in _db.Payments
                     join b in _db.Bills on p.BillId equals b.Id
                     where b.Status != "Deleted" && b.DoctorId == d.Id && p.PaidAtUtc >= start && p.PaidAtUtc < end
                     select (decimal?)p.Amount).Sum() ?? 0m
        });

        var descending = sortDirection == "desc";
        projected = sortBy switch
        {
            "patientCount" => descending ? projected.OrderByDescending(x => x.PatientCount) : projected.OrderBy(x => x.PatientCount),
            "consultationCount" => descending ? projected.OrderByDescending(x => x.ConsultationCount) : projected.OrderBy(x => x.ConsultationCount),
            "total" => descending ? projected.OrderByDescending(x => x.Total) : projected.OrderBy(x => x.Total),
            "doctorName" => descending ? projected.OrderByDescending(x => x.Doctor.Name) : projected.OrderBy(x => x.Doctor.Name),
            _ => projected.OrderBy(x => x.Doctor.Name)
        };

        // Paging is opt-in via `page` - Reporting.vue is the only caller of
        // this endpoint today, but the opt-in check is kept for parity with
        // every other converted grid in case that ever changes.
        if (page.HasValue)
        {
            var paged = await projected.ToPagedResultAsync(page.Value, pageSize ?? 10);
            var shaped = await ShapeDoctorRows(paged.Rows, categories, start, end);
            return Ok(new { rows = shaped, totalCount = paged.TotalCount });
        }

        var all = await projected.ToListAsync();
        return Ok(await ShapeDoctorRows(all, categories, start, end));
    }

    // Per-category income breakdown is only computed for the rows actually
    // being returned (one page, or the full list on the legacy no-paging
    // path) - never for every active doctor up front.
    private async Task<List<object>> ShapeDoctorRows(
        List<DoctorAggregateRow> source,
        List<string> categories,
        DateTime start,
        DateTime end)
    {
        var result = new List<object>();

        foreach (var entry in source)
        {
            // Cash-basis per-category breakdown: actual payments received in
            // the period, joined to this doctor's bills - mirrors Total above.
            var payments = await (from p in _db.Payments
                                   join b in _db.Bills on p.BillId equals b.Id
                                   where b.Status != "Deleted" && b.DoctorId == entry.Doctor.Id
                                         && p.PaidAtUtc >= start && p.PaidAtUtc < end
                                   select new { p.Amount, b.BillType }).ToListAsync();

            var income = categories.ToDictionary(
                category => category,
                category => payments
                    .Where(x => x.BillType.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Amount));

            // Unpaid: the outstanding balance of bills actually issued in
            // this period - a separate, bill-date-based figure from the
            // payment-date-based Total/Income above. E.g. a ₹10,000 bill
            // created today with ₹4,000 paid today contributes ₹4,000 to
            // Total and ₹6,000 here, regardless of when (or whether) the
            // remaining ₹6,000 eventually gets paid.
            var unpaid = await _db.Bills
                .Where(x => x.Status != "Deleted" && x.DoctorId == entry.Doctor.Id && x.BillDate >= start && x.BillDate < end)
                .SumAsync(x => (decimal?)(x.NetAmount - x.PaidAmount)) ?? 0m;

            result.Add(new
            {
                DoctorId = entry.Doctor.Id,
                DoctorName = entry.Doctor.Name,
                PatientCount = entry.PatientCount,
                ConsultationCount = entry.ConsultationCount,
                Income = income,
                Total = entry.Total,
                Unpaid = Math.Max(0, unpaid)
            });
        }

        return result;
    }

    private sealed class DoctorAggregateRow
    {
        public Doctor Doctor { get; set; } = null!;
        public int ConsultationCount { get; set; }
        public int PatientCount { get; set; }
        public decimal Total { get; set; }
    }

    [HttpGet("patient-summary")]
    public async Task<IActionResult> GetPatientSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? patientId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] string? searchColumn,
        [FromQuery] string? searchText)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);

        var categories = await GetActiveCategoryNames();
        var patientsQuery = _db.Patients.Where(x => !x.IsDeleted);

        if (patientId.HasValue)
            patientsQuery = patientsQuery.Where(x => x.Id == patientId.Value);

        // Only patients with at least one matching bill OR payment in range
        // are shown, unless a specific patientId was requested (then it's
        // always shown, even with zero activity). The payment half matters
        // now that Total/Income are cash-basis below - a patient who's only
        // paying off an older bill this period (no new bill issued today)
        // still needs to show up, since they generated real cash this period.
        if (!patientId.HasValue)
        {
            patientsQuery = patientsQuery.Where(p =>
                _db.Bills.Any(b => b.Status != "Deleted" && b.PatientId == p.Id && b.BillDate >= start && b.BillDate < end)
                || _db.Payments.Any(pay => pay.PaidAtUtc >= start && pay.PaidAtUtc < end
                    && _db.Bills.Any(b => b.Id == pay.BillId && b.Status != "Deleted" && b.PatientId == p.Id)));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            patientsQuery = searchColumn switch
            {
                "patientCode" => patientsQuery.Where(x => x.PatientCode.Contains(searchText)),
                "phone" => patientsQuery.Where(x => x.Phone.Contains(searchText)),
                // No column specified - the Patient-wise grid uses a single
                // plain search box, not a column picker, so match any of the
                // fields it used to offer in that dropdown.
                _ => patientsQuery.Where(x =>
                    x.Name.Contains(searchText)
                    || x.PatientCode.Contains(searchText)
                    || x.Phone.Contains(searchText))
            };
        }

        // This grid can return thousands of rows over a wide custom date
        // range, unlike doctor-summary above - Total still has to be sortable
        // in SQL (via a correlated subquery) rather than loaded fully into
        // memory just to order it. Total is cash-basis (see doctor-summary
        // above for why): actual Payments received in the period, not bills
        // issued in the period.
        var projected = patientsQuery.Select(p => new PatientAggregateRow
        {
            Patient = p,
            Total = (from pay in _db.Payments
                     join b in _db.Bills on pay.BillId equals b.Id
                     where b.Status != "Deleted" && b.PatientId == p.Id && pay.PaidAtUtc >= start && pay.PaidAtUtc < end
                     select (decimal?)pay.Amount).Sum() ?? 0m
        });

        var descending = sortDirection == "desc";
        projected = sortBy switch
        {
            "patientCode" => descending ? projected.OrderByDescending(x => x.Patient.PatientCode) : projected.OrderBy(x => x.Patient.PatientCode),
            "phone" => descending ? projected.OrderByDescending(x => x.Patient.Phone) : projected.OrderBy(x => x.Patient.Phone),
            "total" => descending ? projected.OrderByDescending(x => x.Total) : projected.OrderBy(x => x.Total),
            "name" => descending ? projected.OrderByDescending(x => x.Patient.Name) : projected.OrderBy(x => x.Patient.Name),
            _ => projected.OrderBy(x => x.Patient.Name)
        };

        // Paging is opt-in via `page` - Reporting.vue is the only caller of
        // this endpoint today, but the opt-in check is kept for parity with
        // every other converted grid in case that ever changes.
        if (page.HasValue)
        {
            var paged = await projected.ToPagedResultAsync(page.Value, pageSize ?? 10);
            var shaped = await ShapePatientRows(paged.Rows, categories, start, end);
            return Ok(new { rows = shaped, totalCount = paged.TotalCount });
        }

        var all = await projected.ToListAsync();
        return Ok(await ShapePatientRows(all, categories, start, end));
    }

    // Per-category income breakdown is only computed for the rows actually
    // being returned (one page, or the full list on the legacy no-paging
    // path) - never for every matching patient up front.
    private async Task<List<object>> ShapePatientRows(
        List<PatientAggregateRow> source,
        List<string> categories,
        DateTime start,
        DateTime end)
    {
        var result = new List<object>();

        foreach (var entry in source)
        {
            // Cash-basis per-category breakdown: actual payments received in
            // the period, joined to this patient's bills - mirrors Total above.
            var payments = await (from p in _db.Payments
                                   join b in _db.Bills on p.BillId equals b.Id
                                   where b.Status != "Deleted" && b.PatientId == entry.Patient.Id
                                         && p.PaidAtUtc >= start && p.PaidAtUtc < end
                                   select new { p.Amount, b.BillType }).ToListAsync();

            var income = categories.ToDictionary(
                category => category,
                category => payments
                    .Where(x => x.BillType.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Amount));

            // Unpaid: the outstanding balance of bills actually issued in
            // this period - see ShapeDoctorRows for the full explanation.
            var unpaid = await _db.Bills
                .Where(x => x.Status != "Deleted" && x.PatientId == entry.Patient.Id && x.BillDate >= start && x.BillDate < end)
                .SumAsync(x => (decimal?)(x.NetAmount - x.PaidAmount)) ?? 0m;

            result.Add(new
            {
                PatientId = entry.Patient.Id,
                entry.Patient.PatientCode,
                entry.Patient.Name,
                entry.Patient.Phone,
                Income = income,
                // Reuse the Total already computed in SQL above for sorting,
                // instead of re-summing the re-fetched payments list.
                Total = entry.Total,
                Unpaid = Math.Max(0, unpaid)
            });
        }

        return result;
    }

    private sealed class PatientAggregateRow
    {
        public Patient Patient { get; set; } = null!;
        public decimal Total { get; set; }
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

        // Cash-basis, consistent with Doctor-wise/Patient-wise reports above:
        // income reflects actual payments received in the period, not bills
        // issued in it.
        var query =
            from payment in _db.Payments
            join bill in _db.Bills on payment.BillId equals bill.Id
            where bill.Status != "Deleted" && payment.PaidAtUtc >= start && payment.PaidAtUtc < end
            select new { payment.Amount, bill.BillType, bill.DoctorId, bill.PatientId };

        if (doctorId.HasValue)
            query = query.Where(x => x.DoctorId == doctorId.Value);

        if (patientId.HasValue)
            query = query.Where(x => x.PatientId == patientId.Value);

        var payments = await query.ToListAsync();
        var total = payments.Sum(x => x.Amount);

        var result = categories.Select(category =>
        {
            var amount = payments
                .Where(x => x.BillType.Equals(category, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Amount);

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
                         bill.BillType != "Pharmacy" &&
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

    // Pharmacy's own Cash-vs-Online split, mirroring payment-mode-by-bill-type
    // above but scoped to Pharmacy bills only (that action explicitly
    // excludes Pharmacy, since it's filed/reported separately everywhere
    // else now - see GstFilingPharmacy). Grouped by BillType for symmetry
    // with the hospital version even though every Pharmacy bill carries the
    // same BillType today - refunds (negative Payment rows from
    // PharmacyReturnsController) net themselves out automatically since
    // this sums straight from the Payments table.
    [HttpGet("payment-mode-by-bill-type-pharmacy")]
    public async Task<IActionResult> GetPaymentModeByBillTypePharmacy(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);

        var payments =
            await (from payment in _db.Payments
                   join bill in _db.Bills on payment.BillId equals bill.Id
                   where bill.Status != "Deleted" &&
                         bill.BillType == "Pharmacy" &&
                         payment.PaidAtUtc >= start &&
                         payment.PaidAtUtc < end
                   select new { bill.BillType, payment.Mode, payment.Amount })
                  .ToListAsync();

        var rows = payments
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

    // Hospital-side GST filing - every billable category except Pharmacy,
    // which now files separately (see GstFilingPharmacy below) since it has
    // its own GST No/HSN setup and its own return/credit-note flow that
    // doesn't fit the same per-bill NetAmount shape as hospital billing.
    [HttpGet("gst-filing")]
    public async Task<IActionResult> GstFiling([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date;
        if (end < start) end = start;

        var categories = (await GetActiveCategoryNames()).Where(c => c != "Pharmacy").ToList();

        var bills = await _db.Bills
            .Where(x => x.Status != "Deleted" && x.BillType != "Pharmacy" && x.BillDate >= start && x.BillDate < end.AddDays(1))
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

    // Pharmacy-side GST filing - date-wise Taxable/CGST/SGST computed from
    // each sale item's MRP (tax-inclusive, same extraction formula used on
    // the printed pharmacy invoice - see PharmacySalesController.Print),
    // netted against returns on the date the return actually happened (a
    // credit note is filed in the period it's issued, not retroactively
    // against the original sale's date).
    [HttpGet("gst-filing-pharmacy")]
    public async Task<IActionResult> GstFilingPharmacy([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date;
        if (end < start) end = start;
        var endExclusive = end.AddDays(1);

        // A draft walk-in/OPD bill (not yet paid in full, stock not yet
        // committed - see PharmacySalesController.Create/BillsController.Pay)
        // hasn't actually transacted, so it carries no GST liability yet.
        var saleItems = await (
            from item in _db.PharmacySaleItems
            join sale in _db.PharmacySales on item.PharmacySaleId equals sale.Id
            where sale.SaleDateUtc >= start && sale.SaleDateUtc < endExclusive && sale.StockDeducted
            select new { sale.SaleDateUtc, item.TotalAmount, item.CgstPercent, item.SgstPercent }
        ).ToListAsync();

        var returnItems = await (
            from item in _db.PharmacyReturnItems
            join ret in _db.PharmacyReturns on item.PharmacyReturnId equals ret.Id
            where ret.ReturnDateUtc >= start && ret.ReturnDateUtc < endExclusive
            select new { ret.ReturnDateUtc, item.TotalAmount, item.CgstPercent, item.SgstPercent }
        ).ToListAsync();

        (decimal taxable, decimal cgst, decimal sgst) Extract(decimal totalAmount, decimal cgstPercent, decimal sgstPercent)
        {
            var rate = cgstPercent + sgstPercent;
            var taxable = rate > 0 ? Math.Round(totalAmount / (1 + rate / 100m), 2) : totalAmount;
            var cgst = Math.Round(taxable * cgstPercent / 100m, 2);
            var sgst = Math.Round(taxable * sgstPercent / 100m, 2);
            return (taxable, cgst, sgst);
        }

        var rows = new List<object>();
        decimal totalTaxable = 0, totalCgst = 0, totalSgst = 0, totalAmount = 0;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            decimal dayTaxable = 0, dayCgst = 0, daySgst = 0, dayAmount = 0;

            foreach (var item in saleItems.Where(x => x.SaleDateUtc.Date == date))
            {
                var (taxable, cgst, sgst) = Extract(item.TotalAmount, item.CgstPercent, item.SgstPercent);
                dayTaxable += taxable; dayCgst += cgst; daySgst += sgst; dayAmount += item.TotalAmount;
            }

            foreach (var item in returnItems.Where(x => x.ReturnDateUtc.Date == date))
            {
                var (taxable, cgst, sgst) = Extract(item.TotalAmount, item.CgstPercent, item.SgstPercent);
                dayTaxable -= taxable; dayCgst -= cgst; daySgst -= sgst; dayAmount -= item.TotalAmount;
            }

            totalTaxable += dayTaxable; totalCgst += dayCgst; totalSgst += daySgst; totalAmount += dayAmount;
            rows.Add(new
            {
                Date = date.ToString("yyyy-MM-dd"),
                Taxable = Math.Round(dayTaxable, 2),
                Cgst = Math.Round(dayCgst, 2),
                Sgst = Math.Round(daySgst, 2),
                Total = Math.Round(dayAmount, 2)
            });
        }

        return Ok(new
        {
            Rows = rows,
            TotalTaxable = Math.Round(totalTaxable, 2),
            TotalCgst = Math.Round(totalCgst, 2),
            TotalSgst = Math.Round(totalSgst, 2),
            GrandTotal = Math.Round(totalAmount, 2)
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
