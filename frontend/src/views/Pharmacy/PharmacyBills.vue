<script setup>
import { ref, onMounted } from 'vue';
import { api, assetUrl } from '../../api';
import { can } from '../../auth';
import Pagination from '../../components/Pagination.vue';
import GridSearch from '../../components/GridSearch.vue';
import { useServerGrid } from '../../composables/useServerGrid';
import { ageText } from '../../utils/age';

const tab = ref('All');
const todayDate = () => new Date().toISOString().slice(0, 10);
const allDates = ref(true);
const dateFrom = ref(todayDate());
const dateTo = ref(todayDate());
const pageSize = 10;

async function fetchBillsPage(params) {
  const dateParams = allDates.value ? {} : { from: dateFrom.value, to: dateTo.value };
  const { data } = await api.get('/bills', {
    params: {
      status: tab.value,
      billType: 'Pharmacy',
      ...dateParams,
      searchText: params.searchText,
      page: params.page,
      pageSize: params.pageSize,
      sortBy: params.sortBy,
      sortDirection: params.sortDirection
    }
  });
  return { rows: data.rows, totalCount: data.totalCount };
}

const { page, pageCount, rows, totalCount, sortBy, sortIndicator, searchText, reload } = useServerGrid(fetchBillsPage, pageSize);

async function changeTab(value) {
  tab.value = value;
  page.value = 1;
  await reload();
}

async function load() {
  page.value = 1;
  await reload();
}

const paymentModal = ref(false);
const selectedBill = ref(null);
const payment = ref({ amount: 0, mode: 'Cash', reference: '', paymentDate: todayDate() });
const paymentDatesModal = ref(null);

function openPayment(bill) {
  selectedBill.value = bill;
  payment.value = {
    amount: Number((bill.netAmount - bill.paidAmount).toFixed(2)),
    mode: 'Cash',
    reference: '',
    paymentDate: todayDate()
  };
  paymentModal.value = true;
}

function combineDateWithNowTime(dateStr) {
  const now = new Date();
  const [y, m, d] = dateStr.split('-').map(Number);
  return new Date(y, m - 1, d, now.getHours(), now.getMinutes(), now.getSeconds()).toISOString();
}

async function savePayment() {
  try {
    await api.post(`/bills/${selectedBill.value.id}/pay`, {
      amount: payment.value.amount,
      mode: payment.value.mode,
      reference: payment.value.reference,
      paidAtUtc: combineDateWithNowTime(payment.value.paymentDate)
    });
    paymentModal.value = false;
    await reload();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to accept payment.');
  }
}

function openPaymentDates(bill) {
  paymentDatesModal.value = {
    billId: bill.id,
    billNumber: bill.billNumber,
    payments: (bill.payments || [])
      .filter(p => Number(p.amount) > 0)
      .map(p => ({ id: p.id, amount: p.amount, mode: p.mode, paymentDate: new Date(p.paidAtUtc).toISOString().slice(0, 10) }))
  };
}

async function savePaymentDate(paymentRow) {
  try {
    await api.put(`/bills/${paymentDatesModal.value.billId}/payments/${paymentRow.id}/date`, {
      paidAtUtc: combineDateWithNowTime(paymentRow.paymentDate)
    });
    await reload();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to update this payment date.');
  }
}

