using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KrishavERP.Api.Modules.Billing;

namespace KrishavERP.Api.Modules.Pharmacy.Controllers;

[Authorize]
[ApiController]
[Route("api/pharmacy/returns")]
public class PharmacyReturnsController : ControllerBase
{
    private static readonly string[] AllowedRefundModes = { "Cash", "UPI", "Bank Transfer" };

    private readonly AppDbContext _db;

    public PharmacyReturnsController(AppDbContext db)
    {
        _db = db;
    }

    // What's still returnable on a given sale - the original Quantity on each
    // line minus whatever's already been returned against it across every
    // past PharmacyReturn, so the return form can cap input and the user
    // can't return the same tablet twice.
    [HttpGet("returnable/{saleId:int}")]
    public async Task<IActionResult> GetReturnable(int saleId)
    {
        var sale = await _db.PharmacySales.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == saleId);
        if (sale == null) return NotFound();
        return await BuildReturnable(sale);
    }

    // The Pharmacy Bills screen only has the Bill, not the PharmacySale id
    // underneath it (they're 1:1, created together in PharmacySalesController.
    // Create) - this looks the sale up by its bill instead of making the
    // frontend track both ids.
    [HttpGet("returnable-by-bill/{billId:int}")]
    public async Task<IActionResult> GetReturnableByBill(int billId)
    {
        var sale = await _db.PharmacySales.Include(x => x.Items).FirstOrDefaultAsync(x => x.BillId == billId);
        if (sale == null) return NotFound(new { message = "No pharmacy sale is linked to this bill." });
        return await BuildReturnable(sale);
    }

    private async Task<IActionResult> BuildReturnable(PharmacySale sale)
    {

        var itemIds = sale.Items.Select(x => x.Id).ToList();
        var alreadyReturned = await _db.PharmacyReturnItems
            .Where(x => itemIds.Contains(x.PharmacySaleItemId))
            .GroupBy(x => x.PharmacySaleItemId)
            .Select(g => new { PharmacySaleItemId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.PharmacySaleItemId, x => x.Quantity);

        var items = sale.Items.Select(x => new
        {
            x.Id,
            x.ProductName,
            x.BatchNo,
            x.UnitType,
            x.Quantity,
            x.UnitPrice,
            x.TotalAmount,
            AlreadyReturned = alreadyReturned.TryGetValue(x.Id, out var qty) ? qty : 0,
            MaxReturnable = x.Quantity - (alreadyReturned.TryGetValue(x.Id, out var returnedQty) ? returnedQty : 0)
        }).ToList();

        return Ok(new { sale.Id, sale.SaleNumber, sale.BillId, sale.SaleDateUtc, Items = items });
    }

    [HttpGet]
    public async Task<IActionResult> GetReturns([FromQuery] int? billId, [FromQuery] int? saleId)
    {
        var query = _db.PharmacyReturns.Include(x => x.Items).AsQueryable();
        if (billId.HasValue) query = query.Where(x => x.BillId == billId.Value);
        if (saleId.HasValue) query = query.Where(x => x.PharmacySaleId == saleId.Value);
        var returns = await query.OrderByDescending(x => x.Id).ToListAsync();
        return Ok(returns);
    }

    [HttpPost]
    public async Task<IActionResult> Create(PharmacyReturnRequest request)
    {
        if (request.Items.Count == 0)
            return BadRequest(new { message = "Add at least one item to return." });

        var sale = await _db.PharmacySales.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == request.PharmacySaleId);
        if (sale == null) return BadRequest(new { message = "Sale not found." });
        if (!sale.StockDeducted)
            return BadRequest(new { message = "This purchase hasn't been completed yet - nothing has been dispensed to return." });

        var bill = await _db.Bills.FirstOrDefaultAsync(x => x.Id == sale.BillId);
        if (bill == null || bill.Status == "Deleted") return BadRequest(new { message = "Bill for this sale was not found." });

        var itemIds = sale.Items.Select(x => x.Id).ToList();
        var alreadyReturned = await _db.PharmacyReturnItems
            .Where(x => itemIds.Contains(x.PharmacySaleItemId))
            .GroupBy(x => x.PharmacySaleItemId)
            .Select(g => new { PharmacySaleItemId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.PharmacySaleItemId, x => x.Quantity);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var pharmacyReturn = new PharmacyReturn
        {
            PharmacySaleId = sale.Id,
            BillId = bill.Id,
            Reference = request.Reference
        };

        decimal totalReturn = 0;
        decimal totalGross = 0;

        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
                return BadRequest(new { message = "Return quantity must be greater than zero." });

            var saleItem = sale.Items.FirstOrDefault(x => x.Id == line.PharmacySaleItemId);
            if (saleItem == null)
                return BadRequest(new { message = "One of the selected items doesn't belong to this sale." });

            var returnedSoFar = alreadyReturned.TryGetValue(saleItem.Id, out var qty) ? qty : 0;
            var maxReturnable = saleItem.Quantity - returnedSoFar;
            if (line.Quantity > maxReturnable)
                return BadRequest(new { message = $"Only {maxReturnable} {saleItem.UnitType}(s) of {saleItem.ProductName} can still be returned." });

            var stock = await _db.PharmacyPurchaseItems.FirstOrDefaultAsync(x => x.Id == saleItem.PurchaseItemId);
            if (stock == null)
                return BadRequest(new { message = $"Stock batch for {saleItem.ProductName} was not found." });

            // Same ratio of base units per sold unit the original sale used
            // (QuantityInTablets / Quantity) - restocking the exact amount
            // that was deducted, in reverse, rather than recomputing pack/
            // loose-unit conversion independently.
            var unitsPerSoldQuantity = saleItem.QuantityInTablets / saleItem.Quantity;
            var restockUnits = unitsPerSoldQuantity * line.Quantity;

            // Refund at the same net-of-discount rate per unit the patient
            // actually paid, so a returned item of a discounted sale refunds
            // the discounted price, not the full MRP.
            var effectiveUnitPrice = saleItem.TotalAmount / saleItem.Quantity;
            var lineTotal = Math.Round(effectiveUnitPrice * line.Quantity, 2);
            var lineGross = Math.Round(saleItem.UnitPrice * line.Quantity, 2);

            stock.RemainingTablets += restockUnits;
            totalReturn += lineTotal;
            totalGross += lineGross;

            pharmacyReturn.Items.Add(new PharmacyReturnItem
            {
                PharmacySaleItemId = saleItem.Id,
                PurchaseItemId = stock.Id,
                ProductName = saleItem.ProductName,
                BatchNo = saleItem.BatchNo,
                UnitType = saleItem.UnitType,
                Quantity = line.Quantity,
                QuantityInTablets = restockUnits,
                UnitPrice = saleItem.UnitPrice,
                CgstPercent = saleItem.CgstPercent,
                SgstPercent = saleItem.SgstPercent,
                TotalAmount = lineTotal
            });
        }

        totalReturn = Math.Round(totalReturn, 2);
        pharmacyReturn.TotalAmount = totalReturn;

        bill.GrossAmount -= Math.Round(totalGross, 2);
        bill.NetAmount -= totalReturn;

        decimal refundAmount = 0;
        if (bill.NetAmount < bill.PaidAmount)
        {
            refundAmount = bill.PaidAmount - bill.NetAmount;
            if (string.IsNullOrWhiteSpace(request.RefundMode) || !AllowedRefundModes.Contains(request.RefundMode))
                return BadRequest(new { message = "Please select a refund mode (Cash, UPI or Bank Transfer) - the returned amount was already paid." });

            _db.Payments.Add(new Payment
            {
                BillId = bill.Id,
                Amount = -refundAmount,
                Mode = request.RefundMode,
                Reference = "Refund - pharmacy return",
                PaidAtUtc = DateTime.Now
            });
            bill.PaidAmount -= refundAmount;
            pharmacyReturn.RefundedAmount = refundAmount;
            pharmacyReturn.RefundMode = request.RefundMode;
        }

        bill.Status = bill.PaidAmount >= bill.NetAmount ? "Paid" : bill.PaidAmount > 0 ? "Partially Paid" : "Unpaid";

        _db.PharmacyReturns.Add(pharmacyReturn);
        await _db.SaveChangesAsync();

        pharmacyReturn.ReturnNumber = $"RET-{DateTime.Now:yyyyMMdd}-{pharmacyReturn.Id:000000}";
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            pharmacyReturn.Id,
            pharmacyReturn.ReturnNumber,
            pharmacyReturn.TotalAmount,
            refundAmount,
            bill
        });
    }
}
