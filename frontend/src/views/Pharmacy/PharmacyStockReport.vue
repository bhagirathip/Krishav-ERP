<script setup>
import { onMounted, ref } from 'vue';
import { api } from '../../api';
import GridSearch from '../../components/GridSearch.vue';
import Pagination from '../../components/Pagination.vue';
import { useGrid } from '../../composables/useGrid';

const allDates = ref(true);
const today = new Date().toISOString().slice(0, 10);
const from = ref(today);
const to = ref(today);
const rows = ref([]);
const totalClosingStockAmount = ref(0);
const pageSize = 10;
const { search, page, pageCount, pagedRows, sortedRows, sortBy, sortIndicator } = useGrid(rows, pageSize);

async function load() {
  const params = allDates.value ? {} : { from: from.value, to: to.value };
  const { data } = await api.get('/pharmacy/sales/stock-report', { params });
  rows.value = data.rows || [];
  totalClosingStockAmount.value = data.totalClosingStockAmount || 0;
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

function exportExcel() {
  const headerCells = ['Sl No', 'Product Name', 'MRP', 'Purchases', 'Sales', 'Closing Stock', 'Closing Stock Amount'];
  const bodyRows = sortedRows.value.map((row, index) => `<tr>
    <td>${index + 1}</td>
    <td>${row.productName}</td>
    <td>${Number(row.mrp).toFixed(2)}</td>
    <td>${row.purchasesText}</td>
    <td>${row.salesText}</td>
    <td>${row.closingStockText}</td>
    <td>${Number(row.closingStockAmount).toFixed(2)}</td>
  </tr>`).join('');
  const html = `<html><head><meta charset="utf-8"></head><body><table border="1">
    <tr>${headerCells.map(h => `<th>${h}</th>`).join('')}</tr>
    ${bodyRows}
    <tr><td colspan="6"><b>Total Closing Stock Amount</b></td><td><b>${totalClosingStockAmount.value.toFixed(2)}</b></td></tr>
  </table></body></html>`;
  downloadBlob(html, 'application/vnd.ms-excel', `pharmacy-stock-report-${today}.xls`);
}

onMounted(load);
</script>

<template>
  <h1>Pharmacy Stock Report</h1>
  <p class="muted">Purchases and sales are scoped to the selected date range (or all time). Closing Stock always reflects the current on-hand inventory.</p>

  <div class="toolbar">
    <label class="toggle-row"><input type="checkbox" v-model="allDates"> All Dates</label>
    <label>From<input type="date" v-model="from" :disabled="allDates"></label>
    <label>To<input type="date" v-model="to" :disabled="allDates"></label>
    <button @click="load">Apply</button>
    <button class="secondary" @click="exportExcel">Export Excel</button>
  </div>

  <div class="grid">
    <div class="card"><div class="muted">Total Closing Stock Value</div><div class="metric">₹{{ Number(totalClosingStockAmount).toFixed(2) }}</div></div>
  </div>

  <div class="grid-filter-row"><GridSearch v-model="search" placeholder="Search product name..." /></div>
  <div class="table-scroll">
    <table class="table report-table">
      <tr>
        <th>Sl No</th>
        <th class="sortable" @click="sortBy('productName')">Product Name <span class="sort-indicator">{{ sortIndicator('productName') }}</span></th>
        <th class="sortable" @click="sortBy('mrp')">MRP <span class="sort-indicator">{{ sortIndicator('mrp') }}</span></th>
        <th>Purchases</th>
        <th>Sales</th>
        <th>Closing Stock</th>
        <th class="sortable" @click="sortBy('closingStockAmount')">Closing Stock Amount <span class="sort-indicator">{{ sortIndicator('closingStockAmount') }}</span></th>
      </tr>
      <tr v-for="(row, index) in pagedRows" :key="row.productName + '-' + row.mrp">
        <td>{{ (page - 1) * pageSize + index + 1 }}</td>
        <td>{{ row.productName }}</td>
        <td>₹{{ Number(row.mrp).toFixed(2) }}</td>
        <td>{{ row.purchasesText }}</td>
        <td>{{ row.salesText }}</td>
        <td>{{ row.closingStockText }}</td>
        <td><b>₹{{ Number(row.closingStockAmount).toFixed(2) }}</b></td>
      </tr>
    </table>
    <div v-if="!rows.length" class="muted">No purchase data found.</div>
    <Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="pageSize" @update:page="page = $event" />
  </div>
</template>
