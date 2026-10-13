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
const discounts = ref([]);

async function loadDiscounts() {
  discounts.value = (await api.get('/discounts', { params: { scope: 'Individual', activeOnly: true } })).data;
}

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
      paidAtUtc: combineDateWithNowTime(paymentRow.paymentDate),
      mode: paymentRow.mode
    });
    await reload();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to update this payment.');
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

// Edit modal - only ever available while the sale is still a draft
// (bill.pharmacyStockDeducted === false, see BillsController.Pay/
// PharmacySalesController.Edit): quantities can change, items can be added
// or removed, and nothing here touches stock - the server re-validates
// availability and re-prices every line fresh on save, same as Create.
const editModal = ref(null);
const editSearch = ref('');
const editSearchResults = ref([]);

function packLabel(row) {
  if (row.medicineType === 'Tablet') return 'Strip';
  if (row.medicineType === 'Injection') return 'Pack';
  return 'Unit';
}
function looseLabel(row) {
  if (row.medicineType === 'Tablet') return 'Tablet';
  if (row.medicineType === 'Injection') return 'Vial';
  return 'Unit';
}
function editAllowedUnits(row) {
  const pack = packLabel(row);
  const loose = looseLabel(row);
  return pack === loose ? [pack] : [pack, loose];
}
function editMaxQuantity(row) {
  if (row.unitType === packLabel(row)) {
    return Math.floor(Number(row.remainingUnits) / Math.max(Number(row.unitsPerPack), 1));
  }
  return Number(row.remainingUnits);
}
function editUpdateUnit(row) {
  row.unitPrice = row.unitType === packLabel(row)
    ? Number(row.mrp)
    : Number(row.mrp) / Math.max(Number(row.unitsPerPack), 1);
  row.quantity = 1;
}

async function openEdit(bill) {
  if (!bill.pharmacySaleId) return;
  try {
    const [{ data: saleData }, { data: stock }] = await Promise.all([
      api.get(`/pharmacy/sales/${bill.pharmacySaleId}/print`),
      api.get('/pharmacy/sales/search', { params: { q: '' } })
    ]);

    const items = saleData.sale.items.map(item => {
      const match = stock.find(s => s.id === item.purchaseItemId);
      return {
        purchaseItemId: item.purchaseItemId,
        medicineType: match?.medicineType || '',
        productName: item.productName,
        packing: item.packing,
        batchNo: item.batchNo,
        mrp: Number(item.mrp),
        unitsPerPack: Number(match?.unitsPerPack || 1),
        remainingUnits: Number(match?.remainingTablets || 0),
        unitType: item.unitType,
        quantity: item.quantity,
        unitPrice: Number(item.unitPrice),
        discountTypeId: item.discountTypeId
      };
    });

    editModal.value = {
      billId: bill.id,
      saleId: bill.pharmacySaleId,
      billNumber: bill.billNumber,
      roundOff: Number(bill.roundOff || 0),
      items
    };
    editSearch.value = '';
    editSearchResults.value = [];
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to load this bill for editing.');
  }
}

async function searchEditInventory() {
  editSearchResults.value = (await api.get('/pharmacy/sales/search', { params: { q: editSearch.value } })).data;
}

function addEditItem(row) {
  if (editModal.value.items.some(x => x.purchaseItemId === row.id)) return;
  editModal.value.items.push({
    purchaseItemId: row.id,
    medicineType: row.medicineType,
    productName: row.productName,
    packing: row.packing,
    batchNo: row.batchNo,
    mrp: Number(row.mrp),
    unitsPerPack: Number(row.unitsPerPack),
    remainingUnits: Number(row.remainingTablets),
    unitType: row.medicineType === 'Tablet' ? 'Strip' : row.medicineType === 'Injection' ? 'Pack' : 'Unit',
    quantity: 1,
    unitPrice: Number(row.mrp),
    discountTypeId: null
  });
}

function removeEditItem(index) {
  editModal.value.items.splice(index, 1);
}

function editRowGross(row) {
  return Number(row.unitPrice || 0) * Number(row.quantity || 0);
}

function editRowDiscount(row) {
  const d = discounts.value.find(x => x.id === row.discountTypeId);
  if (!d) return 0;
  const gross = editRowGross(row), value = Number(d.value || 0);
  return d.discountMode === 'Amount' ? Math.min(gross, value) : gross * Math.min(100, Math.max(0, value)) / 100;
}

function editRowTotal(row) {
  return Math.max(0, editRowGross(row) - editRowDiscount(row));
}

function editTotals() {
  if (!editModal.value) return { gross: 0, discount: 0, net: 0 };
  const gross = editModal.value.items.reduce((s, r) => s + editRowGross(r), 0);
  const discount = editModal.value.items.reduce((s, r) => s + editRowDiscount(r), 0);
  return { gross, discount, net: gross - discount + Number(editModal.value.roundOff || 0) };
}

