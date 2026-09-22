using System.Security.Claims;
using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/referrals")]
public class ReferralsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReferralsController(AppDbContext db){_db=db;}

    [HttpGet("sources")]
    public async Task<IActionResult> GetSources()
    {
        var sources = await _db.PatientSources
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync();

        return Ok(sources);
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(await _db.Referrers.Where(x=>x.IsActive).OrderBy(x=>x.Name).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Add(ReferrerRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.Name))return BadRequest(new{message="Referrer name is required."});
        var row=new Referrer{Name=request.Name.Trim(),Phone=request.Phone?.Trim(),Address=request.Address?.Trim(),ReferrerType=request.ReferrerType,DoctorId=request.DoctorId,IsActive=request.IsActive};
        _db.Referrers.Add(row); await _db.SaveChangesAsync(); row.ReferrerCode=$"KHC-R-{row.Id:0000}"; await _db.SaveChangesAsync(); return Ok(row);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id,ReferrerRequest request)
    {
        var row=await _db.Referrers.FindAsync(id);if(row==null)return NotFound();
        if(string.IsNullOrWhiteSpace(request.Name))return BadRequest(new{message="Referrer name is required."});
        row.Name=request.Name.Trim();row.Phone=request.Phone?.Trim();row.Address=request.Address?.Trim();row.ReferrerType=request.ReferrerType;row.DoctorId=request.DoctorId;row.IsActive=request.IsActive;
        await _db.SaveChangesAsync();return Ok(row);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row=await _db.Referrers.FindAsync(id);if(row==null)return NotFound();row.IsActive=false;await _db.SaveChangesAsync();return Ok();
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery]DateTime? from,[FromQuery]DateTime? to,[FromQuery]int? referrerId,[FromQuery]string? department)
    {
        var start=(from??DateTime.Today).Date;var end=(to??DateTime.Today).Date.AddDays(1);
        var refs=await _db.Referrers.Where(x=>x.IsActive&&(!referrerId.HasValue||x.Id==referrerId.Value)).OrderBy(x=>x.Name).ToListAsync();
        var categories=await _db.BillTypes.Where(x=>x.IsActive&&x.IsBillable).OrderBy(x=>x.Id).Select(x=>x.Name).ToListAsync();
        var result=new List<object>();
        foreach(var r in refs)
        {
            var patients=await _db.Patients.Where(x=>!x.IsDeleted&&x.ReferrerId==r.Id).OrderBy(x=>x.Name).ToListAsync();
            var patientRows=new List<object>();
            decimal refTotal=0,refPayout=0;
            foreach(var p in patients)
            {
                var bills=await _db.Bills.Where(x=>x.Status!="Deleted"&&x.PatientId==p.Id&&x.CreatedAtUtc>=start&&x.CreatedAtUtc<end&&
                    (string.IsNullOrWhiteSpace(department)||department=="All"||x.BillType==department)).ToListAsync();
                var income=categories.ToDictionary(c=>c,c=>bills.Where(x=>x.BillType.Equals(c,StringComparison.OrdinalIgnoreCase)).Sum(x=>x.NetAmount));
                var total=bills.Sum(x=>x.NetAmount);
                var payout=await _db.ReferralPayouts.FirstOrDefaultAsync(x=>x.ReferrerId==r.Id&&x.PatientId==p.Id);
                refTotal+=total;refPayout+=payout?.Amount??0;
                patientRows.Add(new
                {
                    PatientId=p.Id,
                    p.PatientCode,
                    p.Name,
                    p.Phone,
                    p.MarketingSource,
                    Income=income,
                    TotalIncome=total,
                    ApprovedPayout=payout?.Amount??0,
                    PaymentDate=payout?.PaymentDate,
                    PaymentMode=payout?.PaymentMode??"",
                    ReferenceNumber=payout?.ReferenceNumber??"",
                    PayoutNotes=payout?.Notes??""
                });
            }
            result.Add(new{ReferrerId=r.Id,r.ReferrerCode,r.Name,r.ReferrerType,ReferredCount=patients.Count,TotalIncome=refTotal,ApprovedPayout=refPayout,Patients=patientRows});
        }
        return Ok(new{CanApprovePayout=await IsAdministrator(),Categories=categories,Rows=result});
    }

    [HttpPost("payout")]
    public async Task<IActionResult> SavePayout(ReferralPayoutRequest request)
    {
        if(!await IsAdministrator())return Forbid();
        if(request.Amount<0)return BadRequest(new{message="Payout cannot be negative."});
        if(!await _db.Patients.AnyAsync(x=>x.Id==request.PatientId&&x.ReferrerId==request.ReferrerId))return BadRequest(new{message="Patient is not linked to this referrer."});
        var row=await _db.ReferralPayouts.FirstOrDefaultAsync(x=>x.ReferrerId==request.ReferrerId&&x.PatientId==request.PatientId);
        if(row==null){row=new ReferralPayout{ReferrerId=request.ReferrerId,PatientId=request.PatientId};_db.ReferralPayouts.Add(row);}
        row.Amount=request.Amount;
        row.PaymentDate=request.PaymentDate;
        row.PaymentMode=request.PaymentMode?.Trim();
        row.ReferenceNumber=request.ReferenceNumber?.Trim();
        row.Notes=request.Notes;
        row.UpdatedAtUtc=DateTime.UtcNow;
        if(int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var userId))row.ApprovedByUserId=userId;
        await _db.SaveChangesAsync();return Ok(row);
    }

    [HttpGet("marketing-summary")]
    public async Task<IActionResult> MarketingSummary([FromQuery]DateTime? from,[FromQuery]DateTime? to)
    {
        var start=(from??DateTime.Today).Date;var end=(to??DateTime.Today).Date.AddDays(1);
        var patients=await _db.Patients.Where(x=>!x.IsDeleted&&x.CreatedAtUtc>=start&&x.CreatedAtUtc<end).ToListAsync();
        var items=patients.GroupBy(x=>string.IsNullOrWhiteSpace(x.MarketingSource)?"Walk-in":x.MarketingSource)
            .Select(g=>new{Source=g.Key,Patients=g.Count()}).OrderByDescending(x=>x.Patients).ToList();
        return Ok(items);
    }

    private async Task<bool> IsAdministrator()
    {
        if(!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id))return false;
        var roleId=await _db.AppUsers.Where(x=>x.Id==id).Select(x=>x.RoleId).FirstOrDefaultAsync();
        return roleId.HasValue&&await _db.AppRoles.AnyAsync(x=>x.Id==roleId.Value&&x.Name=="Administrator"&&x.IsActive);
    }
}
