<script setup>
import { ref, onMounted, watch, computed } from 'vue';
import { api, assetUrl } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';

const tab = ref('Unpaid');
const rows = ref([]);
const todayDate = () => new Date().toISOString().slice(0, 10);
const allDates = ref(false);
const dateFrom = ref(todayDate());
const dateTo = ref(todayDate());
const billTypeFilter = ref('All');
const filteredByType = computed(() =>
  billTypeFilter.value === 'All'
    ? rows.value
    : rows.value.filter(x => x.billType === billTypeFilter.value)
);
const { search, page, pageCount, pagedRows, sortedRows, sortBy, sortIndicator } = useGrid(filteredByType, 10);
const pageSize = 10;
const patients = ref([]);
const doctors = ref([]);
const categories = ref([]);
const catalog = ref([]);
const discounts = ref([]);
const individualDiscounts = computed(() => discounts.value.filter(x => x.isActive && x.scope === 'Individual'));
const bulkDiscounts = computed(() => discounts.value.filter(x => x.isActive && x.scope === 'Bulk'));
const paymentModal = ref(false);
const billModal = ref(false);
const editing = ref(null);
const selectedBill = ref(null);
const errors = ref([]);
const payment = ref({ amount: 0, mode: 'Cash', reference: '' });

const emptyBill = () => ({
  patientId: null,
  walkInPatientName: '',
  doctorId: null,
  billType: 'OPD',
  bulkDiscountTypeId: null,
  roundOff: 0,
  items: [emptyItem()]
});

function emptyItem() {
  return {
    description: '',
    quantity: 1,
    unitPrice: 0,
    discountTypeId: null,
    referenceType: null,
    referenceId: null
  };
}

const form = ref(emptyBill());
const selectedPatient = computed(() => patients.value.find(x => x.id === form.value.patientId));
const patientQuery = ref('');
const showPatientOptions = ref(false);
const filteredPatients = computed(() => {
  const q = patientQuery.value.trim().toLowerCase();
  if (!q) return patients.value.slice(0, 25);

  return patients.value
    .filter(p =>
      p.name.toLowerCase().includes(q) ||
      (p.phone || '').includes(q) ||
      (p.patientCode || '').toLowerCase().includes(q)
    )
    .slice(0, 25);
});

function onPatientInput() {
  showPatientOptions.value = true;

  const current = form.value.patientId ? patients.value.find(p => p.id === form.value.patientId) : null;
  if (!current || current.name !== patientQuery.value) {
    form.value.patientId = null;
  }

  form.value.walkInPatientName = patientQuery.value;
}

function selectPatient(patient) {
  form.value.patientId = patient.id;
  form.value.walkInPatientName = '';
  patientQuery.value = patient.name;
  showPatientOptions.value = false;
}

function onPatientBlur() {
  setTimeout(() => { showPatientOptions.value = false; }, 150);
}

async function load() {
  const dateParams = allDates.value ? {} : { from: dateFrom.value, to: dateTo.value };
  const [billsRes, patientsRes, doctorsRes, categoriesRes, discountsRes] = await Promise.all([
    api.get('/bills', { params: { status: tab.value, ...dateParams } }),
    api.get('/patients'),
    api.get('/doctors'),
    api.get('/bill-types', { params: { billableOnly: true } }),
    api.get('/discounts')
  ]);

  rows.value = billsRes.data;
  patients.value = patientsRes.data;
  doctors.value = doctorsRes.data;
  categories.value = categoriesRes.data;
  discounts.value = discountsRes.data;
}

async function loadCatalog() {
  catalog.value = (await api.get('/bills/catalog', {
    params: { type: form.value.billType }
  })).data;
}

watch(() => form.value.billType, loadCatalog);

async function changeTab(value) {
  tab.value = value;
  page.value = 1;
  await load();
}

function newBill() {
  editing.value = null;
  errors.value = [];
  form.value = emptyBill();
  patientQuery.value = '';
  showPatientOptions.value = false;
  billModal.value = true;
  loadCatalog();
}

function editBill(bill) {
  editing.value = bill;
  errors.value = [];
  form.value = {
    patientId: bill.patientId,
    walkInPatientName: bill.patientId ? '' : (bill.patientName || ''),
    doctorId: bill.doctorId,
    billType: bill.billType,
    bulkDiscountTypeId: bill.bulkDiscountTypeId || null,
    roundOff: bill.roundOff || 0,
    items: bill.items.map(item => ({
      description: item.description,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
      discountTypeId: item.discountTypeId || null,
      referenceType: item.referenceType,
      referenceId: item.referenceId
    }))
  };
  patientQuery.value = bill.patientId
    ? (patients.value.find(p => p.id === bill.patientId)?.name || bill.patientName || '')
    : (bill.patientName || '');
  showPatientOptions.value = false;
  billModal.value = true;
  loadCatalog();
}

