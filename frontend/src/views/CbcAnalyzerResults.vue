<script setup>
import { onMounted, ref } from 'vue';
import { api, assetUrl } from '../api';
import GridSearch from '../components/GridSearch.vue';

const today = new Date();
const from = ref(new Date(today.getFullYear(), today.getMonth(), 1).toISOString().slice(0, 10));
const to = ref(today.toISOString().slice(0, 10));
const search = ref('');
const rows = ref([]);
const detail = ref(null);

async function load() {
  rows.value = (await api.get('/cbc-analyzer/results', {
    params: { from: from.value, to: to.value, search: search.value || undefined }
  })).data;
}

async function openDetail(row) {
  detail.value = (await api.get(`/cbc-analyzer/results/${row.id}`)).data;
}

async function deleteResult(row) {
  if (!confirm(`Delete this ${row.resultTypeName || 'CBC'} result for ${row.patientName || 'this sample'}?`)) return;
  await api.delete(`/cbc-analyzer/results/${row.id}`);
  await load();
}

function formatDateTime(value) {
  if (!value) return '-';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '-';
  const pad = n => String(n).padStart(2, '0');
  return `${pad(d.getDate())}-${pad(d.getMonth() + 1)}-${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

onMounted(load);
</script>

<template>
  <h1>CBC Analyzer Results</h1>
  <p class="muted">Results and histogram/scattergram images received automatically from the BM500 CBC analyzer over the LIS network connection. This is independent from manually-entered Lab test results.</p>

  <div class="toolbar">
    <label>From<input type="date" v-model="from"></label>
    <label>To<input type="date" v-model="to"></label>
    <button @click="load">Apply</button>
  </div>
  <GridSearch v-model="search" placeholder="Search patient name, patient ID, sample ID..." />

  <table class="table">
    <tr>
      <th>Received</th><th>Sample ID</th><th>Type</th><th>Patient</th><th>Patient ID</th>
      <th>Gender</th><th>Age</th><th>Test Mode</th><th>Items</th><th>Images</th><th></th>
    </tr>
    <tr v-for="row in rows" :key="row.id">
      <td>{{ formatDateTime(row.receivedAtUtc) }}</td>
      <td>{{ row.sampleId || '-' }}</td>
      <td>{{ row.resultTypeName }}<div class="muted" v-if="row.processingId === 'Q'">QC</div></td>
      <td>{{ row.patientName || '-' }}</td>
      <td>{{ row.patientIdentifier || '-' }}</td>
      <td>{{ row.gender || '-' }}</td>
      <td>{{ row.ageText || '-' }}</td>
      <td>{{ row.testMode || '-' }}</td>
      <td>{{ row.itemCount }}</td>
      <td>{{ row.imageCount }}</td>
      <td class="actions">
        <button @click="openDetail(row)">View</button>
        <button class="danger-btn" @click="deleteResult(row)">Delete</button>
      </td>
    </tr>
  </table>
  <div v-if="!rows.length" class="muted">No CBC analyzer results received for the selected filters.</div>

  <div v-if="detail" class="modal-bg">
    <div class="modal modal-xl">
      <button class="modal-close-x" @click="detail = null">×</button>
      <h2>{{ detail.resultTypeName }} &middot; {{ detail.patientName || 'Unknown patient' }}</h2>

      <div class="form-grid">
        <div><b>Sample ID:</b> {{ detail.sampleId || '-' }}</div>
        <div><b>Patient ID:</b> {{ detail.patientIdentifier || '-' }}</div>
        <div><b>Gender / Age:</b> {{ detail.gender || '-' }} / {{ detail.ageText || '-' }}</div>
        <div><b>Patient Class / Location:</b> {{ detail.patientClass || '-' }} / {{ detail.patientLocation || '-' }}</div>
        <div><b>Tester:</b> {{ detail.tester || '-' }}</div>
        <div><b>Interpreter:</b> {{ detail.interpreter || '-' }}</div>
        <div><b>Loading / Blood / Test Mode:</b> {{ detail.loadingMode || '-' }} / {{ detail.bloodMode || '-' }} / {{ detail.testMode || '-' }}</div>
        <div><b>Reference Group:</b> {{ detail.refGroup || '-' }}</div>
        <div><b>Requested:</b> {{ formatDateTime(detail.requestedAtUtc) }}</div>
        <div><b>Observed:</b> {{ formatDateTime(detail.observationAtUtc) }}</div>
        <div><b>Specimen Received:</b> {{ formatDateTime(detail.specimenReceivedAtUtc) }}</div>
        <div><b>Received in system:</b> {{ formatDateTime(detail.receivedAtUtc) }}</div>
        <div class="full" v-if="detail.remark"><b>Remark:</b> {{ detail.remark }}</div>
      </div>

      <table class="table" style="margin-top:16px">
        <tr><th>Code</th><th>Parameter</th><th>Result</th><th>Unit</th><th>Reference Range</th><th>Flag</th></tr>
        <tr v-for="item in detail.items" :key="item.code + item.name">
          <td>{{ item.code }}</td>
          <td>{{ item.name }}</td>
          <td>{{ item.value ?? '-' }}</td>
          <td>{{ item.unit || '-' }}</td>
          <td>{{ item.referenceRange || '-' }}</td>
          <td :class="{ danger: item.abnormalFlag }">{{ item.abnormalFlag || '-' }}</td>
        </tr>
      </table>
      <div v-if="!detail.items?.length" class="muted" style="margin-top:8px">No numeric/text result parameters in this message.</div>

      <div v-if="detail.images?.length" class="cbc-image-grid">
        <div v-for="image in detail.images" :key="image.id" class="cbc-image-card">
          <img :src="assetUrl(image.imagePath)" :alt="image.name">
          <div class="muted">{{ image.name }}</div>
        </div>
      </div>

      <div class="modal-actions">
        <button class="secondary" @click="detail = null">Close</button>
      </div>
    </div>
  </div>
</template>
