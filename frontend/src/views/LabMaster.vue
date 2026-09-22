<script setup>
import { computed, onMounted, ref } from 'vue';
import { api } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';

const tests = ref([]);
const pageSize = 10;
const showModal = ref(false);
const errors = ref([]);
const master = ref(emptyMaster());

function uid(prefix) {
  return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2,8)}`;
}

function emptyMaster() {
  const columns = [
    { id: uid('c'), name: 'Parameter' },
    { id: uid('c'), name: 'Range' },
    { id: uid('c'), name: 'Unit' },
    { id: uid('c'), name: 'Default Value' }
  ];
  return {
    id: null,
    name: '',
    price: 0,
    note: '',
    columns,
    rows: [{ id: uid('r'), values: {} }]
  };
}

const { search, page, pageCount, pagedRows: pagedTests, sortedRows, sortBy, sortIndicator } = useGrid(tests, pageSize);

async function load() {
  tests.value = (await api.get('/lab/tests')).data;
}

function newMaster() {
  master.value = emptyMaster();
  errors.value = [];
  showModal.value = true;
}

function parseSchema(test) {
  if (test.schemaJson) {
    try {
      const schema = JSON.parse(test.schemaJson);
      if (Array.isArray(schema.columns) && Array.isArray(schema.rows)) return schema;
    } catch {}
  }

  const columns = [
    { id: uid('c'), name: 'Parameter' },
    { id: uid('c'), name: 'Range' },
    { id: uid('c'), name: 'Unit' },
    { id: uid('c'), name: 'Default Value' }
  ];

  const rows = (test.components || []).map(x => ({
    id: uid('r'),
    values: {
      [columns[0].id]: x.name || '',
      [columns[1].id]: x.rangeText || '',
      [columns[2].id]: x.unit || '',
      [columns[3].id]: x.defaultValue || ''
    }
  }));

  return { columns, rows: rows.length ? rows : [{ id: uid('r'), values: {} }] };
}

function editMaster(test) {
  const schema = parseSchema(test);
  master.value = {
    id: test.id,
    name: test.name,
    price: Number(test.price || 0),
    note: test.note || '',
    columns: schema.columns.map(x => ({ id: x.id || uid('c'), name: x.name || '' })),
    rows: schema.rows.map(x => ({ id: x.id || uid('r'), values: { ...(x.values || {}) } }))
  };
  errors.value = [];
  showModal.value = true;
}

function addColumn() {
  const id = uid('c');
  master.value.columns.push({ id, name: `Column ${master.value.columns.length + 1}` });
  master.value.rows.forEach(row => row.values[id] = '');
}

function deleteColumn(index) {
  if (master.value.columns.length <= 1) return alert('At least one column is required.');
  const column = master.value.columns[index];
  if (!confirm(`Delete column "${column.name || 'Unnamed'}"?`)) return;
  master.value.columns.splice(index, 1);
  master.value.rows.forEach(row => delete row.values[column.id]);
}

function addRow() {
  master.value.rows.push({ id: uid('r'), values: {} });
}

function deleteRow(index) {
  if (master.value.rows.length <= 1) return alert('At least one row is required.');
  master.value.rows.splice(index, 1);
}

function valueByNames(row, names) {
  for (const column of master.value.columns) {
    const n = (column.name || '').trim().toLowerCase();
    if (names.some(x => n === x || n.includes(x))) return row.values[column.id] || '';
  }
  return '';
}

function buildComponents() {
  const first = master.value.columns[0];
  if (!first) return [];
  return master.value.rows.map(row => ({
    name: String(row.values[first.id] || '').trim(),
    rangeText: valueByNames(row, ['range','reference']),
    unit: valueByNames(row, ['unit']),
    defaultValue: valueByNames(row, ['default value','default','prefilled'])
  })).filter(x => x.name);
}

async function save() {
  const list = [];
  if (!master.value.name.trim()) list.push('Test name is required.');
  if (master.value.columns.some(x => !String(x.name || '').trim())) list.push('Every column must have a name.');
  if (!master.value.rows.length) list.push('At least one row is required.');
  errors.value = list;
  if (list.length) return;

  const schema = {
    columns: master.value.columns.map(x => ({ id: x.id, name: String(x.name).trim() })),
    rows: master.value.rows.map(x => ({ id: x.id, values: { ...x.values } }))
  };

  const payload = {
    name: master.value.name.trim(),
    price: Number(master.value.price || 0),
    note: master.value.note || '',
    schemaJson: JSON.stringify(schema),
    components: buildComponents()
  };

  if (master.value.id) await api.put('/lab/tests/' + master.value.id, payload);
  else await api.post('/lab/tests', payload);

  showModal.value = false;
  await load();
}

async function remove(test) {
  if (!confirm(`Delete lab test "${test.name}"?`)) return;
  await api.delete('/lab/tests/' + test.id);
  await load();
}

function summary(test) {
  try {
    const s = JSON.parse(test.schemaJson || '{}');
    if (Array.isArray(s.rows) && Array.isArray(s.columns)) return `${s.rows.length} row(s) × ${s.columns.length} column(s)`;
  } catch {}
  return `${test.components?.length || 0} row(s)`;
}

onMounted(load);
</script>

<template>
  <h1>Lab Test Master</h1>
  <div class="toolbar">
    <button v-if="can('LAB','add')" @click="newMaster">+ Add Lab Test Master</button>
  </div>
  <div class="grid-filter-row"><GridSearch v-model="search" placeholder="Search all lab master fields..." /></div>

  <table class="table">
    <tr><th class="sortable" @click="sortBy('name')">Test <span class="sort-indicator">{{sortIndicator('name')}}</span></th><th class="sortable" @click="sortBy('price')">Price <span class="sort-indicator">{{sortIndicator('price')}}</span></th><th>Dynamic Table</th><th class="sortable" @click="sortBy('note')">Note <span class="sort-indicator">{{sortIndicator('note')}}</span></th><th></th></tr>
    <tr v-for="test in pagedTests" :key="test.id">
      <td>{{test.name}}</td>
      <td>₹{{Number(test.price).toFixed(2)}}</td>
      <td>{{summary(test)}}</td>
      <td>{{test.note || '-'}}</td>
      <td class="actions">
        <button v-if="can('LAB','edit')" @click="editMaster(test)">Edit</button>
        <button v-if="can('LAB','delete')" class="danger-btn" @click="remove(test)">Delete</button>
      </td>
    </tr>
  </table>

  <Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="pageSize" @update:page="page=$event" />

  <div v-if="showModal" class="modal-bg">
    <div class="modal modal-xl">
      <button class="modal-close-x" @click="showModal=false">×</button>
      <div class="page-head">
        <div>
          <h2>{{master.id ? 'Edit' : 'Add'}} Lab Test Master</h2>
          <div class="muted">Column name, cell values and rows are fully dynamic.</div>
        </div>
        <button class="secondary" @click="showModal=false">Close</button>
      </div>

      <div v-if="errors.length" class="error-box"><div v-for="e in errors" :key="e">{{e}}</div></div>
      <div class="form-grid">
        <label>Test Name *<input v-model="master.name"></label>
        <label>Price *<input type="number" min="0" step="0.01" v-model.number="master.price"></label>
      </div>

      <div class="toolbar lab-master-toolbar">
        <button @click="addColumn">+ Add Column</button>
        <button @click="addRow">+ Add Row</button>
      </div>

      <div class="lab-master-grid-wrap">
        <table class="table lab-master-grid">
          <thead>
            <tr>
              <th>#</th>
              <th v-for="(column,index) in master.columns" :key="column.id">
                <div class="lab-column-editor">
                  <input v-model="column.name" placeholder="Column name">
                  <button class="danger-btn compact-button" @click="deleteColumn(index)">×</button>
                </div>
              </th>
              <th>Delete</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row,rowIndex) in master.rows" :key="row.id">
              <td>{{rowIndex+1}}</td>
              <td v-for="column in master.columns" :key="column.id">
                <input v-model="row.values[column.id]" :placeholder="column.name">
              </td>
              <td><button class="danger-btn" @click="deleteRow(rowIndex)">Delete Row</button></td>
            </tr>
          </tbody>
        </table>
      </div>

      <label style="display:block;margin-top:18px">Note<textarea rows="4" v-model="master.note"></textarea></label>
      <div class="modal-actions">
        <button @click="save">Save Test Master</button>
        <button class="secondary" @click="showModal=false">Cancel</button>
      </div>
    </div>
  </div>
</template>
