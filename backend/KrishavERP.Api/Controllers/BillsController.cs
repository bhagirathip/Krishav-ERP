using KrishavERP.Api;
using KrishavERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/bills")]
public class BillsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DiscountService _discounts;

    public BillsController(AppDbContext db, DiscountService discounts)
    {
        _db = db;
        _discounts = discounts;
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

        query = status == "Paid"
            ? query.Where(x => x.Status == "Paid")
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
            bill.Status,
            bill.CreatedAtUtc,
            bill.BillDate,
            bill.Items,
            bill.Payments
        }));
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog([FromQuery] string type)
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

        if (type.Equals("OPD", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("Emergency", StringComparison.OrdinalIgnoreCase))
        {
            var settings = await _db.AppSettings
                .Where(x => x.IsActive && x.Type == "Billing" && (x.Name == "Emergency Rate" || x.Name == "Hospital Charge"))
                .ToListAsync();

            return Ok(settings.Select(x =>
            {
                var price = decimal.TryParse(x.Value, out var parsedValue) ? parsedValue : 0m;
                return new { x.Id, Label = x.Name, Price = price, ReferenceType = "Setting" };
            }).ToList());
        }

        return Ok(Array.Empty<object>());
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
            if (roundedNet < bill.PaidAmount)
                return BadRequest(new { message = "Edited bill total cannot be lower than the amount already paid." });

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
            bill.Status = bill.PaidAmount >= bill.NetAmount ? "Paid" : bill.PaidAmount > 0 ? "Partially Paid" : "Unpaid";

            foreach (var item in calculated.Items) bill.Items.Add(item);
            await _db.SaveChangesAsync();
            return Ok(bill);
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

        string Setting(string name) => _db.AppSettings.FirstOrDefault(x => x.Name == name && x.IsActive)?.Value ?? string.Empty;
        var header = bill.BillType.Equals("Lab", StringComparison.OrdinalIgnoreCase)
            ? Setting("Lab Header")
            : bill.BillType.Equals("Pharmacy", StringComparison.OrdinalIgnoreCase) ? Setting("Pharmacy Header") : Setting("OPD Header");

        return Ok(new
        {
            bill,
            patient,
            referrer,
            header,
            totals = new
            {
                BeforeDiscount = beforeDiscount,
                IndividualDiscount = individualDiscount,
                AfterIndividualDiscount = afterIndividualDiscount,
                BulkDiscount = bill.DiscountAmount,
                AfterDiscount = bill.NetAmount,
                Paid = bill.PaidAmount,
                Outstanding = bill.NetAmount - bill.PaidAmount
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
