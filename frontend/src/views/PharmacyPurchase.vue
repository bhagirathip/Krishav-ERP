<script setup>
import { computed, onMounted, ref } from 'vue';
import { api } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';

const invoices = ref([]);
const distributors = ref([]);
const medicineTypes = ref([]);
const showInvoice = ref(false);
const showTypeMaster = ref(false);
const errors = ref([]);
const distributorDocument = ref(null);
const importMessage = ref('');
const importing = ref(false);
const typeForm = ref({ id: null, name: '' });
const pageSize = 10;
const today = new Date().toISOString().slice(0, 10);

function emptyMedicine(index = 1) {
  return {
    id: null,
    slNo: index,
    medicineTypeId: null,
    manufacturer: '',
    hsn: '',
    productName: '',
    packing: '',
    batchNo: '',
    expiryDate: '',
    mrp: 0,
    rate: 0,
    quantity: 1,
    tabletsPerStrip: 1,
    bonus: 0,
    discountAmount: 0,
    cgstPercent: 0,
    sgstPercent: 0,
    isReturnableOnExpiry: true
  };
}

function emptyInvoice() {
  return {
    id: null,
    distributorId: null,
    invoiceNumber: '',
    invoiceDate: today,
    paymentType: 'Cash',
    distributorDocumentName: '',
    distributorDocumentPath: '',
    items: [emptyMedicine(1)]
  };
}

const invoiceForm = ref(emptyInvoice());
const { search, page, pageCount, pagedRows: pagedInvoices, sortedRows, sortBy, sortIndicator } = useGrid(invoices, pageSize);

function typeName(row) {
  return medicineTypes.value.find(x => x.id === row.medicineTypeId)?.name || '';
}

function unitsLabel(row) {
  const name = typeName(row);
  if (name === 'Tablet') return 'Tablet / Strip';
  if (name === 'Injection') return 'Vials / Pack';
  return 'Units / Pack';
}

function typeChanged(row) {
  const name = typeName(row);
  if (name === 'Cream' || name === 'Spray') {
    row.tabletsPerStrip = 1;
  }
}

function calculate(row) {
  const base = Number(row.rate || 0) * Number(row.quantity || 0);
  const discount = Math.min(base, Math.max(0, Number(row.discountAmount || 0)));
  const taxable = base - discount;
  const cgst = taxable * Number(row.cgstPercent || 0) / 100;
  const sgst = taxable * Number(row.sgstPercent || 0) / 100;
  return { base, discount, taxable, cgst, sgst, total: taxable + cgst + sgst };
}

const totals = computed(() => {
  const result = { quantity: 0, discount: 0, taxable: 0, cgst: 0, sgst: 0, total: 0 };
  for (const row of invoiceForm.value.items) {
    const calc = calculate(row);
    result.quantity += Number(row.quantity || 0);
    result.discount += calc.discount;
    result.taxable += calc.taxable;
    result.cgst += calc.cgst;
    result.sgst += calc.sgst;
    result.total += calc.total;
  }
  const floor = Math.floor(result.total);
  const fraction = result.total - floor;
  result.roundedTotal = fraction > 0.50 ? floor + 1 : floor;
  result.roundOff = result.roundedTotal - result.total;
  return result;
});

async function load() {
  invoices.value = (await api.get('/pharmacy/purchases')).data;
  distributors.value = (await api.get('/pharmacy/distributors')).data;
  medicineTypes.value = (await api.get('/pharmacy/medicine-types')).data;
}

function addInvoice() {
  invoiceForm.value = emptyInvoice();
  distributorDocument.value = null;
  errors.value = [];
  importMessage.value = '';
  showInvoice.value = true;
}

