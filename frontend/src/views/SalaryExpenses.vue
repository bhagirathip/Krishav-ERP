<script setup>
import { computed, onMounted, ref } from 'vue';
import { api } from '../api';
import GridSearch from '../components/GridSearch.vue';
import Pagination from '../components/Pagination.vue';
import { useGrid } from '../composables/useGrid';

const now = new Date();
const year = ref(now.getFullYear());
const month = ref(now.getMonth() + 1);
const rows = ref([]);
const staffList = ref([]);
const payment = ref(null);
const { search, page, pageCount, pagedRows, sortedRows, sortBy, sortIndicator } = useGrid(rows, 10);
const totals = computed(() => rows.value.reduce((a,x)=>({gross:a.gross+Number(x.monthlySalary||0),deduction:a.deduction+Number(x.deductionAmount||0),payable:a.payable+Number(x.payableAmount||0),paid:a.paid+Number(x.paidAmount||0)}),{gross:0,deduction:0,payable:0,paid:0}));

async function load(){ const [r,s]=await Promise.all([api.get('/staff/payroll',{params:{year:year.value,month:month.value}}),api.get('/staff')]); rows.value=r.data; staffList.value=s.data; }
function openPayment(row){ payment.value={staffId:row.id,staffName:row.name,salaryYear:year.value,salaryMonth:month.value,payableAmount:row.payableAmount,paidAmount:row.paidAmount||0,paymentDate:new Date().toISOString().slice(0,10),paymentMode:row.paymentMode||'Cash',referenceNumber:row.referenceNumber||'',notes:row.notes||'',paidByStaffId:null}; }
async function savePayment(){ await api.post('/staff/salary-payment',payment.value); payment.value=null; await load(); }
onMounted(load);
</script>

<template>
  <h1>Salary</h1>
  <div class="toolbar">
    <label>Year<input type="number" min="2020" max="2100" v-model.number="year"></label>
    <label>Month<select v-model.number="month"><option v-for="m in 12" :key="m" :value="m">{{ new Date(2000,m-1,1).toLocaleString('en',{month:'long'}) }}</option></select></label>
    <button @click="load">Apply</button>
  </div>
  <GridSearch v-model="search" placeholder="Search staff, designation, salary, attendance..." />
  <table class="table">
    <tr><th>Staff</th><th>Salary</th><th>Present</th><th>Absent</th><th>Half Day</th><th>WeekOff</th><th>Leave</th><th>Deduction</th><th>Payable</th><th>Paid</th><th>Outstanding</th><th></th></tr>
    <tr v-for="row in pagedRows" :key="row.id">
      <td><b>{{ row.name }}</b><div class="muted">{{ row.staffCode }} · {{ row.designation || '-' }}</div></td>
      <td>₹{{ Number(row.monthlySalary).toFixed(2) }}</td><td>{{ row.presentDays }}</td><td>{{ row.absentDays }}</td><td>{{ row.halfDays }}</td><td>{{ row.weekOffDays }}</td><td>{{ row.leaveDays }}</td>
      <td>₹{{ Number(row.deductionAmount).toFixed(2) }}</td><td><b>₹{{ Number(row.payableAmount).toFixed(2) }}</b></td><td>₹{{ Number(row.paidAmount).toFixed(2) }}</td><td>₹{{ Number(row.outstanding).toFixed(2) }}</td>
      <td><button @click="openPayment(row)">Payment</button></td>
    </tr>
  </table>
  <div class="bill-total-box"><div><span>Gross Salary</span><b>₹{{ totals.gross.toFixed(2) }}</b></div><div><span>Deductions</span><b>₹{{ totals.deduction.toFixed(2) }}</b></div><div><span>Payable</span><b>₹{{ totals.payable.toFixed(2) }}</b></div><div class="grand"><span>Paid</span><b>₹{{ totals.paid.toFixed(2) }}</b></div></div>
  <Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="10" @update:page="page=$event" />

  <div v-if="payment" class="modal-bg"><div class="modal">
    <button class="modal-close-x" @click="payment=null">×</button>
    <h2>Salary Payment · {{ payment.staffName }}</h2>
    <div class="card"><b>Payable: ₹{{ Number(payment.payableAmount).toFixed(2) }}</b></div>
    <div class="form-grid" style="margin-top:14px">
      <label>Paid Amount *<input type="number" min="0" :max="payment.payableAmount" step="0.01" v-model.number="payment.paidAmount"></label>
      <label>Payment Date<input type="date" v-model="payment.paymentDate"></label>
      <label>Payment Mode<select v-model="payment.paymentMode"><option>Cash</option><option>UPI</option><option>Bank Transfer</option><option>Cheque</option><option>Other</option></select></label>
      <label>Paid By Staff<select v-model.number="payment.paidByStaffId"><option :value="null">Not specified</option><option v-for="x in staffList" :key="x.id" :value="x.id">{{x.name}}</option></select></label><label>Reference<input v-model="payment.referenceNumber"></label>
      <label class="full">Notes<textarea rows="3" v-model="payment.notes"></textarea></label>
    </div>
    <div class="modal-actions"><button @click="savePayment">Save Payment</button><button class="secondary" @click="payment=null">Cancel</button></div>
  </div></div>
</template>
