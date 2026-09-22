<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import { api, assetUrl } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';
import { ageText } from '../utils/age';

const activeTab = ref('bill');
const inventoryRows = ref([]);
const billSearchRows = ref([]);
const cart = ref([]);
const patients = ref([]);
const doctors = ref([]);
const discounts = ref([]);
const patientId = ref(null);
const doctorId = ref(null);
const doctorMode = ref('none');
const outsideDoctorName = ref('');
const walkInPatientName = ref('');
const walkInPhone = ref('');
const paymentMode = ref('Cash');
const sales = ref([]);
const dayEndDate = ref(new Date().toISOString().slice(0, 10));
const dayEnd = ref(null);

const pageSize = 10;
const soldRows = computed(() => dayEnd.value?.soldProducts || []);
const stockRows = computed(() => dayEnd.value?.remainingStock || []);
const { search: inventorySearch, page: inventoryPage, pageCount: inventoryPageCount, pagedRows: pagedInventory, sortedRows: sortedInventory, sortBy: sortInventory, sortIndicator: inventorySortIndicator } = useGrid(inventoryRows, pageSize);
const { search: billSearch, page: billSearchPage, pageCount: billSearchPageCount, pagedRows: pagedBillSearch, sortedRows: sortedBillSearch, sortBy: sortBillSearch, sortIndicator: billSortIndicator } = useGrid(billSearchRows, pageSize);
const { search: salesSearch, page: salesPage, pageCount: salesPageCount, pagedRows: pagedSales, sortedRows: sortedSales, sortBy: sortSales, sortIndicator: salesSortIndicator } = useGrid(sales, pageSize);
const { search: soldSearch, page: soldPage, pageCount: soldPageCount, pagedRows: pagedSold, sortedRows: sortedSold, sortBy: sortSold, sortIndicator: soldSortIndicator } = useGrid(soldRows, pageSize);
const { search: stockSearch, page: stockPage, pageCount: stockPageCount, pagedRows: pagedStock, sortedRows: sortedStock, sortBy: sortStock, sortIndicator: stockSortIndicator } = useGrid(stockRows, pageSize);

const selectedPatient = computed(() => patients.value.find(x => x.id === patientId.value) || null);
const selectedDoctorName = computed(() => {
  if (doctorMode.value === 'outside' && outsideDoctorName.value.trim()) return outsideDoctorName.value.trim();
  const d = doctors.value.find(x => x.id === doctorId.value);
  return d?.name || selectedPatient.value?.doctorName || '-';
});

watch(patientId, value => {
  if (value) {
    const p = patients.value.find(x => x.id === value);
    doctorId.value = p?.doctorId || null;
    doctorMode.value = p?.doctorId ? 'list' : 'none';
    outsideDoctorName.value = '';
    walkInPatientName.value = '';
    walkInPhone.value = '';
  } else {
    doctorId.value = null;
    doctorMode.value = 'none';
    outsideDoctorName.value = '';
  }
});

const totals = computed(() => {
  const gross = cart.value.reduce((sum, row) => sum + rowGross(row), 0);
  const discount = cart.value.reduce((sum, row) => sum + rowDiscount(row), 0);
  return { gross, discount, net: gross - discount };
});

async function load() {
  const [p,d,di]=await Promise.all([api.get('/patients'),api.get('/doctors'),api.get('/discounts',{params:{scope:'Individual',activeOnly:true}})]);
  patients.value=p.data;
  doctors.value=d.data;
  discounts.value=di.data;
  await searchInventory();
  await searchForBill();
  await loadDayEnd();
}

async function searchInventory() {
  inventoryRows.value = (await api.get('/pharmacy/sales/search', { params: { q: '' } })).data;
}

async function searchForBill() {
  billSearchRows.value = (await api.get('/pharmacy/sales/search', { params: { q: '' } })).data;
}

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

function allowedUnits(row) {
  const pack = packLabel(row);
  const loose = looseLabel(row);
  return pack === loose ? [pack] : [pack, loose];
}

function addToCart(row) {
  if (cart.value.some(x => x.purchaseItemId === row.id)) return;
  const unitType = packLabel(row);
  cart.value.push({
    purchaseItemId: row.id,
    medicineType: row.medicineType,
    manufacturer: row.manufacturer,
    hsn: row.hsn,
    productName: row.productName,
    packing: row.packing,
    batchNo: row.batchNo,
    mrp: Number(row.mrp),
    unitsPerPack: Number(row.unitsPerPack),
    remainingUnits: Number(row.remainingTablets),
    unitType,
    quantity: 1,
    unitPrice: Number(row.mrp),
    discountTypeId: null
  });
}