async function editInvoice(row) {
  const { data } = await api.get(`/pharmacy/purchases/${row.id}`);
  invoiceForm.value = {
    id: data.id,
    distributorId: data.distributorId,
    invoiceNumber: data.invoiceNumber,
    invoiceDate: data.invoiceDate?.slice(0, 10),
    paymentType: data.paymentType,
    distributorDocumentName: data.distributorDocumentName || '',
    distributorDocumentPath: data.distributorDocumentPath || '',
    items: data.items.map((x, index) => ({
      id: x.id,
      slNo: x.slNo || index + 1,
      medicineTypeId: x.medicineTypeId,
      manufacturer: x.manufacturer,
      hsn: x.hsn,
      productName: x.productName,
      packing: x.packing,
      batchNo: x.batchNo,
      expiryDate: x.expiryDate?.slice(0, 10),
      mrp: x.mrp,
      rate: x.rate,
      quantity: x.quantity,
      tabletsPerStrip: x.tabletsPerStrip || 1,
      bonus: x.bonus,
      discountAmount: x.discountAmount,
      cgstPercent: x.cgstPercent,
      sgstPercent: x.sgstPercent,
      isReturnableOnExpiry: x.isReturnableOnExpiry !== false
    }))
  };
  distributorDocument.value = null;
  errors.value = [];
  showInvoice.value = true;
}

async function documentSelected(event) {
  const file = event.target.files?.[0] || null;
  distributorDocument.value = file;
  importMessage.value = '';

  if (!file) return;

  const extension = file.name.split('.').pop()?.toLowerCase();
  if (!['csv', 'xlsx', 'xls'].includes(extension)) {
    return;
  }

  importing.value = true;
  try {
    const formData = new FormData();
    formData.append('file', file);
    const { data } = await api.post(
      '/pharmacy/purchases/import-file',
      formData
    );

    if (data.invoiceNumber) {
      invoiceForm.value.invoiceNumber = data.invoiceNumber;
    }

    if (data.invoiceDate) {
      invoiceForm.value.invoiceDate = String(data.invoiceDate).slice(0, 10);
    }

    if (data.distributorId) {
      invoiceForm.value.distributorId = data.distributorId;
    }

    invoiceForm.value.items = (data.items || []).map((item, index) => ({
      id: null,
      slNo: item.slNo || index + 1,
      medicineTypeId: item.medicineTypeId || null,
      manufacturer: item.manufacturer || '',
      hsn: item.hsn || '',
      productName: item.productName || '',
      packing: item.packing || '',
      batchNo: item.batchNo || '',
      expiryDate: item.expiryDate ? String(item.expiryDate).slice(0, 10) : '',
      mrp: Number(item.mrp || 0),
      rate: Number(item.rate || 0),
      quantity: Number(item.quantity || 0),
      tabletsPerStrip: Number(item.tabletsPerStrip || 1),
      bonus: Number(item.bonus || 0),
      discountAmount: Number(item.discountAmount || 0),
      cgstPercent: Number(item.cgstPercent || 0),
      sgstPercent: Number(item.sgstPercent || 0),
      isReturnableOnExpiry: item.isReturnableOnExpiry !== false
    }));

    importMessage.value = data.message ||
      `Imported ${invoiceForm.value.items.length} medicine rows.`;
  } catch (error) {
    errors.value = [
      error.response?.data?.message ||
      'Unable to import the distributor invoice.'
    ];
  } finally {
    importing.value = false;
  }
}

function addMedicine() {
  invoiceForm.value.items.push(emptyMedicine(invoiceForm.value.items.length + 1));
}

function removeMedicine(index) {
  invoiceForm.value.items.splice(index, 1);
  if (!invoiceForm.value.items.length) addMedicine();
  invoiceForm.value.items.forEach((row, i) => row.slNo = i + 1);
}

