namespace KrishavERP.Api.Modules.Settings;

public class AppSetting
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";

    // Billing, Branding, Generic
    public string Type { get; set; } = "Billing";
    public bool IsActive { get; set; } = true;
}
