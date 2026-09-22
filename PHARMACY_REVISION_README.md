# Pharmacy Revision

## Purchase changes
- Discount is now a flat rupee amount, not percentage.
- Formula: `(Rate × Quantity) - Discount = Taxable Amount`.
- CGST and SGST are calculated on the Taxable Amount.
- Invoice Sl No removed from the UI/API.
- Distributor invoice document upload added.
- Supported uploads: PDF, JPG/JPEG, PNG, WEBP, XLS/XLSX, CSV, DOC/DOCX.
- `TabletsPerStrip` added so stock can be sold as full strips or loose tablets.
- Purchase quantity remains number of strips/packs.
- Bonus adds free strips/packs to inventory but not to invoice value.

## Sales changes
Three tabs:
1. Search Inventory
2. Create Bill
3. Pharmacy Summary

Search Inventory is view-only.

Create Bill:
- Search and add medicine from inventory.
- MRP is the strip price.
- Choose sale unit: Strip or Tablet.
- Tablet price = MRP / TabletsPerStrip.
- Enter quantity.
- Enter flat discount amount.
- Total = (unit price × quantity) - discount.
- Stock is reduced in tablets so partial-strip sales work correctly.

Example:
- MRP per strip: ₹30
- 15 tablets per strip
- Tablet price: ₹2
- Sell 5 tablets
- Gross: ₹10
- Discount: ₹1
- Total: ₹9

## Sale bill output
From Pharmacy Summary each sale bill can be:
- Printed
- Downloaded as CSV
- Downloaded as Excel-compatible `.xls`

## Database
Fresh pharmacy setup:
- Run `06_pharmacy_purchase_sales_upgrade.sql`

If the earlier Pharmacy Purchase/Sales script was already run:
- Run `07_pharmacy_strip_tablet_revision.sql`
