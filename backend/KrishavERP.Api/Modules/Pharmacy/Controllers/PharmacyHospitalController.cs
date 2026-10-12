using KrishavERP.Api;
using KrishavERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Modules.Pharmacy.Controllers;

// Internal hospital consumption (Lab/Dental/OT/Others drawing stock from the
// pharmacy for their own use, not for a specific patient) - deliberately
// kept separate from PharmacySalesController: there is no patient, no Bill,
// no Payment at the point of issue. The hospital settles its running tab
// with the pharmacy periodically instead (see Settle below). Stock lookup
// reuses the existing GET /api/pharmacy/sales/search endpoint - no need to
// duplicate it here, the available-batches data is identical.
[Authorize]
[ApiController]
[Route("api/pharmacy/hospital")]
public class PharmacyHospitalController : ControllerBase
{
    private static readonly string[] Departments = { "Lab", "Dental", "OT", "Others" };
    private static readonly string[] AllowedPaymentModes = { "Cash", "UPI", "Bank Transfer" };

    private readonly AppDbContext _db;

    public PharmacyHospitalController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("departments")]
    public IActionResult GetDepartments() => Ok(Departments);

    [HttpPost("issues")]
    public async Task<IActionResult> CreateIssue(HospitalIssueRequest request)
    {
        if (!Departments.Contains(request.Department))
        {
            return BadRequest(new { message = "Please select a valid department." });
        }

        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "Add at least one medicine to the issue." });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var issue = new HospitalIssue
        {
            Department = request.Department,
            IssueDateUtc = DateTime.Now
        };

        decimal grandTotal = 0;

        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
            {
                return BadRequest(new { message = "Issue quantity must be greater than zero." });
            }

            var stock = await _db.PharmacyPurchaseItems
                .Include(x => x.MedicineType)
                .Include(x => x.PurchaseInvoice)
                .FirstOrDefaultAsync(x => x.Id == line.PurchaseItemId);

            if (stock == null || stock.PurchaseInvoice == null || stock.PurchaseInvoice.IsDeleted)
            {
                return BadRequest(new { message = "One of the selected medicines is no longer available." });
            }

            if (stock.ExpiryDate.Date < DateTime.Today)
            {
                return BadRequest(new { message = $"{stock.ProductName} / batch {stock.BatchNo} is expired." });
            }

            // Same pack/unit-label and stock-reduction math as
            // PharmacySalesController.Create - internal issues deplete the
            // same physical stock a patient sale would, they just aren't
            // billed to anyone at the point of dispensing.
            var typeName = stock.MedicineType?.Name ?? "Tablet";
            var unitsPerPack = Math.Max(stock.TabletsPerStrip, 1);

            string packLabel;
            string unitLabel;

            if (typeName.Equals("Tablet", StringComparison.OrdinalIgnoreCase))
            {
                packLabel = "Strip";
                unitLabel = "Tablet";
            }
            else if (typeName.Equals("Injection", StringComparison.OrdinalIgnoreCase))
            {
                packLabel = "Pack";
                unitLabel = "Vial";
            }
            else
            {
                packLabel = "Unit";
                unitLabel = "Unit";
                unitsPerPack = 1;
            }

            var validUnits = packLabel == unitLabel
                ? new[] { packLabel }
                : new[] { packLabel, unitLabel };

            if (!validUnits.Contains(line.UnitType))
            {
                return BadRequest(new { message = $"Invalid issue unit for {stock.ProductName}." });
            }

            var requiredUnits = line.UnitType == packLabel
                ? line.Quantity * unitsPerPack
                : line.Quantity;

            if (requiredUnits > stock.RemainingTablets)
            {
                return BadRequest(new
                {
                    message = $"Only {stock.RemainingTablets} unit(s) of {stock.ProductName} / batch {stock.BatchNo} are available."
                });
            }

            // Valued at MRP, same as a patient sale - this is what the
            // hospital reimburses the pharmacy for the consumed stock.
            var unitPrice = line.UnitType == packLabel
                ? stock.Mrp
                : Math.Round(stock.Mrp / unitsPerPack, 4);

            var lineTotal = Math.Round(unitPrice * line.Quantity, 2);
            grandTotal += lineTotal;
            stock.RemainingTablets -= requiredUnits;

            issue.Items.Add(new HospitalIssueItem
            {
                PurchaseItemId = stock.Id,
                ProductName = stock.ProductName,
                BatchNo = stock.BatchNo,
                UnitType = line.UnitType,
                Quantity = line.Quantity,
                QuantityInTablets = requiredUnits,
                Mrp = stock.Mrp,
                UnitPrice = unitPrice,
                TotalAmount = lineTotal
            });
        }

        issue.TotalAmount = grandTotal;
        _db.HospitalIssues.Add(issue);
        await _db.SaveChangesAsync();

        issue.IssueNumber = $"HOSP-{DateTime.Now:yyyyMMdd}-{issue.Id:000000}";
        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok(new { issue.Id, issue.IssueNumber, issue.TotalAmount });
    }

    [HttpGet("issues")]
    public async Task<IActionResult> GetIssues(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? department,
        [FromQuery] bool? unsettledOnly,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] string? searchText)
    {
        var query = _db.HospitalIssues.Include(x => x.Items).AsQueryable();

        if (from.HasValue) query = query.Where(x => x.IssueDateUtc >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.IssueDateUtc < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(department)) query = query.Where(x => x.Department == department);
        if (unsettledOnly == true) query = query.Where(x => x.SettlementId == null);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(x =>
                x.IssueNumber.Contains(searchText)
                || x.Department.Contains(searchText));
        }

        var descending = sortDirection == "desc";
        query = sortBy switch
        {
            "issueNumber" => descending ? query.OrderByDescending(x => x.IssueNumber) : query.OrderBy(x => x.IssueNumber),
            "department" => descending ? query.OrderByDescending(x => x.Department) : query.OrderBy(x => x.Department),
            "totalAmount" => descending ? query.OrderByDescending(x => x.TotalAmount) : query.OrderBy(x => x.TotalAmount),
            _ => query.OrderByDescending(x => x.Id)
        };

        List<HospitalIssue> issues;
        int totalCount;
        if (page.HasValue)
        {
            var paged = await query.ToPagedResultAsync(page.Value, pageSize ?? 10);
            issues = paged.Rows;
            totalCount = paged.TotalCount;
        }
        else
        {
            issues = await query.ToListAsync();
            totalCount = issues.Count;
        }

        var shaped = issues.Select(x => new
        {
            x.Id,
            x.IssueNumber,
            x.Department,
            x.IssueDateUtc,
            x.TotalAmount,
            IsSettled = x.SettlementId != null,
            x.SettlementId,
            Items = x.Items.Select(i => new { i.ProductName, i.BatchNo, i.UnitType, i.Quantity, i.UnitPrice, i.TotalAmount })
        }).ToList();

        if (page.HasValue)
        {
            return Ok(new { rows = shaped, totalCount });
        }

        return Ok(shaped);
    }

    // What "Settle Now" is about to sweep up, shown date-wise before the
    // pharmacist commits to it.
    [HttpGet("outstanding")]
    public async Task<IActionResult> GetOutstanding()
    {
        var issues = await _db.HospitalIssues
            .Include(x => x.Items)
            .Where(x => x.SettlementId == null)
            .OrderBy(x => x.IssueDateUtc)
            .ToListAsync();

        var byDate = issues
            .GroupBy(x => x.IssueDateUtc.Date)
            .Select(g => new
            {
                Date = g.Key,
                Issues = g.Select(x => new
                {
                    x.Id,
                    x.IssueNumber,
                    x.Department,
                    x.TotalAmount,
                    Items = x.Items.Select(i => new { i.ProductName, i.BatchNo, i.UnitType, i.Quantity, i.UnitPrice, i.TotalAmount })
                }),
                DateTotal = g.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.Date)
            .ToList();

        return Ok(new { rows = byDate, grandTotal = issues.Sum(x => x.TotalAmount) });
    }

    // Always settles everything currently outstanding in one lump sum - no
    // partial/date-range settlement. The settlement record stays itemized
    // date-wise via its linked issues, so "one payable total" doesn't mean
    // losing the breakdown of what it covers.
    [HttpPost("settle")]
    public async Task<IActionResult> Settle()
    {
        var outstanding = await _db.HospitalIssues
            .Where(x => x.SettlementId == null)
            .ToListAsync();

        if (outstanding.Count == 0)
        {
            return BadRequest(new { message = "There is nothing outstanding to settle." });
        }

        var settlement = new HospitalSettlement
        {
            SettlementDateUtc = DateTime.Now,
            TotalAmount = outstanding.Sum(x => x.TotalAmount)
        };

        _db.HospitalSettlements.Add(settlement);
        await _db.SaveChangesAsync();

        settlement.SettlementNumber = $"HSET-{DateTime.Now:yyyyMMdd}-{settlement.Id:000000}";
        foreach (var issue in outstanding) issue.SettlementId = settlement.Id;
        await _db.SaveChangesAsync();

        return Ok(new { settlement.Id, settlement.SettlementNumber, settlement.TotalAmount, settledCount = outstanding.Count });
    }

    [HttpGet("settlements")]
    public async Task<IActionResult> GetSettlements(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] string? searchText)
    {
        var query = _db.HospitalSettlements.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(x => x.SettlementNumber.Contains(searchText));
        }

        var descending = sortDirection == "desc";
        query = sortBy switch
        {
            "settlementNumber" => descending ? query.OrderByDescending(x => x.SettlementNumber) : query.OrderBy(x => x.SettlementNumber),
            "totalAmount" => descending ? query.OrderByDescending(x => x.TotalAmount) : query.OrderBy(x => x.TotalAmount),
            _ => query.OrderByDescending(x => x.Id)
        };

        List<HospitalSettlement> settlements;
        int totalCount;
        if (page.HasValue)
        {
            var paged = await query.ToPagedResultAsync(page.Value, pageSize ?? 10);
            settlements = paged.Rows;
            totalCount = paged.TotalCount;
        }
        else
        {
            settlements = await query.ToListAsync();
            totalCount = settlements.Count;
        }

        if (page.HasValue)
        {
            return Ok(new { rows = settlements, totalCount });
        }

        return Ok(settlements);
    }

    [HttpGet("settlements/{id:int}")]
    public async Task<IActionResult> GetSettlementDetail(int id)
    {
        var settlement = await _db.HospitalSettlements.FindAsync(id);
        if (settlement == null) return NotFound();

        var issues = await _db.HospitalIssues
            .Include(x => x.Items)
            .Where(x => x.SettlementId == id)
            .OrderBy(x => x.IssueDateUtc)
            .ToListAsync();

        var byDate = issues
            .GroupBy(x => x.IssueDateUtc.Date)
            .Select(g => new
            {
                Date = g.Key,
                Issues = g.Select(x => new
                {
                    x.Id,
                    x.IssueNumber,
                    x.Department,
                    x.TotalAmount,
                    Items = x.Items.Select(i => new { i.ProductName, i.BatchNo, i.UnitType, i.Quantity, i.UnitPrice, i.TotalAmount })
                }),
                DateTotal = g.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.Date)
            .ToList();

        return Ok(new { settlement, rows = byDate });
    }

    [HttpPost("settlements/{id:int}/pay")]
    public async Task<IActionResult> PaySettlement(int id, HospitalSettlementPaymentRequest request)
    {
        var settlement = await _db.HospitalSettlements.FindAsync(id);
        if (settlement == null) return NotFound();

        if (settlement.IsPaid)
        {
            return BadRequest(new { message = "This settlement has already been marked as paid." });
        }

        if (!AllowedPaymentModes.Contains(request.PaymentMode))
        {
            return BadRequest(new { message = "Please select a valid payment mode." });
        }

        settlement.IsPaid = true;
        settlement.PaidAtUtc = DateTime.Now;
        settlement.PaymentMode = request.PaymentMode;
        settlement.ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim();
        await _db.SaveChangesAsync();

        return Ok(settlement);
    }
}
