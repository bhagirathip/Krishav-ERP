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
[Route("api/beds")]
public class BedsController:ControllerBase
{
    private readonly AppDbContext db;
    public BedsController(AppDbContext db)=>this.db=db;
    [HttpGet] public async Task<IActionResult> Get()=>Ok(await db.WardBeds.Where(x=>x.IsActive).OrderBy(x=>x.BedNumber).ToListAsync());
    [HttpPost] public async Task<IActionResult> Add(WardBed x)
    {
        if(string.IsNullOrWhiteSpace(x.BedNumber))return BadRequest(new{message="Bed/Cabin number is required."});
        db.WardBeds.Add(x);
        await db.SaveChangesAsync();
        return Ok(x);
    }
    [HttpPut("{id}")] public async Task<IActionResult> Edit(int id,WardBed x)
    {
        var e=await db.WardBeds.FindAsync(id);
        if(e==null)return NotFound();
        e.BedNumber=x.BedNumber;
        e.RoomType=x.RoomType;
        e.CashRate=x.CashRate;
        e.InsuranceRate=x.InsuranceRate;
        e.AyushmanRate=x.AyushmanRate;
        e.IsActive=x.IsActive;
        await db.SaveChangesAsync();
        return Ok(e);
    }
    [HttpDelete("{id}")] public async Task<IActionResult> Delete(int id)
    {
        var e=await db.WardBeds.FindAsync(id);
        if(e==null)return NotFound();
        if(e.IsOccupied)return BadRequest(new{message="Occupied bed/cabin cannot be deleted."});
        e.IsActive=false;
        await db.SaveChangesAsync();
        return Ok();
    }
}
