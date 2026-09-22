<script setup>
import { computed, onMounted, ref } from 'vue';
import { api, assetUrl } from '../api';
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
const totals = computed(() => rows.value.reduce((a,x)=>({gross:a.gross+Number(x.monthlySalary||0),deduction:a.deduction+Number(x.deductionAmount||0),payable:a.payable+Number(x.payableAmount||0),paid:a.paid+Number(x.paidAmount||0),prevOutstanding:a.prevOutstanding+Number(x.previousOutstanding||0)}),{gross:0,deduction:0,payable:0,paid:0,prevOutstanding:0}));

async function load(){ const [r,s]=await Promise.all([api.get('/staff/payroll',{params:{year:year.value,month:month.value}}),api.get('/staff')]); rows.value=r.data; staffList.value=s.data; }
function openPayment(row){ payment.value={staffId:row.id,staffName:row.name,salaryYear:year.value,salaryMonth:month.value,payableAmount:row.payableAmount,paidAmount:row.paidAmount||0,paymentDate:new Date().toISOString().slice(0,10),paymentMode:row.paymentMode||'Cash',referenceNumber:row.referenceNumber||'',notes:row.notes||'',paidByStaffId:null}; }
async function savePayment(){ await api.post('/staff/salary-payment',payment.value); payment.value=null; await load(); }

function escapeHtml(value){
  return String(value??'')
    .replaceAll('&','&amp;')
    .replaceAll('<','&lt;')
    .replaceAll('>','&gt;')
    .replaceAll('"','&quot;')
    .replaceAll("'",'&#039;');
}

function monthName(m){ return new Date(2000,m-1,1).toLocaleString('en',{month:'long'}); }

function openPrintFrame(html){
  const f=document.createElement('iframe');
  f.style.position='fixed';f.style.width='0';f.style.height='0';f.style.border='0';
  document.body.appendChild(f);
  const d=f.contentWindow.document;
  d.open();d.write(html);d.close();
  const h=e=>{
    if(e.data==='payroll-print'){
      f.remove();
      window.removeEventListener('message',h);
    }
  };
  window.addEventListener('message',h);
}

// Name, account number and payable amount only - no deduction/gross - with
// the OPD letterhead at the top and a total row at the bottom.
async function printPayroll(){
  if(!rows.value.length)return;
  const branding=(await api.get('/settings/branding')).data;
  const header=branding.find(x=>x.name==='OPD Header')?.value;
  const total=rows.value.reduce((a,x)=>a+Number(x.payableAmount||0),0);
  const bodyRows=rows.value
    .map((x,i)=>`<tr><td class="no-col">${i+1}</td><td>${escapeHtml(x.name)}</td><td>${escapeHtml(x.accountNumber||'-')}</td><td class="amount">₹${Number(x.payableAmount).toFixed(0)}</td></tr>`)
    .join('');

  const html=`<!doctype html>
  <html>
    <head>
      <title></title>
      <style>
        @page{size:A4;margin:10mm}
        *{box-sizing:border-box}
        html,body{margin:0;padding:0;font-family:Arial,sans-serif;color:#111}
        body{font-size:11.5px;counter-reset:page 1}

        /* A plain position:fixed header offset by a negative @page margin
           does not render at all under Chromium's real print/PDF engine (it
           only ever showed up in an on-screen preview, never a print) - the
           pattern that verifiably repeats a header on every printed page is
           an outer table with a repeating thead, so the whole document is
           that one table and the staff/payable table sits in its tbody. */
        table.page-frame{width:100%;border-collapse:collapse}
        .page-frame thead{display:table-header-group}
        .page-frame>thead>tr>td{padding:0}
        .page-header img{display:block;width:100%;height:42mm;object-fit:fill}

        h2{text-align:center;margin:6mm 0 8mm;font-size:15px}
        table.data{width:100%;border-collapse:collapse}
        table.data th,table.data td{border:1px solid #333;padding:5px 8px;text-align:left}
        table.data th{background:#eef2f7}
        .no-col{width:12mm;text-align:center}
        .amount{text-align:right}
        table.data tfoot td{font-weight:700;background:#f8fafc}
      </style>
    </head>
    <body>
      <table class="page-frame">
        <thead><tr><td class="page-header">${header?`<img src="${assetUrl(header)}">`:''}</td></tr></thead>
        <tbody><tr><td>
          <h2>Salary / Payroll &middot; ${monthName(month.value)} ${year.value}</h2>
          <table class="data">
            <thead><tr><th class="no-col">No.</th><th>Staff Name</th><th>Account Number</th><th class="amount">Payable Amount</th></tr></thead>
            <tbody>${bodyRows}</tbody>
            <tfoot><tr><td colspan="3">Total</td><td class="amount">₹${total.toFixed(0)}</td></tr></tfoot>
          </table>
        </td></tr></tbody>
      </table>
      <script>
        const waitForImages=()=>Promise.all(Array.from(document.images).map(img=>img.complete?Promise.resolve():new Promise(resolve=>{img.onload=img.onerror=resolve})));
        window.onload=async()=>{await waitForImages();setTimeout(()=>window.print(),100)};
        window.onafterprint=()=>window.parent.postMessage('payroll-print','*');
      <\/script>
    </body>
  </html>`;
  openPrintFrame(html);
}

