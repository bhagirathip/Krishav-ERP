<script setup>
import { onMounted, ref } from 'vue';
import { api } from '../../api';
import GridSearch from '../../components/GridSearch.vue';
import Pagination from '../../components/Pagination.vue';
import { useServerGrid } from '../../composables/useServerGrid';

const allDates = ref(true);
const today = new Date().toISOString().slice(0, 10);
const from = ref(today);
const to = ref(today);
const totalClosingStockAmount = ref(0);
const pageSize = 10;

// No searchColumn - this screen only ever had one searchable field (Product
// Name), so the plain search box just matches that directly server-side
// (see PharmacySalesController.StockReport).
async function fetchStockReportPage(params) {
  const dateParams = allDates.value ? {} : { from: from.value, to: to.value };
  const { data } = await api.get('/pharmacy/sales/stock-report', {
    params: {
      ...dateParams,
      page: params.page,
      pageSize: params.pageSize,
      searchText: params.searchText,
      sortBy: params.sortBy,
      sortDirection: params.sortDirection
    }
  });
  totalClosingStockAmount.value = data.totalClosingStockAmount || 0;
  return { rows: data.rows || [], totalCount: data.totalCount || 0 };
}

const { page, pageCount, rows: pagedRows, totalCount, sortKey, sortDirection, sortBy, sortIndicator, searchText, reload } = useServerGrid(fetchStockReportPage, pageSize);

async function load() {
  await reload();
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

// Export needs every matching/sorted row, not just the current page, so it
// calls the report endpoint directly without `page` - the backend then
// returns the full filtered+sorted array instead of one page of it.
async function exportExcel() {
  const dateParams = allDates.value ? {} : { from: from.value, to: to.value };
  const { data } = await api.get('/pharmacy/sales/stock-report', {
    params: {
      ...dateParams,
      searchText: searchText.value,
      sortBy: sortKey.value,
      sortDirection: sortDirection.value
    }
  });
  const exportRows = data.rows || [];

  const headerCells = ['Sl No', 'Product Name', 'MRP', 'Purchases', 'Sales', 'Closing Stock', 'Closing Stock Amount'];
  const bodyRows = exportRows.map((row, index) => `<tr>
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
    <tr><td colspan="6"><b>Total Closing Stock Amount</b></td><td><b>${(data.totalClosingStockAmount || 0).toFixed(2)}</b></td></tr>
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

  <div class="grid-filter-row"><GridSearch v-model="searchText" placeholder="Search product name..." /></div>
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
    <div v-if="!pagedRows.length" class="muted">No purchase data found.</div>
    <Pagination :page="page" :page-count="pageCount" :total="totalCount" :page-size="pageSize" @update:page="page = $event" />
  </div>
</template>
