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
const noteArea = ref(null);

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
    rows: [{ id: uid('r'), values: {} }]
  };
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
  master.value = {
    id: test.id,
    name: test.name,
    price: Number(test.price || 0),
    note: test.note || '',
    group: test.group || '',
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

// Toggles a marker (** for bold, * for italic) around the selected text in
// the Note field - the same manual convention the lab print already
// understands for test names, results and notes, so these buttons are just a
// shortcut for typing the marker yourself.
function wrapNoteText(marker) {
  const el = noteArea.value;
  if (!el) return;
  const start = el.selectionStart, end = el.selectionEnd;
  if (start === end) { alert('Select the text you want to format first.'); return; }
  const value = master.value.note || '';
  const selected = value.slice(start, end);
  const alreadyWrapped = selected.startsWith(marker) && selected.endsWith(marker) && selected.length >= marker.length * 2;
  const replacement = alreadyWrapped ? selected.slice(marker.length, -marker.length) : `${marker}${selected}${marker}`;
  master.value.note = value.slice(0, start) + replacement + value.slice(end);
  requestAnimationFrame(() => {
    el.focus();
    el.setSelectionRange(start, start + replacement.length);
  });
}

// Prefixes every selected line with "- " so it prints as a bullet list item;
// toggles it back off if the selected lines already start that way.
function toggleNoteBullets() {
  const el = noteArea.value;
  if (!el) return;
  const start = el.selectionStart, end = el.selectionEnd;
  if (start === end) { alert('Select the lines you want as bullets first.'); return; }
  const value = master.value.note || '';
  let lineStart = value.lastIndexOf('\n', start - 1) + 1;
  let lineEnd = value.indexOf('\n', end);
  if (lineEnd === -1) lineEnd = value.length;
  const block = value.slice(lineStart, lineEnd);
  const lines = block.split('\n');
  const alreadyBulleted = lines.every(line => line.trim() === '' || /^[-*]\s/.test(line));
  const newLines = alreadyBulleted
    ? lines.map(line => line.replace(/^[-*]\s/, ''))
    : lines.map(line => line.trim() === '' ? line : `- ${line}`);
  const replacement = newLines.join('\n');
  master.value.note = value.slice(0, lineStart) + replacement + value.slice(lineEnd);
  requestAnimationFrame(() => {
    el.focus();
    el.setSelectionRange(lineStart, lineStart + replacement.length);
  });
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
    group: master.value.group || '',
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
                <textarea rows="2" class="lab-master-cell" v-model="row.values[column.id]" :placeholder="column.name"></textarea>
              </td>
              <td><button class="danger-btn" @click="deleteRow(rowIndex)">Delete Row</button></td>
            </tr>
          </tbody>
        </table>
      </div>

      <label style="display:block;margin-top:18px">
        Note
        <button type="button" class="secondary compact-button" style="margin-left:8px" @click="wrapNoteText('**')" title="Wrap the selected text in ** so it prints bold"><b>B</b></button>
        <button type="button" class="secondary compact-button" @click="wrapNoteText('*')" title="Wrap the selected text in * so it prints italic"><i>I</i></button>
        <button type="button" class="secondary compact-button" @click="toggleNoteBullets" title="Prefix the selected lines with - so they print as bullet points">&bull; List</button>
        <span class="muted" style="margin-left:8px">Select text and click B/I/List, or type **bold**, *italic* or "- " bullet lines yourself.</span>
        <textarea ref="noteArea" rows="4" v-model="master.note"></textarea>
      </label>
      <div class="modal-actions">
        <button @click="save">Save Test Master</button>
        <button class="secondary" @click="showModal=false">Cancel</button>
      </div>
    </div>
  </div>
</template>
