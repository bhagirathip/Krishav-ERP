# Krishav ERP Backend Structure

The backend is now organized by module.

## Controllers
- `AuthController.cs`
- `DashboardController.cs`
- `DoctorsController.cs`
- `PatientsController.cs`
- `OpdController.cs`
- `BillsController.cs`
- `IpdController.cs`
- `OtController.cs`
- `PharmacyController.cs`
- `LabController.cs`
- `BedsController.cs`
- `SettingsController.cs`
- `RolesController.cs`
- `ReportingController.cs`

## Shared
- `Program.cs` — startup, DI, JWT, CORS, Swagger
- `AppDbContext.cs` — EF Core database mapping
- `Models.cs` — database entities
- `Dtos.cs` — request models
- `Services/BillingMath.cs` — shared discount/billing calculations
- `Seed.cs` — initial roles, settings, beds, and admin user

## Local build

```powershell
cd backend\KrishavERP.Api
dotnet clean
dotnet restore
dotnet build
dotnet run
```

If an old failed build is cached, delete the `bin` and `obj` folders before rebuilding.

## Important
The old merged `Controllers.cs` file has been removed. There should be only one controller class per file under `Controllers/`.
