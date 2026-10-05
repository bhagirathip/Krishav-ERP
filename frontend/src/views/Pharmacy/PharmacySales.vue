<script setup>
import { computed, onMounted, ref, watch } from 'vue';
import { api, assetUrl } from '../../api';
import { useGrid } from '../../composables/useGrid';
import SalesCreateBillTab from './components/SalesCreateBillTab.vue';
import SalesSearchInventoryTab from './components/SalesSearchInventoryTab.vue';
import SalesSummaryTab from './components/SalesSummaryTab.vue';

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
const roundOff = ref(0);
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
  return { gross, discount, net: gross - discount + Number(roundOff.value || 0) };
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

  // RoundOff accepts decimals (e.g. 5.05) but is still money, so clamp to 2
  // decimal places - same precision the server rounds to.
  const roundOffValue = Math.round((Number(roundOff.value) || 0) * 100) / 100;
  if (roundOffValue < -9 || roundOffValue > 9) {
    alert('Round off must be between -9 and 9.');
    return;
  }

  try {
    const { data } = await api.post('/pharmacy/sales', {
      patientId: patientId.value,
      doctorId: doctorMode.value === 'list' ? doctorId.value : null,
      outsideDoctorName: doctorMode.value === 'outside' ? outsideDoctorName.value.trim() : null,
      walkInPatientName: patientId.value ? null : walkInPatientName.value,
      walkInPhone: patientId.value ? null : walkInPhone.value,
      paymentMode: paymentMode.value,
      roundOff: roundOffValue,
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
    roundOff.value = 0;
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
  // MRP is already tax-inclusive - CGST/SGST here are a per-row breakdown of
  // each item's own Total (per data.itemTax, computed server-side from its
  // purchase-time CGST/SGST%), not an addition to it, so the patient pays
  // the same Total either way.
  const itemRows = data.sale.items.map((item, index) => {
    const tax = data.itemTax.find(t => t.itemId === item.id);
    return `
    <tr>
      <td class="no-col">${index + 1}</td>
      <td>${item.productName}</td>
      <td>${item.hsn || '-'}</td>
      <td>${item.batchNo}</td>
      <td class="no-col">${item.quantity}</td>
      <td class="amount">₹${(Number(item.unitPrice) * Number(item.quantity)).toFixed(2)}</td>
      <td class="amount">₹${Number(item.discountAmount).toFixed(2)}</td>
      <td class="amount">${Number(tax?.cgstPercent ?? 0).toFixed(1)}%</td>
      <td class="amount">₹${Number(tax?.cgst ?? 0).toFixed(2)}</td>
      <td class="amount">${Number(tax?.sgstPercent ?? 0).toFixed(1)}%</td>
      <td class="amount">₹${Number(tax?.sgst ?? 0).toFixed(2)}</td>
      <td class="amount">₹${Number(item.totalAmount).toFixed(2)}</td>
    </tr>`;
  }).join('');

  const saleDate = data.sale.saleDateUtc ? new Date(data.sale.saleDateUtc).toLocaleDateString() : '-';
  const frame = document.createElement('iframe');
  frame.style.cssText = 'position:fixed;width:0;height:0;border:0';
  document.body.appendChild(frame);
  const doc = frame.contentWindow.document;
  doc.open();
  // Half-A4 (A5) to save paper. Patient-info strip and item table use only
  // top/bottom rules (no side/cell borders), matching the lab report style.
  doc.write(`<!doctype html><html><head><title></title><style>
    @page{size:A5;margin:0}
    *{box-sizing:border-box}
    body{margin:0;font-family:Arial;font-size:11px}
    .header img{width:100%;max-height:90px;object-fit:fill;display:block}
    .content{padding:3mm 6mm 6mm}
    .patient{border-top:1.5px solid #222;border-bottom:1.5px solid #222;padding:4px 0;margin-bottom:10px}
    .patient-row{display:grid;grid-template-columns:1fr 1fr;gap:2px 10px;padding:1.5px 0}
    table{width:100%;border-collapse:collapse;font-size:9px}
    thead th{border-top:1.5px solid #222;border-bottom:1.5px solid #222;padding:3px 2px;text-align:left}
    thead tr:first-child th{border-bottom:0}
    thead .sub-th{border-top:0;padding-top:0}
    tbody td{padding:3px 2px;border:0}
    .no-col{width:7mm;text-align:center}
    .amount{text-align:right}
    .total{border-top:1.5px solid #222;margin-top:6px;padding-top:6px;text-align:right;font-size:13px}
  </style></head><body>
    <div class="header">${data.header ? `<img src="${assetUrl(data.header)}">` : ''}</div>
    <div class="content">
      <div class="patient">
        <div class="patient-row"><span><b>Bill No:</b> ${data.sale.saleNumber}</span><span><b>GST No:</b> ${data.gstNumber || '-'}</span></div>
        <div class="patient-row"><span><b>Patient:</b> ${data.patient?.name || data.sale.walkInPatientName || 'Walk-in'}</span><span><b>Phone:</b> ${data.patient?.phone || data.sale.walkInPhone || '-'}</span></div>
        <div class="patient-row"><span><b>Doctor:</b> ${data.doctor?.name || data.sale.outsideDoctorName || '-'}</span><span><b>Payment:</b> ${data.sale.paymentMode}</span></div>
        <div class="patient-row"><span><b>Date:</b> ${saleDate}</span><span></span></div>
      </div>
      <table>
        <thead>
          <tr>
            <th class="no-col" rowspan="2">Sl</th>
            <th rowspan="2">Product</th>
            <th rowspan="2">HSN Code</th>
            <th rowspan="2">Batch No</th>
            <th class="no-col" rowspan="2">Qty</th>
            <th class="amount" rowspan="2">Amount</th>
            <th class="amount" rowspan="2">Discount</th>
            <th class="amount" colspan="2">CGST</th>
            <th class="amount" colspan="2">SGST</th>
            <th class="amount" rowspan="2">Total</th>
          </tr>
          <tr>
            <th class="amount sub-th">%</th>
            <th class="amount sub-th">₹</th>
            <th class="amount sub-th">%</th>
            <th class="amount sub-th">₹</th>
          </tr>
        </thead>
        <tbody>${itemRows}</tbody>
      </table>
      <div class="total"><b>Total: ₹${Number(data.sale.totalAmount).toFixed(2)}</b></div>
    </div>
    <script>window.onload=()=>window.print();window.onafterprint=()=>window.parent.postMessage('pharmacy-print-complete','*');<\/script>
  </body></html>`);
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

  <SalesSearchInventoryTab
    v-if="activeTab==='inventory'"
    v-model:search="inventorySearch"
    v-model:page="inventoryPage"
    :paged-inventory="pagedInventory"
    :sorted-inventory="sortedInventory"
    :inventory-page-count="inventoryPageCount"
    :page-size="pageSize"
    :sort-inventory="sortInventory"
    :inventory-sort-indicator="inventorySortIndicator"
  />

  <SalesCreateBillTab
    v-if="activeTab==='bill'"
    v-model:patient-id="patientId"
    v-model:doctor-id="doctorId"
    v-model:doctor-mode="doctorMode"
    v-model:outside-doctor-name="outsideDoctorName"
    v-model:walk-in-patient-name="walkInPatientName"
    v-model:walk-in-phone="walkInPhone"
    v-model:payment-mode="paymentMode"
    v-model:round-off="roundOff"
    v-model:bill-search="billSearch"
    v-model:bill-search-page="billSearchPage"
    :patients="patients"
    :doctors="doctors"
    :discounts="discounts"
    :selected-patient="selectedPatient"
    :selected-doctor-name="selectedDoctorName"
    :paged-bill-search="pagedBillSearch"
    :sorted-bill-search="sortedBillSearch"
    :bill-search-page-count="billSearchPageCount"
    :page-size="pageSize"
    :sort-bill-search="sortBillSearch"
    :bill-sort-indicator="billSortIndicator"
    :add-to-cart="addToCart"
    :cart="cart"
    :update-unit="updateUnit"
    :allowed-units="allowedUnits"
    :max-quantity="maxQuantity"
    :row-gross="rowGross"
    :row-total="rowTotal"
    :remove-cart="removeCart"
    :totals="totals"
    :create-sale="createSale"
  />

  <SalesSummaryTab
    v-if="activeTab==='summary'"
    v-model:day-end-date="dayEndDate"
    v-model:sales-search="salesSearch"
    v-model:sales-page="salesPage"
    v-model:sold-search="soldSearch"
    v-model:sold-page="soldPage"
    v-model:stock-search="stockSearch"
    v-model:stock-page="stockPage"
    :load-day-end="loadDayEnd"
    :day-end="dayEnd"
    :paged-sales="pagedSales"
    :sorted-sales="sortedSales"
    :sales-page-count="salesPageCount"
    :page-size="pageSize"
    :sort-sales="sortSales"
    :sales-sort-indicator="salesSortIndicator"
    :print-sale="printSale"
    :download-excel="downloadExcel"
    :download-csv="downloadCsv"
    :paged-sold="pagedSold"
    :sorted-sold="sortedSold"
    :sold-page-count="soldPageCount"
    :sort-sold="sortSold"
    :sold-sort-indicator="soldSortIndicator"
    :paged-stock="pagedStock"
    :sorted-stock="sortedStock"
    :stock-page-count="stockPageCount"
    :sort-stock="sortStock"
    :stock-sort-indicator="stockSortIndicator"
  />
</template>
