using Microsoft.EntityFrameworkCore;
namespace KrishavERP.Api.Services;

public sealed class DiscountService
{
    private readonly AppDbContext _db;
    public DiscountService(AppDbContext db){_db=db;}

    public async Task<ResolvedDiscount> ResolveAsync(decimal gross,int? discountTypeId,string scope)
    {
        if(!discountTypeId.HasValue) return ResolvedDiscount.None(gross);
        var type=await _db.DiscountTypes.FirstOrDefaultAsync(x=>x.Id==discountTypeId.Value&&x.IsActive);
        if(type==null) throw new InvalidOperationException("Selected discount is not active or does not exist.");
        if(!type.Scope.Equals(scope,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{type.Name} is a {type.Scope} discount and cannot be used as {scope}.");
        var value=Math.Max(0,type.Value);
        decimal amount;
        decimal percent;
        if(type.DiscountMode.Equals("Amount",StringComparison.OrdinalIgnoreCase))
        {
            amount=Math.Min(gross,value);
            percent=gross==0?0:Math.Round(amount*100m/gross,2);
        }
        else
        {
            percent=Math.Clamp(value,0,100);
            amount=Math.Round(gross*percent/100m,2);
        }
        return new ResolvedDiscount(type.Id,type.Name,type.DiscountMode,value,percent,amount,gross-amount);
    }
}

public record ResolvedDiscount(int? Id,string? Name,string Mode,decimal Value,decimal Percent,decimal Amount,decimal Net)
{
    public static ResolvedDiscount None(decimal gross)=>new(null,null,"Percent",0,0,0,gross);
}