function lastPaymentText(bill) {
  const received = (bill.payments || []).filter(p => Number(p.amount) > 0);
  if (!received.length) return '-';
  const latest = received.reduce((a, b) => new Date(a.paidAtUtc) > new Date(b.paidAtUtc) ? a : b);
  const d = new Date(latest.paidAtUtc);
  const pad = n => String(n).padStart(2, '0');
  return `${pad(d.getDate())}-${pad(d.getMonth() + 1)}-${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function paymentModesText(bill) {
  if (bill.status !== 'Paid' || !bill.payments?.length) return '-';
  const modes = [...new Set(bill.payments.map(p => p.mode))];
  return modes.join(', ');
}

// Return Items modal - loads what's still returnable on the sale linked to
// this bill, lets the user pick quantities per line, and (if the returned
// portion was already paid) collects a refund mode.
const returnModal = ref(null);
const returnError = ref('');

async function openReturn(bill) {
  returnError.value = '';
  try {
    const { data } = await api.get(`/pharmacy/returns/returnable-by-bill/${bill.id}`);
    returnModal.value = {
      billId: bill.id,
      billNumber: bill.billNumber,
      saleId: data.id,
      saleNumber: data.saleNumber,
      refundMode: 'Cash',
      reference: '',
      items: data.items.map(x => ({ ...x, returnQuantity: 0 }))
    };
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to load this sale for return.');
  }
}

function returnLineAmount(item) {
  const qty = Number(item.returnQuantity || 0);
  if (qty <= 0) return 0;
  return Math.round((Number(item.totalAmount) / Number(item.quantity)) * qty * 100) / 100;
}

function returnTotal() {
  if (!returnModal.value) return 0;
  return returnModal.value.items.reduce((sum, item) => sum + returnLineAmount(item), 0);
}

async function saveReturn() {
  returnError.value = '';
  const lines = returnModal.value.items
    .filter(x => Number(x.returnQuantity) > 0)
    .map(x => ({ pharmacySaleItemId: x.id, quantity: Number(x.returnQuantity) }));

  if (!lines.length) {
    returnError.value = 'Enter a return quantity for at least one item.';
    return;
  }

  try {
    const { data } = await api.post('/pharmacy/returns', {
      pharmacySaleId: returnModal.value.saleId,
      refundMode: returnModal.value.refundMode,
      reference: returnModal.value.reference,
      items: lines
    });
    if (data.refundAmount > 0) {
      alert(`₹${Number(data.refundAmount).toFixed(2)} refunded as ${returnModal.value.refundMode} for this return.`);
    }
    returnModal.value = null;
    await reload();
  } catch (error) {
    returnError.value = error.response?.data?.message || 'Unable to save this return.';
  }
}

function printHtml(html) {
  const iframe = document.createElement('iframe');
  iframe.style.position = 'fixed';
  iframe.style.width = '0';
  iframe.style.height = '0';
  iframe.style.border = '0';
  document.body.appendChild(iframe);

  const doc = iframe.contentWindow.document;
  doc.open();
  doc.write(html);
  doc.close();

  iframe.onload = () => {
    iframe.contentWindow.focus();
    iframe.contentWindow.print();
    iframe.contentWindow.onafterprint = () => iframe.remove();
    setTimeout(() => {
      if (document.body.contains(iframe)) iframe.remove();
    }, 60000);
  };
}

function printableNow() {
  const d = new Date();
  const pad = n => String(n).padStart(2, '0');
  return `${pad(d.getDate())}-${pad(d.getMonth() + 1)}-${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function paymentSummarySentence(payments, outstanding) {
  const byMode = {};
  (payments || []).forEach(p => { byMode[p.mode || 'Unknown'] = (byMode[p.mode || 'Unknown'] || 0) + Number(p.amount || 0); });
  const parts = Object.entries(byMode).map(([mode, amount]) => `₹${amount.toFixed(2)} as ${mode}`);
  let sentence = parts.length ? `Received ${parts.join(' and ')}` : 'No payment received yet';
  sentence += outstanding > 0.004 ? `, and ₹${outstanding.toFixed(2)} outstanding for payment.` : '.';
  return sentence;
}

async function printBill(bill) {
  const { data } = await api.get(`/bills/${bill.id}/print`);
  const itemRows = data.bill.items.map((item, i) => `
    <tr>
      <td class="no-col">${i + 1}</td>
      <td>${item.description}</td>
      <td>${data.hsnNo || '-'}</td>
      <td>${item.quantity}</td>
      <td>₹${Number(item.unitPrice).toFixed(2)}</td>
      <td>₹${Number(item.discountAmount || 0).toFixed(2)}</td>
      <td>₹${Number(item.amount).toFixed(2)}</td>
    </tr>`).join('');

  const totalDiscount = Number(data.totals.individualDiscount || 0) + Number(data.totals.bulkDiscount || 0);
  const totalRow = `
    <tr class="total-row">
      <td></td><td><b>Total</b></td><td></td><td></td>
      <td><b>₹${Number(data.totals.beforeDiscount).toFixed(2)}</b></td>
      <td><b>₹${totalDiscount.toFixed(2)}</b></td>
      <td><b>₹${Number(data.totals.afterDiscount).toFixed(2)}</b></td>
    </tr>`;

  const header = data.header ? `<img class="header-image" src="${assetUrl(data.header)}">` : '';
  const gst = data.gstSummary;
  const gstRows = `
    <div class="pi-row"><span><b>Taxable Value:</b> ₹${Number(gst.taxableValue).toFixed(2)}</span><span><b>CGST (${Number(gst.cgstRate)}%):</b> ₹${Number(gst.cgstAmount).toFixed(2)}</span></div>
    <div class="pi-row"><span><b>SGST (${Number(gst.sgstRate)}%):</b> ₹${Number(gst.sgstAmount).toFixed(2)}</span><span><b>IGST (${Number(gst.igstRate)}%):</b> ₹${Number(gst.igstAmount).toFixed(2)}</span></div>`;

  const content = `
  ${header}
  <div class="content">
    <div class="patient-info">
      <div class="pi-row"><span><b>Bill No:</b> ${data.bill.billNumber}</span><span><b>GST No:</b> ${data.hospitalGstNo || '-'}</span></div>
      <div class="pi-row"><span><b>Patient:</b> ${data.patient?.name || data.bill.walkInPatientName || '-'}</span><span><b>Patient ID:</b> ${data.patient?.patientCode || '-'}</span></div>
      <div class="pi-row"><span><b>Age / Gender:</b> ${data.patient ? `${ageText(data.patient)} / ${data.patient.gender}` : '-'}</span><span><b>Phone:</b> ${data.patient?.phone || '-'}</span></div>
      <div class="pi-row"><span><b>Bill Type:</b> ${data.bill.billType}</span><span><b>Date &amp; Time:</b> ${printableNow()}</span></div>
    </div>
    <table>
      <thead><tr><th class="no-col">Sl No</th><th>Description</th><th>HSN Code</th><th>Qty</th><th>Rate</th><th>Discount</th><th>Amount</th></tr></thead>
      <tbody>${itemRows}${totalRow}</tbody>
    </table>
    <div class="gst-summary">${gstRows}</div>
    <div class="payment-summary">${paymentSummarySentence(data.bill.payments, Number(data.totals.outstanding))}</div>
    <div class="signature-line">Authorized Signature</div>
  </div>`;

  printHtml(`<!doctype html>
<html>
<head><title></title>
<style>
  @page { size: A4; margin: 0; }
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; width: 210mm; font-family: Arial, sans-serif; color: #000; }
  .header-image { display: block; width: 210mm; height: 42mm; object-fit: fill; margin: 0; }
  .content { padding: 2mm 12mm 12mm; }
  .patient-info { border-top: 2px solid #000; margin-bottom: 12px; }
  .pi-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0 20px; padding: 6px 0; font-size: 13px; }
  .pi-row:last-child { border-bottom: 2px solid #000; }
  .pi-row b { display: inline-block; min-width: 110px; }
  table { width: 100%; border-collapse: collapse; }
  th, td { border: 0; padding: 7px; text-align: left; font-size: 12px; }
  thead tr { border-bottom: 1.5px solid #000; }
  .no-col { width: 12mm; text-align: center; }
  .total-row td { border-top: 1.5px solid #000; padding-top: 10px; }
  .gst-summary { border-top: 1px solid #000; margin-top: 4px; padding-top: 4px; }
  .payment-summary { margin-top: 15px; font-size: 15.5px; text-align: right; }
  .signature-line { margin-top: 16mm; text-align: right; font-weight: 700; }
</style>
</head>
<body>${content}</body>
</html>`);
}

onMounted(load);
</script>

<template>
  <h1>Pharmacy Bills</h1>

  <div class="toolbar">
    <button :class="tab === 'Unpaid' ? '' : 'secondary'" @click="changeTab('Unpaid')">Unpaid Bill</button>
    <button :class="tab === 'Paid' ? '' : 'secondary'" @click="changeTab('Paid')">Paid Bill</button>
    <button :class="tab === 'All' ? '' : 'secondary'" @click="changeTab('All')">All Bills</button>
  </div>

  <div class="toolbar report-filter">
    <label class="toggle-row"><input type="checkbox" v-model="allDates" @change="load"> All Dates</label>
    <label v-if="!allDates">From<input type="date" v-model="dateFrom"></label>
    <label v-if="!allDates">To<input type="date" v-model="dateTo"></label>
    <button v-if="!allDates" @click="load">Apply</button>
  </div>

  <div class="grid-filter-row">
    <GridSearch v-model="searchText" placeholder="Search bill number, patient, status..." />
  </div>

  <table class="table">
    <tr>
      <th class="sortable" @click="sortBy('billNumber')">Bill <span class="sort-indicator">{{ sortIndicator('billNumber') }}</span></th>
      <th>Patient</th>
      <th class="sortable" @click="sortBy('netAmount')">Net Amount <span class="sort-indicator">{{ sortIndicator('netAmount') }}</span></th>
      <th class="sortable" @click="sortBy('paidAmount')">Paid <span class="sort-indicator">{{ sortIndicator('paidAmount') }}</span></th>
      <th>Outstanding</th>
      <th class="sortable" @click="sortBy('status')">Status <span class="sort-indicator">{{ sortIndicator('status') }}</span></th>
      <th>Mode</th>
      <th>Last Payment</th>
      <th>Returned</th>
      <th></th>
    </tr>
    <tr v-for="bill in rows" :key="bill.id">
      <td>{{ bill.billNumber }}</td>
      <td>{{ bill.patientName || '-' }}</td>
      <td>₹{{ Number(bill.netAmount).toFixed(2) }}</td>
      <td>₹{{ Number(bill.paidAmount).toFixed(2) }}</td>
      <td>₹{{ Number(bill.netAmount - bill.paidAmount).toFixed(2) }}</td>
      <td>{{ bill.status }}</td>
      <td>{{ paymentModesText(bill) }}</td>
      <td>{{ lastPaymentText(bill) }}</td>
      <td>{{ Number(bill.refundedAmount || 0) > 0 ? '₹' + Number(bill.refundedAmount).toFixed(2) : '-' }}</td>
      <td class="actions">
        <button @click="printBill(bill)">Print</button>
        <button v-if="bill.status !== 'Paid'" @click="openPayment(bill)">Accept Payment</button>
        <button v-if="can('PHARMACY_BILLS', 'edit') && bill.payments?.length" @click="openPaymentDates(bill)">Edit Payment Date</button>
        <button v-if="can('PHARMACY_BILLS', 'edit')" @click="openReturn(bill)">Return Items</button>
      </td>
    </tr>
  </table>

  <Pagination :page="page" :page-count="pageCount" :total="totalCount" :page-size="pageSize" @update:page="page = $event" />

  <div v-if="paymentModal" class="modal-bg">
    <div class="modal">
      <button class="modal-close-x" @click="paymentModal = false">×</button>
      <h2>Accept Payment</h2>
      <div class="muted">Outstanding: ₹{{ Number(selectedBill.netAmount - selectedBill.paidAmount).toFixed(2) }}</div>
      <div class="form-grid">
        <label>Amount<input v-model.number="payment.amount" type="number" min="0.01" :max="selectedBill.netAmount - selectedBill.paidAmount"></label>
        <label>
          Mode
          <select v-model="payment.mode">
            <option>Cash</option><option>UPI</option><option>Card</option><option>Bank Transfer</option><option>Other</option>
          </select>
        </label>
        <label>Payment Date<input v-model="payment.paymentDate" type="date" :max="todayDate()"></label>
        <label class="full">Reference<input v-model="payment.reference"></label>
      </div>
      <div class="modal-actions">
        <button @click="savePayment">Save Payment</button>
        <button class="secondary" @click="paymentModal = false">Cancel</button>
      </div>
    </div>
  </div>

  <div v-if="paymentDatesModal" class="modal-bg">
    <div class="modal">
      <button class="modal-close-x" @click="paymentDatesModal = null">×</button>
      <h2>Edit Payment Date &middot; {{ paymentDatesModal.billNumber }}</h2>
      <div class="muted">Corrects the date on a payment already recorded - doesn't change the amount or add a new payment.</div>
      <table class="table" style="margin-top:12px">
        <tr><th>Amount</th><th>Mode</th><th>Date</th><th></th></tr>
        <tr v-for="p in paymentDatesModal.payments" :key="p.id">
          <td>₹{{ Number(p.amount).toFixed(2) }}</td>
          <td>{{ p.mode }}</td>
          <td><input v-model="p.paymentDate" type="date" :max="todayDate()"></td>
          <td><button @click="savePaymentDate(p)">Save</button></td>
        </tr>
      </table>
      <div class="modal-actions">
        <button class="secondary" @click="paymentDatesModal = null">Close</button>
      </div>
    </div>
  </div>

  <div v-if="returnModal" class="modal-bg">
    <div class="modal modal-xl">
      <button class="modal-close-x" @click="returnModal = null">×</button>
      <h2>Return Items &middot; {{ returnModal.billNumber }}</h2>
      <div class="muted">Sale {{ returnModal.saleNumber }} - enter how many of each item are being returned.</div>

      <div v-if="returnError" class="error-box">{{ returnError }}</div>

      <table class="table" style="margin-top:12px">
        <tr><th>Product</th><th>Batch</th><th>Sold Qty</th><th>Already Returned</th><th>Max Returnable</th><th>Return Qty</th><th>Refund Amount</th></tr>
        <tr v-for="item in returnModal.items" :key="item.id">
          <td>{{ item.productName }}</td>
          <td>{{ item.batchNo }}</td>
          <td>{{ item.quantity }} {{ item.unitType }}</td>
          <td>{{ item.alreadyReturned }}</td>
          <td>{{ item.maxReturnable }}</td>
          <td>
            <input v-model.number="item.returnQuantity" type="number" min="0" :max="item.maxReturnable" style="width:80px">
          </td>
          <td>₹{{ returnLineAmount(item).toFixed(2) }}</td>
        </tr>
      </table>

      <div class="form-grid" style="margin-top:12px">
        <label>
          Refund Mode (if already paid)
          <select v-model="returnModal.refundMode">
            <option>Cash</option><option>UPI</option><option>Bank Transfer</option>
          </select>
        </label>
        <label class="full">Reference<input v-model="returnModal.reference" placeholder="Optional note"></label>
      </div>

      <div class="bill-total-box consistent-total">
        <div class="grand"><span>Total Return Amount</span><b>₹{{ returnTotal().toFixed(2) }}</b></div>
      </div>

      <div class="modal-actions">
        <button @click="saveReturn">Save Return</button>
        <button class="secondary" @click="returnModal = null">Cancel</button>
      </div>
    </div>
  </div>
</template>