function addItem() {
  form.value.items.push(emptyItem());
}

function removeItem(index) {
  form.value.items.splice(index, 1);
  if (!form.value.items.length) addItem();
}

function chooseCatalog(item, event) {
  const id = Number(event.target.value);
  const selected = catalog.value.find(x => x.id === id);
  if (!selected) return;

  item.description = selected.label;
  item.unitPrice = selected.price;
  item.referenceType = selected.referenceType;
  item.referenceId = selected.id;
}

function chooseTest(item, event) {
  const id = Number(event.target.value) || null;
  const selected = catalog.value.find(x => x.id === id);

  item.referenceId = id;
  item.referenceType = selected ? selected.referenceType : null;
  item.description = selected ? selected.label : '';
  item.unitPrice = selected ? selected.price : 0;
}

function raw(item) {
  return Number(item.quantity || 0) * Number(item.unitPrice || 0);
}

function discountObject(id) {
  return discounts.value.find(x => x.id === id && x.isActive);
}

function discountAmountFor(amount, id, scope) {
  const d = discountObject(id);
  if (!d || d.scope !== scope) return 0;
  const value = Number(d.value || 0);
  return d.discountMode === 'Amount'
    ? Math.min(amount, value)
    : amount * Math.min(100, Math.max(0, value)) / 100;
}

function itemDiscount(item) {
  return discountAmountFor(raw(item), item.discountTypeId, 'Individual');
}

function itemNet(item) {
  return raw(item) - itemDiscount(item);
}

function beforeDiscount() {
  return form.value.items.reduce((total, item) => total + raw(item), 0);
}

function individualDiscount() {
  return form.value.items.reduce((total, item) => total + itemDiscount(item), 0);
}

function afterIndividualDiscount() {
  return beforeDiscount() - individualDiscount();
}

function bulkDiscount() {
  return discountAmountFor(afterIndividualDiscount(), form.value.bulkDiscountTypeId, 'Bulk');
}

function afterDiscount() {
  return afterIndividualDiscount() - bulkDiscount();
}

// Small manual nudge (-9..9) to land the total on a round number, e.g. 448 -> 450.
// Never shown as its own line on the printed bill - it's just folded into the total.
function finalAmount() {
  return afterDiscount() + Number(form.value.roundOff || 0);
}

function currentPaid() {
  return Number(editing.value?.paidAmount || 0);
}

function currentOutstanding() {
  return Math.max(0, finalAmount() - currentPaid());
}

async function save() {
  errors.value = [];

  if (!form.value.patientId && !form.value.walkInPatientName?.trim()) errors.value.push('Patient is required.');
  if (!form.value.billType) errors.value.push('Bill category is required.');
  if (form.value.items.some(x => !x.description || x.quantity <= 0 || x.unitPrice < 0)) {
    errors.value.push('Every bill line needs description, quantity and valid rate.');
  }
  if (form.value.roundOff < -9 || form.value.roundOff > 9) {
    errors.value.push('Round off must be between -9 and 9.');
  }
  if (finalAmount() < currentPaid()) {
    errors.value.push('Final amount cannot be lower than amount already paid.');
  }

  if (errors.value.length) return;

  try {
    if (editing.value) {
      await api.put('/bills/' + editing.value.id, form.value);
    } else {
      await api.post('/bills', form.value);
    }

    billModal.value = false;
    await load();
  } catch (error) {
    errors.value = [error.response?.data?.message || 'Unable to save bill.'];
  }
}

async function deleteBill(bill) {
  if (!confirm('Delete this bill?')) return;

  try {
    await api.delete('/bills/' + bill.id);
    await load();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to delete bill.');
  }
}

function openPayment(bill) {
  selectedBill.value = bill;
  payment.value = {
    amount: Number((bill.netAmount - bill.paidAmount).toFixed(2)),
    mode: 'Cash',
    reference: ''
  };
  paymentModal.value = true;
}

