<script setup>
import Pagination from '../../../components/Pagination.vue';

defineProps({
  pagedInventory: { type: Array, default: () => [] },
  sortedInventory: { type: Array, default: () => [] },
  inventoryPageCount: { type: Number, default: 1 },
  pageSize: { type: Number, required: true },
  sortInventory: { type: Function, required: true },
  inventorySortIndicator: { type: Function, required: true }
});

const inventorySearch = defineModel('search');
const inventoryPage = defineModel('page');
</script>

<template>
  <div class="card">
    <h2>Search Inventory</h2>
    <div class="toolbar"><input style="max-width:520px" v-model="inventorySearch" placeholder="Search MF, invoice number, product, type, batch no or HSN"></div>
    <table class="table">
      <tr><th class="sortable" @click="sortInventory('medicineType')">Type <span class="sort-indicator">{{inventorySortIndicator('medicineType')}}</span></th><th class="sortable" @click="sortInventory('manufacturer')">MF <span class="sort-indicator">{{inventorySortIndicator('manufacturer')}}</span></th><th class="sortable" @click="sortInventory('invoiceNumber')">Invoice <span class="sort-indicator">{{inventorySortIndicator('invoiceNumber')}}</span></th><th class="sortable" @click="sortInventory('productName')">Product <span class="sort-indicator">{{inventorySortIndicator('productName')}}</span></th><th class="sortable" @click="sortInventory('batchNo')">Batch <span class="sort-indicator">{{inventorySortIndicator('batchNo')}}</span></th><th class="sortable" @click="sortInventory('expiryDate')">Expire Date <span class="sort-indicator">{{inventorySortIndicator('expiryDate')}}</span></th><th class="sortable" @click="sortInventory('hsn')">HSN <span class="sort-indicator">{{inventorySortIndicator('hsn')}}</span></th><th class="sortable" @click="sortInventory('mrp')">MRP/Pack <span class="sort-indicator">{{inventorySortIndicator('mrp')}}</span></th><th class="sortable" @click="sortInventory('unitsPerPack')">Units/Pack <span class="sort-indicator">{{inventorySortIndicator('unitsPerPack')}}</span></th><th class="sortable" @click="sortInventory('availablePacks')">Available Packs <span class="sort-indicator">{{inventorySortIndicator('availablePacks')}}</span></th><th class="sortable" @click="sortInventory('looseUnits')">Loose Units <span class="sort-indicator">{{inventorySortIndicator('looseUnits')}}</span></th></tr>
      <tr v-for="row in pagedInventory" :key="row.id"><td>{{ row.medicineType }}</td><td>{{ row.manufacturer }}</td><td>{{ row.invoiceNumber }}</td><td>{{ row.productName }}</td><td>{{ row.batchNo }}</td><td>{{ row.expiryDate?.slice(0,10) }}</td><td>{{ row.hsn }}</td><td>₹{{ Number(row.mrp).toFixed(2) }}</td><td>{{ row.unitsPerPack }}</td><td>{{ row.availablePacks }}</td><td>{{ row.looseUnits }}</td></tr>
    </table>
    <Pagination :page="inventoryPage" :page-count="inventoryPageCount" :total="sortedInventory.length" :page-size="pageSize" @update:page="inventoryPage=$event" />
  </div>
</template>
