import { createRouter, createWebHistory } from 'vue-router';
import { can } from './auth';
import Dashboard from './views/Dashboard/Dashboard.vue';
import Doctors from './views/Master/Doctors.vue';
import Patients from './views/Patient/Patients.vue';
import Opd from './views/Opd/Opd.vue';
import Bills from './views/Bill/Bills.vue';
import Ipd from './views/Ipd/Ipd.vue';
import Ot from './views/Ot/Ot.vue';
import PharmacyPurchase from './views/Pharmacy/PharmacyPurchase.vue';
import PharmacySales from './views/Pharmacy/PharmacySales.vue';
import DistributorMaster from './views/Pharmacy/DistributorMaster.vue';
import LabMaster from './views/Lab/LabMaster.vue';
import LabPatientTests from './views/Lab/LabPatientTests.vue';
import Settings from './views/Settings/Settings.vue';
import PatientSources from './views/Master/PatientSources.vue';
import BillTypes from './views/Master/BillTypes.vue';
import PharmacyExpiry from './views/Pharmacy/PharmacyExpiry.vue';
import Beds from './views/Master/Beds.vue';
import RoleManagement from './views/Settings/RoleManagement.vue';
import Permissions from './views/Settings/Permissions.vue';
import Reporting from './views/Reports/Reporting.vue';
import GstFiling from './views/Reports/GstFiling.vue';
import Discounts from './views/Master/Discounts.vue';
import Referrals from './views/Master/Referrals.vue';
import Payouts from './views/Settings/Payouts.vue';
import FollowUps from './views/FollowUp/FollowUps.vue';
import StaffMaster from './views/Staff/StaffMaster.vue';
import Designations from './views/Staff/Designations.vue';
import Staff from './views/Staff/Staff.vue';
import SalaryExpenses from './views/Staff/SalaryExpenses.vue';
import StaffReport from './views/Reports/StaffReport.vue';
import DailyExpenses from './views/Expense/DailyExpenses.vue';
import LabExpenses from './views/Expense/LabExpenses.vue';
import PharmacyExpenses from './views/Expense/PharmacyExpenses.vue';
import DoctorSettlements from './views/Expense/DoctorSettlements.vue';
import ExpenseReport from './views/Reports/ExpenseReport.vue';
import LabReport from './views/Reports/LabReport.vue';
import PharmacyReport from './views/Reports/PharmacyReport.vue';
import PharmacyStockReport from './views/Pharmacy/PharmacyStockReport.vue';
import CbcAnalyzerResults from './views/Lab/CbcAnalyzerResults.vue';
import ServiceCharges from './views/Master/ServiceCharges.vue';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: Dashboard },
    { path: '/doctors', component: Doctors, meta: { module: 'DOCTOR' } },
    { path: '/patients', component: Patients, meta: { module: 'PATIENT' } },
    { path: '/opd', component: Opd, meta: { module: 'OPD' } },
    { path: '/bills', component: Bills, meta: { module: 'BILL' } },
    { path: '/ipd', component: Ipd, meta: { module: 'IPD' } },
    { path: '/ot', component: Ot, meta: { module: 'OT' } },
    { path: '/pharmacy/distributors', component: DistributorMaster, meta: { module: 'PHARMACY_DISTRIBUTOR' } },
    { path: '/pharmacy', redirect: '/pharmacy/purchase' },
    { path: '/pharmacy/purchase', component: PharmacyPurchase, meta: { module: 'PHARMACY_PURCHASE' } },
    { path: '/pharmacy/sales', component: PharmacySales, meta: { module: 'PHARMACY_SALES' } },
    { path: '/pharmacy/report', component: PharmacyReport, meta: { module: 'PHARMACY_REPORT' } },
    { path: '/pharmacy/stock-report', component: PharmacyStockReport, meta: { module: 'PHARMACY_STOCK_REPORT' } },
    { path: '/lab', redirect: '/lab/master' },
    { path: '/lab/master', component: LabMaster, meta: { module: 'LAB_MASTER' } },
    { path: '/lab/patient-tests', component: LabPatientTests, meta: { module: 'LAB_PATIENT_TESTS' } },
    { path: '/lab/report', component: LabReport, meta: { module: 'LAB_REPORT' } },
    { path: '/lab/cbc-analyzer', component: CbcAnalyzerResults, meta: { module: 'LAB_CBC_ANALYZER' } },
    { path: '/beds', component: Beds, meta: { module: 'BED' } },
    { path: '/settings', component: Settings, meta: { module: 'SETTINGS' } },
    { path: '/patient-sources', component: PatientSources, meta: { module: 'PATIENT_SOURCE' } },
    { path: '/bill-types', component: BillTypes, meta: { module: 'BILL_TYPE' } },
    { path: '/pharmacy/expiry', component: PharmacyExpiry, meta: { module: 'PHARMACY_EXPIRY' } },
    { path: '/roles', redirect: '/designations' },
    { path: '/designations', component: RoleManagement, meta: { module: 'ROLE' } },
    { path: '/permissions', component: Permissions, meta: { module: 'PERMISSION' } },
    { path: '/reporting', component: Reporting, meta: { module: 'REPORT_EXECUTIVE' } },
    { path: '/reporting/gst-filing', component: GstFiling, meta: { module: 'GST_FILING' } },
    { path: '/discounts', component: Discounts, meta: { module: 'DISCOUNT' } },
    { path: '/service-charges', component: ServiceCharges, meta: { module: 'SERVICE_CHARGE' } },
    { path: '/referrals', component: Referrals, meta: { module: 'REFERRAL' } },
    { path: '/payouts', component: Payouts, meta: { module: 'PAYOUT' } },
    { path: '/followups', component: FollowUps, meta: { module: 'FOLLOWUP' } },
    { path: '/staff', redirect: '/staff/master' },
    { path: '/staff/master', component: StaffMaster, meta: { module: 'STAFF_MASTER' } },
    { path: '/staff/designations', component: Designations, meta: { module: 'STAFF_DESIGNATION' } },
    { path: '/staff/attendance', component: Staff, meta: { module: 'STAFF_ATTENDANCE' } },
    { path: '/staff/salary', component: SalaryExpenses, meta: { module: 'STAFF_SALARY' } },
    { path: '/staff/report', component: StaffReport, meta: { module: 'STAFF_REPORT' } },
    { path: '/expenses/daily', component: DailyExpenses, meta: { module: 'EXPENSE_DAILY' } },
    { path: '/expenses/lab', component: LabExpenses, meta: { module: 'EXPENSE_LAB' } },
    { path: '/expenses/pharmacy', component: PharmacyExpenses, meta: { module: 'EXPENSE_PHARMACY' } },
    { path: '/expenses/doctor-settlements', component: DoctorSettlements, meta: { module: 'EXPENSE_DOCTOR_SETTLEMENT' } },
    { path: '/expenses/report', component: ExpenseReport, meta: { module: 'EXPENSE_REPORT' } }
  ]
});

// A user without permission for a module could otherwise still reach it by
// typing/bookmarking the URL directly - the sidebar only hides the link.
router.beforeEach(to => {
  const module = to.meta?.module;
  if (module && !can(module, 'view')) {
    return '/';
  }
});

export default router;
