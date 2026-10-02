<script setup>
import { onMounted, ref } from 'vue';
import { api } from '../../api';
import { can } from '../../auth';
import Pagination from '../../components/Pagination.vue';
import GridSearch from '../../components/GridSearch.vue';
import { useGrid } from '../../composables/useGrid';
import PurchaseInvoiceModal from './components/PurchaseInvoiceModal.vue';
import PurchaseTypeMasterModal from './components/PurchaseTypeMasterModal.vue';

const invoices = ref([]);
const distributors = ref([]);
const medicineTypes = ref([]);
const showInvoice = ref(false);
const showTypeMaster = ref(false);
const editingInvoice = ref(null);
const pageSize = 10;

const { search, page, pageCount, pagedRows: pagedInvoices, sortedRows, sortBy, sortIndicator } = useGrid(invoices, pageSize);

async function load() {
  invoices.value = (await api.get('/pharmacy/purchases')).data;
  distributors.value = (await api.get('/pharmacy/distributors')).data;
  medicineTypes.value = (await api.get('/pharmacy/medicine-types')).data;
}

function addInvoice() {
  editingInvoice.value = null;
  showInvoice.value = true;
}

async function editInvoice(row) {
  const { data } = await api.get(`/pharmacy/purchases/${row.id}`);
  editingInvoice.value = {
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
  showInvoice.value = true;
}

async function invoiceSaved() {
  showInvoice.value = false;
  await load();
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
  showTypeMaster.value = true;
}

function typesUpdated(data) {
  medicineTypes.value = data;
}

onMounted(load);
</script>

<template>
  <h1>Pharmacy Purchase</h1>

  <div class="toolbar">
    <button v-if="can('PHARMACY_PURCHASE', 'add')" @click="addInvoice">+ Add Purchase Invoice</button>
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
        <button v-if="can('PHARMACY_PURCHASE','edit')" @click="editInvoice(row)">Edit</button>
        <button v-if="can('PHARMACY_PURCHASE','delete')" class="danger-btn" @click="deleteInvoice(row)">Delete</button>
      </td>
    </tr>
  </table>

  <Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="pageSize" @update:page="page = $event" />

  <PurchaseInvoiceModal
    v-if="showInvoice"
    :initial-invoice="editingInvoice"
    :distributors="distributors"
    :medicine-types="medicineTypes"
    @close="showInvoice=false"
    @saved="invoiceSaved"
  />

  <PurchaseTypeMasterModal
    v-if="showTypeMaster"
    :medicine-types="medicineTypes"
    @close="showTypeMaster=false"
    @updated="typesUpdated"
  />
</template>
