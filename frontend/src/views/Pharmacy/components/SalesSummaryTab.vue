<script setup>
import GridSearch from '../../../components/GridSearch.vue';
import Pagination from '../../../components/Pagination.vue';

defineProps({
  loadDayEnd: { type: Function, required: true },
  dayEnd: { type: Object, default: null },
  pagedSales: { type: Array, default: () => [] },
  sortedSales: { type: Array, default: () => [] },
  salesPageCount: { type: Number, default: 1 },
  pageSize: { type: Number, required: true },
  sortSales: { type: Function, required: true },
  salesSortIndicator: { type: Function, required: true },
  printSale: { type: Function, required: true },
  downloadExcel: { type: Function, required: true },
  downloadCsv: { type: Function, required: true },
  pagedSold: { type: Array, default: () => [] },
  sortedSold: { type: Array, default: () => [] },
  soldPageCount: { type: Number, default: 1 },
  sortSold: { type: Function, required: true },
  soldSortIndicator: { type: Function, required: true },
  pagedStock: { type: Array, default: () => [] },
  sortedStock: { type: Array, default: () => [] },
  stockPageCount: { type: Number, default: 1 },
  sortStock: { type: Function, required: true },
  stockSortIndicator: { type: Function, required: true }
});

const dayEndDate = defineModel('dayEndDate');
const salesSearch = defineModel('salesSearch');
const salesPage = defineModel('salesPage');
const soldSearch = defineModel('soldSearch');
const soldPage = defineModel('soldPage');
const stockSearch = defineModel('stockSearch');
const stockPage = defineModel('stockPage');
</script>

<template>
  <div class="card">
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
