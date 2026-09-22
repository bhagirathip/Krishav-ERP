import { createRouter, createWebHistory } from 'vue-router';
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

export default createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: Dashboard },
    { path: '/doctors', component: Doctors },
    { path: '/patients', component: Patients },
    { path: '/opd', component: Opd },
    { path: '/bills', component: Bills },
    { path: '/ipd', component: Ipd },
    { path: '/ot', component: Ot },
    { path: '/pharmacy/distributors', component: DistributorMaster },
    { path: '/pharmacy', redirect: '/pharmacy/purchase' },
    { path: '/pharmacy/purchase', component: PharmacyPurchase },
    { path: '/pharmacy/sales', component: PharmacySales },
    { path: '/lab', redirect: '/lab/master' },
    { path: '/lab/master', component: LabMaster },
    { path: '/lab/patient-tests', component: LabPatientTests },
    { path: '/beds', component: Beds },
    { path: '/settings', component: Settings },
    { path: '/patient-sources', component: PatientSources },
    { path: '/bill-types', component: BillTypes },
    { path: '/pharmacy/expiry', component: PharmacyExpiry },
    { path: '/roles', redirect: '/designations' },
    { path: '/designations', component: RoleManagement },
    { path: '/permissions', component: Permissions },
    { path: '/reporting', component: Reporting },
    { path: '/discounts', component: Discounts },
    { path: '/referrals', component: Referrals },
    { path: '/payouts', component: Payouts },
    { path: '/followups', component: FollowUps },
    { path: '/staff', redirect: '/staff/master' },
    { path: '/staff/master', component: StaffMaster },
    { path: '/staff/designations', component: Designations },
    { path: '/staff/attendance', component: Staff },
    { path: '/staff/salary', component: SalaryExpenses },
    { path: '/staff/report', component: StaffReport },
    { path: '/expenses/daily', component: DailyExpenses },
    { path: '/expenses/lab', component: LabExpenses },
    { path: '/expenses/pharmacy', component: PharmacyExpenses },
    { path: '/expenses/doctor-settlements', component: DoctorSettlements },
    { path: '/expenses/report', component: ExpenseReport }
  ]
});
