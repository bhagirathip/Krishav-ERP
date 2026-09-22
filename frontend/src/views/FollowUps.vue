<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../api';
import GridSearch from '../components/GridSearch.vue';
import Pagination from '../components/Pagination.vue';
import { useGrid } from '../composables/useGrid';

const today =
  new Date().toISOString().slice(0, 10);

const selectedDate = ref(today);
const rows = ref([]);
const patients = ref([]);
const showAdd = ref(false);
const showComplete = ref(false);
const selected = ref(null);
const form = ref({
  comment: '',
  followUpNeeded: false,
  nextFollowUpDate: null
});

const addForm = ref({
  patientId: null,
  followUpDate: today,
  comment: ''
});

const {
  search,
  page,
  pageCount,
  pagedRows,
  sortedRows,
  sortBy,
  sortIndicator
} = useGrid(rows, 10);

async function load() {
  const [followUpsRes, patientsRes] = await Promise.all([
    api.get('/followups', {
      params: {
        date: selectedDate.value
      }
    }),
    api.get('/patients')
  ]);

  rows.value = followUpsRes.data;
  patients.value = patientsRes.data;
}


function openAdd() {
  addForm.value = {
    patientId: null,
    followUpDate: today,
    comment: ''
  };
  showAdd.value = true;
}

async function saveAdd() {
  if (!addForm.value.patientId) {
    alert('Patient is required.');
    return;
  }

  if (!addForm.value.followUpDate) {
    alert('Follow-up date is required.');
    return;
  }

  try {
    await api.post('/followups', addForm.value);
    showAdd.value = false;

    if (addForm.value.followUpDate === selectedDate.value) {
      await load();
    }
  } catch (error) {
    alert(
      error.response?.data?.message ||
      'Unable to add follow-up.'
    );
  }
}

function openComplete(row) {
  selected.value = row;

  form.value = {
    comment: '',
    followUpNeeded: false,
    nextFollowUpDate: null
  };

  showComplete.value = true;
}

async function saveComplete() {
  if (!form.value.comment.trim()) {
    alert('Follow-up comment is required.');
    return;
  }

  if (
    form.value.followUpNeeded &&
    !form.value.nextFollowUpDate
  ) {
    alert(
      'Please select the next follow-up date.'
    );
    return;
  }

  try {
    await api.post(
      `/followups/${selected.value.id}/complete`,
      form.value
    );

    showComplete.value = false;
    await load();
  } catch (error) {
    alert(
      error.response?.data?.message ||
      'Unable to complete follow-up.'
    );
  }
}

onMounted(load);
</script>

<template>
  <h1>Follow-up</h1>

  <div class="toolbar">
    <button @click="openAdd">
      + Add Follow-up
    </button>

    <label>
      Follow-up Date
      <input
        type="date"
        v-model="selectedDate"
        @change="load"
      >
    </label>

    <button @click="load">
      Apply
    </button>

    <button
      class="secondary"
      @click="
        selectedDate = today;
        load()
      "
    >
      Today
    </button>
  </div>

  <div class="grid-filter-row">
    <GridSearch
      v-model="search"
      placeholder="Search patient, phone, doctor, status, comment..."
    />
  </div>

  <table class="table">
    <tr>
      <th
        class="sortable"
        @click="sortBy('patientCode')"
      >
        Patient ID
        {{ sortIndicator('patientCode') }}
      </th>

      <th
        class="sortable"
        @click="sortBy('patientName')"
      >
        Patient
        {{ sortIndicator('patientName') }}
      </th>

      <th>Phone</th>
      <th>Doctor</th>
      <th>Visit</th>

      <th
        class="sortable"
        @click="sortBy('followUpDate')"
      >
        Follow-up Date
        {{ sortIndicator('followUpDate') }}
      </th>

      <th
        class="sortable"
        @click="sortBy('status')"
      >
        Status
        {{ sortIndicator('status') }}
      </th>

      <th>Comment</th>
      <th>Next Follow-up</th>
      <th></th>
    </tr>

    <tr
      v-for="row in pagedRows"
      :key="row.id"
    >
      <td>{{ row.patientCode }}</td>

      <td>
        <b>{{ row.patientName }}</b>
      </td>

      <td>{{ row.phone }}</td>
      <td>{{ row.doctorName || '-' }}</td>
      <td>{{ row.visitNumber || '-' }}</td>

      <td>
        {{
          String(row.followUpDate)
            .slice(0, 10)
        }}
      </td>

      <td>
        <span class="pill">
          {{ row.status }}
        </span>
      </td>

      <td>{{ row.comment || '-' }}</td>

      <td>
        {{
          row.nextFollowUpDate
            ? String(
                row.nextFollowUpDate
              ).slice(0, 10)
            : '-'
        }}
      </td>

      <td>
        <button
          v-if="row.status !== 'Completed'"
          @click="openComplete(row)"
        >
          Complete Follow-up
        </button>
      </td>
    </tr>
  </table>

  <Pagination
    :page="page"
    :page-count="pageCount"
    :total="sortedRows.length"
    :page-size="10"
    @update:page="page = $event"
  />


  <div
    v-if="showAdd"
    class="modal-bg"
  >
    <div class="modal">
      <button class="modal-close-x" @click="showAdd = false">×</button>
      <h2>Add Follow-up</h2>

      <div class="form-grid">
        <label>
          Patient *
          <select
            v-model.number="addForm.patientId"
          >
            <option :value="null">
              Select Patient
            </option>

            <option
              v-for="patient in patients"
              :key="patient.id"
              :value="patient.id"
            >
              {{ patient.name }} · {{ patient.patientCode }} · {{ patient.phone }}
            </option>
          </select>
        </label>

        <label>
          Follow-up Date *
          <input
            type="date"
            :min="today"
            v-model="addForm.followUpDate"
          >
        </label>

        <label class="full">
          Comment
          <textarea
            rows="4"
            v-model="addForm.comment"
            placeholder="Reason or instructions for follow-up..."
          ></textarea>
        </label>
      </div>

      <div class="modal-actions">
        <button @click="saveAdd">
          Save Follow-up
        </button>

        <button
          class="secondary"
          @click="showAdd = false"
        >
          Cancel
        </button>
      </div>
    </div>
  </div>

  <div
    v-if="showComplete"
    class="modal-bg"
  >
    <div class="modal">
      <button class="modal-close-x" @click="showComplete = false">×</button>
      <h2>Complete Follow-up</h2>

      <div class="card">
        <b>
          {{ selected.patientName }}
        </b>

        <div class="muted">
          {{ selected.patientCode }}
          ·
          {{ selected.phone }}
        </div>
      </div>

      <label
        style="
          display:block;
          margin-top:14px;
        "
      >
        Follow-up Comment *

        <textarea
          rows="5"
          v-model="form.comment"
          placeholder="What was discussed, patient condition, instructions, response..."
        ></textarea>
      </label>

      <label
        class="toggle-row"
        style="margin-top:14px"
      >
        <input
          type="checkbox"
          v-model="
            form.followUpNeeded
          "
        >

        Next follow-up needed
      </label>

      <label
        v-if="form.followUpNeeded"
        style="
          display:block;
          margin-top:12px;
        "
      >
        Next Follow-up Date *

        <input
          type="date"
          :min="today"
          v-model="
            form.nextFollowUpDate
          "
        >
      </label>

      <div class="modal-actions">
        <button @click="saveComplete">
          Save Follow-up
        </button>

        <button
          class="secondary"
          @click="
            showComplete = false
          "
        >
          Cancel
        </button>
      </div>
    </div>
  </div>
</template>
