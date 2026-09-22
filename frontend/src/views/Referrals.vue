<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';

const rows = ref([]);
const doctors = ref([]);
const summary = ref({ rows: [] });
const show = ref(false);
const form = ref(blank());

const {
  search,
  page,
  pageCount,
  pagedRows,
  sortedRows,
  sortBy,
  sortIndicator
} = useGrid(rows, 10);

function blank() {
  return {
    id: null,
    name: '',
    phone: '',
    address: '',
    referrerType: 'Person',
    doctorId: null,
    isActive: true
  };
}

async function load() {
  const [referrersRes, doctorsRes, summaryRes] =
    await Promise.all([
      api.get('/referrals'),
      api.get('/doctors'),
      api.get('/referrals/summary', {
        params: {
          from: '2000-01-01',
          to: '2100-01-01',
          department: 'All'
        }
      })
    ]);

  rows.value = referrersRes.data;
  doctors.value = doctorsRes.data;
  summary.value = summaryRes.data;
}

function referredCount(id) {
  return (
    summary.value.rows?.find(
      x => x.referrerId === id
    )?.referredCount || 0
  );
}

function add() {
  form.value = blank();
  show.value = true;
}

function edit(row) {
  form.value = { ...row };
  show.value = true;
}

async function save() {
  if (!form.value.name.trim()) {
    alert('Referrer name is required.');
    return;
  }

  if (form.value.id) {
    await api.put(
      '/referrals/' + form.value.id,
      form.value
    );
  } else {
    await api.post(
      '/referrals',
      form.value
    );
  }

  show.value = false;
  await load();
}

async function remove(row) {
  if (!confirm(`Delete ${row.name}?`)) {
    return;
  }

  await api.delete(
    '/referrals/' + row.id
  );

  await load();
}

onMounted(load);
</script>

<template>
  <h1>Referral</h1>

  <div class="toolbar">
    <button
      v-if="can('REFERRAL', 'add')"
      @click="add"
    >
      + Add Referrer
    </button>
  </div>

  <div class="grid-filter-row">
    <GridSearch
      v-model="search"
      placeholder="Search referrer name, code, phone, address..."
    />
  </div>

  <table class="table">
    <tr>
      <th
        class="sortable"
        @click="sortBy('referrerCode')"
      >
        Referrer ID
        {{ sortIndicator('referrerCode') }}
      </th>

      <th
        class="sortable"
        @click="sortBy('name')"
      >
        Name
        {{ sortIndicator('name') }}
      </th>

      <th
        class="sortable"
        @click="sortBy('referrerType')"
      >
        Type
        {{ sortIndicator('referrerType') }}
      </th>

      <th>Phone</th>
      <th>Address</th>

      <th
        class="sortable"
        @click="sortBy('referredCount')"
      >
        Referred
      </th>

      <th></th>
    </tr>

    <tr
      v-for="row in pagedRows"
      :key="row.id"
    >
      <td>{{ row.referrerCode }}</td>
      <td>{{ row.name }}</td>
      <td>{{ row.referrerType }}</td>
      <td>{{ row.phone || '-' }}</td>
      <td>{{ row.address || '-' }}</td>
      <td>{{ referredCount(row.id) }}</td>

      <td class="actions">
        <button
          v-if="can('REFERRAL', 'edit')"
          @click="edit(row)"
        >
          Edit
        </button>

        <button
          v-if="can('REFERRAL', 'delete')"
          class="danger-btn"
          @click="remove(row)"
        >
          Delete
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
    v-if="show"
    class="modal-bg"
  >
    <div class="modal">
      <button class="modal-close-x" @click="show = false">×</button>
      <h2>
        {{ form.id ? 'Edit' : 'Add' }}
        Referrer
      </h2>

      <div class="form-grid">
        <label>
          Referrer Name *
          <input v-model="form.name">
        </label>

        <label>
          Referrer Type
          <select v-model="form.referrerType">
            <option>Person</option>
            <option>Doctor</option>
          </select>
        </label>

        <label>
          Phone
          <input v-model="form.phone">
        </label>

        <label>
          Linked Doctor
          <select
            v-model.number="form.doctorId"
            :disabled="form.referrerType !== 'Doctor'"
          >
            <option :value="null">
              None
            </option>

            <option
              v-for="doctor in doctors"
              :key="doctor.id"
              :value="doctor.id"
            >
              {{ doctor.name }}
            </option>
          </select>
        </label>

        <label class="full">
          Address
          <textarea
            rows="3"
            v-model="form.address"
          ></textarea>
        </label>

        <label class="toggle-row">
          <input
            type="checkbox"
            v-model="form.isActive"
          >
          Active
        </label>
      </div>

      <div class="modal-actions">
        <button @click="save">
          Save
        </button>

        <button
          class="secondary"
          @click="show = false"
        >
          Cancel
        </button>
      </div>
    </div>
  </div>
</template>
