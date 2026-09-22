# v5.6 Lab Dynamic Result Update

## Result behavior
View/Edit Result now uses exactly the columns configured in Lab Test Master.

Example master:
- Parameter
- Range
- Unit
- Method
- Remarks

The result modal shows exactly those five columns.
No additional Result column is appended.

Every cell is editable for that patient/test.

## Snapshot behavior
When a test is ordered, the current Lab Test Master dynamic table is copied into:
`LabOrderTests.ResultSchemaJson`

This prevents later master changes from changing previously ordered patient results.

## Printing
Print Result uses the same dynamic columns and values saved for that patient.

## Database
Run:
`database/11_lab_dynamic_result_snapshot.sql`

against `KrishavERPv52`.
