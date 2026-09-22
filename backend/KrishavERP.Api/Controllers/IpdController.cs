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
[Route("api/ipd")]
public class IpdController:ControllerBase
{
    private readonly AppDbContext db;
    public IpdController(AppDbContext db)=>this.db=db;
    [HttpGet] public async Task<IActionResult> Get()=>Ok(await (from a in db.IpdAdmissions where a.Status=="Admitted" join p in db.Patients on a.PatientId equals p.Id join d in db.Doctors on a.DoctorId equals d.Id join b in db.WardBeds on a.BedId equals b.Id select new{a.Id,a.AdmissionNumber,a.PatientId,PatientName=p.Name,DoctorName=d.Name,BedNumber=b.BedNumber,a.PayerType,a.AdmittedAtUtc,a.Status}).ToListAsync());
    [HttpGet("beds")] public async Task<IActionResult> Beds()=>Ok(await db.WardBeds.Where(x=>x.IsActive).OrderBy(x=>x.BedNumber).ToListAsync());
    [HttpGet("{id}/detail")]
    public async Task<IActionResult> Detail(int id)
    {
        var a=await db.IpdAdmissions.FindAsync(id);
        if(a==null)return NotFound();
        var p=await db.Patients.FindAsync(a.PatientId);
        var d=await db.Doctors.FindAsync(a.DoctorId);
        var bed=await db.WardBeds.FindAsync(a.BedId);
        var vitals=await db.IpdVitals.Where(x=>x.IpdAdmissionId==id).OrderByDescending(x=>x.RecordedAtUtc).ToListAsync();
        var dn=await (from n in db.IpdDoctorNotes where n.IpdAdmissionId==id join doc in db.Doctors on n.DoctorId equals doc.Id select new{n.Id,n.Suggestion,n.CreatedAtUtc,DoctorName=doc.Name}).OrderByDescending(x=>x.CreatedAtUtc).ToListAsync();
        var nn=await db.IpdNursingNotes.Where(x=>x.IpdAdmissionId==id).OrderByDescending(x=>x.CreatedAtUtc).ToListAsync();
        var labs=await db.LabOrders.Where(x=>x.IpdAdmissionId==id).OrderByDescending(x=>x.Id).ToListAsync();
        var bills=await db.Bills.Include(x=>x.Items).Where(x=>x.PatientId==a.PatientId&&x.BillType=="IPD"&&x.CreatedAtUtc>=a.AdmittedAtUtc).OrderByDescending(x=>x.Id).ToListAsync();
        return Ok(new{admission=a,patient=p,doctor=d,bed,vitals,doctorNotes=dn,nursingNotes=nn,labOrders=labs,bills});
    }
    [HttpPost("convert/{opdVisitId}")]
    public async Task<IActionResult> Convert(int opdVisitId,ConvertIpdRequest r)
    {
        using var tx=await db.Database.BeginTransactionAsync();
        var source=await db.OpdVisits.FindAsync(opdVisitId);
        if(source==null)return NotFound();
        if(source.ConvertedToIpd)return BadRequest(new{message="This OPD visit was already converted. Book a new OPD appointment first."});
        if(await db.IpdAdmissions.AnyAsync(x=>x.PatientId==source.PatientId&&x.Status=="Admitted"))return BadRequest(new{message="Patient is already admitted."});
        var bed=await db.WardBeds.FirstOrDefaultAsync(x=>x.Id==r.BedId&&x.IsActive&&!x.IsOccupied);
        if(bed==null)return BadRequest(new{message="Selected bed/cabin is unavailable."});
        var a=new IpdAdmission
        {
            PatientId=source.PatientId,SourceOpdVisitId=source.Id,DoctorId=r.DoctorId,BedId=r.BedId,PayerType=r.PayerType
        }
        ;
        db.IpdAdmissions.Add(a);
        await db.SaveChangesAsync();
        a.AdmissionNumber=$"IPD-{DateTime.UtcNow:yyyyMMdd}-{a.Id:000000}";
        bed.IsOccupied=true;
        bed.CurrentIpdAdmissionId=a.Id;
        source.ConvertedToIpd=true;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(a);
    }
    [HttpPost("{id}/vitals")] public async Task<IActionResult> Vital(int id,IpdVitalRequest r)
    {
        var x=new IpdVital
        {
            IpdAdmissionId=id,BloodPressure=r.BloodPressure,TemperatureC=r.TemperatureC,Pulse=r.Pulse,Spo2=r.Spo2,Notes=r.Notes
        }
        ;
        db.IpdVitals.Add(x);
        await db.SaveChangesAsync();
        return Ok(x);
    }
    [HttpPost("{id}/doctor-note")] public async Task<IActionResult> DNote(int id,IpdDoctorNoteRequest r)
    {
        var x=new IpdDoctorNote
        {
            IpdAdmissionId=id,DoctorId=r.DoctorId,Suggestion=r.Suggestion
        }
        ;
        db.IpdDoctorNotes.Add(x);
        await db.SaveChangesAsync();
        return Ok(x);
    }
    [HttpPost("{id}/nursing-note")] public async Task<IActionResult> NNote(int id,IpdNursingNoteRequest r)
    {
        var x=new IpdNursingNote
        {
            IpdAdmissionId=id,ActionTaken=r.ActionTaken
        }
        ;
        db.IpdNursingNotes.Add(x);
        await db.SaveChangesAsync();
        return Ok(x);
    }
}