function validate() {
  const result = [];
  if (!invoiceForm.value.distributorId) result.push('Distributor is required.');
  if (!invoiceForm.value.invoiceNumber.trim()) result.push('Invoice number is required.');
  if (!invoiceForm.value.invoiceDate) result.push('Invoice date is required.');

  invoiceForm.value.items.forEach((row, index) => {
    if (!row.medicineTypeId) result.push(`Medicine type is required in row ${index + 1}.`);
    if (!row.productName.trim()) result.push(`Product name is required in row ${index + 1}.`);
    if (!row.batchNo.trim()) result.push(`Batch number is required in row ${index + 1}.`);
    if (!row.expiryDate) result.push(`Expiry date is required in row ${index + 1}.`);
    if (Number(row.quantity) <= 0) result.push(`Quantity must be greater than zero in row ${index + 1}.`);
    if (Number(row.tabletsPerStrip) <= 0) result.push(`${unitsLabel(row)} must be greater than zero in row ${index + 1}.`);
  });

  errors.value = result;
  return result.length === 0;
}

async function saveInvoice() {
  if (!validate()) return;

  try {
    let invoiceId = invoiceForm.value.id;
    if (invoiceId) {
      const { data } = await api.put(`/pharmacy/purchases/${invoiceId}`, invoiceForm.value);
      invoiceId = data.invoiceId || invoiceId;
    } else {
      const { data } = await api.post('/pharmacy/purchases', invoiceForm.value);
      invoiceId = data.invoiceId;
    }

    if (distributorDocument.value) {
      const fd = new FormData();
      fd.append('file', distributorDocument.value);
      await api.post(`/pharmacy/purchases/${invoiceId}/document`, fd);
    }

    showInvoice.value = false;
    await load();
  } catch (error) {
    errors.value = [error.response?.data?.message || 'Unable to save purchase invoice.'];
  }
}

async function deleteInvoice(row) {
  if (!confirm(`Delete invoice ${row.invoiceNumber}?`)) return;
  try {
    await api.delete(`/pharmacy/purchases/${row.id}`);
    await load();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to delete purchase invoice.');
  }
}

function newType() {
  typeForm.value = { id: null, name: '' };
  showTypeMaster.value = true;
}

async function saveType() {
  if (!typeForm.value.name.trim()) return;
  if (typeForm.value.id) {
    await api.put(`/pharmacy/medicine-types/${typeForm.value.id}`, typeForm.value);
  } else {
    await api.post('/pharmacy/medicine-types', typeForm.value);
  }
  typeForm.value = { id: null, name: '' };
  medicineTypes.value = (await api.get('/pharmacy/medicine-types')).data;
}

async function deleteType(row) {
  try {
    await api.delete(`/pharmacy/medicine-types/${row.id}`);
    medicineTypes.value = (await api.get('/pharmacy/medicine-types')).data;
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to delete medicine type.');
  }
}

onMounted(load);
</script>