async function savePayment() {
  try {
    await api.post(`/bills/${selectedBill.value.id}/pay`, payment.value);
    paymentModal.value = false;
    await load();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to accept payment.');
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

async function printBill(bill) {
  const { data } = await api.get(`/bills/${bill.id}/print`);

  const itemRows = data.bill.items.map(item => `
    <tr>
      <td>${item.description}</td>
      <td>${item.quantity}</td>
      <td>₹${Number(item.unitPrice).toFixed(2)}</td>
      <td>₹${Number(item.discountAmount || 0).toFixed(2)}</td>
      <td>₹${Number(item.amount).toFixed(2)}</td>
    </tr>`).join('');

  const header = data.header
    ? `<img class="header-image" src="${assetUrl(data.header)}">`
    : '';

  printHtml(`<!doctype html>
<html>
<head>
  <title></title>
  <style>
    @page { size: A4; margin: 0; }
    * { box-sizing: border-box; }
    html, body { margin: 0; padding: 0; width: 210mm; font-family: Arial, sans-serif; color: #000; }
    .header-image { display: block; width: 210mm; height: 34mm; object-fit: fill; margin: 0; }
    .content { padding: 8mm 12mm 12mm; }
    .patient { border: 1px solid #333; padding: 9px; margin-bottom: 12px; display: grid; grid-template-columns: 1fr 1fr; gap: 6px 16px; font-size: 13px; }
    table { width: 100%; border-collapse: collapse; }
    th, td { border: 1px solid #333; padding: 7px; text-align: left; font-size: 12px; }
    .summary { width: 92mm; margin: 15px 0 0 auto; border: 1px solid #333; padding: 8px 12px; }
    .summary div { display: flex; justify-content: space-between; padding: 4px 0; }
    .summary .total { border-top: 1px solid #333; margin-top: 5px; padding-top: 8px; font-size: 16px; }
  </style>
</head>
<body>
  ${header}
  <div class="content">
    <div class="patient">
      <div><b>Patient:</b> ${data.patient?.name || data.bill.walkInPatientName || '-'}</div>
      <div><b>Patient ID:</b> ${data.patient?.patientCode || '-'}</div>
      <div><b>Age / Gender:</b> ${data.patient ? `${data.patient.age} / ${data.patient.gender}` : '-'}</div>
      <div><b>Phone:</b> ${data.patient?.phone || '-'}</div>
      <div><b>Bill Type:</b> ${data.bill.billType}</div>
      ${data.referrer ? `<div><b>Referrer:</b> ${data.referrer.name} (${data.referrer.referrerCode})</div>` : ``}
    </div>
    <table>
      <tr><th>Description</th><th>Qty</th><th>Rate</th><th>Individual Discount</th><th>Amount</th></tr>
      ${itemRows}
    </table>
    <div class="summary">
      <div><span>Before Discount</span><b>₹${Number(data.totals.beforeDiscount).toFixed(2)}</b></div>
      <div><span>Individual Discount</span><b>₹${Number(data.totals.individualDiscount).toFixed(2)}</b></div>
      <div><span>After Individual Discount</span><b>₹${Number(data.totals.afterIndividualDiscount).toFixed(2)}</b></div>
      <div><span>Bulk Discount</span><b>₹${Number(data.totals.bulkDiscount).toFixed(2)}</b></div>
      <div class="total"><span>After Discount</span><b>₹${Number(data.totals.afterDiscount).toFixed(2)}</b></div>
      <div><span>Paid</span><b>₹${Number(data.totals.paid).toFixed(2)}</b></div>
      <div><span>Outstanding</span><b>₹${Number(data.totals.outstanding).toFixed(2)}</b></div>
    </div>
  </div>
</body>
</html>`);
}

onMounted(load);
</script>

<template>
  <h1>Bill</h1>

  <div class="toolbar">
    <button v-if="can('BILL', 'add')" @click="newBill">+ Create Bill</button>
    <button :class="tab === 'Unpaid' ? '' : 'secondary'" @click="changeTab('Unpaid')">Unpaid Bill</button>
    <button :class="tab === 'Paid' ? '' : 'secondary'" @click="changeTab('Paid')">Paid Bill</button>
  </div>

  <div class="toolbar report-filter">
    <label class="toggle-row"><input type="checkbox" v-model="allDates" @change="load"> All Dates</label>
    <label v-if="!allDates">From<input type="date" v-model="dateFrom"></label>
    <label v-if="!allDates">To<input type="date" v-model="dateTo"></label>
    <button v-if="!allDates" @click="load">Apply</button>
  </div>

  <div class="grid-filter-row">
    <GridSearch v-model="search" placeholder="Search bill number, type, patient, amount, status..." />
    <label>
      Bill Type
      <select v-model="billTypeFilter" @change="page = 1">
        <option value="All">All Bill Types</option>
        <option v-for="category in categories" :key="category.id" :value="category.name">{{ category.name }}</option>
      </select>
    </label>
  </div>

  <table class="table">
    <tr>
      <th class="sortable" @click="sortBy('billNumber')">Bill <span class="sort-indicator">{{ sortIndicator('billNumber') }}</span></th>
      <th class="sortable" @click="sortBy('billType')">Type <span class="sort-indicator">{{ sortIndicator('billType') }}</span></th>
      <th class="sortable" @click="sortBy('referrerName')">Referrer <span class="sort-indicator">{{ sortIndicator('referrerName') }}</span></th>
      <th class="sortable" @click="sortBy('grossAmount')">Before Discount <span class="sort-indicator">{{ sortIndicator('grossAmount') }}</span></th>
      <th class="sortable" @click="sortBy('discountAmount')">Bulk Discount <span class="sort-indicator">{{ sortIndicator('discountAmount') }}</span></th>
      <th class="sortable" @click="sortBy('netAmount')">After Discount <span class="sort-indicator">{{ sortIndicator('netAmount') }}</span></th>
      <th class="sortable" @click="sortBy('paidAmount')">Paid <span class="sort-indicator">{{ sortIndicator('paidAmount') }}</span></th>
      <th>Outstanding</th>
      <th class="sortable" @click="sortBy('status')">Status <span class="sort-indicator">{{ sortIndicator('status') }}</span></th>
      <th></th>
    </tr>
    <tr v-for="bill in pagedRows" :key="bill.id">
      <td>{{ bill.billNumber }}</td>
      <td>{{ bill.billType }}</td>
      <td>{{ bill.referrerName || '-' }}</td>
      <td>₹{{ Number(bill.grossAmount).toFixed(2) }}</td>
      <td>₹{{ Number(bill.discountAmount).toFixed(2) }}</td>
      <td>₹{{ Number(bill.netAmount).toFixed(2) }}</td>
      <td>₹{{ Number(bill.paidAmount).toFixed(2) }}</td>
      <td>₹{{ Number(bill.netAmount - bill.paidAmount).toFixed(2) }}</td>
      <td>{{ bill.status }}</td>
      <td class="actions">
        <button @click="printBill(bill)">Print</button>
        <button v-if="can('BILL', 'edit')" @click="editBill(bill)">Edit</button>
        <button v-if="bill.status !== 'Paid'" @click="openPayment(bill)">Accept Payment</button>
        <button v-if="can('BILL', 'delete')" class="danger-btn" @click="deleteBill(bill)">Delete</button>
      </td>
    </tr>
  </table>

  <Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="pageSize" @update:page="page = $event" />

  <div v-if="billModal" class="modal-bg">
    <div class="modal modal-xl">
      <button class="modal-close-x" @click="billModal = false">×</button>
      <h2>{{ editing ? 'Edit' : 'Create' }} Bill</h2>

      <div v-if="errors.length" class="error-box">
        <div v-for="error in errors" :key="error">{{ error }}</div>
      </div>

      <div class="form-grid">
        <label class="patient-combo">
          Patient *
          <input
            v-model="patientQuery"
            @input="onPatientInput"
            @focus="showPatientOptions = true"
            @blur="onPatientBlur"
            autocomplete="off"
            placeholder="Type to search patients, or enter a new name"
          >
          <div v-if="showPatientOptions && filteredPatients.length" class="patient-options">
            <div
              v-for="patient in filteredPatients"
              :key="patient.id"
              @mousedown.prevent="selectPatient(patient)"
            >
              {{ patient.name }} · {{ patient.patientCode }} · {{ patient.phone }}
            </div>
          </div>
          <div v-if="showPatientOptions && patientQuery.trim() && !form.patientId" class="muted patient-walkin-hint">
            No matching patient — "{{ patientQuery.trim() }}" will be saved as a walk-in name.
          </div>
        </label>
        <div v-if="selectedPatient" class="card referral-inline-card">
          <div><b>Source:</b> {{ selectedPatient.marketingSource || 'Walk-in' }}</div>
          <div><b>Referrer:</b> {{ selectedPatient.referrerName || 'None' }}</div>
        </div>
        <label>
          Doctor / Consultant
          <select v-model.number="form.doctorId">
            <option :value="null">Not Applicable / Unassigned</option>
            <option v-for="doctor in doctors.filter(x => x.isActive)" :key="doctor.id" :value="doctor.id">{{ doctor.name }}</option>
          </select>
        </label>
        <label>
          Bill Type *
          <select v-model="form.billType">
            <option v-for="category in categories" :key="category.id" :value="category.name">{{ category.name }}</option>
          </select>
        </label>
      </div>

      <h3>Bill Items</h3>
      <table class="table">
        <tr><th>Description</th><th>Qty</th><th>Rate</th><th>Individual Discount</th><th>Net</th><th></th></tr>
        <tr v-for="(item, index) in form.items" :key="index">
          <td>
            <template v-if="form.billType === 'Lab'">
              <select :value="item.referenceId ?? ''" @change="chooseTest(item, $event)">
                <option value="">Select Test</option>
                <option v-for="entry in catalog" :key="entry.id" :value="entry.id">{{ entry.label }} · ₹{{ entry.price }}</option>
              </select>
            </template>
            <template v-else>
              <select v-if="catalog.length" @change="chooseCatalog(item, $event)">
                <option value="">Select existing item or enter manually</option>
                <option v-for="entry in catalog" :key="entry.id" :value="entry.id">{{ entry.label }} · ₹{{ entry.price }}</option>
              </select>
              <input v-model="item.description" style="margin-top: 5px" placeholder="Description">
            </template>
          </td>
          <td><input v-model.number="item.quantity" type="number" min="1"></td>
          <td><input v-model.number="item.unitPrice" type="number" min="0"></td>
          <td>
            <select v-model.number="item.discountTypeId">
              <option :value="null">No Discount</option>
              <option v-for="discount in individualDiscounts" :key="discount.id" :value="discount.id">
                {{ discount.name }} · {{ discount.discountMode === 'Percent' ? discount.value + '%' : '₹' + Number(discount.value).toFixed(2) }}
              </option>
            </select>
          </td>
          <td>₹{{ itemNet(item).toFixed(2) }}</td>
          <td><button class="danger-btn" @click="removeItem(index)">×</button></td>
        </tr>
      </table>

      <div class="toolbar"><button @click="addItem">+ Add Item</button></div>

      <h3>Bulk Discount</h3>
      <div class="discount-box">
        <label>
          Approved Bulk Discount
          <select v-model.number="form.bulkDiscountTypeId">
            <option :value="null">No Bulk Discount</option>
            <option v-for="discount in bulkDiscounts" :key="discount.id" :value="discount.id">
              {{ discount.name }} · {{ discount.discountMode === 'Percent' ? discount.value + '%' : '₹' + Number(discount.value).toFixed(2) }}
            </option>
          </select>
        </label>
      </div>

      <div class="bill-total-box consistent-total">
        <div><span>Before Discount</span><b>₹{{ beforeDiscount().toFixed(2) }}</b></div>
        <div><span>Individual Discount</span><b>₹{{ individualDiscount().toFixed(2) }}</b></div>
        <div><span>After Individual Discount</span><b>₹{{ afterIndividualDiscount().toFixed(2) }}</b></div>
        <div><span>Bulk Discount</span><b>₹{{ bulkDiscount().toFixed(2) }}</b></div>
        <div class="grand"><span>After Discount</span><b>₹{{ afterDiscount().toFixed(2) }}</b></div>
        <div>
          <span>Round Off (-9 to 9)</span>
          <input
            v-model.number="form.roundOff"
            type="number"
            min="-9"
            max="9"
            step="1"
            style="width:80px;display:inline-block"
          >
        </div>
        <div class="grand"><span>Final Amount</span><b>₹{{ finalAmount().toFixed(2) }}</b></div>
        <div><span>Paid</span><b>₹{{ currentPaid().toFixed(2) }}</b></div>
        <div><span>Outstanding</span><b>₹{{ currentOutstanding().toFixed(2) }}</b></div>
      </div>

      <div class="modal-actions">
        <button @click="save">Save Bill</button>
        <button class="secondary" @click="billModal = false">Cancel</button>
      </div>
    </div>
  </div>

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
        <label class="full">Reference<input v-model="payment.reference"></label>
      </div>
      <div class="modal-actions">
        <button @click="savePayment">Save Payment</button>
        <button class="secondary" @click="paymentModal = false">Cancel</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.patient-combo { position: relative; }
.patient-options {
  position: absolute;
  z-index: 30;
  top: 100%;
  left: 0;
  right: 0;
  margin-top: 2px;
  background: #fff;
  border: 1px solid #cbd5e1;
  border-radius: 7px;
  max-height: 220px;
  overflow: auto;
  box-shadow: 0 4px 14px rgba(0, 0, 0, .12);
}
.patient-options div { padding: 8px 10px; cursor: pointer; }
.patient-options div:hover { background: #eef2ff; }
.patient-walkin-hint { margin-top: 4px; }
</style>
