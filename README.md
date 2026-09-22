# Krishav Health Care ERP v1

Clean hospital ERP starter built from scratch around 8 modules: Dashboard, Doctor Master, Patient, Bill, IPD, OT Management, Pharmacy and Lab.

Stack: ASP.NET Core 8 Web API + EF Core, Vue 3 + Vite, SQL Server, JWT.

## Run
1. `docker compose up -d`
2. `cd backend/KrishavERP.Api && dotnet restore && dotnet run`
3. `cd frontend && npm install && npm run dev`

Backend: http://localhost:5000
Swagger: http://localhost:5000/swagger
Frontend: http://localhost:5173 (or another Vite localhost port; backend CORS allows localhost development ports)

Login: admin / Admin@123

## Production / IIS hosting (v6)
The app now resolves its own API URL at runtime instead of a hardcoded `http://localhost:5000`, so the same build works under any hostname or IP IIS assigns, with no code change or rebuild when that hostname/IP changes later.

**Option A - two IIS sites (matches local dev, no config needed):** frontend as one site (any port), API as another site/process on port 5000, both on the same server. By default the frontend calls the API at its own hostname/IP on port 5000, so nothing needs to be set up - just:
1. `cd frontend && npm run build` and point an IIS site at `frontend/dist`.
2. `cd backend/KrishavERP.Api && dotnet publish -c Release` and point another IIS site/app at the publish folder (port 5000), or run it as a Windows Service / `dotnet run` bound to port 5000.

If the API is *not* on port 5000 or is on a different server entirely, edit `config.js` in the deployed frontend folder and set `apiBaseUrl` to its exact URL (e.g. `"http://192.168.1.50:6000"`) - no rebuild needed, takes effect on next page load.

**Option B - one merged IIS site:** copy everything from `frontend/dist/` into the API's publish folder's `wwwroot/` (merge with the existing `wwwroot/uploads`) and point a single IIS site at the publish folder - the API then serves both the SPA and `/api/*` from the same origin/port. This needs one extra setting: edit `wwwroot/config.js` and set `apiBaseUrl: ''` (empty string), otherwise the frontend will still look for the API on port 5000 instead of the merged site's own port.

Either way, hard-reloading a deep link (e.g. `/patients`) needs the IIS URL Rewrite module for the SPA fallback rule in `web.config` (included in `frontend/dist`) - for the merged option this is handled automatically by the backend instead (see `Program.cs`).

Rules implemented:
- Hospital charge ₹100 (setting), emergency charge ₹500 (setting).
- Emergency replaces doctor consultation charge; hospital charge still applies.
- Duplicate phone numbers are allowed.
- Patient deletion is soft-delete.
- Partial payment remains in Unpaid.
- Pharmacy stock reduces only after full payment.
- OT 1 and OT 2 overlapping bookings are blocked per room.
- Blank prescription is white with no horizontal writing lines.


## v2 workflow changes
- Dashboard now reports OPD(s) and Emergency(es), and hides Current Admitted/Beds when a multi-day range is selected.
- New OPD menu owns appointments, repeat visits, blank prescription printing and OPD→IPD conversion.
- Patient modal has a Type dropdown: OPD / Emergency / IPD / OT. OPD/IPD request doctor; IPD also requests a bed.
- Initial OPD registration creates consultation + hospital charge automatically. Other patient registration types do not create a bill.
- OPD repeat appointment explicitly asks whether a consultation bill should be created.
- Billing supports all six bill types and one discount value controlled by %/₹ mode.
- IPD detail uses one large modal with vitals history, doctor suggestions, nursing activities and lab ordering.
- Lab master and patient lab order/results are separated so every patient/test occurrence has independent results.

## Database note
This v2 changes the schema. For a local development database, the safest clean start is to drop `KrishavERPv1` and let the API recreate it with `EnsureCreated()`, or run `database/01_schema.sql` against a fresh SQL Server.


