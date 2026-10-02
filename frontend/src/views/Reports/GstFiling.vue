<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../../api';

const today = new Date().toISOString().slice(0, 10);
const from = ref(today.slice(0, 8) + '01');
const to = ref(today);
const data = ref({ billTypes: [], rows: [], columnTotals: {}, grandTotal: 0 });

function money(value) {
  return Number(value || 0).toFixed(2);
}

async function load() {
  data.value = (await api.get('/reporting/gst-filing', { params: { from: from.value, to: to.value } })).data;
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

function downloadCsv() {
  const header = ['Date', ...data.value.billTypes, 'Total'].map(csvEscape).join(',');
  const lines = [header];
  for (const row of data.value.rows) {
    lines.push([row.date, ...data.value.billTypes.map(bt => money(row.amounts[bt])), money(row.total)].map(csvEscape).join(','));
  }
  lines.push(['Total', ...data.value.billTypes.map(bt => money(data.value.columnTotals[bt])), money(data.value.grandTotal)].map(csvEscape).join(','));
  downloadBlob(lines.join('\n'), 'text/csv;charset=utf-8', `gst-filing-${from.value}-to-${to.value}.csv`);
}

function downloadExcel() {
  const headCells = ['Date', ...data.value.billTypes, 'Total'].map(h => `<th>${h}</th>`).join('');
  const bodyRows = data.value.rows.map(row =>
    `<tr><td>${row.date}</td>${data.value.billTypes.map(bt => `<td>${money(row.amounts[bt])}</td>`).join('')}<td>${money(row.total)}</td></tr>`
  ).join('');
  const totalCells = data.value.billTypes.map(bt => `<td><b>${money(data.value.columnTotals[bt])}</b></td>`).join('');
  const html = `<html><head><meta charset="utf-8"></head><body><table border="1"><tr>${headCells}</tr>${bodyRows}<tr><td><b>Total</b></td>${totalCells}<td><b>${money(data.value.grandTotal)}</b></td></tr></table></body></html>`;
  downloadBlob(html, 'application/vnd.ms-excel', `gst-filing-${from.value}-to-${to.value}.xls`);
}

onMounted(load);
</script>

<template>
  <h1>GST Filing</h1>
  <div class="muted">Taxable turnover by bill type for the selected date range, for GST return filing.</div>

  <div class="toolbar report-filter">
    <label>From<input v-model="from" type="date"></label>
    <label>To<input v-model="to" type="date"></label>
    <button @click="load">Apply</button>
    <button class="secondary" @click="downloadCsv">Download CSV</button>
    <button class="secondary" @click="downloadExcel">Download Excel</button>
  </div>

  <div v-if="!data.rows.length" class="muted">No bills in this date range.</div>
  <table v-else class="table">
    <tr>
      <th>Date</th>
      <th v-for="bt in data.billTypes" :key="bt">{{ bt }}</th>
      <th>Total</th>
    </tr>
    <tr v-for="row in data.rows" :key="row.date">
      <td>{{ row.date }}</td>
      <td v-for="bt in data.billTypes" :key="bt">₹{{ money(row.amounts[bt]) }}</td>
      <td><b>₹{{ money(row.total) }}</b></td>
    </tr>
    <tr>
      <td><b>Total</b></td>
      <td v-for="bt in data.billTypes" :key="bt"><b>₹{{ money(data.columnTotals[bt]) }}</b></td>
      <td><b>₹{{ money(data.grandTotal) }}</b></td>
    </tr>
  </table>
</template>
