using KrishavERP.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrishavERP.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/discounts")]
public class DiscountsController : ControllerBase
{
    private readonly AppDbContext _db;
    public DiscountsController(AppDbContext db){_db=db;}

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? scope,[FromQuery] bool activeOnly=false)
    {
        var query=_db.DiscountTypes.AsQueryable();
        if(activeOnly) query=query.Where(x=>x.IsActive);
        if(!string.IsNullOrWhiteSpace(scope)) query=query.Where(x=>x.Scope==scope);
        return Ok(await query.OrderBy(x=>x.Scope).ThenBy(x=>x.Name).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Add(DiscountTypeRequest request)
    {
        var error=Validate(request); if(error!=null)return BadRequest(new{message=error});
        if(await _db.DiscountTypes.AnyAsync(x=>x.Name==request.Name.Trim())) return Conflict(new{message="Discount name already exists."});
        var row=new DiscountType{Name=request.Name.Trim(),DiscountMode=request.DiscountMode,Value=request.Value,Scope=request.Scope,IsActive=request.IsActive};
        _db.DiscountTypes.Add(row); await _db.SaveChangesAsync(); return Ok(row);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id,DiscountTypeRequest request)
    {
        var row=await _db.DiscountTypes.FindAsync(id); if(row==null)return NotFound();
        var error=Validate(request); if(error!=null)return BadRequest(new{message=error});
        if(await _db.DiscountTypes.AnyAsync(x=>x.Id!=id&&x.Name==request.Name.Trim())) return Conflict(new{message="Discount name already exists."});
        row.Name=request.Name.Trim();row.DiscountMode=request.DiscountMode;row.Value=request.Value;row.Scope=request.Scope;row.IsActive=request.IsActive;
        await _db.SaveChangesAsync(); return Ok(row);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var row=await _db.DiscountTypes.FindAsync(id); if(row==null)return NotFound();
        row.IsActive=false; await _db.SaveChangesAsync(); return Ok();
    }

    private static string? Validate(DiscountTypeRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.Name))return "Discount name is required.";
        if(request.DiscountMode is not ("Percent" or "Amount"))return "Discount must be Percent or Amount.";
        if(request.Scope is not ("Individual" or "Bulk"))return "Discount scope must be Individual or Bulk.";
        if(request.Value<0)return "Discount value cannot be negative.";
        if(request.DiscountMode=="Percent"&&request.Value>100)return "Percentage discount cannot exceed 100%.";
        return null;
    }
}
