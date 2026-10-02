<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../../api';
import { can } from '../../auth';
import Pagination from '../../components/Pagination.vue';
import GridSearch from '../../components/GridSearch.vue';
import { useGrid } from '../../composables/useGrid';

const rows = ref([]);
const billTypes = ref([]);
const { search, page, pageCount, pagedRows, sortedRows, sortBy, sortIndicator } = useGrid(rows, 10);
const show = ref(false);
const errors = ref([]);
const form = ref(blank());

function blank() {
  return { id: null, name: '', price: 0, billType: '', isActive: true };
}

async function load() {
  rows.value = (await api.get('/service-charges')).data;
}

async function loadBillTypes() {
  billTypes.value = (await api.get('/bill-types', { params: { billableOnly: true } })).data;
}

function add() {
  form.value = blank();
  errors.value = [];
  show.value = true;
}

function edit(row) {
  form.value = { ...row };
  errors.value = [];
  show.value = true;
}

async function save() {
  errors.value = [];
  if (!form.value.name.trim()) errors.value.push('Charge name is required.');
  if (form.value.price < 0) errors.value.push('Price cannot be negative.');
  if (!form.value.billType) errors.value.push('Bill type is required.');
  if (errors.value.length) return;

  try {
    if (form.value.id) await api.put('/service-charges/' + form.value.id, form.value);
    else await api.post('/service-charges', form.value);
    show.value = false;
    await load();
  } catch (e) {
    errors.value = [e.response?.data?.message || 'Unable to save charge.'];
  }
}

async function remove(row) {
  if (!confirm(`Deactivate ${row.name}?`)) return;
  await api.delete('/service-charges/' + row.id);
  await load();
}

onMounted(() => {
  load();
  loadBillTypes();
});
</script>

<template>
  <h1>Hospital Expense Charge Master</h1>
  <div class="muted">Dressing Charge, Injection Charge, Saline Charge, Emergency Charge and any other flat-rate hospital charge. Tag each with a Bill Type so it shows up in that bill type's Description dropdown when creating a bill.</div>
  <div class="toolbar">
    <button v-if="can('SERVICE_CHARGE', 'add')" @click="add">+ Add Charge</button>
  </div>
  <div class="grid-filter-row">
    <GridSearch v-model="search" placeholder="Search charge name..." />
  </div>
  <table class="table">
    <tr>
      <th class="sortable" @click="sortBy('name')">Charge Name {{ sortIndicator('name') }}</th>
      <th class="sortable" @click="sortBy('price')">Price {{ sortIndicator('price') }}</th>
      <th class="sortable" @click="sortBy('billType')">Bill Type {{ sortIndicator('billType') }}</th>
      <th class="sortable" @click="sortBy('isActive')">Status {{ sortIndicator('isActive') }}</th>
      <th></th>
    </tr>
    <tr v-for="row in pagedRows" :key="row.id">
      <td>{{ row.name }}</td>
      <td>₹{{ Number(row.price).toFixed(2) }}</td>
      <td>{{ row.billType || '-' }}</td>
      <td>{{ row.isActive ? 'Active' : 'Inactive' }}</td>
      <td class="actions">
        <button v-if="can('SERVICE_CHARGE', 'edit')" @click="edit(row)">Edit</button>
        <button v-if="can('SERVICE_CHARGE', 'delete')" class="danger-btn" @click="remove(row)">Deactivate</button>
      </td>
    </tr>
  </table>
  <Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="10" @update:page="page = $event" />
  <div v-if="show" class="modal-bg">
    <div class="modal">
      <button class="modal-close-x" @click="show = false">×</button>
      <h2>{{ form.id ? 'Edit' : 'Add' }} Charge</h2>
      <div v-if="errors.length" class="error-box">
        <div v-for="e in errors" :key="e">{{ e }}</div>
      </div>
      <div class="form-grid">
        <label>Charge Name *<input v-model="form.name" placeholder="e.g. Dressing Charge"></label>
        <label>Price ₹<input type="number" min="0" step="0.01" v-model.number="form.price"></label>
        <label>
          Bill Type *
          <select v-model="form.billType">
            <option value="">Select bill type</option>
            <option v-for="bt in billTypes" :key="bt.id" :value="bt.name">{{ bt.name }}</option>
          </select>
        </label>
        <label class="toggle-row"><input type="checkbox" v-model="form.isActive"> Active</label>
      </div>
      <div class="modal-actions">
        <button @click="save">Save</button>
        <button class="secondary" @click="show = false">Cancel</button>
      </div>
    </div>
  </div>
</template>
