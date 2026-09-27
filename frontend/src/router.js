import { createRouter, createWebHistory } from 'vue-router';
import { can } from './auth';
import Dashboard from './views/Dashboard.vue';
import Doctors from './views/Doctors.vue';
import Patients from './views/Patients.vue';
import Opd from './views/Opd.vue';
import Bills from './views/Bills.vue';
import Ipd from './views/Ipd.vue';
import Ot from './views/Ot.vue';
import PharmacyPurchase from './views/PharmacyPurchase.vue';
import PharmacySales from './views/PharmacySales.vue';
import DistributorMaster from './views/DistributorMaster.vue';
import LabMaster from './views/LabMaster.vue';
import LabPatientTests from './views/LabPatientTests.vue';
import Settings from './views/Settings.vue';
import PatientSources from './views/PatientSources.vue';
import BillTypes from './views/BillTypes.vue';
import PharmacyExpiry from './views/PharmacyExpiry.vue';
import Beds from './views/Beds.vue';
import RoleManagement from './views/RoleManagement.vue';
import Permissions from './views/Permissions.vue';
import Reporting from './views/Reporting.vue';
import Discounts from './views/Discounts.vue';
import Referrals from './views/Referrals.vue';
import Payouts from './views/Payouts.vue';
import FollowUps from './views/FollowUps.vue';
import StaffMaster from './views/StaffMaster.vue';
import Designations from './views/Designations.vue';
import Staff from './views/Staff.vue';
import SalaryExpenses from './views/SalaryExpenses.vue';
import StaffReport from './views/StaffReport.vue';
import DailyExpenses from './views/DailyExpenses.vue';
import LabExpenses from './views/LabExpenses.vue';
import PharmacyExpenses from './views/PharmacyExpenses.vue';
import DoctorSettlements from './views/DoctorSettlements.vue';
import ExpenseReport from './views/ExpenseReport.vue';
import LabReport from './views/LabReport.vue';
import PharmacyReport from './views/PharmacyReport.vue';
import CbcAnalyzerResults from './views/CbcAnalyzerResults.vue';

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
    { path: '/pharmacy/distributors', component: DistributorMaster, meta: { module: 'PHARMACY' } },
    { path: '/pharmacy', redirect: '/pharmacy/purchase' },
    { path: '/pharmacy/purchase', component: PharmacyPurchase, meta: { module: 'PHARMACY' } },
    { path: '/pharmacy/sales', component: PharmacySales, meta: { module: 'PHARMACY' } },
    { path: '/pharmacy/report', component: PharmacyReport, meta: { module: 'PHARMACY' } },
    { path: '/lab', redirect: '/lab/master' },
    { path: '/lab/master', component: LabMaster, meta: { module: 'LAB' } },
    { path: '/lab/patient-tests', component: LabPatientTests, meta: { module: 'LAB' } },
    { path: '/lab/report', component: LabReport, meta: { module: 'LAB' } },
    { path: '/lab/cbc-analyzer', component: CbcAnalyzerResults, meta: { module: 'LAB' } },
    { path: '/beds', component: Beds, meta: { module: 'BED' } },
    { path: '/settings', component: Settings, meta: { module: 'SETTINGS' } },
    { path: '/patient-sources', component: PatientSources, meta: { module: 'PATIENT_SOURCE' } },
    { path: '/bill-types', component: BillTypes, meta: { module: 'BILL_TYPE' } },
    { path: '/pharmacy/expiry', component: PharmacyExpiry, meta: { module: 'PHARMACY' } },
    { path: '/roles', redirect: '/designations' },
    { path: '/designations', component: RoleManagement, meta: { module: 'ROLE' } },
    { path: '/permissions', component: Permissions, meta: { module: 'PERMISSION' } },
    { path: '/reporting', component: Reporting, meta: { module: 'REPORTING' } },
    { path: '/discounts', component: Discounts, meta: { module: 'DISCOUNT' } },
    { path: '/referrals', component: Referrals, meta: { module: 'REFERRAL' } },
    { path: '/payouts', component: Payouts, meta: { module: 'REFERRAL' } },
    { path: '/followups', component: FollowUps, meta: { module: 'FOLLOWUP' } },
    { path: '/staff', redirect: '/staff/master' },
    { path: '/staff/master', component: StaffMaster, meta: { module: 'STAFF' } },
    { path: '/staff/designations', component: Designations, meta: { module: 'STAFF' } },
    { path: '/staff/attendance', component: Staff, meta: { module: 'STAFF' } },
    { path: '/staff/salary', component: SalaryExpenses, meta: { module: 'STAFF' } },
    { path: '/staff/report', component: StaffReport, meta: { module: 'STAFF' } },
    { path: '/expenses/daily', component: DailyExpenses, meta: { module: 'EXPENSE' } },
    { path: '/expenses/lab', component: LabExpenses, meta: { module: 'EXPENSE' } },
    { path: '/expenses/pharmacy', component: PharmacyExpenses, meta: { module: 'EXPENSE' } },
    { path: '/expenses/doctor-settlements', component: DoctorSettlements, meta: { module: 'EXPENSE' } },
    { path: '/expenses/report', component: ExpenseReport, meta: { module: 'EXPENSE' } }
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
