using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

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

    [HttpGet("pharmacy")]
    public async Task<IActionResult> Pharmacy()
    {
        var invoices = await _db.PharmacyPurchaseInvoices
            .Where(x => !x.IsDeleted)
            .Include(x => x.Distributor)
            .OrderByDescending(x => x.InvoiceDate)
            .ToListAsync();
        var payments = await _db.PharmacyPurchasePayments.ToListAsync();
        return Ok(invoices.Select(x =>
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
        }));
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
    public async Task<IActionResult> Lab([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = _db.LabPurchaseExpenses.AsQueryable();
        if (from.HasValue) query = query.Where(x => x.ExpenseDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.ExpenseDate < to.Value.Date.AddDays(1));
        var rows = await query.OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.Id).ToListAsync();
        var staff = await StaffMap(rows.Select(x => x.PaidByStaffId));
        return Ok(rows.Select(x => new
        {
            x.Id, x.ExpenseDate, x.VendorName, x.InvoiceNumber, x.Description, x.Amount, x.PaidAmount,
            x.PaymentDate, x.PaymentMode, x.ReferenceNumber, x.Notes, x.PaidByStaffId,
            PaidByStaffName = x.PaidByStaffId.HasValue && staff.TryGetValue(x.PaidByStaffId.Value, out var n) ? n : "",
            x.CreatedAtUtc
        }));
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
