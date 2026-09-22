using System.Text;
using System.Text.Json.Serialization;
using KrishavERP.Api;
using KrishavERP.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Bind to all network interfaces (not just localhost) so the API is reachable
// by machine IP/hostname, not only from the box it runs on. The port stays
// overridable via appsettings ("Urls") or the ASPNETCORE_URLS env var without
// a rebuild. Under IIS in-process hosting this is ignored (IIS/ANCM controls
// the binding), so it only matters when running via `dotnet run`/Kestrel directly.
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://0.0.0.0:5000");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddCors(options =>
{
    // The site can be reached under any hostname/IP the IIS admin later chooses
    // (there is no fixed production origin to allow-list), so any origin is
    // reflected back rather than hardcoding localhost. The frontend is expected
    // to be served from the same origin as the API in production anyway
    // (see the SPA fallback below); this policy is mainly a safety net for
    // cross-origin setups (e.g. the API reachable on a different host/port).
    options.AddPolicy("AppCors", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<FollowUpService>();
builder.Services.AddScoped<KrishavERP.Api.Services.DiscountService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AppCors");

// Serves wwwroot (uploaded logos/headers/documents) and, when the built Vue app
// (frontend/dist) is copied into wwwroot as part of publishing, the SPA itself -
// same origin as the API, so no CORS or hardcoded host/port is needed in the
// browser regardless of what hostname/IP this is deployed under.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SPA client-side routing fallback: a hard reload/deep link (e.g. /patients)
// has no matching physical file or controller route, so serve index.html and
// let Vue Router resolve it. Without this, reloading anything but "/" 404s.
if (File.Exists(Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html")))
{
    app.MapFallbackToFile("index.html");
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    //db.Database.EnsureCreated();
    //DbSeeder.Seed(db);
}

app.Run();
