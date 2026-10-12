using System.Security.Claims;
using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Modules.Referrals.Controllers;

[Authorize]
[ApiController]
[Route("api/referrals")]
public class ReferralsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReferralsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("sources")]
    public async Task<IActionResult> GetSources()
    {
        var sources = await _db.PatientSources
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync();

        return Ok(sources);
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(await _db.Referrers.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Add(ReferrerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Referrer name is required." });

        var row = new Referrer
        {
            Name = request.Name.Trim(),
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            ReferrerType = request.ReferrerType,
            DoctorId = request.DoctorId,
            IsActive = request.IsActive
        };

        _db.Referrers.Add(row);
        await _db.SaveChangesAsync();
        row.ReferrerCode = $"KHC-R-{row.Id:0000}";
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, ReferrerRequest request)
    {
        var row = await _db.Referrers.FindAsync(id);
        if (row == null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Referrer name is required." });

        row.Name = request.Name.Trim();
        row.Phone = request.Phone?.Trim();
        row.Address = request.Address?.Trim();
        row.ReferrerType = request.ReferrerType;
        row.DoctorId = request.DoctorId;
        row.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row = await _db.Referrers.FindAsync(id);
        if (row == null) return NotFound();

        row.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? referrerId,
        [FromQuery] string? department,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] string? searchColumn,
        [FromQuery] string? searchText)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);

        var refs = await _db.Referrers
            .Where(x => x.IsActive && (!referrerId.HasValue || x.Id == referrerId.Value))
            .OrderBy(x => x.Name)
            .ToListAsync();

        var categories = await _db.BillTypes
            .Where(x => x.IsActive && x.IsBillable)
            .OrderBy(x => x.Id)
            .Select(x => x.Name)
            .ToListAsync();

        var result = new List<object>();
        // Built alongside `result` so the Payouts grid's paged/flattened view
        // (one row per referrer+patient) never has to be re-derived from the
        // nested shape - it's just assembled directly in the same loop below.
        var flatRows = new List<ReferralPayoutRow>();

        foreach (var r in refs)
        {
            var patients = await _db.Patients
                .Where(x => !x.IsDeleted && x.ReferrerId == r.Id)
                .OrderBy(x => x.Name)
                .ToListAsync();

            var patientRows = new List<object>();
            decimal refTotal = 0, refPayout = 0;

            foreach (var p in patients)
            {
                var bills = await _db.Bills.Where(x =>
                    x.Status != "Deleted" &&
                    x.PatientId == p.Id &&
                    x.CreatedAtUtc >= start &&
                    x.CreatedAtUtc < end &&
                    (string.IsNullOrWhiteSpace(department) || department == "All" || x.BillType == department))
                    .ToListAsync();

                var income = categories.ToDictionary(c => c, c => bills.Where(x => x.BillType.Equals(c, StringComparison.OrdinalIgnoreCase)).Sum(x => x.NetAmount));
                var total = bills.Sum(x => x.NetAmount);
                var payout = await _db.ReferralPayouts.FirstOrDefaultAsync(x => x.ReferrerId == r.Id && x.PatientId == p.Id);

                refTotal += total;
                refPayout += payout?.Amount ?? 0;

                patientRows.Add(new
                {
                    PatientId = p.Id,
                    p.PatientCode,
                    p.Name,
                    p.Phone,
                    p.MarketingSource,
                    Income = income,
                    TotalIncome = total,
                    ApprovedPayout = payout?.Amount ?? 0,
                    PaymentDate = payout?.PaymentDate,
                    PaymentMode = payout?.PaymentMode ?? "",
                    ReferenceNumber = payout?.ReferenceNumber ?? "",
                    PayoutNotes = payout?.Notes ?? ""
                });

                flatRows.Add(new ReferralPayoutRow
                {
                    PatientId = p.Id,
                    PatientCode = p.PatientCode,
                    Name = p.Name,
                    Phone = p.Phone,
                    MarketingSource = p.MarketingSource,
                    Income = income,
                    TotalIncome = total,
                    ApprovedPayout = payout?.Amount ?? 0,
                    PaymentDate = payout?.PaymentDate,
                    PaymentMode = payout?.PaymentMode ?? "",
                    ReferenceNumber = payout?.ReferenceNumber ?? "",
                    PayoutNotes = payout?.Notes ?? "",
                    ReferrerId = r.Id,
                    ReferrerCode = r.ReferrerCode,
                    ReferrerName = r.Name
                });
            }

            result.Add(new
            {
                ReferrerId = r.Id,
                r.ReferrerCode,
                r.Name,
                r.ReferrerType,
                ReferredCount = patients.Count,
                TotalIncome = refTotal,
                ApprovedPayout = refPayout,
                Patients = patientRows
            });
        }

        var canApprovePayout = await IsAdministrator();

        // Paging is opt-in via `page` - Master/Referrals.vue calls this same
        // endpoint (to show each referrer's referred-patient count) without
        // `page` and needs the original nested { rows: [ { ..., patients: []
        // } ] } shape back unchanged. Only the Payouts grid passes `page`, in
        // which case rows[].patients is flattened into one row per referrer+
        // patient (the same flattening the Payouts grid used to do client-
        // side) and that flattened list is searched/sorted/paginated instead.
        if (!page.HasValue)
        {
            return Ok(new { CanApprovePayout = canApprovePayout, Categories = categories, Rows = result });
        }

        IEnumerable<ReferralPayoutRow> filtered = flatRows;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filtered = searchColumn switch
            {
                "referrerName" => filtered.Where(x => x.ReferrerName.Contains(searchText, StringComparison.OrdinalIgnoreCase)),
                "marketingSource" => filtered.Where(x => (x.MarketingSource ?? "").Contains(searchText, StringComparison.OrdinalIgnoreCase)),
                "phone" => filtered.Where(x => (x.Phone ?? "").Contains(searchText, StringComparison.OrdinalIgnoreCase)),
                "patientCode" => filtered.Where(x => (x.PatientCode ?? "").Contains(searchText, StringComparison.OrdinalIgnoreCase)),
                // No column specified - the Payouts grid uses a single plain
                // search box, not a column picker, so match any of the fields
                // it used to offer in that dropdown.
                _ => filtered.Where(x =>
                    x.ReferrerName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || x.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || (x.MarketingSource ?? "").Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || (x.Phone ?? "").Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || (x.PatientCode ?? "").Contains(searchText, StringComparison.OrdinalIgnoreCase))
            };
        }

        var descending = sortDirection == "desc";
        filtered = sortBy switch
        {
            "name" => descending ? filtered.OrderByDescending(x => x.Name) : filtered.OrderBy(x => x.Name),
            "marketingSource" => descending ? filtered.OrderByDescending(x => x.MarketingSource) : filtered.OrderBy(x => x.MarketingSource),
            "totalIncome" => descending ? filtered.OrderByDescending(x => x.TotalIncome) : filtered.OrderBy(x => x.TotalIncome),
            "approvedPayout" => descending ? filtered.OrderByDescending(x => x.ApprovedPayout) : filtered.OrderBy(x => x.ApprovedPayout),
            "paymentDate" => descending ? filtered.OrderByDescending(x => x.PaymentDate) : filtered.OrderBy(x => x.PaymentDate),
            "paymentMode" => descending ? filtered.OrderByDescending(x => x.PaymentMode) : filtered.OrderBy(x => x.PaymentMode),
            "referenceNumber" => descending ? filtered.OrderByDescending(x => x.ReferenceNumber) : filtered.OrderBy(x => x.ReferenceNumber),
            "referrerName" => descending ? filtered.OrderByDescending(x => x.ReferrerName) : filtered.OrderBy(x => x.ReferrerName),
            _ => descending ? filtered.OrderByDescending(x => x.ReferrerName) : filtered.OrderBy(x => x.ReferrerName)
        };

        var materialized = filtered.ToList();
        var clampedPageSize = Math.Clamp(pageSize ?? 10, 1, 200);
        var clampedPage = Math.Max(1, page.Value);
        var pageRows = materialized
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToList();

        return Ok(new
        {
            CanApprovePayout = canApprovePayout,
            Categories = categories,
            Rows = pageRows,
            TotalCount = materialized.Count
        });
    }

    private sealed class ReferralPayoutRow
    {
        public int PatientId { get; set; }
        public string PatientCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? MarketingSource { get; set; }
        public Dictionary<string, decimal> Income { get; set; } = new();
        public decimal TotalIncome { get; set; }
        public decimal ApprovedPayout { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string PaymentMode { get; set; } = "";
        public string ReferenceNumber { get; set; } = "";
        public string PayoutNotes { get; set; } = "";
        public int ReferrerId { get; set; }
        public string ReferrerCode { get; set; } = "";
        public string ReferrerName { get; set; } = "";
    }

    [HttpPost("payout")]
    public async Task<IActionResult> SavePayout(ReferralPayoutRequest request)
    {
        if (!await IsAdministrator()) return Forbid();
        if (request.Amount < 0) return BadRequest(new { message = "Payout cannot be negative." });
        if (!await _db.Patients.AnyAsync(x => x.Id == request.PatientId && x.ReferrerId == request.ReferrerId))
            return BadRequest(new { message = "Patient is not linked to this referrer." });

        var row = await _db.ReferralPayouts.FirstOrDefaultAsync(x => x.ReferrerId == request.ReferrerId && x.PatientId == request.PatientId);
        if (row == null)
        {
            row = new ReferralPayout { ReferrerId = request.ReferrerId, PatientId = request.PatientId };
            _db.ReferralPayouts.Add(row);
        }

        row.Amount = request.Amount;
        row.PaymentDate = request.PaymentDate;
        row.PaymentMode = request.PaymentMode?.Trim();
        row.ReferenceNumber = request.ReferenceNumber?.Trim();
        row.Notes = request.Notes;
        row.UpdatedAtUtc = DateTime.UtcNow;

        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) row.ApprovedByUserId = userId;

        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpGet("marketing-summary")]
    public async Task<IActionResult> MarketingSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);

        var patients = await _db.Patients
            .Where(x => !x.IsDeleted && x.CreatedAtUtc >= start && x.CreatedAtUtc < end)
            .ToListAsync();

        var items = patients
            .GroupBy(x => string.IsNullOrWhiteSpace(x.MarketingSource) ? "Walk-in" : x.MarketingSource)
            .Select(g => new { Source = g.Key, Patients = g.Count() })
            .OrderByDescending(x => x.Patients)
            .ToList();

        return Ok(items);
    }

    private async Task<bool> IsAdministrator()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return false;

        var roleId = await _db.AppUsers.Where(x => x.Id == id).Select(x => x.RoleId).FirstOrDefaultAsync();
        return roleId.HasValue && await _db.AppRoles.AnyAsync(x => x.Id == roleId.Value && x.Name == "Administrator" && x.IsActive);
    }
}