function updateUnit(row) {
  row.unitPrice = row.unitType === packLabel(row)
    ? Number(row.mrp)
    : Number(row.mrp) / Math.max(Number(row.unitsPerPack), 1);
  row.quantity = 1;
  row.discountTypeId = null;
}

function maxQuantity(row) {
  if (row.unitType === packLabel(row)) {
    return Math.floor(Number(row.remainingUnits) / Math.max(Number(row.unitsPerPack), 1));
  }
  return Number(row.remainingUnits);
}

function rowGross(row) {
  return Number(row.unitPrice || 0) * Number(row.quantity || 0);
}

function rowDiscount(row) {
  const d=discounts.value.find(x=>x.id===row.discountTypeId);
  if(!d)return 0;
  const gross=rowGross(row),value=Number(d.value||0);
  return d.discountMode==='Amount'?Math.min(gross,value):gross*Math.min(100,Math.max(0,value))/100;
}

function rowTotal(row) {
  return Math.max(0, rowGross(row) - rowDiscount(row));
}

function removeCart(index) {
  cart.value.splice(index, 1);
}

async function createSale() {
  if (!cart.value.length) {
    alert('Add at least one medicine.');
    return;
  }

  if (!patientId.value && !walkInPatientName.value.trim()) {
    alert('Enter walk-in patient name or select an existing patient.');
    return;
  }

  for (const row of cart.value) {
    if (row.quantity <= 0 || row.quantity > maxQuantity(row)) {
      alert(`Invalid ${row.unitType.toLowerCase()} quantity for ${row.productName}.`);
      return;
    }
  }

  try {
    const { data } = await api.post('/pharmacy/sales', {
      patientId: patientId.value,
      doctorId: doctorMode.value === 'list' ? doctorId.value : null,
      outsideDoctorName: doctorMode.value === 'outside' ? outsideDoctorName.value.trim() : null,
      walkInPatientName: patientId.value ? null : walkInPatientName.value,
      walkInPhone: patientId.value ? null : walkInPhone.value,
      paymentMode: paymentMode.value,
      items: cart.value.map(x => ({
        purchaseItemId: x.purchaseItemId,
        unitType: x.unitType,
        quantity: x.quantity,
        discountTypeId: x.discountTypeId
      }))
    });

    alert(`Sale created successfully. Bill ${data.billNumber}`);
    cart.value = [];
    patientId.value = null;
    doctorId.value = null;
    doctorMode.value = 'none';
    outsideDoctorName.value = '';
    walkInPatientName.value = '';
    walkInPhone.value = '';
    activeTab.value = 'summary';
    await searchInventory();
    await searchForBill();
    await loadDayEnd();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to create pharmacy sale.');
  }
}

async function loadDayEnd() {
  dayEnd.value = (await api.get('/pharmacy/sales/day-end', { params: { date: dayEndDate.value } })).data;
  sales.value = (await api.get('/pharmacy/sales', { params: { date: dayEndDate.value } })).data;
  salesPage.value = soldPage.value = stockPage.value = 1;
}

async function saleData(row) {
  return (await api.get(`/pharmacy/sales/${row.id}/print`)).data;
}

