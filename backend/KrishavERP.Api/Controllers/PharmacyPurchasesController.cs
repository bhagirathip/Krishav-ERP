using KrishavERP.Api;
using ExcelDataReader;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/pharmacy/purchases")]
public class PharmacyPurchasesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public PharmacyPurchasesController(
        AppDbContext db,
        IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var rows = await _db.PharmacyPurchaseInvoices
            .Where(x => !x.IsDeleted)
            .Include(x => x.Distributor)
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.InvoiceNumber,
                x.InvoiceDate,
                x.PaymentType,
                x.DistributorId,
                DistributorName = x.Distributor != null
                    ? x.Distributor.Name
                    : "",
                x.DistributorDocumentName,
                x.DistributorDocumentPath,
                x.TotalQuantity,
                x.TotalDiscount,
                x.TotalTaxableAmount,
                x.TotalCgst,
                x.TotalSgst,
                x.PreRoundTotalAmount,
                x.RoundOffAmount,
                x.TotalAmount
            })
            .ToListAsync();

        return Ok(rows);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var invoice = await _db.PharmacyPurchaseInvoices
            .Where(x => x.Id == id && !x.IsDeleted)
            .Include(x => x.Distributor)
            .Include(x => x.Items)
            .ThenInclude(x => x.MedicineType)
            .FirstOrDefaultAsync();

        if (invoice == null)
        {
            return NotFound(new { message = "Purchase invoice not found." });
        }

        return Ok(invoice);
    }

    [HttpPost("import-file")]
    public async Task<IActionResult> ImportFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Please select an Excel or CSV invoice file." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".csv" and not ".xlsx" and not ".xls")
        {
            return BadRequest(new { message = "Only CSV, XLSX and XLS invoice files are supported for auto import." });
        }

        try
        {
            await using var memory = new MemoryStream();
            await file.CopyToAsync(memory);
            memory.Position = 0;

            using IExcelDataReader reader = extension == ".csv"
                ? ExcelReaderFactory.CreateCsvReader(memory)
                : ExcelReaderFactory.CreateReader(memory);

            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            });

            if (dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count == 0)
            {
                return BadRequest(new { message = "The uploaded invoice file does not contain medicine rows." });
            }

            var table = dataSet.Tables[0];
            var rows = new List<Dictionary<string, string>>(table.Rows.Count);

            foreach (DataRow row in table.Rows)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (DataColumn column in table.Columns)
                {
                    var key = NormalizeHeader(column.ColumnName);
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    values[key] = Convert.ToString(row[column], CultureInfo.InvariantCulture)?.Trim() ?? "";
                }
                if (values.Values.Any(x => !string.IsNullOrWhiteSpace(x))) rows.Add(values);
            }

            if (rows.Count == 0)
            {
                return BadRequest(new { message = "No invoice rows could be read from the file." });
            }

            var first = rows[0];
            var distributorName = Get(first, "compname", "distributor", "distributorname", "supplier", "suppliername");
            var distributorId = string.IsNullOrWhiteSpace(distributorName)
                ? (int?)null
                : await _db.PharmacyDistributors
                    .Where(x => x.IsActive && x.Name == distributorName)
                    .Select(x => (int?)x.Id)
                    .FirstOrDefaultAsync();

            var medicineTypes = await _db.PharmacyMedicineTypes
                .Where(x => x.IsActive)
                .ToListAsync();

            int? TypeId(string name) => medicineTypes
                .FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Id;

            var importedItems = new List<object>();
            var slNo = 1;

            foreach (var row in rows)
            {
                var product = Get(row, "pname", "productname", "product", "medicine", "medicinename");
                if (string.IsNullOrWhiteSpace(product)) continue;

                var packing = Get(row, "ppack", "packing", "pack", "packsize");
                var typeName = InferMedicineType(product, packing);
                var unitsPerPack = InferUnitsPerPack(typeName, packing);
                var quantity = ToInt(Get(row, "qty", "quantity"), 0);
                var bonus = ToInt(Get(row, "free", "bonus", "freeqty"), 0);
                var rate = ToDecimal(Get(row, "rate", "prate", "purchaserate"));
                var baseAmount = rate * quantity;
                var discountAmount = ToDecimal(Get(row, "disamnt", "discountamount", "discount"));
                var discountPercent = ToDecimal(Get(row, "disrt", "discountrate", "discountpercent"));
                var totalGstRate = ToDecimal(Get(row, "vatrt", "gstrate", "gstpercent", "taxrate"));
                var productTotal = ToDecimal(Get(row, "prdamt", "productamount", "linetotal", "total"));

                // Many distributor exports contain a nominal discount rate that is not
                // actually applied on every row. When a final product amount is present,
                // derive the effective discount from that amount so the imported invoice
                // reproduces the distributor invoice total exactly.
                if (productTotal > 0 && baseAmount > 0)
                {
                    var taxableFromTotal = totalGstRate > 0
                        ? productTotal / (1m + totalGstRate / 100m)
                        : productTotal;
                    discountAmount = Math.Max(0m, Math.Round(baseAmount - taxableFromTotal, 2));
                }
                else if (discountAmount <= 0 && discountPercent > 0)
                {
                    discountAmount = Math.Round(baseAmount * discountPercent / 100m, 2);
                }

                var cgst = totalGstRate > 0 ? totalGstRate / 2m : 0m;
                var sgst = totalGstRate > 0 ? totalGstRate / 2m : 0m;

                importedItems.Add(new
                {
                    Id = (int?)null,
                    SlNo = slNo++,
                    MedicineTypeId = TypeId(typeName),
                    MedicineTypeName = typeName,
                    Manufacturer = Get(row, "mfgname", "manufacturer", "mf", "mfg"),
                    Hsn = Get(row, "hsncode", "hsn", "hsncode"),
                    ProductName = product,
                    Packing = packing,
                    BatchNo = Get(row, "batchno", "batch", "batchnumber"),
                    ExpiryDate = ParseExpiry(Get(row, "expmnth", "expiry", "expirydate", "expiredate")),
                    Mrp = ToDecimal(Get(row, "mrp")),
                    Rate = rate,
                    Quantity = quantity,
                    Bonus = bonus,
                    DiscountAmount = discountAmount,
                    CgstPercent = cgst,
                    SgstPercent = sgst,
                    TabletsPerStrip = unitsPerPack,
                    IsReturnableOnExpiry = true
                });
            }

            if (importedItems.Count == 0)
            {
                return BadRequest(new { message = "No medicine rows were recognized. Check the invoice column headings." });
            }

            var invoiceDateText = Get(first, "invdt", "invoicedate", "date");
            var invoiceDate = ParseDate(invoiceDateText) ?? DateTime.Today;
            var invoiceAmount = ToDecimal(Get(first, "invamt", "invoiceamount", "totalamount"));

            return Ok(new
            {
                InvoiceNumber = Get(first, "invno", "invoicenumber", "invoice", "billno"),
                InvoiceDate = invoiceDate,
                ImportedInvoiceAmount = invoiceAmount,
                DistributorName = distributorName,
                DistributorId = distributorId,
                Items = importedItems,
                Message = distributorId.HasValue
                    ? $"Imported {importedItems.Count} medicine rows."
                    : $"Imported {importedItems.Count} medicine rows. Distributor '{distributorName}' was not matched; please select/create it."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = "Unable to read the invoice file. Please verify the CSV/Excel format.",
                detail = ex.Message
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        PharmacyPurchaseInvoiceRequest request)
    {
        var validation = await ValidateRequest(request);

        if (validation != null)
        {
            return BadRequest(new { message = validation });
        }

        var invoiceNumber = request.InvoiceNumber.Trim();

        var duplicate = await _db.PharmacyPurchaseInvoices.AnyAsync(x =>
            !x.IsDeleted &&
            x.DistributorId == request.DistributorId &&
            x.InvoiceNumber == invoiceNumber);

        if (duplicate)
        {
            return Conflict(new
            {
                message =
                    "This distributor already has an invoice with the same invoice number."
            });
        }

        var invoice = new PharmacyPurchaseInvoice
        {
            DistributorId = request.DistributorId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = request.InvoiceDate,
            PaymentType = request.PaymentType
        };

        foreach (var requestItem in request.Items)
        {
            invoice.Items.Add(CreatePurchaseItem(requestItem));
        }

        RecalculateInvoice(invoice);

        _db.PharmacyPurchaseInvoices.Add(invoice);
        await _db.SaveChangesAsync();

        return Ok(new { InvoiceId = invoice.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(
        int id,
        PharmacyPurchaseInvoiceRequest request)
    {
        var validation = await ValidateRequest(request);

        if (validation != null)
        {
            return BadRequest(new { message = validation });
        }

        var invoice = await _db.PharmacyPurchaseInvoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                !x.IsDeleted);

        if (invoice == null)
        {
            return NotFound(new { message = "Purchase invoice not found." });
        }

        var invoiceNumber = request.InvoiceNumber.Trim();

        var duplicate = await _db.PharmacyPurchaseInvoices.AnyAsync(x =>
            x.Id != id &&
            !x.IsDeleted &&
            x.DistributorId == request.DistributorId &&
            x.InvoiceNumber == invoiceNumber);

        if (duplicate)
        {
            return Conflict(new
            {
                message =
                    "This distributor already has an invoice with the same invoice number."
            });
        }

        invoice.DistributorId = request.DistributorId;
        invoice.InvoiceNumber = invoiceNumber;
        invoice.InvoiceDate = request.InvoiceDate;
        invoice.PaymentType = request.PaymentType;

        var incomingIds = request.Items
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        var removedItems = invoice.Items
            .Where(x => !incomingIds.Contains(x.Id))
            .ToList();

        foreach (var existing in removedItems)
        {
            var originalTablets =
                (existing.Quantity + existing.Bonus) *
                Math.Max(existing.TabletsPerStrip, 1);

            var soldTablets =
                originalTablets - existing.RemainingTablets;

            if (soldTablets > 0)
            {
                return BadRequest(new
                {
                    message =
                        $"Cannot remove {existing.ProductName} / batch {existing.BatchNo} because stock from this line has already been sold."
                });
            }

            _db.PharmacyPurchaseItems.Remove(existing);
        }

        foreach (var requestItem in request.Items)
        {
            if (!requestItem.Id.HasValue)
            {
                invoice.Items.Add(CreatePurchaseItem(requestItem));
                continue;
            }

            var existing = invoice.Items
                .FirstOrDefault(x => x.Id == requestItem.Id.Value);

            if (existing == null)
            {
                return BadRequest(new
                {
                    message =
                        "One of the purchase medicine rows no longer exists."
                });
            }

            var originalTablets =
                (existing.Quantity + existing.Bonus) *
                Math.Max(existing.TabletsPerStrip, 1);

            var soldTablets =
                originalTablets - existing.RemainingTablets;

            var newTablets =
                (requestItem.Quantity + requestItem.Bonus) *
                Math.Max(requestItem.TabletsPerStrip, 1);

            if (newTablets < soldTablets)
            {
                return BadRequest(new
                {
                    message =
                        $"{existing.ProductName} has already sold {soldTablets} tablet(s). New stock cannot be lower than sold quantity."
                });
            }

            ApplyPurchaseItem(
                existing,
                requestItem,
                newTablets - soldTablets);
        }

        RecalculateInvoice(invoice);
        await _db.SaveChangesAsync();

        return Ok(new { InvoiceId = invoice.Id });
    }

    [HttpPost("{id:int}/document")]
    public async Task<IActionResult> UploadDocument(
        int id,
        IFormFile file)
    {
        var invoice = await _db.PharmacyPurchaseInvoices
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                !x.IsDeleted);

        if (invoice == null)
        {
            return NotFound(new { message = "Purchase invoice not found." });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Please select a distributor invoice document."
            });
        }

        var extension =
            Path.GetExtension(file.FileName)
                .ToLowerInvariant();

        var allowedExtensions = new HashSet<string>
        {
            ".pdf",
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".xls",
            ".xlsx",
            ".csv",
            ".doc",
            ".docx"
        };

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                message =
                    "Supported files: PDF, image, Excel, CSV, Word."
            });
        }

        var webRoot =
            _environment.WebRootPath ??
            Path.Combine(
                _environment.ContentRootPath,
                "wwwroot");

        var folder =
            Path.Combine(
                webRoot,
                "uploads",
                "pharmacy",
                "purchase-invoices");

        Directory.CreateDirectory(folder);

        var storedName =
            $"invoice-{id}-{Guid.NewGuid():N}{extension}";

        var fullPath =
            Path.Combine(
                folder,
                storedName);

        await using (var stream =
            System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        invoice.DistributorDocumentName =
            file.FileName;

        invoice.DistributorDocumentPath =
            $"/uploads/pharmacy/purchase-invoices/{storedName}";

        await _db.SaveChangesAsync();

        return Ok(new
        {
            invoice.DistributorDocumentName,
            invoice.DistributorDocumentPath
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _db.PharmacyPurchaseInvoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                !x.IsDeleted);

        if (invoice == null)
        {
            return NotFound();
        }

        var hasSoldStock =
            invoice.Items.Any(x =>
            {
                var totalTablets =
                    (x.Quantity + x.Bonus) *
                    Math.Max(x.TabletsPerStrip, 1);

                return x.RemainingTablets <
                       totalTablets;
            });

        if (hasSoldStock)
        {
            return BadRequest(new
            {
                message =
                    "This invoice cannot be deleted because medicine from it has already been sold."
            });
        }

        invoice.IsDeleted = true;
        await _db.SaveChangesAsync();

        return Ok();
    }

    private async Task<string?> ValidateRequest(
        PharmacyPurchaseInvoiceRequest request)
    {
        var distributorExists =
            await _db.PharmacyDistributors.AnyAsync(x =>
                x.Id == request.DistributorId &&
                x.IsActive);

        if (!distributorExists)
        {
            return "Please select a valid distributor.";
        }

        if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
        {
            return "Invoice number is required.";
        }

        if (request.InvoiceDate == default)
        {
            return "Invoice date is required.";
        }

        if (request.PaymentType != "Cash" &&
            request.PaymentType != "Credit")
        {
            return "Payment type must be Cash or Credit.";
        }

        if (request.Items.Count == 0)
        {
            return "Add at least one medicine to the invoice.";
        }

        var typeIds = request.Items
            .Select(x => x.MedicineTypeId)
            .Distinct()
            .ToList();

        var typeMap = await _db.PharmacyMedicineTypes
            .Where(x => typeIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        if (typeMap.Count != typeIds.Count)
        {
            return "One or more medicine types are invalid.";
        }

        for (var index = 0;
             index < request.Items.Count;
             index++)
        {
            var item = request.Items[index];
            var typeName = typeMap[item.MedicineTypeId];

            if (string.IsNullOrWhiteSpace(item.ProductName))
            {
                return
                    $"Product name is required for medicine row {index + 1}.";
            }

            if (string.IsNullOrWhiteSpace(item.BatchNo))
            {
                return
                    $"Batch number is required for medicine row {index + 1}.";
            }

            if (item.ExpiryDate == default)
            {
                return
                    $"Expiry date is required for medicine row {index + 1}.";
            }

            if (item.Quantity <= 0)
            {
                return
                    $"Quantity must be greater than zero for medicine row {index + 1}.";
            }

            if (typeName is "Cream" or "Spray")
            {
                item.TabletsPerStrip = 1;
            }

            if (item.TabletsPerStrip <= 0)
            {
                return
                    $"Units per pack must be greater than zero in row {index + 1}.";
            }

            if (item.Rate < 0 ||
                item.Mrp < 0 ||
                item.DiscountAmount < 0 ||
                item.CgstPercent < 0 ||
                item.SgstPercent < 0)
            {
                return
                    $"Invalid rate, tax or discount in medicine row {index + 1}.";
            }

            var baseAmount =
                item.Rate *
                item.Quantity;

            if (item.DiscountAmount > baseAmount)
            {
                return
                    $"Discount cannot exceed Rate × Quantity in medicine row {index + 1}.";
            }
        }

        return null;
    }

    private static PharmacyPurchaseItem CreatePurchaseItem(
        PharmacyPurchaseItemRequest request)
    {
        var item = new PharmacyPurchaseItem();

        var totalTablets =
            (request.Quantity + request.Bonus) *
            Math.Max(request.TabletsPerStrip, 1);

        ApplyPurchaseItem(
            item,
            request,
            totalTablets);

        return item;
    }

    private static void ApplyPurchaseItem(
        PharmacyPurchaseItem item,
        PharmacyPurchaseItemRequest request,
        int remainingTablets)
    {
        var baseAmount =
            request.Rate *
            request.Quantity;

        var discountAmount =
            Math.Min(
                baseAmount,
                request.DiscountAmount);

        var taxableAmount =
            Math.Round(
                baseAmount -
                discountAmount,
                2);

        var cgstAmount =
            Math.Round(
                taxableAmount *
                request.CgstPercent /
                100m,
                2);

        var sgstAmount =
            Math.Round(
                taxableAmount *
                request.SgstPercent /
                100m,
                2);

        var totalAmount =
            Math.Round(
                taxableAmount +
                cgstAmount +
                sgstAmount,
                2);

        item.MedicineTypeId = request.MedicineTypeId;
        item.SlNo = request.SlNo;
        item.Manufacturer =
            request.Manufacturer.Trim();

        item.Hsn =
            request.Hsn.Trim();

        item.ProductName =
            request.ProductName.Trim();

        item.Packing =
            request.Packing.Trim();

        item.BatchNo =
            request.BatchNo.Trim();

        item.ExpiryDate =
            request.ExpiryDate;

        item.Mrp =
            request.Mrp;

        item.Rate =
            request.Rate;

        item.Quantity =
            request.Quantity;

        item.Bonus =
            request.Bonus;

        item.DiscountAmount =
            discountAmount;

        item.TaxableAmount =
            taxableAmount;

        item.CgstPercent =
            request.CgstPercent;

        item.CgstAmount =
            cgstAmount;

        item.SgstPercent =
            request.SgstPercent;

        item.SgstAmount =
            sgstAmount;

        item.TotalAmount =
            totalAmount;

        item.TabletsPerStrip =
            Math.Max(
                request.TabletsPerStrip,
                1);

        item.RemainingTablets =
            remainingTablets;

        item.IsReturnableOnExpiry =
            request.IsReturnableOnExpiry;
    }

    private static void RecalculateInvoice(
        PharmacyPurchaseInvoice invoice)
    {
        invoice.TotalQuantity =
            invoice.Items.Sum(x => x.Quantity);

        invoice.TotalDiscount =
            invoice.Items.Sum(x => x.DiscountAmount);

        invoice.TotalTaxableAmount =
            invoice.Items.Sum(x => x.TaxableAmount);

        invoice.TotalCgst =
            invoice.Items.Sum(x => x.CgstAmount);

        invoice.TotalSgst =
            invoice.Items.Sum(x => x.SgstAmount);

        invoice.PreRoundTotalAmount =
            Math.Round(invoice.Items.Sum(x => x.TotalAmount), 2);

        var floor = Math.Floor(invoice.PreRoundTotalAmount);
        var fraction = invoice.PreRoundTotalAmount - floor;
        invoice.TotalAmount = fraction > 0.50m
            ? floor + 1m
            : floor;
        invoice.RoundOffAmount =
            invoice.TotalAmount - invoice.PreRoundTotalAmount;
    }

    private static string NormalizeHeader(string? value)
    {
        return Regex.Replace((value ?? "").Trim().ToLowerInvariant(), "[^a-z0-9]", "");
    }

    private static string Get(
        IReadOnlyDictionary<string, string> row,
        params string[] names)
    {
        foreach (var name in names)
        {
            var key = NormalizeHeader(name);
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return "";
    }

    private static decimal ToDecimal(string? value)
    {
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)) return result;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result)) return result;
        return 0m;
    }

    private static int ToInt(string? value, int fallback)
    {
        if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)) return result;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue)) return (int)decimalValue;
        return fallback;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy" };
        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact)) return exact;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
    }

    private static DateTime ParseExpiry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DateTime.Today;
        var text = value.Trim();
        var formats = new[] { "MM/yy", "M/yy", "MM/yyyy", "M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            if (!text.Contains('-') && text.Count(c => c == '/') == 1)
                return new DateTime(parsed.Year, parsed.Month, DateTime.DaysInMonth(parsed.Year, parsed.Month));
            return parsed.Date;
        }
        if (DateTime.TryParse(text, out parsed)) return parsed.Date;
        return DateTime.Today;
    }

    private static string InferMedicineType(string product, string packing)
    {
        var p = product.ToUpperInvariant();
        var pack = packing.ToUpperInvariant();
        if (p.Contains("CREAM") || p.Contains("CRM")) return "Cream";
        if (p.Contains("SPRAY")) return "Spray";
        if (p.Contains("INJ") || pack.Contains("VIAL") || pack.Contains("VAIL")) return "Injection";
        if (p.Contains("SYP") || p.Contains("SYRUP") || p.Contains("EXPT")) return "Syrup";
        if (p.Contains("POWDER") || p.Contains("POW")) return "Powder";
        if (p.Contains("TAB") || p.Contains("CAP") || Regex.IsMatch(pack, @"^\s*\d+\s*S\s*$")) return "Tablet";
        return "Other";
    }

    private static int InferUnitsPerPack(string typeName, string packing)
    {
        if (typeName is "Cream" or "Spray" or "Syrup" or "Powder" or "Other") return 1;
        var match = Regex.Match(packing ?? "", @"\d+");
        if (match.Success && int.TryParse(match.Value, out var units) && units > 0) return units;
        return 1;
    }

}
