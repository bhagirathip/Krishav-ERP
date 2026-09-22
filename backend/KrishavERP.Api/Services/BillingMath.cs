namespace KrishavERP.Api.Services;

public static class BillingMath
{
    public static (decimal percent, decimal amount, decimal net) Discount(
        decimal gross,
        string mode,
        decimal value)
    {
        value = Math.Max(0, value);

        if (mode.Equals("Amount", StringComparison.OrdinalIgnoreCase))
        {
            var amount = Math.Min(gross, value);
            var percent = gross == 0
                ? 0
                : Math.Round(amount * 100m / gross, 2);

            return (percent, amount, gross - amount);
        }

        var percentValue = Math.Clamp(value, 0, 100);
        var discountAmount =
            Math.Round(gross * percentValue / 100m, 2);

        return (
            percentValue,
            discountAmount,
            gross - discountAmount);
    }

    public static (decimal discount, decimal amount) Line(
        decimal gross,
        string mode,
        decimal value)
    {
        var result = Discount(gross, mode, value);
        return (result.amount, result.net);
    }
}
