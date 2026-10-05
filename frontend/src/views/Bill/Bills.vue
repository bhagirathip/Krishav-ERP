<script setup>
import { ref, onMounted, watch, computed } from 'vue';
import { api, assetUrl } from '../../api';
import { can, role } from '../../auth';
import Pagination from '../../components/Pagination.vue';
import GridSearch from '../../components/GridSearch.vue';
import { useGrid } from '../../composables/useGrid';
import { ageText } from '../../utils/age';

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

// Grouped view ignores paging (a patient's bills shouldn't be split across
// pages) but still respects the current search/sort/bill-type/date filters,
// since it's built from the same `sortedRows` the flat table uses.
const groupByPatient = ref(false);
const groupedRows = computed(() => {
  const patientMap = new Map();

  for (const bill of sortedRows.value) {
    const key = bill.patientId ?? `walkin:${bill.patientName || 'Unknown'}`;
    if (!patientMap.has(key)) {
      patientMap.set(key, {
        key,
        patientName: bill.patientName || 'Walk-in',
        patientCode: bill.patientCode || null,
        patientPhone: bill.patientPhone || null,
        bills: [],
        typeGroups: new Map()
      });
    }

    const group = patientMap.get(key);
    group.bills.push(bill);
    if (!group.typeGroups.has(bill.billType)) group.typeGroups.set(bill.billType, []);
    group.typeGroups.get(bill.billType).push(bill);
  }

  return Array.from(patientMap.values()).map(group => ({
    ...group,
    typeGroups: Array.from(group.typeGroups.entries()).map(([type, bills]) => ({ type, bills }))
  }));
});
const patients = ref([]);
const doctors = ref([]);
const categories = ref([]);
const catalog = ref([]);
const discounts = ref([]);
const individualDiscounts = computed(() => discounts.value.filter(x => x.isActive && x.scope === 'Individual'));

// The Bulk Discount section is a privileged control - only Manager and
// Administrator see the dropdown at all; every other role never sees any
// Bulk Discount control. "Admin Discount" is further hidden from the
// dropdown unless the current role is Administrator (Manager would only
// ever get rejected trying it, so there's no point listing it as a dead
// end). A General Manager never sees this dropdown either - they get their
// own raw %/amount box instead (see isGeneralManager below), which (like
// the dropdown's Manager/Admin Discount entries) still offers a
// Percent/Amount mode choice.
const currentRole = role();
const isGeneralManager = computed(() => currentRole === 'General Manager');
const canSeeBulkDiscount = computed(() => currentRole === 'Manager' || currentRole === 'Administrator');
const bulkDiscounts = computed(() =>
  discounts.value.filter(x => x.isActive && x.scope === 'Bulk' && (currentRole === 'Administrator' || x.name !== 'Admin Discount'))
);
const managerDiscountMaxPercent = ref(15);
const generalManagerDiscountMaxPercent = ref(10);
const selectedBulkDiscount = computed(() => discountObject(form.value.bulkDiscountTypeId));
const isVariableBulkDiscount = computed(() =>
  selectedBulkDiscount.value && (selectedBulkDiscount.value.name === 'Manager Discount' || selectedBulkDiscount.value.name === 'Admin Discount')
);
const selectedBulkDiscountCap = computed(() =>
  selectedBulkDiscount.value?.name === 'Admin Discount' ? 100 : managerDiscountMaxPercent.value
);
// The cap is stored as a %, but in Amount mode the user needs to see it in
// rupees - it's the same ceiling either way, just converted against the
// current bill total rather than always displayed as a percentage.
const selectedBulkDiscountCapAmount = computed(() => afterIndividualDiscount() * selectedBulkDiscountCap.value / 100);
const generalManagerDiscountCapAmount = computed(() => afterIndividualDiscount() * generalManagerDiscountMaxPercent.value / 100);
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
  bulkDiscountValue: null,
  bulkDiscountMode: 'Percent',
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
  const [billsRes, patientsRes, doctorsRes, categoriesRes, discountsRes, settingsRes] = await Promise.all([
    api.get('/bills', { params: { status: tab.value, ...dateParams } }),
    api.get('/patients'),
    api.get('/doctors'),
    api.get('/bill-types', { params: { billableOnly: true } }),
    api.get('/discounts'),
    api.get('/settings')
  ]);

  rows.value = billsRes.data;
  patients.value = patientsRes.data;
  doctors.value = doctorsRes.data;
  categories.value = categoriesRes.data;
  discounts.value = discountsRes.data;

  const setting = name => Number(settingsRes.data.find(x => x.name === name)?.value);
  if (!Number.isNaN(setting('Manager Discount Max Percent'))) managerDiscountMaxPercent.value = setting('Manager Discount Max Percent');
  if (!Number.isNaN(setting('General Manager Discount Max Percent'))) generalManagerDiscountMaxPercent.value = setting('General Manager Discount Max Percent');
}

