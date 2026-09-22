# v5.5 Pharmacy / Lab Update

- Pharmacy sidebar is collapsible.
- Pharmacy Sales tabs are ordered: Create Bill, Search Inventory, Pharmacy Summary.
- Create Bill is the default Sales tab.
- Lab is split into two submenu pages: Add Lab Test Master and Add Test Patient.
- Lab Test Master supports dynamic column names, dynamic values, dynamic rows, delete any row, delete any column, and Note at the bottom.
- Dynamic schema is stored in LabTests.SchemaJson.
- The first column becomes the patient-result component name; columns containing Range/Reference, Unit, or Default/Prefilled are mapped into the existing lab result workflow.

Run database/10_lab_dynamic_schema_and_menu.sql against KrishavERPv52.
