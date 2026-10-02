using KrishavERP.Api;
using KrishavERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/pharmacy/sales")]
public class PharmacySalesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DiscountService _discounts;
    private readonly IWebHostEnvironment _env;

    public PharmacySalesController(AppDbContext db, DiscountService discounts, IWebHostEnvironment env)
    {
        _db = db;
        _discounts = discounts;
        _env = env;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        var query = _db.PharmacyPurchaseItems
            .Include(x => x.MedicineType)
            .Include(x => x.PurchaseInvoice)
            .ThenInclude(x => x!.Distributor)
            .Where(x =>
                x.RemainingTablets > 0 &&
                x.ExpiryDate.Date >= DateTime.Today &&
                x.PurchaseInvoice != null &&
                !x.PurchaseInvoice.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();
            query = query.Where(x =>
                x.Manufacturer.Contains(text) ||
                x.Hsn.Contains(text) ||
                x.ProductName.Contains(text) ||
                x.BatchNo.Contains(text) ||
                (x.MedicineType != null && x.MedicineType.Name.Contains(text)) ||
                (x.PurchaseInvoice != null && x.PurchaseInvoice.InvoiceNumber.Contains(text)));
        }

        // No row cap here - the frontend already paginates this list
        // client-side (useGrid), so capping it server-side just silently
        // hid inventory past the 200th batch instead of paging through it.
        var raw = await query
            .OrderBy(x => x.ProductName)
            .ThenBy(x => x.ExpiryDate)
            .Select(x => new
            {
                x.Id,
                x.Manufacturer,
                x.Hsn,
                x.ProductName,
                x.Packing,
                x.BatchNo,
                x.ExpiryDate,
                x.Mrp,
                x.TabletsPerStrip,
                x.RemainingTablets,
                MedicineType = x.MedicineType != null ? x.MedicineType.Name : "Tablet",
                InvoiceNumber = x.PurchaseInvoice != null ? x.PurchaseInvoice.InvoiceNumber : "",
                DistributorName = x.PurchaseInvoice != null && x.PurchaseInvoice.Distributor != null
                    ? x.PurchaseInvoice.Distributor.Name
                    : ""
            })
            .ToListAsync();

        var rows = raw.Select(x =>
        {
            var unitsPerPack = Math.Max(x.TabletsPerStrip, 1);
            return new
            {
                x.Id,
                x.Manufacturer,
                x.Hsn,
                x.ProductName,
                x.Packing,
                x.BatchNo,
                x.ExpiryDate,
                x.Mrp,
                UnitsPerPack = unitsPerPack,
                x.RemainingTablets,
                x.MedicineType,
                AvailablePacks = x.RemainingTablets / unitsPerPack,
                LooseUnits = x.RemainingTablets % unitsPerPack,
                UnitPrice = Math.Round(x.Mrp / unitsPerPack, 4),
                x.InvoiceNumber,
                x.DistributorName
            };
        });

        return Ok(rows);
    }

    [HttpGet]
    public async Task<IActionResult> GetSales([FromQuery] DateTime? date)
    {
        var start = (date ?? DateTime.Today).Date;
        var end = start.AddDays(1);

        var sales = await _db.PharmacySales
            .Where(x => x.SaleDateUtc >= start && x.SaleDateUtc < end)
            .Include(x => x.Items)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.SaleNumber,
                x.PatientId,
                x.DoctorId,
                x.WalkInPatientName,
                x.WalkInPhone,
                x.BillId,
                x.PaymentMode,
                x.TotalAmount,
                x.SaleDateUtc,
                ItemCount = x.Items.Sum(i => i.Quantity)
            })
            .ToListAsync();

        return Ok(sales);
    }

    [HttpPost]
    public async Task<IActionResult> Create(PharmacySaleRequest request)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "Add at least one medicine to the sale." });
        }

        var allowedModes = new[] { "Cash", "UPI", "Card", "Bank Transfer", "Other" };
        if (!allowedModes.Contains(request.PaymentMode))
        {
            return BadRequest(new { message = "Please select a valid payment mode." });
        }

        if (request.RoundOff < -9 || request.RoundOff > 9)
        {
            return BadRequest(new { message = "Round off must be between -9 and 9." });
        }

        if (request.PatientId.HasValue)
        {
            if (!await _db.Patients.AnyAsync(x => x.Id == request.PatientId.Value && !x.IsDeleted))
            {
                return BadRequest(new { message = "Selected patient was not found." });
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.WalkInPatientName))
            {
                return BadRequest(new { message = "Walk-in patient name is required." });
            }
        }

        if (request.DoctorId.HasValue &&
            !await _db.Doctors.AnyAsync(x =>
                x.Id == request.DoctorId.Value &&
                x.IsActive))
        {
            return BadRequest(new
            {
                message = "Selected doctor was not found."
            });
        }

        if (request.DoctorId.HasValue &&
            !string.IsNullOrWhiteSpace(request.OutsideDoctorName))
        {
            return BadRequest(new
            {
                message = "Select either an existing doctor or enter an outside doctor, not both."
            });
        }

        // Medicine dispensed against an ongoing IPD stay, rather than a
        // walk-in/OPD counter sale - patient owes for it via their IPD
        // account (settled when the stay is billed), not paid up front at
        // the pharmacy counter. Mirrors how LabController.CreateOrder
        // resolves the doctor from the admission when IpdAdmissionId is set.
        IpdAdmission? ipdAdmission = null;
        if (request.IpdAdmissionId.HasValue)
        {
            ipdAdmission = await _db.IpdAdmissions.FindAsync(request.IpdAdmissionId.Value);
            if (ipdAdmission == null || ipdAdmission.Status != "Admitted")
            {
                return BadRequest(new { message = "Selected IPD admission was not found or is no longer active." });
            }

            if (!request.PatientId.HasValue || request.PatientId.Value != ipdAdmission.PatientId)
            {
                return BadRequest(new { message = "Patient does not match the selected IPD admission." });
            }
        }

        var resolvedDoctorId = request.DoctorId ?? ipdAdmission?.DoctorId;

        var linkedPatient = request.PatientId.HasValue
            ? await _db.Patients.FindAsync(request.PatientId.Value)
            : null;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var sale = new PharmacySale
        {
            PatientId = request.PatientId,
            DoctorId = resolvedDoctorId,
            OutsideDoctorName = string.IsNullOrWhiteSpace(request.OutsideDoctorName)
                ? null
                : request.OutsideDoctorName.Trim(),
            WalkInPatientName = request.PatientId.HasValue ? null : request.WalkInPatientName?.Trim(),
            WalkInPhone = request.PatientId.HasValue ? null : request.WalkInPhone?.Trim(),
            PaymentMode = request.PaymentMode,
            SaleDateUtc = DateTime.Now,
            IpdAdmissionId = request.IpdAdmissionId
        };

        var bill = new Bill
        {
            PatientId = request.PatientId,
            DoctorId = resolvedDoctorId,
            ReferrerId = linkedPatient?.ReferrerId,
            BillType = "Pharmacy",
            Status = request.IpdAdmissionId.HasValue ? "Unpaid" : "Paid",
            CreatedAtUtc = DateTime.Now
        };

        decimal grandTotal = 0;

        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
            {
                return BadRequest(new { message = "Sale quantity must be greater than zero." });
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
                return BadRequest(new { message = $"Invalid sale unit for {stock.ProductName}." });
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

            var unitPrice = line.UnitType == packLabel
                ? stock.Mrp
                : Math.Round(stock.Mrp / unitsPerPack, 4);

            var grossAmount = Math.Round(unitPrice * line.Quantity, 2);
            ResolvedDiscount discount;
            try
            {
                discount = await _discounts.ResolveAsync(grossAmount, line.DiscountTypeId, "Individual");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            var discountAmount = discount.Amount;
            var lineTotal = Math.Round(discount.Net, 2);

            grandTotal += lineTotal;
            stock.RemainingTablets -= requiredUnits;

            sale.Items.Add(new PharmacySaleItem
            {
                PurchaseItemId = stock.Id,
                Manufacturer = stock.Manufacturer,
                Hsn = stock.Hsn,
                CgstPercent = stock.CgstPercent,
                SgstPercent = stock.SgstPercent,
                ProductName = stock.ProductName,
                BatchNo = stock.BatchNo,
                Packing = stock.Packing,
                Mrp = stock.Mrp,
                UnitType = line.UnitType,
                Quantity = line.Quantity,
                QuantityInTablets = requiredUnits,
                UnitPrice = unitPrice,
                DiscountTypeId = discount.Id,
                DiscountName = discount.Name,
                DiscountAmount = discountAmount,
                TotalAmount = lineTotal
            });

            bill.Items.Add(new BillItem
            {
                Description = $"{stock.ProductName} - {stock.BatchNo} ({line.UnitType})",
                Quantity = line.Quantity,
                UnitPrice = unitPrice,
                DiscountTypeId = discount.Id,
                DiscountName = discount.Name,
                DiscountMode = discount.Mode,
                DiscountValue = discount.Value,
                DiscountAmount = discountAmount,
                Amount = lineTotal,
                ReferenceType = "PharmacyPurchaseItem",
                ReferenceId = stock.Id
            });
        }

        grandTotal = Math.Round(grandTotal, 2) + request.RoundOff;
        bill.GrossAmount = bill.Items.Sum(x => x.UnitPrice * x.Quantity);
        bill.DiscountAmount = 0;
        bill.DiscountPercent = 0;
        bill.RoundOff = request.RoundOff;
        bill.NetAmount = grandTotal;
        bill.PaidAmount = request.IpdAdmissionId.HasValue ? 0 : grandTotal;

        _db.Bills.Add(bill);
        await _db.SaveChangesAsync();

        bill.BillNumber = $"BILL-{DateTime.Now:yyyyMMdd}-{bill.Id:000000}";

        // Walk-in/OPD pharmacy sales are cash-and-carry, paid in full at the
        // counter. IPD dispensing is on credit against the patient's stay -
        // no payment is collected now, so no Payment row is recorded.
        if (!request.IpdAdmissionId.HasValue)
        {
            _db.Payments.Add(new Payment
            {
                BillId = bill.Id,
                Amount = grandTotal,
                Mode = request.PaymentMode,
                PaidAtUtc = DateTime.Now
            });
        }

        sale.BillId = bill.Id;
        sale.TotalAmount = grandTotal;
        _db.PharmacySales.Add(sale);
        await _db.SaveChangesAsync();

        sale.SaleNumber = $"PH-{DateTime.Now:yyyyMMdd}-{sale.Id:000000}";
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            SaleId = sale.Id,
            sale.SaleNumber,
            BillId = bill.Id,
            bill.BillNumber,
            sale.TotalAmount
        });
    }

    [HttpGet("{id:int}/print")]
    public async Task<IActionResult> Print(int id)
    {
        var sale = await _db.PharmacySales
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (sale == null)
        {
            return NotFound();
        }

        Patient? patient = null;
        Doctor? doctor = null;

        if (sale.PatientId.HasValue)
        {
            patient = await _db.Patients.FindAsync(sale.PatientId.Value);
        }

        if (sale.DoctorId.HasValue)
        {
            doctor = await _db.Doctors.FindAsync(sale.DoctorId.Value);
        }

        var header = AssetVersioning.Stamp(_env, await _db.AppSettings
            .Where(x => x.IsActive && x.Name == "Pharmacy Header")
            .Select(x => x.Value)
            .FirstOrDefaultAsync());

        var gstNumber = await _db.AppSettings
            .Where(x => x.IsActive && x.Name == "Pharmacy GST No")
            .Select(x => x.Value)
            .FirstOrDefaultAsync();

        // MRP is retail-standard tax-inclusive, so the tax breakdown is
        // extracted from each item's existing (already-discounted) total
        // rather than added on top - the patient's total never changes.
        // IGST isn't computed (this hospital bills intra-state only); the
        // field still prints, just always zero.
        decimal taxableTotal = 0, cgstTotal = 0, sgstTotal = 0;
        var itemTax = sale.Items.Select(item =>
        {
            var rate = item.CgstPercent + item.SgstPercent;
            var taxable = rate > 0 ? Math.Round(item.TotalAmount / (1 + rate / 100m), 2) : item.TotalAmount;
            var cgst = Math.Round(taxable * item.CgstPercent / 100m, 2);
            var sgst = Math.Round(taxable * item.SgstPercent / 100m, 2);
            taxableTotal += taxable;
            cgstTotal += cgst;
            sgstTotal += sgst;
            return new { itemId = item.Id, taxable, cgstPercent = item.CgstPercent, cgst, sgstPercent = item.SgstPercent, sgst };
        }).ToList();

        return Ok(new
        {
            sale,
            patient,
            doctor,
            header = header ?? "",
            gstNumber = gstNumber ?? "",
            itemTax,
            gstSummary = new { taxableTotal, cgstTotal, sgstTotal, igstTotal = 0m }
        });
    }

    [HttpGet("day-end")]
    public async Task<IActionResult> DayEnd([FromQuery] DateTime? date)
    {
        var start = (date ?? DateTime.Today).Date;
        var end = start.AddDays(1);

        var sales = await _db.PharmacySales
            .Where(x => x.SaleDateUtc >= start && x.SaleDateUtc < end)
            .Include(x => x.Items)
            .ToListAsync();

        var soldProducts = sales
            .SelectMany(x => x.Items)
            .GroupBy(x => new { x.ProductName, x.BatchNo, x.Packing })
            .Select(g => new
            {
                g.Key.ProductName,
                g.Key.BatchNo,
                g.Key.Packing,
                QuantitySold = g.Sum(x => x.Quantity),
                UnitsSold = g.Sum(x => x.QuantityInTablets),
                Amount = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.UnitsSold)
            .ToList();

        var stock = await _db.PharmacyPurchaseItems
            .Include(x => x.MedicineType)
            .Where(x =>
                x.RemainingTablets > 0 &&
                x.PurchaseInvoice != null &&
                !x.PurchaseInvoice.IsDeleted)
            .OrderBy(x => x.ProductName)
            .ThenBy(x => x.ExpiryDate)
            .Select(x => new
            {
                x.ProductName,
                x.BatchNo,
                x.Packing,
                MedicineType = x.MedicineType != null ? x.MedicineType.Name : "Tablet",
                x.TabletsPerStrip,
                x.RemainingTablets
            })
            .ToListAsync();

        var remaining = stock.Select(x =>
        {
            var unitsPerPack = Math.Max(x.TabletsPerStrip, 1);
            return new
            {
                x.ProductName,
                x.BatchNo,
                x.Packing,
                x.MedicineType,
                RemainingPacks = x.RemainingTablets / unitsPerPack,
                LooseUnits = x.RemainingTablets % unitsPerPack,
                RemainingUnits = x.RemainingTablets
            };
        });

        return Ok(new
        {
            Date = start,
            NumberOfBills = sales.Count,
            TotalUnitsSold = sales.SelectMany(x => x.Items).Sum(x => x.QuantityInTablets),
            TotalSalesAmount = sales.Sum(x => x.TotalAmount),
            SoldProducts = soldProducts,
            RemainingStock = remaining
        });
    }

    // Grouped by (ProductName, Mrp) rather than by individual batch, since a
    // product/price combination is what a stock ledger report cares about -
    // batches are an implementation detail of how purchases/expiry tracking
    // works internally. Purchases/Sales are scoped to the selected date
    // range (or all time, when neither from/to is given); Closing Stock is
    // always the current live balance (PharmacyPurchaseItem.RemainingTablets
    // has no historical snapshot to reconstruct a past closing balance from -
    // see PharmacyPurchasesController/PharmacyExpiryController for where
    // that counter is mutated).
    [HttpGet("stock-report")]
    public async Task<IActionResult> StockReport(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search)
    {
        var purchaseItemsQuery = _db.PharmacyPurchaseItems
            .Include(x => x.MedicineType)
            .Include(x => x.PurchaseInvoice)
            .Where(x => x.PurchaseInvoice != null && !x.PurchaseInvoice.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            purchaseItemsQuery = purchaseItemsQuery.Where(x => x.ProductName.Contains(text));
        }

        var purchaseItems = await purchaseItemsQuery.ToListAsync();

        var purchaseItemGroupKey = purchaseItems.ToDictionary(x => x.Id, x => (x.ProductName, x.Mrp));

        var relevantSaleItems = await _db.PharmacySaleItems
            .Where(x => purchaseItemGroupKey.Keys.Contains(x.PurchaseItemId))
            .ToListAsync();

        var saleIds = relevantSaleItems.Select(x => x.PharmacySaleId).Distinct().ToList();
        var saleDates = await _db.PharmacySales
            .Where(x => saleIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.SaleDateUtc);

        var start = from?.Date;
        var end = to?.Date.AddDays(1);
        bool InRange(DateTime d) => !start.HasValue || (d >= start.Value && d < end!.Value);

        var salesBaseUnitsByGroup = relevantSaleItems
            .Where(x => saleDates.TryGetValue(x.PharmacySaleId, out var d) && InRange(d))
            .GroupBy(x => purchaseItemGroupKey[x.PurchaseItemId])
            .ToDictionary(g => g.Key, g => g.Sum(x => x.QuantityInTablets));

        string PackAndUnitLabel(string? medicineTypeName)
        {
            if (string.Equals(medicineTypeName, "Injection", StringComparison.OrdinalIgnoreCase))
                return "Pack|Vial";
            if (string.Equals(medicineTypeName, "Tablet", StringComparison.OrdinalIgnoreCase) || medicineTypeName == null)
                return "Strip|Tablet";
            return "Unit|Unit";
        }

        var rows = purchaseItems
            .GroupBy(x => (x.ProductName, x.Mrp))
            .Select(g =>
            {
                var sample = g.First();
                var labels = PackAndUnitLabel(sample.MedicineType?.Name).Split('|');
                var packLabel = labels[0];
                var unitLabel = labels[1];
                var unitsPerPack = packLabel == unitLabel ? 1 : Math.Max(sample.TabletsPerStrip, 1);

                var purchaseBaseUnits = g
                    .Where(x => InRange(x.PurchaseInvoice!.InvoiceDate))
                    .Sum(x => (x.Quantity + x.Bonus) * Math.Max(x.TabletsPerStrip, 1));

                var salesBaseUnits = salesBaseUnitsByGroup.TryGetValue(g.Key, out var sold) ? sold : 0;
                var closingBaseUnits = g.Sum(x => x.RemainingTablets);
                var closingStockAmount = Math.Round(closingBaseUnits * (g.Key.Mrp / unitsPerPack), 2);

                string Format(int baseUnits)
                {
                    if (packLabel == unitLabel) return $"{baseUnits} {unitLabel}";
                    var packs = baseUnits / unitsPerPack;
                    var loose = baseUnits % unitsPerPack;
                    return loose == 0 ? $"{packs} {packLabel}" : $"{packs} {packLabel} {loose} {unitLabel}";
                }

                return new
                {
                    ProductName = g.Key.ProductName,
                    Mrp = g.Key.Mrp,
                    PurchasesBaseUnits = purchaseBaseUnits,
                    PurchasesText = Format(purchaseBaseUnits),
                    SalesBaseUnits = salesBaseUnits,
                    SalesText = Format(salesBaseUnits),
                    ClosingStockBaseUnits = closingBaseUnits,
                    ClosingStockText = Format(closingBaseUnits),
                    ClosingStockAmount = closingStockAmount
                };
            })
            .OrderBy(x => x.ProductName)
            .ThenBy(x => x.Mrp)
            .ToList();

        return Ok(new
        {
            rows,
            totalClosingStockAmount = Math.Round(rows.Sum(x => x.ClosingStockAmount), 2)
        });
    }
}
