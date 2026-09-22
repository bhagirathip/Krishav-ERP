# v5.2 Backend changes

- Added `DocumentCategory` master and `DocumentCategoriesController`.
- Added `ReportCategory` master seeded with OPD, IPD, Lab, Pharmacy, Emergency, OT, Dressing and Dental.
- Added optional `DoctorId` to `Bill` and `BillCreateRequest` for doctor-wise reporting.
- Rewrote `PatientsController`, `OpdController`, `BillsController`, and `ReportingController` in clean modular C#.
- Fixed the previous Patient edit syntax issue.
- Bill totals now preserve before-discount gross, item discounts, bulk discount, net, paid and outstanding values.
- Reporting APIs support doctor/patient filters, consultation counts, unique patient counts, category-wise income and income share.
- Fresh local DB name: `KrishavERPv52`.