<template>
  <h1>Pharmacy Purchase</h1>

  <div class="toolbar">
    <button v-if="can('PHARMACY', 'add')" @click="addInvoice">+ Add Purchase Invoice</button>
    <button class="secondary" @click="newType">Medicine Type Master</button>
  </div>
  <div class="grid-filter-row"><GridSearch v-model="search" placeholder="Search all purchase invoice fields..." /></div>

  <table class="table">
    <tr>
      <th class="sortable" @click="sortBy('invoiceNumber')">Invoice No <span class="sort-indicator">{{sortIndicator('invoiceNumber')}}</span></th>
      <th class="sortable" @click="sortBy('invoiceDate')">Date <span class="sort-indicator">{{sortIndicator('invoiceDate')}}</span></th>
      <th class="sortable" @click="sortBy('distributorName')">Distributor <span class="sort-indicator">{{sortIndicator('distributorName')}}</span></th>
      <th class="sortable" @click="sortBy('paymentType')">Cash/Credit <span class="sort-indicator">{{sortIndicator('paymentType')}}</span></th>
      <th class="sortable" @click="sortBy('totalQuantity')">Total Qty <span class="sort-indicator">{{sortIndicator('totalQuantity')}}</span></th>
      <th class="sortable" @click="sortBy('totalDiscount')">Discount <span class="sort-indicator">{{sortIndicator('totalDiscount')}}</span></th>
      <th class="sortable" @click="sortBy('totalTaxableAmount')">Taxable <span class="sort-indicator">{{sortIndicator('totalTaxableAmount')}}</span></th>
      <th class="sortable" @click="sortBy('totalCgst')">CGST <span class="sort-indicator">{{sortIndicator('totalCgst')}}</span></th>
      <th class="sortable" @click="sortBy('totalSgst')">SGST <span class="sort-indicator">{{sortIndicator('totalSgst')}}</span></th>
      <th>Round Off</th><th class="sortable" @click="sortBy('totalAmount')">Total <span class="sort-indicator">{{sortIndicator('totalAmount')}}</span></th><th></th>
    </tr>
    <tr v-for="row in pagedInvoices" :key="row.id">
      <td>{{ row.invoiceNumber }}</td>
      <td>{{ row.invoiceDate?.slice(0,10) }}</td>
      <td>{{ row.distributorName }}</td>
      <td>{{ row.paymentType }}</td>
      <td>{{ row.totalQuantity }}</td>
      <td>₹{{ Number(row.totalDiscount).toFixed(2) }}</td>
      <td>₹{{ Number(row.totalTaxableAmount).toFixed(2) }}</td>
      <td>₹{{ Number(row.totalCgst).toFixed(2) }}</td>
      <td>₹{{ Number(row.totalSgst).toFixed(2) }}</td>
      <td>{{ Number(row.roundOffAmount || 0) >= 0 ? '+' : '' }}₹{{ Number(row.roundOffAmount || 0).toFixed(2) }}</td>
      <td><b>₹{{ Number(row.totalAmount).toFixed(2) }}</b></td>
      <td class="actions">
        <button v-if="can('PHARMACY','edit')" @click="editInvoice(row)">Edit</button>
        <button v-if="can('PHARMACY','delete')" class="danger-btn" @click="deleteInvoice(row)">Delete</button>
      </td>
    </tr>
  </table>

  <Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="pageSize" @update:page="page = $event" />

  <div v-if="showInvoice" class="modal-bg">
    <div class="modal modal-pharmacy-invoice">
      <button class="modal-close-x" @click="showInvoice=false">×</button>
      <div class="page-head"><h2>{{ invoiceForm.id ? 'Edit' : 'Add' }} Purchase Invoice</h2><button class="secondary" @click="showInvoice=false">Close</button></div>
      <div v-if="errors.length" class="error-box"><div v-for="e in errors" :key="e">{{ e }}</div></div>

      <div class="form-grid">
        <label>Distributor *<select v-model.number="invoiceForm.distributorId"><option :value="null">Select Distributor</option><option v-for="d in distributors" :key="d.id" :value="d.id">{{ d.name }}</option></select></label>
        <label>Invoice Number *<input v-model="invoiceForm.invoiceNumber"></label>
        <label>Invoice Date *<input type="date" v-model="invoiceForm.invoiceDate"></label>
        <label>Cash / Credit *<select v-model="invoiceForm.paymentType"><option>Cash</option><option>Credit</option></select></label>
        <label class="full">Distributor Invoice Document<input type="file" accept=".pdf,.jpg,.jpeg,.png,.webp,.xls,.xlsx,.csv,.doc,.docx" @change="documentSelected"></label>
        <div v-if="importing" class="muted full">Reading invoice file...</div>
        <div v-if="importMessage" class="success-box full">{{ importMessage }}</div>
      </div>

      <div class="toolbar"><button @click="addMedicine">+ Add Multiple Medicines</button></div>

      <div class="pharmacy-table-scroll">
        <table class="table pharmacy-entry-table">
          <tr>
            <th>SL No</th><th>MF</th><th>HSN</th><th>Product Name</th><th>Packing</th><th>Batch No</th><th>Expire Date</th><th>MRP</th><th>Rate</th><th>Quantity</th><th>Medicine Type</th><th>Tablet/Strip or Units/Pack</th><th>Bonus</th><th>Discount ₹</th><th>Taxable</th><th>CGST %</th><th>CGST</th><th>SGST %</th><th>SGST</th><th>Expiry Returnable</th><th>Total</th><th></th>
          </tr>
          <tr v-for="(row,index) in invoiceForm.items" :key="row.id || index">
            <td><input class="tiny-input" type="number" v-model.number="row.slNo"></td>
            <td><input v-model="row.manufacturer"></td>
            <td><input v-model="row.hsn"></td>
            <td><input v-model="row.productName"></td>
            <td><input v-model="row.packing"></td>
            <td><input v-model="row.batchNo"></td>
            <td><input type="date" v-model="row.expiryDate"></td>
            <td><input type="number" min="0" step="0.01" v-model.number="row.mrp"></td>
            <td><input type="number" min="0" step="0.01" v-model.number="row.rate"></td>
            <td><input type="number" min="1" v-model.number="row.quantity"></td>
            <td><select v-model.number="row.medicineTypeId" @change="typeChanged(row)"><option :value="null">Select</option><option v-for="t in medicineTypes" :key="t.id" :value="t.id">{{ t.name }}</option></select></td>
            <td><input type="number" min="1" :disabled="['Cream','Spray'].includes(typeName(row))" v-model.number="row.tabletsPerStrip"><div class="muted">{{ unitsLabel(row) }}</div></td>
            <td><input type="number" min="0" v-model.number="row.bonus"></td>
            <td><input type="number" min="0" step="0.01" v-model.number="row.discountAmount"></td>
            <td>₹{{ calculate(row).taxable.toFixed(2) }}</td>
            <td><input type="number" min="0" step="0.01" v-model.number="row.cgstPercent"></td>
            <td>₹{{ calculate(row).cgst.toFixed(2) }}</td>
            <td><input type="number" min="0" step="0.01" v-model.number="row.sgstPercent"></td>
            <td>₹{{ calculate(row).sgst.toFixed(2) }}</td>
            <td><input type="checkbox" v-model="row.isReturnableOnExpiry"></td>
            <td><b>₹{{ calculate(row).total.toFixed(2) }}</b></td>
            <td><button class="danger-btn" @click="removeMedicine(index)">×</button></td>
          </tr>
        </table>
      </div>

      <div class="bill-total-box">
        <div><span>Total Quantity</span><b>{{ totals.quantity }}</b></div>
        <div><span>Total Discount</span><b>₹{{ totals.discount.toFixed(2) }}</b></div>
        <div><span>Total Taxable</span><b>₹{{ totals.taxable.toFixed(2) }}</b></div>
        <div><span>Total CGST</span><b>₹{{ totals.cgst.toFixed(2) }}</b></div>
        <div><span>Total SGST</span><b>₹{{ totals.sgst.toFixed(2) }}</b></div>
        <div><span>Before Round Off</span><b>₹{{ totals.total.toFixed(2) }}</b></div>
        <div><span>Round Off</span><b>{{ totals.roundOff >= 0 ? '+' : '' }}₹{{ totals.roundOff.toFixed(2) }}</b></div>
        <div class="grand"><span>Total Bill</span><b>₹{{ totals.roundedTotal.toFixed(2) }}</b></div>
      </div>

      <div class="modal-actions"><button @click="saveInvoice">Save Invoice</button><button class="secondary" @click="showInvoice=false">Cancel</button></div>
    </div>
  </div>

  <div v-if="showTypeMaster" class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="showTypeMaster=false">×</button>
      <h2>Medicine Type Master</h2>
      <div class="form-grid"><label>Type Name *<input v-model="typeForm.name" placeholder="Tablet, Cream, Injection, Spray"></label></div>
      <div class="toolbar"><button @click="saveType">{{ typeForm.id ? 'Update' : 'Add' }} Type</button></div>
      <table class="table"><tr><th>Type</th><th></th></tr><tr v-for="t in medicineTypes" :key="t.id"><td>{{ t.name }}</td><td class="actions"><button @click="typeForm={id:t.id,name:t.name}">Edit</button><button class="danger-btn" @click="deleteType(t)">Delete</button></td></tr></table>
      <div class="modal-actions"><button class="secondary" @click="showTypeMaster=false">Close</button></div>
    </div>
  </div>
</template>
