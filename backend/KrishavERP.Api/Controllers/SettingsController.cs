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
[Route("api/settings")]
public class SettingsController:ControllerBase
{
    private readonly AppDbContext db;
    private readonly IWebHostEnvironment env;
    public SettingsController(AppDbContext db,IWebHostEnvironment env)
    {
        this.db=db;
        this.env=env;
    }
    [AllowAnonymous,HttpGet("branding")]
    public async Task<IActionResult> Branding()
    {
        var names=new[]
        {
            "Hospital Name","Hospital Logo","Hospital Address","Hospital Phone","OPD Header","Lab Header","Pharmacy Header"
        }
        ;
        return Ok(await db.AppSettings.Where(x=>x.IsActive&&names.Contains(x.Name)).ToListAsync());
    }
    [HttpGet]
    public async Task<IActionResult> Get()=>Ok(await db.AppSettings.Where(x=>x.IsActive).OrderBy(x=>x.Type).ThenBy(x=>x.Name).ToListAsync());
    [HttpPost]
    public async Task<IActionResult> Add(AppSetting x)
    {
        if(string.IsNullOrWhiteSpace(x.Name))return BadRequest(new{message="Setting name is required."});
        if(await db.AppSettings.AnyAsync(s=>s.Name==x.Name))return Conflict(new{message="A setting with this name already exists."});
        db.AppSettings.Add(x);
        await db.SaveChangesAsync();
        return Ok(x);
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> Edit(int id,AppSetting x)
    {
        var e=await db.AppSettings.FindAsync(id);
        if(e==null)return NotFound();
        e.Name=x.Name;
        e.Value=x.Value;
        e.Type=x.Type;
        e.IsActive=x.IsActive;
        await db.SaveChangesAsync();
        return Ok(e);
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var e=await db.AppSettings.FindAsync(id);
        if(e==null)return NotFound();
        e.IsActive=false;
        await db.SaveChangesAsync();
        return Ok();
    }
    [HttpPost("image")]
    public async Task<IActionResult> Image(IFormFile file,[FromForm]string name)
    {
        if(file==null||file.Length==0)return BadRequest(new{message="Please select an image file."});
        if(string.IsNullOrWhiteSpace(name))return BadRequest(new{message="Setting name is required."});
        var ext=Path.GetExtension(file.FileName).ToLowerInvariant();
        if(!new[]{".png",".jpg",".jpeg",".webp"}.Contains(ext))return BadRequest(new{message="Image must be PNG, JPG or WEBP."});
        var web=env.WebRootPath??Path.Combine(env.ContentRootPath,"wwwroot");
        Directory.CreateDirectory(web);
        var safe=Regex.Replace(name.ToLowerInvariant(),"[^a-z0-9]+","-").Trim('-');
        var path=Path.Combine(web,safe+ext);
        using(var fs=System.IO.File.Create(path))await file.CopyToAsync(fs);
        var relative="/"+safe+ext;
        var setting=await db.AppSettings.FirstOrDefaultAsync(x=>x.Name==name);
        if(setting==null)db.AppSettings.Add(new AppSetting{Name=name,Value=relative,Type="Branding"});
        else
        {
            setting.Value=relative;
            setting.Type="Branding";
            setting.IsActive=true;
        }
        await db.SaveChangesAsync();
        return Ok(new{path=relative,name});
    }
}
