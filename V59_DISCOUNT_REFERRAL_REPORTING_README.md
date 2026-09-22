# Krishav ERP v5.9 - Discount, Referral & Executive Reporting

## Discount Master
New `Discounts` menu with controlled discount definitions:
- Discount Name
- Percent or Amount
- Value
- Individual or Bulk
- Active / Inactive

Seeded names are created inactive with value 0 so management can configure the actual approved rates/amounts instead of the system guessing them.

Billing security rule: Bill, Lab, IPD and Pharmacy sale APIs only accept a Discount Type ID. They do not accept arbitrary manual discount values.

## Referral + Marketing
New `Referral + Marketing` menu.

Referrer master:
- auto code `KHC-R-0001`
- Name (required)
- Phone
- Address
- Person / Doctor
- optional link to Doctor Master

Patient attribution:
- Walk-in
- Existing Patient
- Doctor Referral
- Hospital Referral
- Staff Referral
- Patient Referral
- Google
- Facebook
- Instagram
- WhatsApp
- Health Camp
- Banner-Hoarding
- Corporate
- Insurance-TPA
- Ambulance
- Other

Each patient stores its source and optional referrer. New bills snapshot the patient's referrer.

Referral report shows:
- count referred
- patient-wise revenue
- department-wise revenue
- total revenue
- approved payout
- marketing-source counts

Only an Administrator can save approved referral payout amounts.

## Executive Reporting
Reporting now includes date presets:
- Today
- Yesterday
- This Week
- This Month
- Last Month
- Custom Date

Executive cards:
- OPD
- IPD
- Emergency
- Admissions
- Discharges
- Surgeries
- Bed Occupancy
- Lab Tests
- Pharmacy Bills
- Gross Revenue
- Discounts
- Net Revenue
- Collections
- Outstanding
- Expenses
- Operating Result

`Expenses` currently represents recorded Pharmacy Purchase invoice cost because a general Expense module does not yet exist.

A month-wise revenue progress section shows Gross, Discounts, Net Revenue, Collections, Expenses and Operating Result for all 12 months of the selected year.

## Database migration
Run:

`database/12_discount_referral_reporting.sql`

against the existing `KrishavERPv52` database.
