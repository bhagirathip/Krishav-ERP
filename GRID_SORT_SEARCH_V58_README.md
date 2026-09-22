# v5.8 Grid Search / Sort / Bill Filter

## Common grid behavior
Main ERP list grids now use a shared `useGrid` composable.

Features:
- Search all loaded fields from one textbox
- Click a column heading to sort ascending/descending
- Pagination is applied after search and sort
- Default page size remains 10 rows

Applied to main grids in:
- Doctor Master
- Patient
- OPD
- Bill
- IPD
- OT
- Bed / Cabin Master
- Default Settings
- Distributor Master
- Pharmacy Purchase
- Pharmacy Sales inventory / bill search / summary tables
- Lab Test Master
- Patient Lab Tests
- Role / User lists
- Reporting doctor / patient tables

Line-entry tables inside Add/Edit modals are not paginated because they are active editing surfaces rather than list grids.

## Bill filters
Bill now has independent filters:
- Status: Unpaid / Paid
- Bill Type: All Bill Types / OPD / IPD / Lab / Pharmacy / Emergency / OT / any configured report category
- Search all bill fields

The bill type dropdown is populated from Report Category Master.

`All Bill Types` is selected by default.

## Bill search data
`BillsController` now also returns patient name, patient code, patient phone and doctor name with each bill list item so the global bill search can find bills using patient/doctor information.
