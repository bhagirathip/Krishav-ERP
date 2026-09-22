<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';

const rows = ref([]);
const show = ref(false);
const form = ref({
  id: null,
  name: '',
  isActive: true
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
  rows.value = (
    await api.get('/patient-sources', {
      params: { includeInactive: true }
    })
  ).data;
}

function add() {
  form.value = {
    id: null,
    name: '',
    isActive: true
  };
  show.value = true;
}

function edit(row) {
  form.value = { ...row };
  show.value = true;
}

async function save() {
  if (!form.value.name.trim()) {
    alert('Patient source name is required.');
    return;
  }

  if (form.value.id) {
    await api.put(
      `/patient-sources/${form.value.id}`,
      form.value
    );
  } else {
    await api.post(
      '/patient-sources',
      form.value
    );
  }

  show.value = false;
  await load();
}

async function remove(row) {
  if (!confirm(`Deactivate ${row.name}?`)) {
    return;
  }

  await api.delete(
    `/patient-sources/${row.id}`
  );

  await load();
}

onMounted(load);
</script>

<template>
  <h1>Patient Source Master</h1>

  <div class="toolbar">
    <button
      v-if="can('PATIENT_SOURCE', 'add')"
      @click="add"
    >
      + Add Patient Source
    </button>
  </div>

  <div class="grid-filter-row">
    <GridSearch
      v-model="search"
      placeholder="Search patient sources..."
    />
  </div>

  <table class="table">
    <tr>
      <th
        class="sortable"
        @click="sortBy('name')"
      >
        Source Name
        {{ sortIndicator('name') }}
      </th>

      <th
        class="sortable"
        @click="sortBy('isActive')"
      >
        Status
        {{ sortIndicator('isActive') }}
      </th>

      <th></th>
    </tr>

    <tr
      v-for="row in pagedRows"
      :key="row.id"
    >
      <td>{{ row.name }}</td>
      <td>
        <span class="pill">
          {{ row.isActive ? 'Active' : 'Inactive' }}
        </span>
      </td>

      <td class="actions">
        <button
          v-if="can('PATIENT_SOURCE', 'edit')"
          @click="edit(row)"
        >
          Edit
        </button>

        <button
          v-if="
            row.isActive &&
            can('PATIENT_SOURCE', 'delete')
          "
          class="danger-btn"
          @click="remove(row)"
        >
          Deactivate
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
        Patient Source
      </h2>

      <div class="form-grid">
        <label>
          Source Name *
          <input v-model="form.name">
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