async function printSale(row) {
  const data = await saleData(row);
  const itemRows = data.sale.items.map(item => `
    <tr><td>${item.productName}</td><td>${item.batchNo}</td><td>${item.unitType}</td><td>${item.quantity}</td><td>₹${Number(item.unitPrice).toFixed(2)}</td><td>₹${Number(item.discountAmount).toFixed(2)}</td><td>₹${Number(item.totalAmount).toFixed(2)}</td></tr>
  `).join('');
  const frame = document.createElement('iframe');
  frame.style.cssText = 'position:fixed;width:0;height:0;border:0';
  document.body.appendChild(frame);
  const doc = frame.contentWindow.document;
  doc.open();
  doc.write(`<!doctype html><html><head><title></title><style>@page{size:A4;margin:0}body{margin:0;font-family:Arial}.header img{width:100%;max-height:150px;object-fit:fill}.content{padding:12mm}.patient{border:1px solid #222;padding:9px;margin-bottom:12px}table{width:100%;border-collapse:collapse}th,td{border:1px solid #333;padding:7px}.total{width:340px;margin:16px 0 0 auto}</style></head><body><div class="header">${data.header ? `<img src="${assetUrl(data.header)}">` : ''}</div><div class="content"><div class="patient"><b>Patient:</b> ${data.patient?.name || data.sale.walkInPatientName || 'Walk-in'} &nbsp; <b>Phone:</b> ${data.patient?.phone || data.sale.walkInPhone || '-'}<br><b>Doctor:</b> ${data.doctor?.name || data.sale.outsideDoctorName || '-'} &nbsp; <b>Payment:</b> ${data.sale.paymentMode}</div><table><tr><th>Product</th><th>Batch</th><th>Unit</th><th>Qty</th><th>Price</th><th>Discount</th><th>Total</th></tr>${itemRows}</table><div class="total"><b>Total: ₹${Number(data.sale.totalAmount).toFixed(2)}</b></div></div><script>window.onload=()=>window.print();window.onafterprint=()=>window.parent.postMessage('pharmacy-print-complete','*');<\/script></body></html>`);
  doc.close();
  const listener = event => {
    if (event.data === 'pharmacy-print-complete') {
      frame.remove();
      window.removeEventListener('message', listener);
    }
  };
  window.addEventListener('message', listener);
}

function csvEscape(value) {
  return `"${String(value ?? '').replaceAll('"', '""')}"`;
}

