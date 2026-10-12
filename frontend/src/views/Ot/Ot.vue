<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../../api';
import Pagination from '../../components/Pagination.vue';
import GridSearch from '../../components/GridSearch.vue';
import { useServerGrid } from '../../composables/useServerGrid';

const pageSize = 10;

const docs = ref([]);
const patients = ref([]);
const show = ref(false);
const f = ref(blank());
const err = ref('');

// DoctorName isn't a real OtBooking column (OtController resolves it via a
// bulk dictionary lookup after the fetch), so it's matched via a
// correlated subquery against Doctors in the backend's default search
// branch - same trick Bills uses for patientName. No searchColumn is sent -
// a plain search box matches booking number, OT room, doctor, procedure and
// status together server-side.
async function fetchOtPage(params) {
  const { data } = await api.get('/ot', {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      searchText: params.searchText,
      sortBy: params.sortBy,
      sortDirection: params.sortDirection
    }
  });
  return { rows: data.rows, totalCount: data.totalCount };
}

const { page, pageCount, rows, totalCount, sortBy, sortIndicator, searchText, reload } = useServerGrid(fetchOtPage, pageSize);

function blank() {
  return {
    patientId: null,
    externalPatientName: '',
    externalPatientPhone: '',
    doctorId: null,
    otRoom: 'OT 1',
    startAtUtc: '',
    endAtUtc: '',
    procedureName: '',
    doctorPayoutAmount: 0
  };
}

async function load() {
  const [d, p] = await Promise.all([api.get('/doctors'), api.get('/patients'), reload()]);
  docs.value = d.data;
  patients.value = p.data;
}

function add() {
  f.value = blank();
  err.value = '';
  show.value = true;
}

async function save() {
  try {
    err.value = '';
    await api.post('/ot', f.value);
    show.value = false;
    await load();
  } catch (e) {
    err.value = e.response?.data?.message || 'Unable to book OT';
  }
}

onMounted(load);
</script>

<template>
  <h1>OT Management</h1>

  <div class="toolbar">
    <button @click="add">+ Book OT</button>
  </div>

  <div class="grid-filter-row">
    <GridSearch v-model="searchText" placeholder="Search booking number, OT room, doctor, procedure, status..." />
  </div>

  <table class="table">
    <tr>
      <th class="sortable" @click="sortBy('bookingNumber')">Booking <span class="sort-indicator">{{ sortIndicator('bookingNumber') }}</span></th>
      <th class="sortable" @click="sortBy('otRoom')">OT <span class="sort-indicator">{{ sortIndicator('otRoom') }}</span></th>
      <th class="sortable" @click="sortBy('doctorName')">Doctor <span class="sort-indicator">{{ sortIndicator('doctorName') }}</span></th>
      <th class="sortable" @click="sortBy('startAtUtc')">Start <span class="sort-indicator">{{ sortIndicator('startAtUtc') }}</span></th>
      <th class="sortable" @click="sortBy('endAtUtc')">End <span class="sort-indicator">{{ sortIndicator('endAtUtc') }}</span></th>
      <th class="sortable" @click="sortBy('procedureName')">Procedure <span class="sort-indicator">{{ sortIndicator('procedureName') }}</span></th>
      <th class="sortable" @click="sortBy('doctorPayoutAmount')">Doctor Payout <span class="sort-indicator">{{ sortIndicator('doctorPayoutAmount') }}</span></th>
      <th class="sortable" @click="sortBy('status')">Status <span class="sort-indicator">{{ sortIndicator('status') }}</span></th>
    </tr>
    <tr v-for="x in rows" :key="x.id">
      <td>{{ x.bookingNumber }}</td>
      <td>{{ x.otRoom }}</td>
      <td>{{ x.doctorName }}</td>
      <td>{{ new Date(x.startAtUtc).toLocaleString() }}</td>
      <td>{{ new Date(x.endAtUtc).toLocaleString() }}</td>
      <td>{{ x.procedureName }}</td>
      <td>₹{{ Number(x.doctorPayoutAmount || 0).toFixed(2) }}</td>
      <td>{{ x.status }}</td>
    </tr>
  </table>

  <Pagination :page="page" :page-count="pageCount" :total="totalCount" :page-size="pageSize" @update:page="page = $event" />

  <div v-if="show" class="modal-bg">
    <div class="modal">
      <button class="modal-close-x" @click="show = false">×</button>
      <h2>Book OT</h2>

      <div class="form-grid">
        <label>
          Existing Patient
          <select v-model.number="f.patientId">
            <option :value="null">Outside Patient</option>
            <option v-for="p in patients" :value="p.id">{{ p.name }} - {{ p.patientCode }}</option>
          </select>
        </label>

        <label>
          Doctor
          <select v-model.number="f.doctorId">
            <option :value="null">Select Doctor</option>
            <option v-for="d in docs" :value="d.id">{{ d.name }}</option>
          </select>
        </label>

        <label v-if="!f.patientId">Outside Patient Name<input v-model="f.externalPatientName"></label>
        <label v-if="!f.patientId">Phone<input v-model="f.externalPatientPhone"></label>

        <label>
          OT
          <select v-model="f.otRoom">
            <option>OT 1</option>
            <option>OT 2</option>
          </select>
        </label>

        <label>Procedure<input v-model="f.procedureName"></label>
        <label>Start<input type="datetime-local" v-model="f.startAtUtc"></label>
        <label>End<input type="datetime-local" v-model="f.endAtUtc"></label>
        <label>Doctor OT Payout ₹<input type="number" min="0" step="0.01" v-model.number="f.doctorPayoutAmount"></label>
      </div>

      <div class="danger">{{ err }}</div>

      <div class="modal-actions">
        <button @click="save">Book</button>
        <button class="secondary" @click="show = false">Cancel</button>
      </div>
    </div>
  </div>
</template>
