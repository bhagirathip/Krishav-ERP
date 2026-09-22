using KrishavERP.Api;
using KrishavERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/lab")]
public class LabController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DiscountService _discounts;

    public LabController(AppDbContext db, DiscountService discounts)
    {
        _db = db;
        _discounts = discounts;
    }

    [HttpGet("tests")]
    public async Task<IActionResult> Tests()
    {
        var rows = await _db.LabTests
            .Include(x => x.Components)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost("tests")]
    public async Task<IActionResult> AddTest(LabTest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Test name is required." });
        }

        request.Name = request.Name.Trim();
        request.IsActive = true;
        NormalizeComponents(request);

        _db.LabTests.Add(request);
        await _db.SaveChangesAsync();
        return Ok(request);
    }

    [HttpPut("tests/{id:int}")]
    public async Task<IActionResult> EditTest(int id, LabTest request)
    {
        var existing = await _db.LabTests
            .Include(x => x.Components)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (existing == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Test name is required." });
        }

        existing.Name = request.Name.Trim();
        existing.Price = request.Price;
        existing.Note = request.Note;
        existing.SchemaJson = request.SchemaJson;

        _db.LabTestComponents.RemoveRange(existing.Components);
        NormalizeComponents(request);
        existing.Components = request.Components;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("tests/{id:int}")]
    public async Task<IActionResult> DeleteTest(int id)
    {
        var existing = await _db.LabTests.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        existing.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("orders")]
    public async Task<IActionResult> Orders()
    {
        var rows = await (
            from order in _db.LabOrders
            join patient in _db.Patients on order.PatientId equals patient.Id
            select new
            {
                order.Id,
                order.OrderNumber,
                order.PatientId,
                PatientName = patient.Name,
                patient.PatientCode,
                order.CreatedAtUtc,
                order.Status,
                TestCount = order.Tests.Count
            })
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(LabOrderCreateRequest request)
    {
        if (request.Tests.Count == 0)
        {
            return BadRequest(new { message = "Select at least one lab test." });
        }

        if (request.RoundOff < -9 || request.RoundOff > 9)
        {
            return BadRequest(new { message = "Round off must be between -9 and 9." });
        }

        var ids = request.Tests.Select(x => x.LabTestId).Distinct().ToList();
        var tests = await _db.LabTests
            .Include(x => x.Components)
            .Where(x => ids.Contains(x.Id) && x.IsActive)
            .ToListAsync();

        if (tests.Count != ids.Count)
        {
            return BadRequest(new { message = "One or more tests are invalid." });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        int? doctorId = null;
        if (request.IpdAdmissionId.HasValue)
        {
            doctorId = await _db.IpdAdmissions
                .Where(x => x.Id == request.IpdAdmissionId.Value)
                .Select(x => (int?)x.DoctorId)
                .FirstOrDefaultAsync();
        }
        else
        {
            doctorId = await _db.OpdVisits
                .Where(x => x.PatientId == request.PatientId && !x.IsCancelled && x.DoctorId.HasValue)
                .OrderByDescending(x => x.VisitDateUtc)
                .Select(x => x.DoctorId)
                .FirstOrDefaultAsync();
        }

        var patient = await _db.Patients.FindAsync(request.PatientId);
        var bill = new Bill
        {
            PatientId = request.PatientId,
            DoctorId = doctorId,
            ReferrerId = patient?.ReferrerId,
            BillType = "Lab"
        };

        var order = new LabOrder
        {
            PatientId = request.PatientId,
            IpdAdmissionId = request.IpdAdmissionId
        };

        _db.LabOrders.Add(order);
        await _db.SaveChangesAsync();
        order.OrderNumber = $"LAB-{DateTime.UtcNow:yyyyMMdd}-{order.Id:000000}";

        foreach (var requestedTest in request.Tests)
        {
            var test = tests.First(x => x.Id == requestedTest.LabTestId);
            ResolvedDiscount line;
            try
            {
                line = await _discounts.ResolveAsync(test.Price, requestedTest.DiscountTypeId, "Individual");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            var orderTest = new LabOrderTest
            {
                LabOrderId = order.Id,
                LabTestId = test.Id,
                UnitPrice = test.Price,
                DiscountTypeId = line.Id,
                DiscountName = line.Name,
                DiscountMode = line.Mode,
                DiscountValue = line.Value,
                DiscountAmount = line.Amount,
                NetAmount = line.Net,
                ResultSchemaJson = BuildResultSchema(test)
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

            bill.Items.Add(new BillItem
            {
                Description = test.Name,
                Quantity = 1,
                UnitPrice = test.Price,
                DiscountTypeId = line.Id,
                DiscountName = line.Name,
                DiscountMode = line.Mode,
                DiscountValue = line.Value,
                DiscountAmount = line.Amount,
                Amount = line.Net,
                ReferenceType = "LabTest",
                ReferenceId = test.Id
            });
        }

        bill.GrossAmount = bill.Items.Sum(x => x.UnitPrice * x.Quantity);
        bill.RoundOff = request.RoundOff;
        bill.NetAmount = bill.Items.Sum(x => x.Amount) + request.RoundOff;
        _db.Bills.Add(bill);
        await _db.SaveChangesAsync();
        bill.BillNumber = $"BILL-{DateTime.UtcNow:yyyyMMdd}-{bill.Id:000000}";
        order.BillId = bill.Id;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            BillId = bill.Id,
            BillNumber = bill.BillNumber
        });
    }

    [HttpGet("orders/{orderId:int}/tests")]
    public async Task<IActionResult> OrderTests(int orderId)
    {
        var rows = await (
            from orderTest in _db.LabOrderTests
            where orderTest.LabOrderId == orderId
            join test in _db.LabTests on orderTest.LabTestId equals test.Id
            select new
            {
                orderTest.Id,
                orderTest.LabTestId,
                TestName = test.Name,
                test.Note,
                orderTest.Status,
                orderTest.UnitPrice,
                orderTest.DiscountTypeId,
                orderTest.DiscountName,
                orderTest.DiscountAmount,
                orderTest.NetAmount
            })
            .ToListAsync();

        return Ok(rows);
    }

    [HttpDelete("order-tests/{id:int}")]
    public async Task<IActionResult> DeleteOrderTest(int id)
    {
        var orderTest = await _db.LabOrderTests.Include(x => x.Results).FirstOrDefaultAsync(x => x.Id == id);
        if (orderTest == null) return NotFound();

        var order = await _db.LabOrders.FindAsync(orderTest.LabOrderId);

        if (order?.BillId != null)
        {
            var bill = await _db.Bills.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == order.BillId.Value);
            var billItem = bill?.Items.FirstOrDefault(x => x.ReferenceType == "LabTest" && x.ReferenceId == orderTest.LabTestId);

            if (bill != null && billItem != null)
            {
                var newNet = bill.NetAmount - billItem.Amount;
                if (newNet < bill.PaidAmount)
                {
                    return BadRequest(new { message = "Cannot delete: this bill already has payments covering this test." });
                }

                bill.GrossAmount -= billItem.UnitPrice * billItem.Quantity;
                bill.NetAmount = newNet;
                bill.Status = bill.PaidAmount >= bill.NetAmount ? "Paid" : bill.PaidAmount > 0 ? "Partially Paid" : "Unpaid";
                _db.BillItems.Remove(billItem);
            }
        }

        _db.LabResults.RemoveRange(orderTest.Results);
        _db.LabOrderTests.Remove(orderTest);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("order-tests/{orderTestId:int}")]
    public async Task<IActionResult> OrderTest(int orderTestId)
    {
        var orderTest =
            await _db.LabOrderTests.FindAsync(orderTestId);

        if (orderTest == null)
        {
            return NotFound();
        }

        var order =
            await _db.LabOrders.FindAsync(orderTest.LabOrderId);

        var test =
            await _db.LabTests
                .Include(x => x.Components)
                .FirstOrDefaultAsync(x => x.Id == orderTest.LabTestId);

        var patient = order == null
            ? null
            : await _db.Patients.FindAsync(order.PatientId);

        var schemaJson =
            !string.IsNullOrWhiteSpace(orderTest.ResultSchemaJson)
                ? orderTest.ResultSchemaJson
                : test == null
                    ? EmptySchema()
                    : BuildResultSchema(test);

        return Ok(new
        {
            orderTestId,
            testName = test?.Name,
            testNote = test?.Note,
            patient,
            resultSchema = ParseSchemaForResponse(schemaJson),
            status = orderTest.Status
        });
    }

    [HttpPut("order-tests/{orderTestId:int}/results")]
    public async Task<IActionResult> SaveResults(
        int orderTestId,
        LabDynamicResultSaveRequest request)
    {
        var orderTest =
            await _db.LabOrderTests.FindAsync(orderTestId);

        if (orderTest == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.SchemaJson))
        {
            return BadRequest(new
            {
                message = "Lab result table data is required."
            });
        }

        try
        {
            using var document =
                System.Text.Json.JsonDocument.Parse(request.SchemaJson);

            if (!document.RootElement.TryGetProperty("columns", out var columns) ||
                columns.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return BadRequest(new
                {
                    message = "Lab result columns are invalid."
                });
            }

            if (!document.RootElement.TryGetProperty("rows", out var rows) ||
                rows.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return BadRequest(new
                {
                    message = "Lab result rows are invalid."
                });
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(new
            {
                message = "Lab result table JSON is invalid."
            });
        }

        orderTest.ResultSchemaJson = request.SchemaJson;
        orderTest.Status = "Completed";
        orderTest.ResultUpdatedAtUtc = DateTime.Now;

        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("order-tests/{orderTestId:int}/print")]
    public async Task<IActionResult> Print(int orderTestId)
    {
        var orderTest =
            await _db.LabOrderTests.FindAsync(orderTestId);

        if (orderTest == null)
        {
            return NotFound();
        }

        var order =
            await _db.LabOrders.FindAsync(orderTest.LabOrderId);

        if (order == null)
        {
            return NotFound();
        }

        var patient =
            await _db.Patients.FindAsync(order.PatientId);

        var test =
            await _db.LabTests
                .Include(x => x.Components)
                .FirstOrDefaultAsync(x => x.Id == orderTest.LabTestId);

        if (test == null)
        {
            return NotFound();
        }

        var schemaJson =
            !string.IsNullOrWhiteSpace(orderTest.ResultSchemaJson)
                ? orderTest.ResultSchemaJson
                : BuildResultSchema(test);

        string Setting(string name)
        {
            return _db.AppSettings
                .FirstOrDefault(x =>
                    x.Name == name &&
                    x.IsActive)
                ?.Value ?? "";
        }

        return Ok(new
        {
            patient,
            test,
            order = new
            {
                order.OrderNumber,
                order.CreatedAtUtc
            },
            orderTest.ResultUpdatedAtUtc,
            resultSchema = ParseSchemaForResponse(schemaJson),
            hospital = new
            {
                name = Setting("Hospital Name"),
                address = Setting("Hospital Address"),
                phone = Setting("Hospital Phone"),
                logo = Setting("Hospital Logo"),
                labHeader = Setting("Lab Header"),
                labSignature = Setting("Lab Signature"),
                labSignatoryName = Setting("Lab Signatory Name"),
                labSignatoryQualification = Setting("Lab Signatory Qualification")
            }
        });
    }

    private static string BuildResultSchema(LabTest test)
    {
        if (!string.IsNullOrWhiteSpace(test.SchemaJson))
        {
            return test.SchemaJson;
        }

        var columns = new[]
        {
            new { id = "parameter", name = "Parameter" },
            new { id = "range", name = "Range" },
            new { id = "unit", name = "Unit" },
            new { id = "value", name = "Value" }
        };

        var rows = test.Components
            .Select((component, index) => new
            {
                id = $"row-{index + 1}",
                values = new Dictionary<string, string?>
                {
                    ["parameter"] = component.Name,
                    ["range"] = component.RangeText,
                    ["unit"] = component.Unit,
                    ["value"] = component.DefaultValue
                }
            })
            .ToList();

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            columns,
            rows
        });
    }

    private static string EmptySchema()
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            columns = Array.Empty<object>(),
            rows = Array.Empty<object>()
        });
    }


    private static object ParseSchemaForResponse(string schemaJson)
    {
        try
        {
            using var document =
                System.Text.Json.JsonDocument.Parse(schemaJson);

            var root = document.RootElement.Clone();

            return new
            {
                columns =
                    root.TryGetProperty("columns", out var columns)
                        ? columns
                        : default,
                rows =
                    root.TryGetProperty("rows", out var rows)
                        ? rows
                        : default
            };
        }
        catch
        {
            return new
            {
                columns = Array.Empty<object>(),
                rows = Array.Empty<object>()
            };
        }
    }

    private static void NormalizeComponents(LabTest test)
    {
        test.Components ??= new List<LabTestComponent>();

        foreach (var component in test.Components)
        {
            component.Id = 0;
            component.LabTestId = 0;
            component.Name = component.Name?.Trim() ?? "";
        }

        test.Components = test.Components
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToList();
    }
}
