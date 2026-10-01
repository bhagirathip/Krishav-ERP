namespace KrishavERP.Api.Services;

// Uploaded branding images (logo, headers, signatures) are always saved to the
// same fixed filename per setting (see SettingsController.Image), so a
// re-upload keeps the exact same URL - browsers then keep showing the old
// cached copy until a hard refresh. Appending the file's own last-write time
// as a "?v=" query string gives every re-upload a fresh URL without needing
// any new database column.
public static class AssetVersioning
{
    public static string Stamp(IWebHostEnvironment env, string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith("/"))
        {
            return value ?? "";
        }

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var physicalPath = Path.Combine(webRoot, value.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(physicalPath))
        {
            return value;
        }

        var version = File.GetLastWriteTimeUtc(physicalPath).Ticks;
        var separator = value.Contains('?') ? "&" : "?";
        return $"{value}{separator}v={version}";
    }
}
