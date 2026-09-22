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
[Route("api/doctors")]
public class DoctorsController:ControllerBase
{
    private readonly AppDbContext db;
    public DoctorsController(AppDbContext db)=>this.db=db;
    [HttpGet] public async Task<IActionResult> Get()=>Ok(await db.Doctors.OrderBy(x=>x.Name).ToListAsync());
    [HttpPost] public async Task<IActionResult> Add(Doctor x)
    {
        if(string.IsNullOrWhiteSpace(x.Name))return BadRequest(new{message="Doctor name is required."});
        if(string.IsNullOrWhiteSpace(x.Specialisation))return BadRequest(new{message="Specialisation is required."});
        if(x.ConsultationCharge<0||x.IpdCharge<0)return BadRequest(new{message="Charges cannot be negative."});
        db.Doctors.Add(x);
        await db.SaveChangesAsync();
        return Ok(x);
    }
    [HttpPut("{id}")] public async Task<IActionResult> Edit(int id,Doctor x)
    {
        var e=await db.Doctors.FindAsync(id);
        if(e==null)return NotFound();
        e.Name=x.Name;
        e.Specialisation=x.Specialisation;
        e.ConsultationCharge=x.ConsultationCharge;
        e.IpdCharge=x.IpdCharge;
        e.IsActive=x.IsActive;
        await db.SaveChangesAsync();
        return Ok(e);
    }
    [HttpDelete("{id}")] public async Task<IActionResult> Delete(int id)
    {
        var e=await db.Doctors.FindAsync(id);
        if(e==null)return NotFound();
        e.IsActive=false;
        await db.SaveChangesAsync();
        return Ok();
    }
}