function downloadBlob(content, type, fileName) {
  const blob = new Blob([content], { type });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

async function downloadCsv(row) {
  const data = await saleData(row);
  const lines = [['Product','Batch','Unit','Quantity','Price','Discount','Total'].map(csvEscape).join(',')];
  for (const item of data.sale.items) {
    lines.push([item.productName,item.batchNo,item.unitType,item.quantity,item.unitPrice,item.discountAmount,item.totalAmount].map(csvEscape).join(','));
  }
  lines.push('');
  lines.push(['Total', data.sale.totalAmount].map(csvEscape).join(','));
  downloadBlob(lines.join('\n'), 'text/csv;charset=utf-8', `${data.sale.saleNumber}.csv`);
}

async function downloadExcel(row) {
  const data = await saleData(row);
  const rows = data.sale.items.map(item => `<tr><td>${item.productName}</td><td>${item.batchNo}</td><td>${item.unitType}</td><td>${item.quantity}</td><td>${item.unitPrice}</td><td>${item.discountAmount}</td><td>${item.totalAmount}</td></tr>`).join('');
  const html = `<html><head><meta charset="utf-8"></head><body><table border="1"><tr><th>Product</th><th>Batch</th><th>Unit</th><th>Quantity</th><th>Price</th><th>Discount</th><th>Total</th></tr>${rows}<tr><td colspan="6"><b>Total</b></td><td><b>${data.sale.totalAmount}</b></td></tr></table></body></html>`;
  downloadBlob(html, 'application/vnd.ms-excel', `${data.sale.saleNumber}.xls`);
}

onMounted(async () => {
  await load();
  const id = Number(new URLSearchParams(location.search).get('patientId'));
  if (id) {
    patientId.value = id;
    activeTab.value = 'bill';
  }
});
</script>

<template>
  <h1>Pharmacy Sales</h1>

  <div class="pharmacy-tabs">
    <button :class="{active:activeTab==='bill'}" @click="activeTab='bill'">Create Bill</button>
    <button :class="{active:activeTab==='inventory'}" @click="activeTab='inventory'">Search Inventory</button>
    <button :class="{active:activeTab==='summary'}" @click="activeTab='summary'">Pharmacy Summary</button>
  </div>

  <div v-if="activeTab==='inventory'" class="card">
    <h2>Search Inventory</h2>
    <div class="toolbar"><input style="max-width:520px" v-model="inventorySearch" placeholder="Search MF, invoice number, product, type, batch no or HSN"></div>
    <table class="table">
      <tr><th class="sortable" @click="sortInventory('medicineType')">Type <span class="sort-indicator">{{inventorySortIndicator('medicineType')}}</span></th><th class="sortable" @click="sortInventory('manufacturer')">MF <span class="sort-indicator">{{inventorySortIndicator('manufacturer')}}</span></th><th class="sortable" @click="sortInventory('invoiceNumber')">Invoice <span class="sort-indicator">{{inventorySortIndicator('invoiceNumber')}}</span></th><th class="sortable" @click="sortInventory('productName')">Product <span class="sort-indicator">{{inventorySortIndicator('productName')}}</span></th><th class="sortable" @click="sortInventory('batchNo')">Batch <span class="sort-indicator">{{inventorySortIndicator('batchNo')}}</span></th><th class="sortable" @click="sortInventory('expiryDate')">Expire Date <span class="sort-indicator">{{inventorySortIndicator('expiryDate')}}</span></th><th class="sortable" @click="sortInventory('hsn')">HSN <span class="sort-indicator">{{inventorySortIndicator('hsn')}}</span></th><th class="sortable" @click="sortInventory('mrp')">MRP/Pack <span class="sort-indicator">{{inventorySortIndicator('mrp')}}</span></th><th class="sortable" @click="sortInventory('unitsPerPack')">Units/Pack <span class="sort-indicator">{{inventorySortIndicator('unitsPerPack')}}</span></th><th class="sortable" @click="sortInventory('availablePacks')">Available Packs <span class="sort-indicator">{{inventorySortIndicator('availablePacks')}}</span></th><th class="sortable" @click="sortInventory('looseUnits')">Loose Units <span class="sort-indicator">{{inventorySortIndicator('looseUnits')}}</span></th></tr>
      <tr v-for="row in pagedInventory" :key="row.id"><td>{{ row.medicineType }}</td><td>{{ row.manufacturer }}</td><td>{{ row.invoiceNumber }}</td><td>{{ row.productName }}</td><td>{{ row.batchNo }}</td><td>{{ row.expiryDate?.slice(0,10) }}</td><td>{{ row.hsn }}</td><td>₹{{ Number(row.mrp).toFixed(2) }}</td><td>{{ row.unitsPerPack }}</td><td>{{ row.availablePacks }}</td><td>{{ row.looseUnits }}</td></tr>
    </table>
    <Pagination :page="inventoryPage" :page-count="inventoryPageCount" :total="sortedInventory.length" :page-size="pageSize" @update:page="inventoryPage=$event" />
  </div>

  <div v-if="activeTab==='bill'" class="card">
    <h2>Create Pharmacy Bill</h2>

    <div class="form-grid">
      <label>Patient<select v-model.number="patientId"><option :value="null">Walk-in Patient</option><option v-for="p in patients" :key="p.id" :value="p.id">{{ p.name }} · {{ p.patientCode }} · {{ p.phone }}</option></select></label>
      <label>Payment Mode<select v-model="paymentMode"><option>Cash</option><option>UPI</option><option>Card</option><option>Bank Transfer</option><option>Other</option></select></label>
    </div>

    <div v-if="patientId && selectedPatient" class="card patient-summary-card">
      <b>{{ selectedPatient.name }}</b> · {{ selectedPatient.patientCode }}<br>
      Age/Gender: {{ ageText(selectedPatient) }}/{{ selectedPatient.gender }} · Phone: {{ selectedPatient.phone }} · Doctor: {{ selectedPatient.doctorName || '-' }}
    </div>

    <div v-else class="form-grid">
      <label>Walk-in Patient Name *<input v-model="walkInPatientName"></label>
      <label>Phone<input v-model="walkInPhone"></label>
    </div>

    <div class="form-grid">
      <label>Doctor Source
        <select v-model="doctorMode" @change="if(doctorMode!=='list') doctorId=null; if(doctorMode!=='outside') outsideDoctorName=''">
          <option value="none">No Doctor</option>
          <option value="list">Select Existing Doctor</option>
          <option value="outside">Outside Doctor</option>
        </select>
      </label>
      <label v-if="doctorMode==='list'">Doctor
        <select v-model.number="doctorId">
          <option :value="null">Select Doctor</option>
          <option v-for="d in doctors.filter(x=>x.isActive)" :key="d.id" :value="d.id">{{ d.name }}</option>
        </select>
      </label>
      <label v-if="doctorMode==='outside'">Outside Doctor Name
        <input v-model="outsideDoctorName" placeholder="Enter doctor name">
      </label>
    </div>

    <div class="toolbar"><input style="max-width:520px" v-model="billSearch" placeholder="Search product, MF, invoice, type, batch or HSN"></div>
    <table class="table">
      <tr><th class="sortable" @click="sortBillSearch('medicineType')">Type <span class="sort-indicator">{{billSortIndicator('medicineType')}}</span></th><th class="sortable" @click="sortBillSearch('productName')">Product <span class="sort-indicator">{{billSortIndicator('productName')}}</span></th><th class="sortable" @click="sortBillSearch('manufacturer')">MF <span class="sort-indicator">{{billSortIndicator('manufacturer')}}</span></th><th class="sortable" @click="sortBillSearch('batchNo')">Batch <span class="sort-indicator">{{billSortIndicator('batchNo')}}</span></th><th class="sortable" @click="sortBillSearch('mrp')">MRP <span class="sort-indicator">{{billSortIndicator('mrp')}}</span></th><th>Stock</th><th></th></tr>
      <tr v-for="row in pagedBillSearch" :key="row.id"><td>{{ row.medicineType }}</td><td>{{ row.productName }}</td><td>{{ row.manufacturer }}</td><td>{{ row.batchNo }}</td><td>₹{{ Number(row.mrp).toFixed(2) }}</td><td>{{ row.availablePacks }} pack(s) + {{ row.looseUnits }} loose</td><td><button v-if="can('PHARMACY','add')" @click="addToCart(row)">Add to Bill</button></td></tr>
    </table>
    <Pagination :page="billSearchPage" :page-count="billSearchPageCount" :total="sortedBillSearch.length" :page-size="pageSize" @update:page="billSearchPage=$event" />

    <h3>Selected Medicines</h3>
    <table class="table">
      <tr><th>Product</th><th>Type</th><th>Batch</th><th>Sale Unit</th><th>Quantity</th><th>Price</th><th>Gross</th><th>Approved Discount</th><th>Total</th><th></th></tr>
      <tr v-for="(row,index) in cart" :key="row.purchaseItemId">
        <td>{{ row.productName }}<div class="muted">{{ row.packing }}</div></td><td>{{ row.medicineType }}</td><td>{{ row.batchNo }}</td>
        <td><select v-model="row.unitType" @change="updateUnit(row)"><option v-for="u in allowedUnits(row)" :key="u">{{ u }}</option></select></td>
        <td><input type="number" min="1" :max="maxQuantity(row)" v-model.number="row.quantity"><div class="muted">Max {{ maxQuantity(row) }} {{ row.unitType }}</div></td>
        <td>₹{{ Number(row.unitPrice).toFixed(2) }}</td><td>₹{{ rowGross(row).toFixed(2) }}</td>
        <td><select v-model.number="row.discountTypeId"><option :value="null">No Discount</option><option v-for="d in discounts" :key="d.id" :value="d.id">{{d.name}} · {{d.discountMode==='Percent'?d.value+'%':'₹'+Number(d.value).toFixed(2)}}</option></select></td>
        <td><b>₹{{ rowTotal(row).toFixed(2) }}</b></td><td><button class="danger-btn" @click="removeCart(index)">×</button></td>
      </tr>
    </table>

    <div class="bill-total-box"><div><span>Before Discount</span><b>₹{{ totals.gross.toFixed(2) }}</b></div><div><span>Discount</span><b>₹{{ totals.discount.toFixed(2) }}</b></div><div class="grand"><span>Total</span><b>₹{{ totals.net.toFixed(2) }}</b></div></div>
    <div class="modal-actions"><button v-if="can('PHARMACY','add')" @click="createSale">Complete Sale</button></div>
  </div>

  <div v-if="activeTab==='summary'" class="card">
    <div class="page-head"><div><h2>Pharmacy Summary</h2><div class="muted">Day-end sales and remaining stock.</div></div><div class="toolbar"><input type="date" v-model="dayEndDate"><button @click="loadDayEnd">Apply</button></div></div>

    <div v-if="dayEnd" class="grid"><div class="card"><div class="muted">Bills</div><div class="metric">{{ dayEnd.numberOfBills }}</div></div><div class="card"><div class="muted">Units Sold</div><div class="metric">{{ dayEnd.totalUnitsSold }}</div></div><div class="card"><div class="muted">Sales Amount</div><div class="metric">₹{{ Number(dayEnd.totalSalesAmount).toFixed(2) }}</div></div></div>

    <h3>Sales Bills</h3><div class="grid-filter-row"><GridSearch v-model="salesSearch" placeholder="Search all pharmacy sale fields..." /></div>
    <table class="table"><tr><th class="sortable" @click="sortSales('saleNumber')">Sale <span class="sort-indicator">{{salesSortIndicator('saleNumber')}}</span></th><th class="sortable" @click="sortSales('saleDateUtc')">Time <span class="sort-indicator">{{salesSortIndicator('saleDateUtc')}}</span></th><th class="sortable" @click="sortSales('itemCount')">Items <span class="sort-indicator">{{salesSortIndicator('itemCount')}}</span></th><th class="sortable" @click="sortSales('paymentMode')">Payment <span class="sort-indicator">{{salesSortIndicator('paymentMode')}}</span></th><th class="sortable" @click="sortSales('totalAmount')">Amount <span class="sort-indicator">{{salesSortIndicator('totalAmount')}}</span></th><th></th></tr><tr v-for="row in pagedSales" :key="row.id"><td>{{ row.saleNumber }}</td><td>{{ new Date(row.saleDateUtc).toLocaleString() }}</td><td>{{ row.itemCount }}</td><td>{{ row.paymentMode }}</td><td>₹{{ Number(row.totalAmount).toFixed(2) }}</td><td class="actions"><button @click="printSale(row)">Print</button><button @click="downloadExcel(row)">Excel</button><button @click="downloadCsv(row)">CSV</button></td></tr></table>
    <Pagination :page="salesPage" :page-count="salesPageCount" :total="sortedSales.length" :page-size="pageSize" @update:page="salesPage=$event" />

    <div v-if="dayEnd" class="report-two-column">
      <div><h3>Medicine Sold</h3><div class="grid-filter-row"><GridSearch v-model="soldSearch" placeholder="Search sold medicine fields..." /></div><table class="table"><tr><th class="sortable" @click="sortSold('productName')">Product <span class="sort-indicator">{{soldSortIndicator('productName')}}</span></th><th class="sortable" @click="sortSold('batchNo')">Batch <span class="sort-indicator">{{soldSortIndicator('batchNo')}}</span></th><th class="sortable" @click="sortSold('unitsSold')">Units Sold <span class="sort-indicator">{{soldSortIndicator('unitsSold')}}</span></th><th class="sortable" @click="sortSold('amount')">Amount <span class="sort-indicator">{{soldSortIndicator('amount')}}</span></th></tr><tr v-for="row in pagedSold" :key="row.productName+row.batchNo"><td>{{ row.productName }}</td><td>{{ row.batchNo }}</td><td>{{ row.unitsSold }}</td><td>₹{{ Number(row.amount).toFixed(2) }}</td></tr></table><Pagination :page="soldPage" :page-count="soldPageCount" :total="sortedSold.length" :page-size="pageSize" @update:page="soldPage=$event" /></div>
      <div><h3>Remaining Stock</h3><div class="grid-filter-row"><GridSearch v-model="stockSearch" placeholder="Search remaining stock fields..." /></div><table class="table"><tr><th class="sortable" @click="sortStock('productName')">Product <span class="sort-indicator">{{stockSortIndicator('productName')}}</span></th><th class="sortable" @click="sortStock('medicineType')">Type <span class="sort-indicator">{{stockSortIndicator('medicineType')}}</span></th><th class="sortable" @click="sortStock('batchNo')">Batch <span class="sort-indicator">{{stockSortIndicator('batchNo')}}</span></th><th class="sortable" @click="sortStock('remainingPacks')">Packs <span class="sort-indicator">{{stockSortIndicator('remainingPacks')}}</span></th><th class="sortable" @click="sortStock('looseUnits')">Loose Units <span class="sort-indicator">{{stockSortIndicator('looseUnits')}}</span></th></tr><tr v-for="row in pagedStock" :key="row.productName+row.batchNo"><td>{{ row.productName }}</td><td>{{ row.medicineType }}</td><td>{{ row.batchNo }}</td><td>{{ row.remainingPacks }}</td><td>{{ row.looseUnits }}</td></tr></table><Pagination :page="stockPage" :page-count="stockPageCount" :total="sortedStock.length" :page-size="pageSize" @update:page="stockPage=$event" /></div>
    </div>
  </div>
</template>
