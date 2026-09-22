Krishav ERP v3 database

This version uses database: KrishavERPv3.
The API uses EF Core EnsureCreated() on first run and seeds Administrator, permissions, defaults, and 18 beds.
Do not run the older v1/v2 schema SQL against KrishavERPv3 because the v3 entity model contains new Settings, Roles, IPD pricing, bill-line discount and lab-result fields.
For local development, simply start SQL Server and run the API.