async function loadCatalog() {
  catalog.value = (await api.get('/bills/catalog', {
    params: { type: form.value.billType, doctorId: form.value.doctorId || undefined }
  })).data;
}

watch(() => [form.value.billType, form.value.doctorId], loadCatalog);

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
    // Mode isn't persisted per-bill, so re-editing always reloads as Percent
    // - the stored value was already resolved to a percent either way. The
    // user can retype it (or switch to Amount for a GM entry) if they want
    // to change it; leaving it blank just means "no bulk discount this time".
    bulkDiscountValue: (bill.bulkDiscountTypeId || Number(bill.discountAmount) > 0) ? Number(bill.discountPercent) || null : null,
    bulkDiscountMode: 'Percent',
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

// A live client-side preview only - the authoritative amount is always
// recomputed (and capped/role-checked) on the server at save time.
function bulkDiscount() {
  const gross = afterIndividualDiscount();

  if (isGeneralManager.value) {
    const value = Math.max(0, Number(form.value.bulkDiscountValue || 0));
    return form.value.bulkDiscountMode === 'Amount'
      ? Math.min(gross, value)
      : gross * Math.min(100, value) / 100;
  }

  if (isVariableBulkDiscount.value) {
    const value = Math.max(0, Number(form.value.bulkDiscountValue || 0));
    if (form.value.bulkDiscountMode === 'Amount') {
      const maxAmount = gross * selectedBulkDiscountCap.value / 100;
      return Math.min(maxAmount, value);
    }
    const percent = Math.min(selectedBulkDiscountCap.value, value);
    return gross * percent / 100;
  }

  return discountAmountFor(gross, form.value.bulkDiscountTypeId, 'Bulk');
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

function currentReturned() {
  return Math.max(0, currentPaid() - finalAmount());
}

async function save() {
  errors.value = [];

  // RoundOff accepts decimals (e.g. 5.05) but is still money, so clamp to 2
  // decimal places - same precision the server rounds to.
  form.value.roundOff = Math.round((Number(form.value.roundOff) || 0) * 100) / 100;

  if (!form.value.patientId && !form.value.walkInPatientName?.trim()) errors.value.push('Patient is required.');
  if (!form.value.billType) errors.value.push('Bill category is required.');
  if (form.value.items.some(x => !x.description || x.quantity <= 0 || x.unitPrice < 0)) {
    errors.value.push('Every bill line needs description, quantity and valid rate.');
  }
  if (form.value.roundOff < -9 || form.value.roundOff > 9) {
    errors.value.push('Round off must be between -9 and 9.');
  }
  if (isGeneralManager.value && Number(form.value.bulkDiscountValue || 0) > generalManagerDiscountMaxPercent.value) {
    errors.value.push(`Discount cannot exceed ${generalManagerDiscountMaxPercent.value}% of the bill.`);
  }
  if (isVariableBulkDiscount.value) {
    const value = Number(form.value.bulkDiscountValue || 0);
    if (form.value.bulkDiscountMode === 'Amount') {
      const maxAmount = afterIndividualDiscount() * selectedBulkDiscountCap.value / 100;
      if (value > maxAmount) {
        errors.value.push(`${selectedBulkDiscount.value.name} amount cannot exceed ₹${maxAmount.toFixed(2)} (${selectedBulkDiscountCap.value}% of the bill).`);
      }
    } else if (value > selectedBulkDiscountCap.value) {
      errors.value.push(`${selectedBulkDiscount.value.name} cannot exceed ${selectedBulkDiscountCap.value}%.`);
    }
  }

  if (errors.value.length) return;

  try {
    if (editing.value) {
      const { data } = await api.put('/bills/' + editing.value.id, form.value);
      if (data.refundAmount > 0) {
        alert(`The edited total is ₹${Number(data.refundAmount).toFixed(2)} less than what was already paid - this amount has been recorded as returned to the patient.`);
      }
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

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function printableNow() {
  const d = new Date();
  const pad = n => String(n).padStart(2, '0');
  return `${pad(d.getDate())}-${pad(d.getMonth() + 1)}-${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

// "Received ₹X as Cash and ₹Y as UPI, and ₹Z outstanding for payment." -
// groups every payment on this bill by mode rather than just naming the
// latest one, since a bill can be settled in more than one mode over time.
function paymentSummarySentence(payments, outstanding) {
  const byMode = {};
  (payments || []).forEach(p => { byMode[p.mode || 'Unknown'] = (byMode[p.mode || 'Unknown'] || 0) + Number(p.amount || 0); });
  const parts = Object.entries(byMode).map(([mode, amount]) => `₹${amount.toFixed(2)} as ${mode}`);
  let sentence = parts.length ? `Received ${parts.join(' and ')}` : 'No payment received yet';
  sentence += outstanding > 0.004 ? `, and ₹${outstanding.toFixed(2)} outstanding for payment.` : '.';
  return sentence;
}

// Grid column: the mode(s) a Paid bill was actually settled in (e.g. "Cash",
// or "Cash, UPI" if paid across more than one mode) - blank until a bill is
// fully Paid, since a Partial/Unpaid bill's payment history isn't final yet.
function paymentModesText(bill) {
  if (bill.status !== 'Paid' || !bill.payments?.length) return '-';
  const modes = [...new Set(bill.payments.map(p => p.mode))];
  return modes.join(', ');
}

// Builds just the printable content for one bill (header image + info block +
// items table + GST/payment summary) - reused both for a single-bill print
// and for the "Print All" per-patient job, where several of these get
// concatenated into one document with a page break between each.
function billContentHtml(data) {
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
      <td></td>
      <td><b>Total</b></td>
      <td></td>
      <td></td>
      <td><b>₹${Number(data.totals.beforeDiscount).toFixed(2)}</b></td>
      <td><b>₹${totalDiscount.toFixed(2)}</b></td>
      <td><b>₹${Number(data.totals.afterDiscount).toFixed(2)}</b></td>
    </tr>`;

  const header = data.header
    ? `<img class="header-image" src="${assetUrl(data.header)}">`
    : '';

  // The bill amount is already treated as tax-inclusive (GST rate defaults
  // to 0/exempt) - this is a breakdown of data.totals.afterDiscount, not an
  // addition to it, so the Total row above is unaffected either way. HSN is
  // shown as its own column on every line instead of once here.
  const gst = data.gstSummary;
  const gstRows = `
    <div class="pi-row"><span><b>Taxable Value:</b> ₹${Number(gst.taxableValue).toFixed(2)}</span><span><b>CGST (${Number(gst.cgstRate)}%):</b> ₹${Number(gst.cgstAmount).toFixed(2)}</span></div>
    <div class="pi-row"><span><b>SGST (${Number(gst.sgstRate)}%):</b> ₹${Number(gst.sgstAmount).toFixed(2)}</span><span><b>IGST (${Number(gst.igstRate)}%):</b> ₹${Number(gst.igstAmount).toFixed(2)}</span></div>`;

  return `
  ${header}
  <div class="content">
    <div class="patient-info">
      <div class="pi-row"><span><b>Bill No:</b> ${data.bill.billNumber}</span><span><b>GST No:</b> ${data.hospitalGstNo || '-'}</span></div>
      <div class="pi-row"><span><b>Patient:</b> ${data.patient?.name || data.bill.walkInPatientName || '-'}</span><span><b>Patient ID:</b> ${data.patient?.patientCode || '-'}</span></div>
      <div class="pi-row"><span><b>Age / Gender:</b> ${data.patient ? `${ageText(data.patient)} / ${data.patient.gender}` : '-'}</span><span><b>Phone:</b> ${data.patient?.phone || '-'}</span></div>
      <div class="pi-row"><span><b>Bill Type:</b> ${data.bill.billType}</span><span><b>Date &amp; Time:</b> ${printableNow()}</span></div>
      ${data.referrer ? `<div class="pi-row"><span><b>Referrer:</b> ${data.referrer.name} (${data.referrer.referrerCode})</span><span></span></div>` : ''}
    </div>
    <table>
      <thead><tr><th class="no-col">Sl No</th><th>Description</th><th>HSN Code</th><th>Qty</th><th>Rate</th><th>Discount</th><th>Amount</th></tr></thead>
      <tbody>${itemRows}${totalRow}</tbody>
    </table>
    <div class="gst-summary">${gstRows}</div>
    <div class="payment-summary">${escapeHtml(paymentSummarySentence(data.bill.payments, Number(data.totals.outstanding)))}</div>
    <div class="signature-line">Authorized Signature</div>
  </div>`;
}

// Wraps one or more bill content blocks into a single printable document.
// Each section gets its own page via page-break-after, so "Print All" for a
// patient opens one print dialog that pages through every bill in one go.
function wrapPrintDocument(sections) {
  return `<!doctype html>
<html>
<head>
  <title></title>
  <style>
    @page { size: A4; margin: 0; }
    * { box-sizing: border-box; }
    html, body { margin: 0; padding: 0; width: 210mm; font-family: Arial, sans-serif; color: #000; }
    .bill-page { page-break-after: always; }
    .bill-page:last-child { page-break-after: auto; }
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
<body>${sections.map(section => `<div class="bill-page">${section}</div>`).join('')}</body>
</html>`;
}

async function printBill(bill) {
  const { data } = await api.get(`/bills/${bill.id}/print`);
  printHtml(wrapPrintDocument([billContentHtml(data)]));
}

// Fetches every bill in a patient group (across all its bill-type subgroups)
// and prints them as one combined job - one print dialog, one page per bill.
async function printPatientGroup(group) {
  const results = await Promise.all(group.bills.map(bill => api.get(`/bills/${bill.id}/print`)));
  printHtml(wrapPrintDocument(results.map(r => billContentHtml(r.data))));
}

onMounted(load);
</script>

<template>
  <h1>Bill</h1>

  <div class="toolbar">
    <button v-if="can('BILL', 'add')" @click="newBill">+ Create Bill</button>
    <button :class="tab === 'Unpaid' ? '' : 'secondary'" @click="changeTab('Unpaid')">Unpaid Bill</button>
    <button :class="tab === 'Paid' ? '' : 'secondary'" @click="changeTab('Paid')">Paid Bill</button>
    <button :class="tab === 'All' ? '' : 'secondary'" @click="changeTab('All')">All Bills</button>
    <button :class="groupByPatient ? '' : 'secondary'" @click="groupByPatient = !groupByPatient">{{ groupByPatient ? 'Flat View' : 'Group by Patient' }}</button>
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

  <template v-if="!groupByPatient">
    <table class="table">
      <tr>
        <th class="sortable" @click="sortBy('billNumber')">Bill <span class="sort-indicator">{{ sortIndicator('billNumber') }}</span></th>
        <th class="sortable" @click="sortBy('patientName')">Patient <span class="sort-indicator">{{ sortIndicator('patientName') }}</span></th>
        <th class="sortable" @click="sortBy('billType')">Type <span class="sort-indicator">{{ sortIndicator('billType') }}</span></th>
        <th class="sortable" @click="sortBy('referrerName')">Referrer <span class="sort-indicator">{{ sortIndicator('referrerName') }}</span></th>
        <th class="sortable" @click="sortBy('grossAmount')">Before Discount <span class="sort-indicator">{{ sortIndicator('grossAmount') }}</span></th>
        <th class="sortable" @click="sortBy('discountAmount')">Bulk Discount <span class="sort-indicator">{{ sortIndicator('discountAmount') }}</span></th>
        <th class="sortable" @click="sortBy('netAmount')">After Discount <span class="sort-indicator">{{ sortIndicator('netAmount') }}</span></th>
        <th class="sortable" @click="sortBy('paidAmount')">Paid <span class="sort-indicator">{{ sortIndicator('paidAmount') }}</span></th>
        <th>Outstanding</th>
        <th class="sortable" @click="sortBy('status')">Status <span class="sort-indicator">{{ sortIndicator('status') }}</span></th>
        <th>Mode</th>
        <th>Returned</th>
        <th></th>
      </tr>
      <tr v-for="bill in pagedRows" :key="bill.id">
        <td>{{ bill.billNumber }}</td>
        <td>{{ bill.patientName || '-' }}</td>
        <td>{{ bill.billType }}</td>
        <td>{{ bill.referrerName || '-' }}</td>
        <td>₹{{ Number(bill.grossAmount).toFixed(2) }}</td>
        <td>₹{{ Number(bill.discountAmount).toFixed(2) }}</td>
        <td>₹{{ Number(bill.netAmount).toFixed(2) }}</td>
        <td>₹{{ Number(bill.paidAmount).toFixed(2) }}</td>
        <td>₹{{ Number(bill.netAmount - bill.paidAmount).toFixed(2) }}</td>
        <td>{{ bill.status }}</td>
        <td>{{ paymentModesText(bill) }}</td>
        <td>{{ Number(bill.refundedAmount || 0) > 0 ? '₹' + Number(bill.refundedAmount).toFixed(2) : '-' }}</td>
        <td class="actions">
          <button @click="printBill(bill)">Print</button>
          <button v-if="can('BILL', 'edit')" @click="editBill(bill)">Edit</button>
          <button v-if="bill.status !== 'Paid'" @click="openPayment(bill)">Accept Payment</button>
          <button v-if="can('BILL', 'delete')" class="danger-btn" @click="deleteBill(bill)">Delete</button>
        </td>
      </tr>
    </table>

    <Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="pageSize" @update:page="page = $event" />
  </template>

  <template v-else>
    <div v-if="!groupedRows.length" class="muted">No bills found.</div>
    <div v-for="group in groupedRows" :key="group.key" class="patient-group">
      <div class="patient-group-header">
        <div>
          <b>{{ group.patientName }}</b>
          <span v-if="group.patientCode" class="muted"> · {{ group.patientCode }}</span>
          <span v-if="group.patientPhone" class="muted"> · {{ group.patientPhone }}</span>
          <span class="muted"> · {{ group.bills.length }} bill{{ group.bills.length === 1 ? '' : 's' }}</span>
        </div>
        <button @click="printPatientGroup(group)">Print All</button>
      </div>

      <div v-for="typeGroup in group.typeGroups" :key="typeGroup.type" class="bill-type-group">
        <h4>{{ typeGroup.type }}</h4>
        <table class="table">
          <tr>
            <th>Bill</th>
            <th>Referrer</th>
            <th>Before Discount</th>
            <th>Bulk Discount</th>
            <th>After Discount</th>
            <th>Paid</th>
            <th>Outstanding</th>
            <th>Status</th>
            <th>Mode</th>
            <th>Returned</th>
            <th></th>
          </tr>
          <tr v-for="bill in typeGroup.bills" :key="bill.id">
            <td>{{ bill.billNumber }}</td>
            <td>{{ bill.referrerName || '-' }}</td>
            <td>₹{{ Number(bill.grossAmount).toFixed(2) }}</td>
            <td>₹{{ Number(bill.discountAmount).toFixed(2) }}</td>
            <td>₹{{ Number(bill.netAmount).toFixed(2) }}</td>
            <td>₹{{ Number(bill.paidAmount).toFixed(2) }}</td>
            <td>₹{{ Number(bill.netAmount - bill.paidAmount).toFixed(2) }}</td>
            <td>{{ bill.status }}</td>
            <td>{{ paymentModesText(bill) }}</td>
            <td>{{ Number(bill.refundedAmount || 0) > 0 ? '₹' + Number(bill.refundedAmount).toFixed(2) : '-' }}</td>
            <td class="actions">
              <button @click="printBill(bill)">Print</button>
              <button v-if="can('BILL', 'edit')" @click="editBill(bill)">Edit</button>
              <button v-if="bill.status !== 'Paid'" @click="openPayment(bill)">Accept Payment</button>
              <button v-if="can('BILL', 'delete')" class="danger-btn" @click="deleteBill(bill)">Delete</button>
            </td>
          </tr>
        </table>
      </div>
    </div>
  </template>

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

      <template v-if="isGeneralManager">
        <h3>Discount</h3>
        <div class="discount-box">
          <label>
            Discount Mode
            <select v-model="form.bulkDiscountMode">
              <option value="Percent">Percentage</option>
              <option value="Amount">Amount</option>
            </select>
          </label>
          <label>
            {{ form.bulkDiscountMode === 'Amount' ? 'Discount Amount ₹' : 'Discount %' }}
            (max {{ form.bulkDiscountMode === 'Amount' ? '₹' + generalManagerDiscountCapAmount.toFixed(2) : generalManagerDiscountMaxPercent + '%' }})
            <input v-model.number="form.bulkDiscountValue" type="number" min="0" step="0.01">
          </label>
        </div>
      </template>
      <template v-else-if="canSeeBulkDiscount">
        <h3>Bulk Discount</h3>
        <div class="discount-box">
          <label>
            Approved Bulk Discount
            <select v-model.number="form.bulkDiscountTypeId">
              <option :value="null">No Bulk Discount</option>
              <option v-for="discount in bulkDiscounts" :key="discount.id" :value="discount.id">
                {{ discount.name }} ·
                {{
                  discount.name === 'Manager Discount' ? `max ${managerDiscountMaxPercent}%`
                  : discount.name === 'Admin Discount' ? 'max 100%'
                  : (discount.discountMode === 'Percent' ? discount.value + '%' : '₹' + Number(discount.value).toFixed(2))
                }}
              </option>
            </select>
          </label>
          <template v-if="isVariableBulkDiscount">
            <label>
              Discount Mode
              <select v-model="form.bulkDiscountMode">
                <option value="Percent">Percentage</option>
                <option value="Amount">Amount</option>
              </select>
            </label>
            <label>
              {{ form.bulkDiscountMode === 'Amount' ? 'Discount Amount ₹' : 'Discount %' }}
              (max {{ form.bulkDiscountMode === 'Amount' ? '₹' + selectedBulkDiscountCapAmount.toFixed(2) : selectedBulkDiscountCap + '%' }})
              <input v-model.number="form.bulkDiscountValue" type="number" min="0" step="0.01">
            </label>
          </template>
        </div>
      </template>

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
            step="0.01"
            style="width:80px;display:inline-block"
          >
        </div>
        <div class="grand"><span>Final Amount</span><b>₹{{ finalAmount().toFixed(2) }}</b></div>
        <div><span>Paid</span><b>₹{{ currentPaid().toFixed(2) }}</b></div>
        <div><span>Outstanding</span><b>₹{{ currentOutstanding().toFixed(2) }}</b></div>
        <div v-if="currentReturned() > 0"><span>Returned to Patient</span><b>₹{{ currentReturned().toFixed(2) }}</b></div>
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

.patient-group { margin-bottom: 24px; border: 1px solid #e2e8f0; border-radius: 8px; padding: 12px 14px; }
.patient-group-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 10px; }
.bill-type-group { margin-top: 10px; }
.bill-type-group h4 { margin: 0 0 6px; color: #475569; }
</style>
