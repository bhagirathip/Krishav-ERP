<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../../api';

const today = new Date().toISOString().slice(0, 10);
const from = ref(today.slice(0, 8) + '01');
const to = ref(today);
const tab = ref('Hospital');

const hospital = ref({ billTypes: [], rows: [], columnTotals: {}, grandTotal: 0 });
const pharmacy = ref({ rows: [], totalTaxable: 0, totalCgst: 0, totalSgst: 0, grandTotal: 0 });

function money(value) {
  return Number(value || 0).toFixed(2);
}

async function load() {
  const [hospitalRes, pharmacyRes] = await Promise.all([
    api.get('/reporting/gst-filing', { params: { from: from.value, to: to.value } }),
    api.get('/reporting/gst-filing-pharmacy', { params: { from: from.value, to: to.value } })
  ]);
  hospital.value = hospitalRes.data;
  pharmacy.value = pharmacyRes.data;
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

function downloadHospitalCsv() {
  const header = ['Date', ...hospital.value.billTypes, 'Total'].map(csvEscape).join(',');
  const lines = [header];
  for (const row of hospital.value.rows) {
    lines.push([row.date, ...hospital.value.billTypes.map(bt => money(row.amounts[bt])), money(row.total)].map(csvEscape).join(','));
  }
  lines.push(['Total', ...hospital.value.billTypes.map(bt => money(hospital.value.columnTotals[bt])), money(hospital.value.grandTotal)].map(csvEscape).join(','));
  downloadBlob(lines.join('\n'), 'text/csv;charset=utf-8', `gst-filing-hospital-${from.value}-to-${to.value}.csv`);
}

function downloadHospitalExcel() {
  const headCells = ['Date', ...hospital.value.billTypes, 'Total'].map(h => `<th>${h}</th>`).join('');
  const bodyRows = hospital.value.rows.map(row =>
    `<tr><td>${row.date}</td>${hospital.value.billTypes.map(bt => `<td>${money(row.amounts[bt])}</td>`).join('')}<td>${money(row.total)}</td></tr>`
  ).join('');
  const totalCells = hospital.value.billTypes.map(bt => `<td><b>${money(hospital.value.columnTotals[bt])}</b></td>`).join('');
  const html = `<html><head><meta charset="utf-8"></head><body><table border="1"><tr>${headCells}</tr>${bodyRows}<tr><td><b>Total</b></td>${totalCells}<td><b>${money(hospital.value.grandTotal)}</b></td></tr></table></body></html>`;
  downloadBlob(html, 'application/vnd.ms-excel', `gst-filing-hospital-${from.value}-to-${to.value}.xls`);
}

function downloadPharmacyCsv() {
  const header = ['Date', 'Taxable Value', 'CGST', 'SGST', 'Total'].map(csvEscape).join(',');
  const lines = [header];
  for (const row of pharmacy.value.rows) {
    lines.push([row.date, money(row.taxable), money(row.cgst), money(row.sgst), money(row.total)].map(csvEscape).join(','));
  }
  lines.push(['Total', money(pharmacy.value.totalTaxable), money(pharmacy.value.totalCgst), money(pharmacy.value.totalSgst), money(pharmacy.value.grandTotal)].map(csvEscape).join(','));
  downloadBlob(lines.join('\n'), 'text/csv;charset=utf-8', `gst-filing-pharmacy-${from.value}-to-${to.value}.csv`);
}

function downloadPharmacyExcel() {
  const headCells = ['Date', 'Taxable Value', 'CGST', 'SGST', 'Total'].map(h => `<th>${h}</th>`).join('');
  const bodyRows = pharmacy.value.rows.map(row =>
    `<tr><td>${row.date}</td><td>${money(row.taxable)}</td><td>${money(row.cgst)}</td><td>${money(row.sgst)}</td><td>${money(row.total)}</td></tr>`
  ).join('');
  const html = `<html><head><meta charset="utf-8"></head><body><table border="1"><tr>${headCells}</tr>${bodyRows}<tr><td><b>Total</b></td><td><b>${money(pharmacy.value.totalTaxable)}</b></td><td><b>${money(pharmacy.value.totalCgst)}</b></td><td><b>${money(pharmacy.value.totalSgst)}</b></td><td><b>${money(pharmacy.value.grandTotal)}</b></td></tr></table></body></html>`;
  downloadBlob(html, 'application/vnd.ms-excel', `gst-filing-pharmacy-${from.value}-to-${to.value}.xls`);
}

onMounted(load);
</script>

<template>
  <h1>GST Filing</h1>
  <div class="muted">Taxable turnover for the selected date range, for GST return filing - Hospital and Pharmacy are filed separately.</div>

  <div class="toolbar report-filter">
    <label>From<input v-model="from" type="date"></label>
    <label>To<input v-model="to" type="date"></label>
    <button @click="load">Apply</button>
  </div>

  <div class="toolbar">
    <button :class="tab === 'Hospital' ? '' : 'secondary'" @click="tab = 'Hospital'">Hospital</button>
    <button :class="tab === 'Pharmacy' ? '' : 'secondary'" @click="tab = 'Pharmacy'">Pharmacy</button>
  </div>

  <template v-if="tab === 'Hospital'">
    <div class="toolbar">
      <button class="secondary" @click="downloadHospitalCsv">Download CSV</button>
      <button class="secondary" @click="downloadHospitalExcel">Download Excel</button>
    </div>

    <div v-if="!hospital.rows.length" class="muted">No bills in this date range.</div>
    <table v-else class="table">
      <tr>
        <th>Date</th>
        <th v-for="bt in hospital.billTypes" :key="bt">{{ bt }}</th>
        <th>Total</th>
      </tr>
      <tr v-for="row in hospital.rows" :key="row.date">
        <td>{{ row.date }}</td>
        <td v-for="bt in hospital.billTypes" :key="bt">₹{{ money(row.amounts[bt]) }}</td>
        <td><b>₹{{ money(row.total) }}</b></td>
      </tr>
      <tr>
        <td><b>Total</b></td>
        <td v-for="bt in hospital.billTypes" :key="bt"><b>₹{{ money(hospital.columnTotals[bt]) }}</b></td>
        <td><b>₹{{ money(hospital.grandTotal) }}</b></td>
      </tr>
    </table>
  </template>

  <template v-else>
    <div class="toolbar">
      <button class="secondary" @click="downloadPharmacyCsv">Download CSV</button>
      <button class="secondary" @click="downloadPharmacyExcel">Download Excel</button>
    </div>
    <div class="muted">Taxable value, CGST and SGST are extracted from each sale's tax-inclusive MRP, netted against any returns on the date the return happened.</div>

    <div v-if="!pharmacy.rows.length" class="muted">No pharmacy sales in this date range.</div>
    <table v-else class="table">
      <tr>
        <th>Date</th>
        <th>Taxable Value</th>
        <th>CGST</th>
        <th>SGST</th>
        <th>Total</th>
      </tr>
      <tr v-for="row in pharmacy.rows" :key="row.date">
        <td>{{ row.date }}</td>
        <td>₹{{ money(row.taxable) }}</td>
        <td>₹{{ money(row.cgst) }}</td>
        <td>₹{{ money(row.sgst) }}</td>
        <td><b>₹{{ money(row.total) }}</b></td>
      </tr>
      <tr>
        <td><b>Total</b></td>
        <td><b>₹{{ money(pharmacy.totalTaxable) }}</b></td>
        <td><b>₹{{ money(pharmacy.totalCgst) }}</b></td>
        <td><b>₹{{ money(pharmacy.totalSgst) }}</b></td>
        <td><b>₹{{ money(pharmacy.grandTotal) }}</b></td>
      </tr>
    </table>
  </template>
</template>
