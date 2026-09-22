<script setup>
import { computed, onMounted, ref } from 'vue';
import { api } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';

const rows = ref([]);
const show = ref(false);
const pageSize = 10;
const form = ref(emptyForm());

function emptyForm() {
  return {
    id: null,
    name: '',
    phone: '',
    gstNumber: '',
    pan: '',
    drugsBazaarId: '',
    fssai: '',
    address: ''
  };
}

const { search, page, pageCount, pagedRows, sortedRows, sortBy, sortIndicator } = useGrid(rows, pageSize);

async function load() {
  rows.value = (await api.get('/pharmacy/distributors')).data;
}

function add() {
  form.value = emptyForm();
  show.value = true;
}

function edit(row) {
  form.value = { ...row };
  show.value = true;
}

async function save() {
  if (!form.value.name.trim()) {
    alert('Distributor name is required.');
    return;
  }

  if (form.value.id) {
    await api.put(`/pharmacy/distributors/${form.value.id}`, form.value);
  } else {
    await api.post('/pharmacy/distributors', form.value);
  }

  show.value = false;
  await load();
}

async function remove(row) {
  if (!confirm(`Delete distributor ${row.name}?`)) return;
  await api.delete(`/pharmacy/distributors/${row.id}`);
  await load();
}

onMounted(load);
</script>

<template>
  <h1>Distributor Master</h1>

  <div class="toolbar">
    <button v-if="can('PHARMACY', 'add')" @click="add">+ Add Distributor</button>
  </div>

  <div class="grid-filter-row"><GridSearch v-model="search" placeholder="Search all distributor fields..." /></div>

  <table class="table">
    <tr>
      <th class="sortable" @click="sortBy('name')">Name <span class="sort-indicator">{{sortIndicator('name')}}</span></th>
      <th class="sortable" @click="sortBy('phone')">Phone <span class="sort-indicator">{{sortIndicator('phone')}}</span></th>
      <th class="sortable" @click="sortBy('gstNumber')">GST <span class="sort-indicator">{{sortIndicator('gstNumber')}}</span></th>
      <th class="sortable" @click="sortBy('pan')">PAN <span class="sort-indicator">{{sortIndicator('pan')}}</span></th>
      <th class="sortable" @click="sortBy('drugsBazaarId')">DrugsBazaar ID <span class="sort-indicator">{{sortIndicator('drugsBazaarId')}}</span></th>
      <th class="sortable" @click="sortBy('fssai')">FSSAI <span class="sort-indicator">{{sortIndicator('fssai')}}</span></th>
      <th class="sortable" @click="sortBy('address')">Address <span class="sort-indicator">{{sortIndicator('address')}}</span></th>
      <th></th>
    </tr>

    <tr v-for="row in pagedRows" :key="row.id">
      <td>{{ row.name }}</td>
      <td>{{ row.phone || '-' }}</td>
      <td>{{ row.gstNumber || '-' }}</td>
      <td>{{ row.pan || '-' }}</td>
      <td>{{ row.drugsBazaarId || '-' }}</td>
      <td>{{ row.fssai || '-' }}</td>
      <td>{{ row.address || '-' }}</td>
      <td class="actions">
        <button v-if="can('PHARMACY', 'edit')" @click="edit(row)">Edit</button>
        <button v-if="can('PHARMACY', 'delete')" class="danger-btn" @click="remove(row)">Delete</button>
      </td>
    </tr>
  </table>

  <Pagination
    :page="page"
    :page-count="pageCount"
    :total="sortedRows.length"
    :page-size="pageSize"
    @update:page="page = $event"
  />

  <div v-if="show" class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="show = false">×</button>
      <h2>{{ form.id ? 'Edit' : 'Add' }} Distributor</h2>

      <div class="form-grid">
        <label>Distributor Name *<input v-model="form.name"></label>
        <label>Phone<input v-model="form.phone"></label>
        <label>GST Number<input v-model="form.gstNumber"></label>
        <label>PAN<input v-model="form.pan"></label>
        <label>DrugsBazaar ID<input v-model="form.drugsBazaarId"></label>
        <label>FSSAI<input v-model="form.fssai"></label>
        <label class="full">Address<textarea rows="4" v-model="form.address"></textarea></label>
      </div>

      <div class="modal-actions">
        <button @click="save">Save</button>
        <button class="secondary" @click="show = false">Cancel</button>
      </div>
    </div>
  </div>
</template>
