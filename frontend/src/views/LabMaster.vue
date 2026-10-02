<script setup>
import { computed, onMounted, ref } from 'vue';
import { api } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import RichTextEditor from '../components/RichTextEditor.vue';
import { useGrid } from '../composables/useGrid';
import { noteToEditableHtml } from '../utils/richNote';

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
    group: '',
    columns,
    rows: [{ id: uid('r'), values: {} }],
    staticTable: { columns: [], rows: [] }
  };
}

function parseStaticTable(test) {
  if (test.staticTableJson) {
    try {
      const t = JSON.parse(test.staticTableJson);
      if (Array.isArray(t.columns) && Array.isArray(t.rows)) return t;
    } catch {}
  }
  return { columns: [], rows: [] };
}

const { search, page, pageCount, pagedRows: pagedTests, sortedRows, sortBy, sortIndicator } = useGrid(tests, pageSize);

const groupOptions = computed(() => {
  const names = new Set(tests.value.map(x => (x.group || '').trim()).filter(Boolean));
  return [...names].sort();
});

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
  const staticTable = parseStaticTable(test);
  master.value = {
    id: test.id,
    name: test.name,
    price: Number(test.price || 0),
    note: noteToEditableHtml(test.note || ''),
    group: test.group || '',
    columns: schema.columns.map(x => ({ id: x.id || uid('c'), name: x.name || '' })),
    rows: schema.rows.map(x => x.isGroupHeader
      ? { id: x.id || uid('g'), isGroupHeader: true, groupName: x.groupName || '' }
      : { id: x.id || uid('r'), values: { ...(x.values || {}) } }),
    staticTable: {
      columns: staticTable.columns.map(x => ({ id: x.id || uid('sc'), name: x.name || '' })),
      rows: staticTable.rows.map(x => ({ id: x.id || uid('sr'), values: { ...(x.values || {}) } }))
    }
  };
  errors.value = [];
  showModal.value = true;
}

