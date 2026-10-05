using Microsoft.EntityFrameworkCore;
using KrishavERP.Api.Modules.Billing;
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

    // Bulk Discount is a bill-level slot visible to every role. Ordinary
    // active Bulk-scope discounts from Discount Master (e.g. "Package
    // Discount") can be applied by anyone, same as before "Manager
    // Discount"/"Admin Discount" existed. Those two are the only
    // role-restricted entries: each lets the user type their own
    // %/Amount, capped per role (Manager's cap lives in Settings so it can
    // change without editing the Discount Master; Admin's is a fixed 100%
    // i.e. no real cap). A General Manager never picks a discount type at
    // all - they get a raw %/amount box instead, capped by its own Settings
    // value.
    public async Task<ResolvedDiscount> ResolveBulkAsync(decimal gross,int? discountTypeId,decimal? manualValue,string? manualMode,string? currentRole)
    {
        if(discountTypeId.HasValue)
        {
            var type=await _db.DiscountTypes.FirstOrDefaultAsync(x=>x.Id==discountTypeId.Value&&x.IsActive);
            if(type==null) throw new InvalidOperationException("Selected discount is not active or does not exist.");
            if(!type.Scope.Equals("Bulk",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{type.Name} is a {type.Scope} discount and cannot be used as Bulk.");

            if(type.Name=="Admin Discount"||type.Name=="Manager Discount")
            {
                if(type.Name=="Admin Discount"&&currentRole!="Administrator")
                    throw new InvalidOperationException("Only Administrator can apply Admin Discount.");
                if(type.Name=="Manager Discount"&&currentRole!="Manager"&&currentRole!="Administrator")
                    throw new InvalidOperationException("Only Manager or Administrator can apply Manager Discount.");

                var cap=type.Name=="Admin Discount"
                    ?100m
                    :await GetMaxPercentSettingAsync("Manager Discount Max Percent",15m);

                var mode=manualMode=="Amount"?"Amount":"Percent";
                decimal amount,percent;
                if(mode=="Amount")
                {
                    var maxAmount=Math.Round(gross*cap/100m,2);
                    var requested=Math.Max(0,manualValue??0);
                    if(requested>maxAmount)
                        throw new InvalidOperationException($"{type.Name} amount cannot exceed Rs.{maxAmount:0.00} ({cap}% of the bill).");
                    amount=requested;
                    percent=gross==0?0:Math.Round(amount*100m/gross,2);
                }
                else
                {
                    percent=Math.Clamp(manualValue??0,0,100);
                    if(percent>cap) throw new InvalidOperationException($"{type.Name} cannot exceed {cap}%.");
                    amount=Math.Round(gross*percent/100m,2);
                }
                return new ResolvedDiscount(type.Id,type.Name,mode,percent,percent,amount,gross-amount);
            }

            // Any other active Bulk-scope discount from Discount Master is a
            // plain fixed-value discount available to every role - only
            // Manager Discount/Admin Discount above are role-restricted.
            var value=Math.Max(0,type.Value);
            decimal fixedAmount,fixedPercent;
            if(type.DiscountMode.Equals("Amount",StringComparison.OrdinalIgnoreCase))
            {
                fixedAmount=Math.Min(gross,value);
                fixedPercent=gross==0?0:Math.Round(fixedAmount*100m/gross,2);
            }
            else
            {
                fixedPercent=Math.Clamp(value,0,100);
                fixedAmount=Math.Round(gross*fixedPercent/100m,2);
            }
            return new ResolvedDiscount(type.Id,type.Name,type.DiscountMode,value,fixedPercent,fixedAmount,gross-fixedAmount);
        }

        if(manualValue.HasValue&&manualValue.Value>0)
        {
            if(currentRole!="General Manager")
                throw new InvalidOperationException("Only General Manager can apply a discount without selecting a discount type.");

            var cap=await GetMaxPercentSettingAsync("General Manager Discount Max Percent",10m);
            var mode=manualMode=="Amount"?"Amount":"Percent";
            decimal amount,percent;
            if(mode=="Amount")
            {
                var maxAmount=Math.Round(gross*cap/100m,2);
                if(manualValue.Value>maxAmount)
                    throw new InvalidOperationException($"Discount amount cannot exceed Rs.{maxAmount:0.00} ({cap}% of the bill).");
                amount=manualValue.Value;
                percent=gross==0?0:Math.Round(amount*100m/gross,2);
            }
            else
            {
                if(manualValue.Value>cap)
                    throw new InvalidOperationException($"Discount percentage cannot exceed {cap}%.");
                percent=manualValue.Value;
                amount=Math.Round(gross*percent/100m,2);
            }
            return new ResolvedDiscount(null,"General Manager Discount",mode,percent,percent,amount,gross-amount);
        }

        return ResolvedDiscount.None(gross);
    }

    private async Task<decimal> GetMaxPercentSettingAsync(string name,decimal fallback)
    {
        var raw=await _db.AppSettings.Where(x=>x.Name==name&&x.IsActive).Select(x=>x.Value).FirstOrDefaultAsync();
        return decimal.TryParse(raw,out var parsed)?parsed:fallback;
    }
}

public record ResolvedDiscount(int? Id,string? Name,string Mode,decimal Value,decimal Percent,decimal Amount,decimal Net)
{
    public static ResolvedDiscount None(decimal gross)=>new(null,null,"Percent",0,0,0,gross);
}
