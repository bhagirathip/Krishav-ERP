<script setup>
import { computed, ref } from 'vue';
import { api } from '../../../api';

const props = defineProps({
  initialInvoice: { type: Object, default: null },
  distributors: { type: Array, default: () => [] },
  medicineTypes: { type: Array, default: () => [] }
});

const emit = defineEmits(['close', 'saved']);

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

const invoiceForm = ref(props.initialInvoice ? { ...props.initialInvoice } : emptyInvoice());
const distributorDocument = ref(null);
const errors = ref([]);
const importMessage = ref('');
const importing = ref(false);

function typeName(row) {
  return props.medicineTypes.find(x => x.id === row.medicineTypeId)?.name || '';
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

    emit('saved');
  } catch (error) {
    errors.value = [error.response?.data?.message || 'Unable to save purchase invoice.'];
  }
}
</script>

<template>
  <div class="modal-bg">
    <div class="modal modal-pharmacy-invoice">
      <button class="modal-close-x" @click="emit('close')">×</button>
      <div class="page-head"><h2>{{ invoiceForm.id ? 'Edit' : 'Add' }} Purchase Invoice</h2><button class="secondary" @click="emit('close')">Close</button></div>
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

      <div class="modal-actions"><button @click="saveInvoice">Save Invoice</button><button class="secondary" @click="emit('close')">Cancel</button></div>
    </div>
  </div>
</template>
