using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/pharmacy/expiry")]
public class PharmacyExpiryController : ControllerBase
{
    private readonly AppDbContext _db;
    public PharmacyExpiryController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int months = 1)
    {
        months = Math.Clamp(months, 1, 6);
        var cutoff = DateTime.Today.AddMonths(months);

        var rows = await _db.PharmacyPurchaseItems
            .Include(x => x.PurchaseInvoice)
            .ThenInclude(x => x!.Distributor)
            .Include(x => x.MedicineType)
            .Where(x =>
                x.RemainingTablets > 0 &&
                x.ExpiryDate <= cutoff &&
                x.PurchaseInvoice != null &&
                !x.PurchaseInvoice.IsDeleted)
            .OrderBy(x => x.ExpiryDate)
            .ThenBy(x => x.ProductName)
            .ToListAsync();

        return Ok(rows.Select(x => new
        {
            x.Id,
            x.ProductName,
            x.BatchNo,
            x.Packing,
            x.ExpiryDate,
            x.Rate,
            x.Mrp,
            x.TabletsPerStrip,
            x.RemainingTablets,
            x.IsReturnableOnExpiry,
            MedicineType = x.MedicineType?.Name ?? "Other",
            InvoiceNumber = x.PurchaseInvoice?.InvoiceNumber ?? "",
            DistributorName = x.PurchaseInvoice?.Distributor?.Name ?? "",
            RemainingPacks = x.RemainingTablets / Math.Max(x.TabletsPerStrip, 1),
            LooseUnits = x.RemainingTablets % Math.Max(x.TabletsPerStrip, 1),
            Status = x.ExpiryDate.Date < DateTime.Today ? "Expired" : "Expiring Soon"
        }));
    }

    [HttpPost("action")]
    public async Task<IActionResult> Action(PharmacyExpiryActionRequest request)
    {
        if (request.ActionType is not "Return" and not "Discard")
            return BadRequest(new { message = "Action must be Return or Discard." });
        if (request.Quantity <= 0)
            return BadRequest(new { message = "Quantity must be greater than zero." });

        await using var tx = await _db.Database.BeginTransactionAsync();

        var item = await _db.PharmacyPurchaseItems
            .Include(x => x.MedicineType)
            .FirstOrDefaultAsync(x => x.Id == request.PurchaseItemId);

        if (item == null) return NotFound(new { message = "Medicine batch not found." });
        if (request.ActionType == "Return" && !item.IsReturnableOnExpiry)
            return BadRequest(new { message = "This medicine is marked as non-returnable on expiry." });

        var unitsPerPack = Math.Max(item.TabletsPerStrip, 1);
        var baseUnits = request.UnitType == "Pack"
            ? request.Quantity * unitsPerPack
            : request.Quantity;

        if (baseUnits > item.RemainingTablets)
            return BadRequest(new { message = $"Only {item.RemainingTablets} base unit(s) are available." });

        var unitCost = item.Rate / unitsPerPack;
        var amount = Math.Round(unitCost * baseUnits, 2);

        item.RemainingTablets -= baseUnits;
        var action = new PharmacyExpiryAction
        {
            PurchaseItemId = item.Id,
            ActionType = request.ActionType,
            QuantityInBaseUnits = baseUnits,
            UnitType = request.UnitType,
            Amount = amount,
            Notes = request.Notes?.Trim()
        };
        _db.PharmacyExpiryActions.Add(action);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { action.Id, action.ActionType, action.Amount, RemainingTablets = item.RemainingTablets });
    }

    [HttpGet("report")]
    public async Task<IActionResult> Report([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = (from ?? DateTime.Today.AddMonths(-1)).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);
        var actions = await _db.PharmacyExpiryActions
            .Where(x => x.ActionDateUtc >= start && x.ActionDateUtc < end)
            .OrderByDescending(x => x.ActionDateUtc)
            .ToListAsync();

        var itemIds = actions.Select(x => x.PurchaseItemId).Distinct().ToList();
        var items = await _db.PharmacyPurchaseItems
            .Where(x => itemIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        return Ok(new
        {
            ReturnedAmount = actions.Where(x => x.ActionType == "Return").Sum(x => x.Amount),
            LossAmount = actions.Where(x => x.ActionType == "Discard").Sum(x => x.Amount),
            ReturnCount = actions.Count(x => x.ActionType == "Return"),
            DiscardCount = actions.Count(x => x.ActionType == "Discard"),
            Rows = actions.Select(x => new
            {
                x.Id,
                x.ActionDateUtc,
                x.ActionType,
                x.QuantityInBaseUnits,
                x.UnitType,
                x.Amount,
                x.Notes,
                ProductName = items.TryGetValue(x.PurchaseItemId, out var item) ? item.ProductName : "",
                BatchNo = items.TryGetValue(x.PurchaseItemId, out var batch) ? batch.BatchNo : ""
            })
        });
    }
}