## v3 changes
- Doctor Master: added IPD Charge.
- Patient: IPD shows doctor + bed/cabin; OT shows doctor + OT room/time; Emergency requires doctor; multiple documents supported.
- Duplicate phone validation: API returns matching patient(s) before insert and UI lets staff add a new patient anyway or book a new OPD for an existing patient.
- OPD: Emergency visit option added to new booking; Problem removed from list view; prescription opens directly in a print window and closes after print; payer type added for OPD→IPD.
- Default Settings menu: generic Name/Value/Type entries plus hospital logo upload. Seeded Emergency Rate, Hospital Charge, branding, oxygen charges.
- Bed/Cabin Master: Cash / Insurance / Ayushman rates.
- Bill: patient required; individual line discounts and bulk discounts; Lab/OPD catalog suggestions with manual description support; payment still blocks overpayment.
- IPD: payer type retained; one large modal; multiple IPD bills; bed/cabin rate and doctor IPD visit charge can be used as starting lines; lab ordering remains available.
- Lab: master note field; patient test selection is tabular with individual discount per test; every lab order automatically creates a bill; individual test result printing added.
- Role & Permission: role creation, module View/Add/Edit/Delete flags, sidebar/action visibility, and user-role assignment.

### Database
This version uses a NEW local development database name: `KrishavERPv3`.
On first API start, EF Core `EnsureCreated()` creates the v3 schema and the seeder creates:
- Administrator role with all permissions
- admin / Admin@123
- default settings
- 18 default beds

Do not point this version at your old KrishavERPv1/v2 database without a migration.


## v4 changes
- Patient/OPD vitals: BP, Temperature, Weight, Height and SpO2 with frontend + backend validation.
- Problem renamed to Chief Complaint.
- OPD visit type: OPD / Emergency Visit / Appointment.
  - OPD and Emergency use current time.
  - Appointment requires a future date/time.
- Blank Prescription uses the uploaded OPD Header and prints doctor specialisation plus all vitals; Chief Complaint is intentionally not printed.
- Bill editor supports removing sub-items, individual + bulk discount totals and bill printing with the appropriate uploaded header and paid/outstanding amounts.
- IPD row now exposes four actions: IPD Details, Add Bill, Add Lab, Add Pharmacy.
- IPD lab entry supports multiple dropdown rows with price + item discount and automatically creates the Lab bill.
- Lab patient test entry is dropdown-based with multiple test rows, per-test discounts and before/after totals.
- Lab reports use the uploaded Lab Header.
- Default Settings supports images and has OPD Header, Hospital Logo, Lab Header and Pharmacy Header.
- Role & Permission supports creating users with username/password and assigning roles.
- Dashboard cards are permission-aware.
- Reporting menu added with Doctor-wise and Patient-wise income breakdowns across OPD/IPD/Lab/Pharmacy/OT and graphical bar summaries.
- REPORTING is included in role permissions.

## Database
v4 points to a fresh local database: `KrishavERPv4`.
The API uses EF Core EnsureCreated on first run.

## Validation ranges currently enforced
- Indian mobile: 10 digits starting 6-9
- BP: `NN/NN` or `NNN/NNN` format
- Temperature: 30–45 °C
- Height: 30–250 cm
- Weight: 1–500 kg
- SpO2: 50–100 %


## v5.2 changes
- Patient Document Category Master with multiple files per selected category.
- Patient and OPD lists no longer show vitals.
- OPD prescription printing uses an invisible print frame, full-width OPD header image, no visible prescription title, doctor specialisation and writable blank vital fields.
- Bill totals standardized: before discount, individual discount, after individual discount, bulk discount, after discount, paid and outstanding.
- Bill print uses full-width uploaded header and the same totals layout.
- Report Category Master seeded with OPD, IPD, Lab, Pharmacy, Emergency, OT, Dressing and Dental. Billing uses the same master.
- Bills now include optional DoctorId for accurate doctor-wise income attribution.
- Reporting supports All/individual Doctor and Patient filters, doctor consultation counts, unique patient counts, category-wise income and income-share pie chart.
- New local development DB: KrishavERPv52.