async function saveEdit() {
  if (!editModal.value.items.length) {
    alert('Add at least one medicine to the bill.');
    return;
  }
  try {
    const { data } = await api.put(`/pharmacy/sales/${editModal.value.saleId}`, {
      roundOff: Number(editModal.value.roundOff || 0),
      items: editModal.value.items.map(x => ({
        purchaseItemId: x.purchaseItemId,
        unitType: x.unitType,
        quantity: x.quantity,
        discountTypeId: x.discountTypeId
      }))
    });
    if (data.refundAmount > 0) {
      alert(`₹${Number(data.refundAmount).toFixed(2)} refunded - the edited total is less than what was already paid.`);
    }
    editModal.value = null;
    await reload();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to save changes to this bill.');
  }
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

onMounted(async () => {
  await Promise.all([load(), loadDiscounts()]);
});
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
      <th>Purchase</th>
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
      <td>{{ bill.pharmacyStockDeducted === false ? 'Pending' : 'Completed' }}</td>
      <td>{{ paymentModesText(bill) }}</td>
      <td>{{ lastPaymentText(bill) }}</td>
      <td>{{ Number(bill.refundedAmount || 0) > 0 ? '₹' + Number(bill.refundedAmount).toFixed(2) : '-' }}</td>
      <td class="actions">
        <button @click="printBill(bill)">Print</button>
        <button v-if="bill.status !== 'Paid'" @click="openPayment(bill)">Accept Payment</button>
        <button v-if="can('PHARMACY_BILLS', 'edit') && bill.pharmacyStockDeducted === false" @click="openEdit(bill)">Edit</button>
        <button v-if="can('PHARMACY_BILLS', 'edit') && bill.payments?.length" @click="openPaymentDates(bill)">Edit Payment</button>
        <button v-if="can('PHARMACY_BILLS', 'edit') && bill.pharmacyStockDeducted !== false" @click="openReturn(bill)">Return Items</button>
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

  <div v-if="editModal" class="modal-bg">
    <div class="modal modal-xl">
      <button class="modal-close-x" @click="editModal = null">×</button>
      <h2>Edit Bill &middot; {{ editModal.billNumber }}</h2>
      <div class="muted">This purchase hasn't been completed yet - quantities can still change and nothing has been deducted from stock.</div>

      <div class="toolbar" style="margin-top:12px">
        <input style="max-width:420px" v-model="editSearch" placeholder="Search product, MF, batch or HSN" @input="searchEditInventory">
        <button @click="searchEditInventory">Search</button>
      </div>
      <table v-if="editSearchResults.length" class="table">
        <tr><th>Type</th><th>Product</th><th>Batch</th><th>MRP</th><th>Stock</th><th></th></tr>
        <tr v-for="row in editSearchResults" :key="row.id">
          <td>{{ row.medicineType }}</td>
          <td>{{ row.productName }}</td>
          <td>{{ row.batchNo }}</td>
          <td>₹{{ Number(row.mrp).toFixed(2) }}</td>
          <td>{{ row.availablePacks }} pack(s) + {{ row.looseUnits }} loose</td>
          <td><button @click="addEditItem(row)">Add</button></td>
        </tr>
      </table>

      <h3>Medicines</h3>
      <table class="table">
        <tr><th>Product</th><th>Batch</th><th>Sale Unit</th><th>Quantity</th><th>Price</th><th>Discount</th><th>Total</th><th></th></tr>
        <tr v-for="(row, index) in editModal.items" :key="row.purchaseItemId">
          <td>{{ row.productName }}<div class="muted">{{ row.packing }}</div></td>
          <td>{{ row.batchNo }}</td>
          <td><select v-model="row.unitType" @change="editUpdateUnit(row)"><option v-for="u in editAllowedUnits(row)" :key="u">{{ u }}</option></select></td>
          <td><input type="number" min="1" :max="editMaxQuantity(row)" v-model.number="row.quantity"><div class="muted">Max {{ editMaxQuantity(row) }} {{ row.unitType }}</div></td>
          <td>₹{{ Number(row.unitPrice).toFixed(2) }}</td>
          <td>
            <select v-model.number="row.discountTypeId">
              <option :value="null">No Discount</option>
              <option v-for="d in discounts" :key="d.id" :value="d.id">{{ d.name }} · {{ d.discountMode === 'Percent' ? d.value + '%' : '₹' + Number(d.value).toFixed(2) }}</option>
            </select>
          </td>
          <td><b>₹{{ editRowTotal(row).toFixed(2) }}</b></td>
          <td><button class="danger-btn" @click="removeEditItem(index)">×</button></td>
        </tr>
      </table>

      <div class="bill-total-box consistent-total">
        <div><span>Before Discount</span><b>₹{{ editTotals().gross.toFixed(2) }}</b></div>
        <div><span>Discount</span><b>₹{{ editTotals().discount.toFixed(2) }}</b></div>
        <div>
          <span>Round Off (-9 to 9)</span>
          <input v-model.number="editModal.roundOff" type="number" min="-9" max="9" step="0.01" style="width:80px;display:inline-block" @focus="$event.target.select()">
        </div>
        <div class="grand"><span>Total</span><b>₹{{ editTotals().net.toFixed(2) }}</b></div>
      </div>

      <div class="modal-actions">
        <button @click="saveEdit">Save Changes</button>
        <button class="secondary" @click="editModal = null">Cancel</button>
      </div>
    </div>
  </div>

  <div v-if="paymentDatesModal" class="modal-bg">
    <div class="modal">
      <button class="modal-close-x" @click="paymentDatesModal = null">×</button>
      <h2>Edit Payment &middot; {{ paymentDatesModal.billNumber }}</h2>
      <div class="muted">Corrects the date and/or mode on a payment already recorded - doesn't change the amount or add a new payment.</div>
      <table class="table" style="margin-top:12px">
        <tr><th>Amount</th><th>Mode</th><th>Date</th><th></th></tr>
        <tr v-for="p in paymentDatesModal.payments" :key="p.id">
          <td>₹{{ Number(p.amount).toFixed(2) }}</td>
          <td>
            <select v-model="p.mode">
              <option>Cash</option><option>UPI</option><option>Card</option><option>Bank Transfer</option><option>Other</option>
            </select>
          </td>
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
