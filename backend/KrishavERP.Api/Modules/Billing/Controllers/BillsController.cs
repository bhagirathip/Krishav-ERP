using KrishavERP.Api;
using KrishavERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KrishavERP.Api.Modules.Lab;
using KrishavERP.Api.Modules.Lab.Controllers;
using KrishavERP.Api.Modules.Doctors;

namespace KrishavERP.Api.Modules.Billing.Controllers;

[Authorize]
[ApiController]
[Route("api/bills")]
public class BillsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DiscountService _discounts;
    private readonly IWebHostEnvironment _env;

    public BillsController(AppDbContext db, DiscountService discounts, IWebHostEnvironment env)
    {
        _db = db;
        _discounts = discounts;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string status = "Unpaid",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _db.Bills
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Where(x => x.Status != "Deleted");

        query = status == "Paid" ? query.Where(x => x.Status == "Paid")
            : status == "All" ? query
            : query.Where(x => x.Status != "Paid");

        if (from.HasValue) query = query.Where(x => x.BillDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.BillDate < to.Value.Date.AddDays(1));

        var bills = await query.OrderByDescending(x => x.Id).ToListAsync();
        var patientIds = bills.Where(x => x.PatientId.HasValue).Select(x => x.PatientId!.Value).Distinct().ToList();
        var doctorIds = bills.Where(x => x.DoctorId.HasValue).Select(x => x.DoctorId!.Value).Distinct().ToList();
        var referrerIds = bills.Where(x => x.ReferrerId.HasValue).Select(x => x.ReferrerId!.Value).Distinct().ToList();

        var patients = await _db.Patients.Where(x => patientIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var doctors = await _db.Doctors.Where(x => doctorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var referrers = await _db.Referrers.Where(x => referrerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        return Ok(bills.Select(bill => new
        {
            bill.Id,
            bill.BillNumber,
            bill.PatientId,
            bill.DoctorId,
            bill.ReferrerId,
            ReferrerName = bill.ReferrerId.HasValue && referrers.TryGetValue(bill.ReferrerId.Value, out var referrer) ? referrer.Name : "",
            PatientName = bill.PatientId.HasValue && patients.TryGetValue(bill.PatientId.Value, out var patient) ? patient.Name : (bill.WalkInPatientName ?? ""),
            PatientCode = bill.PatientId.HasValue && patients.TryGetValue(bill.PatientId.Value, out var patientCodeValue) ? patientCodeValue.PatientCode : "",
            PatientPhone = bill.PatientId.HasValue && patients.TryGetValue(bill.PatientId.Value, out var patientPhoneValue) ? patientPhoneValue.Phone : "",
            DoctorName = bill.DoctorId.HasValue && doctors.TryGetValue(bill.DoctorId.Value, out var doctor) ? doctor.Name : "",
            bill.BillType,
            bill.GrossAmount,
            bill.DiscountPercent,
            bill.DiscountAmount,
            bill.BulkDiscountTypeId,
            bill.BulkDiscountName,
            bill.RoundOff,
            bill.NetAmount,
            bill.PaidAmount,
            RefundedAmount = bill.Payments.Where(p => p.Amount < 0).Sum(p => -p.Amount),
            bill.Status,
            bill.CreatedAtUtc,
            bill.BillDate,
            bill.Items,
            bill.Payments
        }));
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog([FromQuery] string type, [FromQuery] int? doctorId)
    {
        if (type.Equals("Lab", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(await _db.LabTests.Where(x => x.IsActive).Select(x => new
            {
                x.Id,
                Label = x.Name,
                Price = x.Price,
                ReferenceType = "LabTest"
            }).ToListAsync());
        }

        if (type.Equals("Pharmacy", StringComparison.OrdinalIgnoreCase))
        {
            var pharmacyItems = await _db.PharmacyPurchaseItems
                .Where(x => x.RemainingTablets > 0 && x.ExpiryDate.Date >= DateTime.Today &&
                    x.PurchaseInvoice != null && !x.PurchaseInvoice.IsDeleted)
                .GroupBy(x => new { x.ProductName, x.Mrp })
                .Select(g => new
                {
                    Id = g.Min(x => x.Id),
                    Label = g.Key.ProductName,
                    Price = g.Key.Mrp,
                    ReferenceType = "PharmacyPurchaseItem"
                })
                .ToListAsync();

            return Ok(pharmacyItems.OrderBy(x => x.Label).ToList());
        }

        // Every other bill type (OPD, Emergency, Dental, IPD, OT, Dressing, ...)
        // pulls from the Hospital Expense Charge master, filtered to charges
        // tagged for this specific bill type.
        var charges = await _db.ServiceCharges
            .Where(x => x.IsActive && x.BillType == type)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, Label = x.Name, x.Price, ReferenceType = "ServiceCharge" })
            .ToListAsync();

        // When a doctor is picked on an OPD/Emergency bill, offer their own
        // consultation charge (from the Doctor master) as a catalog entry
        // too - a negative Id keeps it from ever colliding with a real
        // ServiceCharge's auto-increment Id.
        if (doctorId.HasValue &&
            (type.Equals("OPD", StringComparison.OrdinalIgnoreCase) || type.Equals("Emergency", StringComparison.OrdinalIgnoreCase)))
        {
            var doctor = await _db.Doctors.FindAsync(doctorId.Value);
            if (doctor != null && doctor.IsActive)
            {
                var consultationEntry = new { Id = -doctor.Id, Label = $"Consultation - {doctor.Name}", Price = doctor.ConsultationCharge, ReferenceType = "DoctorConsultation" };
                return Ok(new[] { consultationEntry }.Concat(charges).ToList());
            }
        }

        return Ok(charges);
    }

    [HttpPost]
    public async Task<IActionResult> Add(BillCreateRequest request)
    {
        if (!request.PatientId.HasValue && string.IsNullOrWhiteSpace(request.WalkInPatientName))
            return BadRequest(new { message = "Patient is required." });

        if (!await _db.BillTypes.AnyAsync(x => x.IsActive && x.IsBillable && x.Name == request.BillType))
            return BadRequest(new { message = "Please select a valid bill type." });

        if (request.RoundOff < -9 || request.RoundOff > 9)
            return BadRequest(new { message = "Round off must be between -9 and 9." });

        try
        {
            var bill = await BuildBill(request);
            return Ok(bill);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, BillCreateRequest request)
    {
        var bill = await _db.Bills.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (bill == null) return NotFound(new { message = "Bill not found." });
        if (!request.PatientId.HasValue && string.IsNullOrWhiteSpace(request.WalkInPatientName))
            return BadRequest(new { message = "Patient is required." });
        if (!await _db.BillTypes.AnyAsync(x => x.IsActive && x.IsBillable && x.Name == request.BillType))
            return BadRequest(new { message = "Please select a valid bill type." });
        if (request.RoundOff < -9 || request.RoundOff > 9)
            return BadRequest(new { message = "Round off must be between -9 and 9." });

        try
        {
            var calculated = await CalculateAsync(request.Items, request.BulkDiscountTypeId);
            var roundedNet = calculated.Net + request.RoundOff;

            _db.BillItems.RemoveRange(bill.Items);
            var patient = request.PatientId.HasValue ? await _db.Patients.FindAsync(request.PatientId.Value) : null;

            bill.PatientId = request.PatientId;
            bill.WalkInPatientName = request.PatientId.HasValue ? null : request.WalkInPatientName?.Trim();
            bill.DoctorId = request.DoctorId;
            bill.ReferrerId = patient?.ReferrerId;
            bill.BillType = request.BillType;
            bill.GrossAmount = calculated.BeforeDiscount;
            bill.DiscountPercent = calculated.Bulk.Percent;
            bill.DiscountAmount = calculated.Bulk.Amount;
            bill.BulkDiscountTypeId = calculated.Bulk.Id;
            bill.BulkDiscountName = calculated.Bulk.Name;
            bill.RoundOff = request.RoundOff;
            bill.NetAmount = roundedNet;

            foreach (var item in calculated.Items) bill.Items.Add(item);

            // If this is a Lab bill with a properly linked LabOrder (created
            // via LabController.CreateOrder, not an ad-hoc Lab-type bill with
            // no lab-side rows at all), keep that order's tests in sync with
            // whatever the edited bill's lines now reference - e.g. swapping
            // which test is billed should show the new test on the lab
            // result screen, not the old one.
            if (bill.BillType.Equals("Lab", StringComparison.OrdinalIgnoreCase))
            {
                var labOrder = await _db.LabOrders.Include(x => x.Tests).FirstOrDefaultAsync(x => x.BillId == id);
                if (labOrder != null)
                {
                    var newTestLines = calculated.Items
                        .Where(x => x.ReferenceType == "LabTest" && x.ReferenceId.HasValue)
                        .ToList();
                    var newTestIds = newTestLines.Select(x => x.ReferenceId!.Value).ToHashSet();

                    var toRemove = labOrder.Tests.Where(t => !newTestIds.Contains(t.LabTestId)).ToList();
                    foreach (var orderTest in toRemove)
                    {
                        _db.LabResults.RemoveRange(_db.LabResults.Where(r => r.LabOrderTestId == orderTest.Id));
                        _db.LabOrderTests.Remove(orderTest);
                    }

                    var existingTestIds = labOrder.Tests.Select(t => t.LabTestId).ToHashSet();
                    foreach (var line in newTestLines.Where(x => !existingTestIds.Contains(x.ReferenceId!.Value)))
                    {
                        var test = await _db.LabTests.Include(x => x.Components).FirstOrDefaultAsync(x => x.Id == line.ReferenceId!.Value);
                        if (test == null) continue;

                        var orderTest = new LabOrderTest
                        {
                            LabOrderId = labOrder.Id,
                            LabTestId = test.Id,
                            UnitPrice = line.UnitPrice,
                            DiscountTypeId = line.DiscountTypeId,
                            DiscountName = line.DiscountName,
                            DiscountMode = line.DiscountMode,
                            DiscountValue = line.DiscountValue,
                            DiscountAmount = line.DiscountAmount,
                            NetAmount = line.Amount,
                            ResultSchemaJson = LabController.BuildResultSchema(test)
                        };
                        _db.LabOrderTests.Add(orderTest);
                        await _db.SaveChangesAsync();

                        foreach (var component in test.Components)
                        {
                            _db.LabResults.Add(new LabResult
                            {
                                LabOrderTestId = orderTest.Id,
                                LabTestComponentId = component.Id,
                                ComponentName = component.Name,
                                RangeText = component.RangeText,
                                Unit = component.Unit,
                                ResultValue = component.DefaultValue
                            });
                        }
                    }
                }
            }

            // Editing an already-paid bill down to a lower total (e.g. an item
            // was removed) doesn't block the edit - it immediately records the
            // excess as a refund (a negative Payment, same table every revenue
            // report already sums from, so the refunded amount nets itself out
            // everywhere automatically) and settles the bill back to Paid.
            decimal refundAmount = 0;
            if (roundedNet < bill.PaidAmount)
            {
                refundAmount = bill.PaidAmount - roundedNet;
                var lastMode = await _db.Payments
                    .Where(p => p.BillId == id)
                    .OrderByDescending(p => p.PaidAtUtc)
                    .Select(p => p.Mode)
                    .FirstOrDefaultAsync() ?? "Cash";
                _db.Payments.Add(new Payment
                {
                    BillId = id,
                    Amount = -refundAmount,
                    Mode = lastMode,
                    Reference = "Refund - bill amount reduced on edit",
                    PaidAtUtc = DateTime.Now
                });
                bill.PaidAmount -= refundAmount;
            }

            bill.Status = bill.PaidAmount >= bill.NetAmount ? "Paid" : bill.PaidAmount > 0 ? "Partially Paid" : "Unpaid";

            // An OPD/Emergency consultation bill's settlement row (created at
            // bill-creation time) doesn't automatically follow a later doctor
            // change or amount change - resync it here, unless the doctor has
            // already been paid out for it (that payment history is left alone).
            var settlement = await _db.DoctorSettlements
                .FirstOrDefaultAsync(x => x.SourceType == "Consultation" && x.SourceId == id && x.PaidAmount == 0);
            var editedConsultationItem = calculated.Items.FirstOrDefault(x => x.ReferenceType == "DoctorConsultation");
            if (settlement != null)
            {
                if (bill.DoctorId.HasValue) settlement.DoctorId = bill.DoctorId.Value;
                if (editedConsultationItem != null) settlement.PayableAmount = editedConsultationItem.Amount;
                settlement.UpdatedAtUtc = DateTime.UtcNow;
            }
            else if (editedConsultationItem != null && bill.DoctorId.HasValue)
            {
                // A consultation line was added where there was none before
                // (and so no settlement row existed yet) - create one now,
                // same as a brand-new consultation bill would.
                _db.DoctorSettlements.Add(new DoctorSettlement
                {
                    DoctorId = bill.DoctorId.Value,
                    SourceType = "Consultation",
                    SourceId = bill.Id,
                    Description = $"{bill.BillType} consultation · {bill.BillNumber}",
                    PayableAmount = editedConsultationItem.Amount,
                    EarnedDate = bill.BillDate.Date
                });
            }

            await _db.SaveChangesAsync();
            return Ok(new { bill, refundAmount });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var bill = await _db.Bills.FindAsync(id);
        if (bill == null) return NotFound();
        if (bill.PaidAmount > 0) return BadRequest(new { message = "A bill with payment history cannot be deleted." });
        bill.Status = "Deleted";

        // An OPD/Emergency consultation bill earns the consulting doctor a
        // settlement row at creation time (OpdController/PatientsController).
        // Once the bill is gone, that payable shouldn't still show up owed -
        // unless the doctor was already settled for it, in which case that
        // payment history is left alone rather than silently erased.
        var settlements = await _db.DoctorSettlements
            .Where(x => x.SourceType == "Consultation" && x.SourceId == id && x.PaidAmount == 0)
            .ToListAsync();
        if (settlements.Count > 0) _db.DoctorSettlements.RemoveRange(settlements);

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("{id:int}/print")]
    public async Task<IActionResult> Print(int id)
    {
        var bill = await _db.Bills.Include(x => x.Items).Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == id);
        if (bill == null) return NotFound();

        var patient = bill.PatientId.HasValue ? await _db.Patients.FindAsync(bill.PatientId.Value) : null;
        var referrer = bill.ReferrerId.HasValue ? await _db.Referrers.FindAsync(bill.ReferrerId.Value) : null;
        var beforeDiscount = bill.Items.Sum(x => x.UnitPrice * x.Quantity);
        var individualDiscount = bill.Items.Sum(x => x.DiscountAmount);
        var afterIndividualDiscount = beforeDiscount - individualDiscount;

        string Setting(string name) => AssetVersioning.Stamp(_env, _db.AppSettings.FirstOrDefault(x => x.Name == name && x.IsActive)?.Value ?? string.Empty);
        var header = bill.BillType.Equals("Lab", StringComparison.OrdinalIgnoreCase)
            ? Setting("Lab Header")
            : bill.BillType.Equals("Pharmacy", StringComparison.OrdinalIgnoreCase) ? Setting("Pharmacy Header") : Setting("OPD Header");

        // The bill amount is treated as already tax-inclusive (the GST rate
        // defaults to 0/exempt, per how this hospital's non-pharmacy billing
        // works today), so GST is extracted from the existing NetAmount as a
        // breakdown rather than added on top - the patient's total never
        // changes here. IGST isn't computed (intra-state billing only); the
        // field still prints, just always zero.
        var hsnNo = Setting("Bill HSN No");
        var hospitalGstNo = Setting("Hospital GST No");
        decimal.TryParse(Setting("Bill GST Rate"), out var gstRate);
        var taxableValue = gstRate > 0 ? Math.Round(bill.NetAmount / (1 + gstRate / 100m), 2) : bill.NetAmount;
        var cgstRate = gstRate / 2;
        var sgstRate = gstRate / 2;
        var cgstAmount = Math.Round(taxableValue * cgstRate / 100m, 2);
        var sgstAmount = Math.Round(taxableValue * sgstRate / 100m, 2);

        return Ok(new
        {
            bill,
            patient,
            referrer,
            header,
            hsnNo,
            hospitalGstNo,
            totals = new
            {
                BeforeDiscount = beforeDiscount,
                IndividualDiscount = individualDiscount,
                AfterIndividualDiscount = afterIndividualDiscount,
                BulkDiscount = bill.DiscountAmount,
                AfterDiscount = bill.NetAmount,
                Paid = bill.PaidAmount,
                Outstanding = bill.NetAmount - bill.PaidAmount
            },
            gstSummary = new
            {
                TaxableValue = taxableValue,
                CgstRate = cgstRate,
                CgstAmount = cgstAmount,
                SgstRate = sgstRate,
                SgstAmount = sgstAmount,
                IgstRate = 0m,
                IgstAmount = 0m
            }
        });
    }

    [HttpPost("{id:int}/pay")]
    public async Task<IActionResult> Pay(int id, PaymentRequest request)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var bill = await _db.Bills.FindAsync(id);
        if (bill == null) return NotFound();
        var outstanding = bill.NetAmount - bill.PaidAmount;
        if (request.Amount <= 0) return BadRequest(new { message = "Payment amount must be greater than zero." });
        if (request.Amount > outstanding) return BadRequest(new { message = $"Payment cannot exceed outstanding amount ₹{outstanding:0.00}." });

        _db.Payments.Add(new Payment { BillId = id, Amount = request.Amount, Mode = request.Mode, Reference = request.Reference });
        bill.PaidAmount += request.Amount;
        bill.Status = bill.PaidAmount >= bill.NetAmount ? "Paid" : "Partially Paid";
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(bill);
    }

    private async Task<Bill> BuildBill(BillCreateRequest request)
    {
        var calculated = await CalculateAsync(request.Items, request.BulkDiscountTypeId);
        var patient = request.PatientId.HasValue ? await _db.Patients.FindAsync(request.PatientId.Value) : null;

        var bill = new Bill
        {
            PatientId = request.PatientId,
            WalkInPatientName = request.PatientId.HasValue ? null : request.WalkInPatientName?.Trim(),
            DoctorId = request.DoctorId,
            ReferrerId = patient?.ReferrerId,
            BillType = request.BillType,
            GrossAmount = calculated.BeforeDiscount,
            DiscountPercent = calculated.Bulk.Percent,
            DiscountAmount = calculated.Bulk.Amount,
            BulkDiscountTypeId = calculated.Bulk.Id,
            BulkDiscountName = calculated.Bulk.Name,
            RoundOff = request.RoundOff,
            NetAmount = calculated.Net + request.RoundOff
        };

        foreach (var item in calculated.Items) bill.Items.Add(item);
        _db.Bills.Add(bill);
        await _db.SaveChangesAsync();
        bill.BillNumber = $"BILL-{DateTime.Now:yyyyMMdd}-{bill.Id:000000}";

        // A bill whose Description catalog included "Consultation - Dr. X"
        // earns the doctor a settlement row here, same as the dedicated OPD
        // visit/registration flows already do - this generic Create Bill
        // path just hadn't been wired up to do that yet.
        var consultationItem = bill.Items.FirstOrDefault(x => x.ReferenceType == "DoctorConsultation");
        if (consultationItem != null && bill.DoctorId.HasValue)
        {
            _db.DoctorSettlements.Add(new DoctorSettlement
            {
                DoctorId = bill.DoctorId.Value,
                SourceType = "Consultation",
                SourceId = bill.Id,
                Description = $"{bill.BillType} consultation · {bill.BillNumber}",
                PayableAmount = consultationItem.Amount,
                EarnedDate = bill.BillDate.Date
            });
        }

        await _db.SaveChangesAsync();
        return bill;
    }

    private async Task<BillCalculation> CalculateAsync(List<BillCreateItem> source, int? bulkDiscountTypeId)
    {
        if (source.Count == 0) throw new InvalidOperationException("At least one bill item is required.");

        var items = new List<BillItem>();
        decimal beforeDiscount = 0;
        decimal afterIndividualDiscount = 0;

        foreach (var sourceItem in source)
        {
            var raw = sourceItem.UnitPrice * sourceItem.Quantity;
            var discount = await _discounts.ResolveAsync(raw, sourceItem.DiscountTypeId, "Individual");
            beforeDiscount += raw;
            afterIndividualDiscount += discount.Net;

            items.Add(new BillItem
            {
                Description = sourceItem.Description,
                Quantity = sourceItem.Quantity,
                UnitPrice = sourceItem.UnitPrice,
                DiscountTypeId = discount.Id,
                DiscountName = discount.Name,
                DiscountMode = discount.Mode,
                DiscountValue = discount.Value,
                DiscountAmount = discount.Amount,
                Amount = discount.Net,
                ReferenceType = sourceItem.ReferenceType,
                ReferenceId = sourceItem.ReferenceId
            });
        }

        var bulk = await _discounts.ResolveAsync(afterIndividualDiscount, bulkDiscountTypeId, "Bulk");
        return new BillCalculation
        {
            BeforeDiscount = beforeDiscount,
            AfterIndividualDiscount = afterIndividualDiscount,
            Bulk = bulk,
            Net = bulk.Net,
            Items = items
        };
    }

    private sealed class BillCalculation
    {
        public decimal BeforeDiscount { get; init; }
        public decimal AfterIndividualDiscount { get; init; }
        public ResolvedDiscount Bulk { get; init; } = ResolvedDiscount.None(0);
        public decimal Net { get; init; }
        public List<BillItem> Items { get; init; } = new();
    }
}
