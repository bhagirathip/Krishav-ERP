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
[Route("api/dashboard")]
public class DashboardController:ControllerBase
{
    private readonly AppDbContext db;
    public DashboardController(AppDbContext db)=>this.db=db;
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery]DateTime? from,[FromQuery]DateTime? to)
    {
        var f=(from??DateTime.UtcNow.Date).Date;
        var end=(to??f).Date.AddDays(1);
        var multi=(end-f).TotalDays>1;
        var bills=await db.Bills.Include(x=>x.Items).Where(x=>x.Status!="Deleted"&&x.BillDate>=f&&x.BillDate<end).ToListAsync();
        var visits=db.OpdVisits.Where(x=>!x.IsCancelled&&x.VisitDateUtc>=f&&x.VisitDateUtc<end);
        var labs=db.LabOrders.Where(x=>x.CreatedAtUtc>=f&&x.CreatedAtUtc<end);
        var totalBeds=await db.WardBeds.CountAsync(x=>x.IsActive);
        var occupied=await db.WardBeds.CountAsync(x=>x.IsActive&&x.IsOccupied);

        // Doctor consultation money belongs to the doctor (settled via Doctor Settlement),
        // so it is excluded from the hospital's daily income totals below.
        decimal doctorConsultationAmount=0,totalPaidAmount=0,totalUnpaidAmount=0,totalLabAmount=0,totalPharmacyAmount=0,totalOpdAmount=0;
        foreach(var bill in bills)
        {
            var consultationAmount=bill.Items.Where(i=>i.ReferenceType=="Doctor").Sum(i=>i.Amount);
            var hospitalNet=bill.NetAmount-consultationAmount;
            var hospitalPaid=Math.Min(bill.PaidAmount,hospitalNet);
            doctorConsultationAmount+=consultationAmount;
            totalPaidAmount+=hospitalPaid;
            totalUnpaidAmount+=hospitalNet-hospitalPaid;
            if(bill.BillType=="Lab") totalLabAmount+=bill.NetAmount;
            if(bill.BillType=="Pharmacy") totalPharmacyAmount+=bill.NetAmount;
            if(bill.BillType=="OPD") totalOpdAmount+=hospitalNet;
        }

        return Ok(new{isMultiDay=multi,opdCount=await visits.CountAsync(x=>!x.IsEmergency),emergencyCount=await visits.CountAsync(x=>x.IsEmergency),currentlyAdmitted=multi?(int?)null:await db.IpdAdmissions.CountAsync(x=>x.Status=="Admitted"),beds=multi?null:new{occupied,total=totalBeds,available=totalBeds-occupied},numberOfLabTests=await labs.SelectMany(x=>x.Tests).CountAsync(),totalUnpaidAmount,totalPaidAmount,totalLabAmount,totalPharmacyAmount,totalOpdAmount,doctorConsultationAmount});
    }
}