// A Group header row (its own name can be left blank) breaks the test's
// parameters into labelled sections when there are several distinct panels
// worth of rows within one test - it isn't itself a parameter row, so it
// carries a groupName instead of per-column values.
function addGroupHeader() {
  master.value.rows.push({ id: uid('g'), isGroupHeader: true, groupName: '' });
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

// A separate, independent small reference table (its own columns/rows) that
// prints below the Note on every report for this test - always the same for
// every patient (not tied to entered results), so it's entirely optional
// and starts out empty rather than pre-populated like the main dynamic table.
function addStaticColumn() {
  const id = uid('sc');
  master.value.staticTable.columns.push({ id, name: `Column ${master.value.staticTable.columns.length + 1}` });
  master.value.staticTable.rows.forEach(row => row.values[id] = '');
  if (!master.value.staticTable.rows.length) addStaticRow();
}

function deleteStaticColumn(index) {
  const column = master.value.staticTable.columns[index];
  if (!confirm(`Delete column "${column.name || 'Unnamed'}"?`)) return;
  master.value.staticTable.columns.splice(index, 1);
  master.value.staticTable.rows.forEach(row => delete row.values[column.id]);
}

function addStaticRow() {
  master.value.staticTable.rows.push({ id: uid('sr'), values: {} });
}

function deleteStaticRow(index) {
  master.value.staticTable.rows.splice(index, 1);
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
  return master.value.rows.filter(row => !row.isGroupHeader).map(row => ({
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
  if (master.value.staticTable.columns.some(x => !String(x.name || '').trim())) list.push('Every static table column must have a name.');
  errors.value = list;
  if (list.length) return;

  const schema = {
    columns: master.value.columns.map(x => ({ id: x.id, name: String(x.name).trim() })),
    rows: master.value.rows.map(x => x.isGroupHeader
      ? { id: x.id, isGroupHeader: true, groupName: String(x.groupName || '').trim() }
      : { id: x.id, values: { ...x.values } })
  };

  const staticTable = {
    columns: master.value.staticTable.columns.map(x => ({ id: x.id, name: String(x.name).trim() })),
    rows: master.value.staticTable.rows.map(x => ({ id: x.id, values: { ...x.values } }))
  };

  const payload = {
    name: master.value.name.trim(),
    price: Number(master.value.price || 0),
    note: master.value.note || '',
    group: master.value.group || '',
    schemaJson: JSON.stringify(schema),
    staticTableJson: JSON.stringify(staticTable),
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
    <button v-if="can('LAB_MASTER','add')" @click="newMaster">+ Add Lab Test Master</button>
  </div>
  <div class="grid-filter-row"><GridSearch v-model="search" placeholder="Search all lab master fields..." /></div>

  <table class="table">
    <tr><th class="sortable" @click="sortBy('name')">Test <span class="sort-indicator">{{sortIndicator('name')}}</span></th><th class="sortable" @click="sortBy('group')">Group <span class="sort-indicator">{{sortIndicator('group')}}</span></th><th class="sortable" @click="sortBy('price')">Price <span class="sort-indicator">{{sortIndicator('price')}}</span></th><th>Dynamic Table</th><th class="sortable" @click="sortBy('note')">Note <span class="sort-indicator">{{sortIndicator('note')}}</span></th><th></th></tr>
    <tr v-for="test in pagedTests" :key="test.id">
      <td>{{test.name}}</td>
      <td>{{test.group || '-'}}</td>
      <td>₹{{Number(test.price).toFixed(2)}}</td>
      <td>{{summary(test)}}</td>
      <td>{{test.note || '-'}}</td>
      <td class="actions">
        <button v-if="can('LAB_MASTER','edit')" @click="editMaster(test)">Edit</button>
        <button v-if="can('LAB_MASTER','delete')" class="danger-btn" @click="remove(test)">Delete</button>
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
        <label>
          Group
          <input v-model="master.group" list="lab-test-groups" placeholder="e.g. Fever Panel, Sugar Panel">
          <datalist id="lab-test-groups">
            <option v-for="g in groupOptions" :key="g" :value="g" />
          </datalist>
        </label>
      </div>
      <div class="muted" style="margin-top:-6px;margin-bottom:14px">Tests sharing the same Group print merged onto one page together; leave blank to always print this test on its own page.</div>

      <div class="toolbar lab-master-toolbar">
        <button @click="addColumn">+ Add Column</button>
        <button @click="addRow">+ Add Row</button>
        <button class="secondary" @click="addGroupHeader">+ Add Group</button>
      </div>
      <div class="muted" style="margin-top:-6px;margin-bottom:10px">A Group header (name can be left blank) splits this test's rows into labelled sections - add one, then keep using Add Row underneath it for that section's parameters, and add another Group for the next section.</div>

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
            <tr v-for="(row,rowIndex) in master.rows" :key="row.id" :class="{'lab-master-group-row':row.isGroupHeader}">
              <td>{{rowIndex+1}}</td>
              <template v-if="row.isGroupHeader">
                <td :colspan="master.columns.length">
                  <input v-model="row.groupName" placeholder="Group name (optional) - e.g. Fever Panel">
                </td>
              </template>
              <template v-else>
                <td v-for="column in master.columns" :key="column.id">
                  <textarea rows="2" class="lab-master-cell" v-model="row.values[column.id]" :placeholder="column.name"></textarea>
                </td>
              </template>
              <td><button class="danger-btn" @click="deleteRow(rowIndex)">Delete Row</button></td>
            </tr>
          </tbody>
        </table>
      </div>

      <div style="margin-top:18px">
        <!-- A <label> wrapping a focusable descendant makes the browser
             redirect any click inside it to the first "labelable" control
             it contains - a plain contenteditable div doesn't count as one,
             but the toolbar's buttons do, so a <label> here would silently
             steal every click meant for the editor onto the Bold button
             instead. Keep this a plain heading, not a wrapping <label>. -->
        <label style="display:block;margin-bottom:4px">Note</label>
        <RichTextEditor v-model="master.note" />
      </div>

      <div style="margin-top:18px">
        <label style="display:block;margin-bottom:4px">Additional Static Table (optional)</label>
        <div class="muted" style="margin-bottom:8px">Prints below the Note on every report for this test, exactly as entered here - the same for every patient, not tied to results.</div>
        <div class="toolbar lab-master-toolbar">
          <button type="button" @click="addStaticColumn">+ Add Column</button>
          <button type="button" :disabled="!master.staticTable.columns.length" @click="addStaticRow">+ Add Row</button>
        </div>
        <div v-if="master.staticTable.columns.length" class="lab-master-grid-wrap">
          <table class="table lab-master-grid">
            <thead>
              <tr>
                <th v-for="(column,index) in master.staticTable.columns" :key="column.id">
                  <div class="lab-column-editor">
                    <input v-model="column.name" placeholder="Column name">
                    <button type="button" class="danger-btn compact-button" @click="deleteStaticColumn(index)">×</button>
                  </div>
                </th>
                <th>Delete</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row,rowIndex) in master.staticTable.rows" :key="row.id">
                <td v-for="column in master.staticTable.columns" :key="column.id">
                  <textarea rows="2" class="lab-master-cell" v-model="row.values[column.id]" :placeholder="column.name"></textarea>
                </td>
                <td><button type="button" class="danger-btn" @click="deleteStaticRow(rowIndex)">Delete Row</button></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="modal-actions">
        <button @click="save">Save Test Master</button>
        <button class="secondary" @click="showModal=false">Cancel</button>
      </div>
    </div>
  </div>
</template>
