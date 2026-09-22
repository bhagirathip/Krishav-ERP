using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using KrishavERP.Api;
using KrishavERP.Api.Services;
namespace KrishavERP.Api.Controllers;
[Authorize]
[ApiController]
[Route("api/pharmacy")]
public class PharmacyController:ControllerBase
{
    private readonly AppDbContext db;
    public PharmacyController(AppDbContext db)=>this.db=db;
    [HttpGet("medicines")]
    public async Task<IActionResult> Medicines([FromQuery]string? q)
    {
        var x=db.Medicines.Where(m=>m.IsActive);
        if(!string.IsNullOrWhiteSpace(q))x=x.Where(m=>m.Name.Contains(q)||m.BatchNumber.Contains(q)||(m.BoxNumber??"").Contains(q)||(m.Section??"").Contains(q)||(m.Component??"").Contains(q));
        return Ok(await x.OrderBy(m=>m.Name).ToListAsync());
    }
    [HttpPost("medicines")] public async Task<IActionResult> AddMedicine(Medicine m)
    {
        db.Medicines.Add(m);
        await db.SaveChangesAsync();
        return Ok(m);
    }
    [HttpPut("medicines/{id}")]
    public async Task<IActionResult> EditMedicine(int id,Medicine m)
    {
        var x=await db.Medicines.FindAsync(id);
        if(x==null)return NotFound();
        x.Name=m.Name;
        x.ExpiryDate=m.ExpiryDate;
        x.QuantityTablets=m.QuantityTablets;
        x.TabletsPerStrip=m.TabletsPerStrip;
        x.PricePerStrip=m.PricePerStrip;
        x.BatchNumber=m.BatchNumber;
        x.BoxNumber=m.BoxNumber;
        x.Section=m.Section;
        x.Component=m.Component;
        await db.SaveChangesAsync();
        return Ok(x);
    }
    [HttpDelete("medicines/{id}")] public async Task<IActionResult> DeleteMedicine(int id)
    {
        var x=await db.Medicines.FindAsync(id);
        if(x==null)return NotFound();
        x.IsActive=false;
        await db.SaveChangesAsync();
        return Ok();
    }
    [HttpPost("bill")]
    public async Task<IActionResult> Bill(PharmacyBillRequest r)
    {
        if(!r.PatientId.HasValue)return BadRequest(new{message="Patient is required."});
        var doctorId = await db.OpdVisits
            .Where(x => x.PatientId == r.PatientId && !x.IsCancelled && x.DoctorId.HasValue)
            .OrderByDescending(x => x.VisitDateUtc)
            .Select(x => x.DoctorId)
            .FirstOrDefaultAsync();
        var patient = await db.Patients.FindAsync(r.PatientId.Value);
        var bill=new Bill
        {
            PatientId=r.PatientId,DoctorId=doctorId,ReferrerId=patient?.ReferrerId,BillType="Pharmacy"
        }
        ;
        var reserve=new List<(Medicine m,int qty)>();
        foreach(var i in r.Items)
        {
            var m=await db.Medicines.FindAsync(i.MedicineId);
            if(m==null||!m.IsActive)return BadRequest(new{message="Medicine not found."});
            var tablets=i.UnitType=="Strip"?i.Quantity*m.TabletsPerStrip:i.Quantity;
            if(tablets>m.QuantityTablets)return BadRequest(new{message=$"Only {m.QuantityTablets} tablets of {m.Name} are available."});
            var unit=i.UnitType=="Strip"?m.PricePerStrip:m.PricePerStrip/m.TabletsPerStrip;
            var raw=Math.Round(unit*i.Quantity,2);
            bill.Items.Add(new BillItem{Description=$"{m.Name} ({i.UnitType})",Quantity=i.Quantity,UnitPrice=unit,Amount=raw,ReferenceType="Medicine",ReferenceId=m.Id});
            reserve.Add((m,tablets));
        }
        var sub=bill.Items.Sum(x=>x.Amount);
        bill.GrossAmount=sub;
        bill.DiscountPercent=0;
        bill.DiscountAmount=0;
        bill.NetAmount=sub;
        db.Bills.Add(bill);
        await db.SaveChangesAsync();
        bill.BillNumber=$"BILL-{DateTime.UtcNow:yyyyMMdd}-{bill.Id:000000}";
        foreach(var v in reserve)db.PharmacyBillReservations.Add(new PharmacyBillReservation{BillId=bill.Id,MedicineId=v.m.Id,QuantityTablets=v.qty});
        await db.SaveChangesAsync();
        return Ok(bill);
    }
}
