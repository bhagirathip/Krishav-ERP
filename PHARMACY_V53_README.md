# Krishav ERP Pharmacy v5.3 Update

## Distributor Master
New main menu under the Pharmacy permission.

Fields:
- Distributor Name
- Phone
- GST Number
- PAN
- DrugsBazaar ID
- FSSAI
- Address

## Medicine Type Master
Default types:
- Tablet
- Cream
- Injection
- Spray

Rules:
- Tablet: user can set tablets per strip. Sale can be Strip or Tablet.
- Cream: units per pack is forced to 1. Sale is by Unit.
- Spray: units per pack is forced to 1. Sale is by Unit.
- Injection: user can set vials per pack. Sale can be Pack or Vial.

## Tablet pricing example
If MRP is ₹132 per strip and there are 10 tablets per strip:
- 1 tablet price = 132 / 10 = ₹13.20
- 4 tablets = 13.20 × 4 = ₹52.80

## Pharmacy Sales patient handling
Existing patient:
- Select patient
- Patient details are displayed
- Doctor is preselected from the patient/visit data when available

Walk-in:
- Patient Name
- Phone
- Doctor Name

## Pagination
Main ERP grids use 10 rows per page with Previous/Next controls.
Nested editable line-item tables inside invoices/bills are intentionally not paginated.

## Existing database upgrade
Run:
- database/08_pharmacy_distributor_medicine_type_pagination.sql

This assumes the earlier Pharmacy migrations have already been applied.