onMounted(load);
</script>

<template>
  <h1>Salary</h1>
  <div class="toolbar">
    <label>Year<input type="number" min="2020" max="2100" v-model.number="year"></label>
    <label>Month<select v-model.number="month"><option v-for="m in 12" :key="m" :value="m">{{ new Date(2000,m-1,1).toLocaleString('en',{month:'long'}) }}</option></select></label>
    <button @click="load">Apply</button>
  </div>
  <div class="toolbar">
    <GridSearch v-model="search" placeholder="Search staff, designation, salary, attendance..." style="flex:1" />
    <button class="secondary" @click="printPayroll">🖶 Print</button>
  </div>
  <table class="table">
    <tr><th>Staff</th><th>Salary</th><th>Present</th><th>Absent</th><th>Half Day</th><th>WeekOff</th><th>Leave</th><th>Not Marked</th><th>Deduction</th><th>Payable</th><th>Paid</th><th>Outstanding</th><th>Prev. Outstanding</th><th></th></tr>
    <tr v-for="row in pagedRows" :key="row.id">
      <td><b>{{ row.name }}</b><div class="muted">{{ row.staffCode }} · {{ row.designation || '-' }}</div></td>
      <td>₹{{ Number(row.monthlySalary).toFixed(0) }}</td><td>{{ row.presentDays }}</td><td>{{ row.absentDays }}</td><td>{{ row.halfDays }}</td><td>{{ row.weekOffDays }}</td><td>{{ row.leaveDays }}</td><td>{{ row.unmarkedDays }}</td>
      <td>₹{{ Number(row.deductionAmount).toFixed(0) }}</td><td><b>₹{{ Number(row.payableAmount).toFixed(0) }}</b></td><td>₹{{ Number(row.paidAmount).toFixed(0) }}</td><td>₹{{ Number(row.outstanding).toFixed(0) }}</td>
      <td :class="{ danger: Number(row.previousOutstanding) > 0 }">₹{{ Number(row.previousOutstanding).toFixed(0) }}</td>
      <td><button @click="openPayment(row)">Payment</button></td>
    </tr>
  </table>
  <div class="bill-total-box"><div><span>Gross Salary</span><b>₹{{ totals.gross.toFixed(0) }}</b></div><div><span>Deductions</span><b>₹{{ totals.deduction.toFixed(0) }}</b></div><div><span>Payable</span><b>₹{{ totals.payable.toFixed(0) }}</b></div><div><span>Prev. Outstanding</span><b>₹{{ totals.prevOutstanding.toFixed(0) }}</b></div><div class="grand"><span>Paid</span><b>₹{{ totals.paid.toFixed(0) }}</b></div></div>
  <Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="10" @update:page="page=$event" />

  <div v-if="payment" class="modal-bg"><div class="modal">
    <button class="modal-close-x" @click="payment=null">×</button>
    <h2>Salary Payment · {{ payment.staffName }}</h2>
    <div class="card"><b>Payable: ₹{{ Number(payment.payableAmount).toFixed(0) }}</b></div>
    <div class="form-grid" style="margin-top:14px">
      <label>Paid Amount *<input type="number" min="0" :max="payment.payableAmount" step="1" v-model.number="payment.paidAmount"></label>
      <label>Payment Date<input type="date" v-model="payment.paymentDate"></label>
      <label>Payment Mode<select v-model="payment.paymentMode"><option>Cash</option><option>UPI</option><option>Bank Transfer</option><option>Cheque</option><option>Other</option></select></label>
      <label>Paid By Staff<select v-model.number="payment.paidByStaffId"><option :value="null">Not specified</option><option v-for="x in staffList" :key="x.id" :value="x.id">{{x.name}}</option></select></label><label>Reference<input v-model="payment.referenceNumber"></label>
      <label class="full">Notes<textarea rows="3" v-model="payment.notes"></textarea></label>
    </div>
    <div class="modal-actions"><button @click="savePayment">Save Payment</button><button class="secondary" @click="payment=null">Cancel</button></div>
  </div></div>
</template>
