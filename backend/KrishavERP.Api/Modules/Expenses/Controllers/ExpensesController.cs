using KrishavERP.Api;
using KrishavERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Modules.Expenses.Controllers;

[Authorize]
[ApiController]
[Route("api/expenses")]
public class ExpensesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExpensesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("daily")]
    public async Task<IActionResult> Daily([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? start).Date.AddDays(1);
        var rows = await _db.DailyExpenses
            .Where(x => x.ExpenseDate >= start && x.ExpenseDate < end)
            .OrderByDescending(x => x.ExpenseDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        var staff = await StaffMap(rows.Select(x => x.PaidByStaffId));
        return Ok(rows.Select(x => new
        {
            x.Id, x.ExpenseDate, x.Category, x.Description, x.Amount, x.PaymentMode,
            x.ReferenceNumber, x.Notes, x.PaidByStaffId,
            PaidByStaffName = x.PaidByStaffId.HasValue && staff.TryGetValue(x.PaidByStaffId.Value, out var n) ? n : "",
            x.CreatedAtUtc
        }));
    }

    [HttpPost("daily")]
    public async Task<IActionResult> AddDaily(DailyExpenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description) || request.Amount <= 0)
            return BadRequest(new { message = "Description and amount are required." });
        if (!await ValidStaff(request.PaidByStaffId))
            return BadRequest(new { message = "Please select a valid staff member who paid." });

        var row = new DailyExpense
        {
            ExpenseDate = request.ExpenseDate.Date,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            Description = request.Description.Trim(),
            Amount = request.Amount,
            PaymentMode = request.PaymentMode,
            PaidByStaffId = request.PaidByStaffId,
            ReferenceNumber = request.ReferenceNumber?.Trim(),
            Notes = request.Notes?.Trim()
        };
        _db.DailyExpenses.Add(row);
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpPut("daily/{id:int}")]
    public async Task<IActionResult> EditDaily(int id, DailyExpenseRequest request)
    {
        var row = await _db.DailyExpenses.FindAsync(id);
        if (row == null) return NotFound();
        if (!await ValidStaff(request.PaidByStaffId))
            return BadRequest(new { message = "Please select a valid staff member who paid." });
        row.ExpenseDate = request.ExpenseDate.Date;
        row.Category = request.Category?.Trim() ?? "General";
        row.Description = request.Description.Trim();
        row.Amount = request.Amount;
        row.PaymentMode = request.PaymentMode;
        row.PaidByStaffId = request.PaidByStaffId;
        row.ReferenceNumber = request.ReferenceNumber?.Trim();
        row.Notes = request.Notes?.Trim();
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpDelete("daily/{id:int}")]
    public async Task<IActionResult> DeleteDaily(int id)
    {
        var row = await _db.DailyExpenses.FindAsync(id);
        if (row == null) return NotFound();
        _db.DailyExpenses.Remove(row);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // Only the Pharmacy Expenses grid reads this endpoint (no other screen
    // uses it as a full-array dropdown/autocomplete source), so paging is
    // always applied rather than being opt-in. PaidAmount/Outstanding are
    // computed by summing PharmacyPurchasePayments per invoice after the
    // page is fetched (same shape as the previous implementation, just
    // scoped to the current page's invoices instead of every payment ever
    // made) - that join-and-sum can't be pushed into the SQL ORDER BY/WHERE
    // without real risk, so (per the Patients precedent for columns that
    // are computed post-fetch) those two columns are left without
    // sort/search support in the frontend grid.
    [HttpGet("pharmacy")]
    public async Task<IActionResult> Pharmacy(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? searchColumn,
        [FromQuery] string? searchText,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection)
    {
        var query = _db.PharmacyPurchaseInvoices
            .Where(x => !x.IsDeleted)
            .Include(x => x.Distributor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = searchColumn switch
            {
                "distributorName" => query.Where(x => x.Distributor != null && x.Distributor.Name.Contains(searchText)),
                "paymentType" => query.Where(x => x.PaymentType.Contains(searchText)),
                "invoiceNumber" => query.Where(x => x.InvoiceNumber.Contains(searchText)),
                // No column specified - the Pharmacy Expenses grid uses a
                // single plain search box, not a column picker, so match any
                // of the fields a user would plausibly be searching by.
                _ => query.Where(x =>
                    x.InvoiceNumber.Contains(searchText)
                    || x.PaymentType.Contains(searchText)
                    || (x.Distributor != null && x.Distributor.Name.Contains(searchText)))
            };
        }

        var descending = sortDirection == "desc";
        query = sortBy switch
        {
            "invoiceNumber" => descending ? query.OrderByDescending(x => x.InvoiceNumber) : query.OrderBy(x => x.InvoiceNumber),
            "invoiceDate" => descending ? query.OrderByDescending(x => x.InvoiceDate) : query.OrderBy(x => x.InvoiceDate),
            "distributorName" => descending ? query.OrderByDescending(x => x.Distributor!.Name) : query.OrderBy(x => x.Distributor!.Name),
            "paymentType" => descending ? query.OrderByDescending(x => x.PaymentType) : query.OrderBy(x => x.PaymentType),
            "totalAmount" => descending ? query.OrderByDescending(x => x.TotalAmount) : query.OrderBy(x => x.TotalAmount),
            _ => query.OrderByDescending(x => x.InvoiceDate)
        };

        var paged = await query.ToPagedResultAsync(page ?? 1, pageSize ?? 10);

        var invoiceIds = paged.Rows.Select(x => x.Id).ToList();
        var payments = await _db.PharmacyPurchasePayments
            .Where(x => invoiceIds.Contains(x.PurchaseInvoiceId))
            .ToListAsync();

        var rows = paged.Rows.Select(x =>
        {
            var paid = payments.Where(p => p.PurchaseInvoiceId == x.Id).Sum(p => p.Amount);
            return new
            {
                x.Id, x.InvoiceNumber, x.InvoiceDate,
                DistributorName = x.Distributor?.Name ?? "",
                x.PaymentType, x.TotalAmount,
                PaidAmount = paid,
                Outstanding = Math.Max(0, x.TotalAmount - paid)
            };
        });

        return Ok(new { rows, totalCount = paged.TotalCount });
    }

    [HttpGet("pharmacy/{invoiceId:int}/payments")]
    public async Task<IActionResult> PharmacyPayments(int invoiceId)
    {
        var rows = await _db.PharmacyPurchasePayments
            .Where(x => x.PurchaseInvoiceId == invoiceId)
            .OrderByDescending(x => x.PaymentDate)
            .ToListAsync();
        var staff = await StaffMap(rows.Select(x => x.PaidByStaffId));
        return Ok(rows.Select(x => new
        {
            x.Id, x.PurchaseInvoiceId, x.Amount, x.PaymentDate, x.PaymentMode,
            x.ReferenceNumber, x.Notes, x.PaidByStaffId,
            PaidByStaffName = x.PaidByStaffId.HasValue && staff.TryGetValue(x.PaidByStaffId.Value, out var n) ? n : "",
            x.CreatedAtUtc
        }));
    }

    [HttpPost("pharmacy/payment")]
    public async Task<IActionResult> PharmacyPayment(PharmacyPurchasePaymentRequest request)
    {
        var invoice = await _db.PharmacyPurchaseInvoices.FindAsync(request.PurchaseInvoiceId);
        if (invoice == null || invoice.IsDeleted) return BadRequest(new { message = "Purchase invoice not found." });
        if (!await ValidStaff(request.PaidByStaffId)) return BadRequest(new { message = "Please select a valid staff member who paid." });
        var alreadyPaid = await _db.PharmacyPurchasePayments
            .Where(x => x.PurchaseInvoiceId == request.PurchaseInvoiceId)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;
        var outstanding = Math.Max(0, invoice.TotalAmount - alreadyPaid);
        if (request.Amount <= 0 || request.Amount > outstanding)
            return BadRequest(new { message = $"Payment must be greater than zero and cannot exceed outstanding amount ₹{outstanding:0.00}." });

        var row = new PharmacyPurchasePayment
        {
            PurchaseInvoiceId = request.PurchaseInvoiceId,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate.Date,
            PaymentMode = request.PaymentMode,
            PaidByStaffId = request.PaidByStaffId,
            ReferenceNumber = request.ReferenceNumber?.Trim(),
            Notes = request.Notes?.Trim()
        };
        _db.PharmacyPurchasePayments.Add(row);
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpGet("lab")]
    public async Task<IActionResult> Lab(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] string? searchColumn,
        [FromQuery] string? searchText)
    {
        var query = _db.LabPurchaseExpenses.AsQueryable();
        if (from.HasValue) query = query.Where(x => x.ExpenseDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.ExpenseDate < to.Value.Date.AddDays(1));

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = searchColumn switch
            {
                "invoiceNumber" => query.Where(x => x.InvoiceNumber != null && x.InvoiceNumber.Contains(searchText)),
                "description" => query.Where(x => x.Description.Contains(searchText)),
                "vendorName" => query.Where(x => x.VendorName != null && x.VendorName.Contains(searchText)),
                // No column specified - the Lab Expenses grid uses a single
                // plain search box, not a column picker, so match any of the
                // fields a user would plausibly be searching by.
                _ => query.Where(x =>
                    (x.VendorName != null && x.VendorName.Contains(searchText))
                    || (x.InvoiceNumber != null && x.InvoiceNumber.Contains(searchText))
                    || x.Description.Contains(searchText))
            };
        }

        // PaidByStaffName isn't sorted here - like Patients'/Bills' own
        // dictionary-resolved display fields (doctorName, referrerName, ...)
        // it's only known after the staff lookup below, not a real column on
        // LabPurchaseExpense. "outstanding" (Amount - PaidAmount) is plain
        // arithmetic over two real columns, so it still translates to a
        // normal SQL ORDER BY.
        var descending = sortDirection == "desc";
        query = sortBy switch
        {
            "vendorName" => descending ? query.OrderByDescending(x => x.VendorName) : query.OrderBy(x => x.VendorName),
            "invoiceNumber" => descending ? query.OrderByDescending(x => x.InvoiceNumber) : query.OrderBy(x => x.InvoiceNumber),
            "description" => descending ? query.OrderByDescending(x => x.Description) : query.OrderBy(x => x.Description),
            "amount" => descending ? query.OrderByDescending(x => x.Amount) : query.OrderBy(x => x.Amount),
            "paidAmount" => descending ? query.OrderByDescending(x => x.PaidAmount) : query.OrderBy(x => x.PaidAmount),
            "outstanding" => descending ? query.OrderByDescending(x => x.Amount - x.PaidAmount) : query.OrderBy(x => x.Amount - x.PaidAmount),
            _ => query.OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.Id)
        };

        // The "Total Lab Purchases" footer box sums every row matching the
        // current filters/search, not just the current page - computed from
        // the same filtered query before paging is applied.
        var totalAmount = await query.SumAsync(x => (decimal?)x.Amount) ?? 0;

        // Paging is opt-in via `page` - this endpoint is only called by the
        // Lab Expenses grid today, but kept opt-in to match the
        // Patients/Bills pattern in case something else ever reads it as a
        // full-array source.
        List<LabPurchaseExpense> rows;
        int totalCount;
        if (page.HasValue)
        {
            var paged = await query.ToPagedResultAsync(page.Value, pageSize ?? 10);
            rows = paged.Rows;
            totalCount = paged.TotalCount;
        }
        else
        {
            rows = await query.ToListAsync();
            totalCount = rows.Count;
        }

        var staff = await StaffMap(rows.Select(x => x.PaidByStaffId));
        var shaped = rows.Select(x => new
        {
            x.Id, x.ExpenseDate, x.VendorName, x.InvoiceNumber, x.Description, x.Amount, x.PaidAmount,
            x.PaymentDate, x.PaymentMode, x.ReferenceNumber, x.Notes, x.PaidByStaffId,
            PaidByStaffName = x.PaidByStaffId.HasValue && staff.TryGetValue(x.PaidByStaffId.Value, out var n) ? n : "",
            x.CreatedAtUtc
        }).ToList();

        if (page.HasValue)
        {
            return Ok(new { rows = shaped, totalCount, totalAmount });
        }

        return Ok(shaped);
    }

    [HttpPost("lab")]
    public async Task<IActionResult> AddLab(LabPurchaseExpenseRequest request)
    {
        var validation = await ValidateLab(request);
        if (validation != null) return BadRequest(new { message = validation });
        var row = new LabPurchaseExpense
        {
            ExpenseDate = request.ExpenseDate.Date,
            VendorName = request.VendorName?.Trim(),
            InvoiceNumber = request.InvoiceNumber?.Trim(),
            Description = request.Description.Trim(),
            Amount = request.Amount,
            PaidAmount = request.PaidAmount,
            PaymentDate = request.PaymentDate,
            PaymentMode = request.PaymentMode?.Trim(),
            PaidByStaffId = request.PaidByStaffId,
            ReferenceNumber = request.ReferenceNumber?.Trim(),
            Notes = request.Notes?.Trim()
        };
        _db.LabPurchaseExpenses.Add(row);
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpPut("lab/{id:int}")]
    public async Task<IActionResult> EditLab(int id, LabPurchaseExpenseRequest request)
    {
        var row = await _db.LabPurchaseExpenses.FindAsync(id);
        if (row == null) return NotFound();
        var validation = await ValidateLab(request);
        if (validation != null) return BadRequest(new { message = validation });
        row.ExpenseDate = request.ExpenseDate.Date;
        row.VendorName = request.VendorName?.Trim();
        row.InvoiceNumber = request.InvoiceNumber?.Trim();
        row.Description = request.Description.Trim();
        row.Amount = request.Amount;
        row.PaidAmount = request.PaidAmount;
        row.PaymentDate = request.PaymentDate;
        row.PaymentMode = request.PaymentMode?.Trim();
        row.PaidByStaffId = request.PaidByStaffId;
        row.ReferenceNumber = request.ReferenceNumber?.Trim();
        row.Notes = request.Notes?.Trim();
        await _db.SaveChangesAsync();
        return Ok(row);
    }

    [HttpDelete("lab/{id:int}")]
    public async Task<IActionResult> DeleteLab(int id)
    {
        var row = await _db.LabPurchaseExpenses.FindAsync(id);
        if (row == null) return NotFound();
        _db.LabPurchaseExpenses.Remove(row);
        await _db.SaveChangesAsync();
        return Ok();
    }

    private async Task<string?> ValidateLab(LabPurchaseExpenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description) || request.Amount <= 0) return "Description and amount are required.";
        if (request.PaidAmount < 0 || request.PaidAmount > request.Amount) return "Paid amount cannot exceed purchase amount.";
        if (!await ValidStaff(request.PaidByStaffId)) return "Please select a valid staff member who paid.";
        return null;
    }

    private async Task<bool> ValidStaff(int? id)
    {
        return !id.HasValue || await _db.StaffMembers.AnyAsync(x => x.Id == id.Value && x.IsActive);
    }

    private async Task<Dictionary<int,string>> StaffMap(IEnumerable<int?> ids)
    {
        var list = ids.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        return await _db.StaffMembers.Where(x => list.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
    }
}
