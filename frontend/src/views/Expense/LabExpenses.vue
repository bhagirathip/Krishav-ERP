<script setup>
import { onMounted, ref } from 'vue';
import { api } from '../../api';
import GridSearch from '../../components/GridSearch.vue';
import Pagination from '../../components/Pagination.vue';
import { useServerGrid } from '../../composables/useServerGrid';

const today = new Date().toISOString().slice(0, 10);
const pageSize = 10;
const staff = ref([]);
const show = ref(false);
const form = ref(blank());
const totalAmount = ref(0);

// The endpoint already accepted from/to, but this screen never sent them -
// it always got the full unfiltered purchase history. allDates defaults to
// true to keep that same "everything" view by default; unchecking it lets
// the user narrow to a date range like Bills.vue does.
const allDates = ref(true);
const dateFrom = ref(today);
const dateTo = ref(today);

// No searchColumn - a plain search box matches vendor, invoice number and
// description together server-side (see ExpensesController.Lab's default
// branch), rather than making the user pick a column first. paidByStaffName
// is resolved via a post-fetch dictionary lookup (same as Patients'/Bills'
// own joined display fields), not a real LabPurchaseExpense column, so it
// isn't sortable/searchable here.
async function fetchLabExpensesPage(params) {
  const dateParams = allDates.value ? {} : { from: dateFrom.value, to: dateTo.value };
  const { data } = await api.get('/expenses/lab', {
    params: {
      ...dateParams,
      page: params.page,
      pageSize: params.pageSize,
      searchText: params.searchText,
      sortBy: params.sortBy,
      sortDirection: params.sortDirection
    }
  });
  totalAmount.value = data.totalAmount;
  return { rows: data.rows, totalCount: data.totalCount };
}

const { page, pageCount, rows: pagedRows, totalCount, sortBy, sortIndicator, searchText, reload } = useServerGrid(fetchLabExpensesPage, pageSize);

function blank() {
  return {
    id: null,
    expenseDate: today,
    vendorName: '',
    invoiceNumber: '',
    description: '',
    amount: 0,
    paidAmount: 0,
    paymentDate: today,
    paymentMode: 'Cash',
    referenceNumber: '',
    notes: '',
    paidByStaffId: null
  };
}

async function load() {
  const [s] = await Promise.all([api.get('/staff'), reload()]);
  staff.value = s.data;
}

function add() {
  form.value = blank();
  show.value = true;
}

function edit(row) {
  form.value = {
    ...row,
    expenseDate: String(row.expenseDate).slice(0, 10),
    paymentDate: row.paymentDate ? String(row.paymentDate).slice(0, 10) : null
  };
  show.value = true;
}

async function save() {
  if (form.value.id) await api.put(`/expenses/lab/${form.value.id}`, form.value);
  else await api.post('/expenses/lab', form.value);
  show.value = false;
  await load();
}

async function remove(row) {
  if (confirm('Delete this lab purchase?')) {
    await api.delete(`/expenses/lab/${row.id}`);
    await load();
  }
}

onMounted(load);
</script>
<template>
  <h1>Lab Expenses</h1>
  <div class="toolbar">
    <button @click="add">+ Add Lab Purchase</button>
  </div>

  <div class="toolbar report-filter">
    <label class="toggle-row"><input type="checkbox" v-model="allDates" @change="load"> All Dates</label>
    <label v-if="!allDates">From<input type="date" v-model="dateFrom"></label>
    <label v-if="!allDates">To<input type="date" v-model="dateTo"></label>
    <button v-if="!allDates" @click="load">Apply</button>
  </div>

  <div class="grid-filter-row">
    <GridSearch v-model="searchText" placeholder="Search vendor, invoice, description..." />
  </div>

  <table class="table">
    <tr>
      <th class="sortable" @click="sortBy('expenseDate')">Date <span class="sort-indicator">{{ sortIndicator('expenseDate') }}</span></th>
      <th class="sortable" @click="sortBy('vendorName')">Vendor <span class="sort-indicator">{{ sortIndicator('vendorName') }}</span></th>
      <th class="sortable" @click="sortBy('invoiceNumber')">Invoice <span class="sort-indicator">{{ sortIndicator('invoiceNumber') }}</span></th>
      <th class="sortable" @click="sortBy('description')">Description <span class="sort-indicator">{{ sortIndicator('description') }}</span></th>
      <th class="sortable" @click="sortBy('amount')">Amount <span class="sort-indicator">{{ sortIndicator('amount') }}</span></th>
      <th class="sortable" @click="sortBy('paidAmount')">Paid <span class="sort-indicator">{{ sortIndicator('paidAmount') }}</span></th>
      <th>Paid By</th>
      <th class="sortable" @click="sortBy('outstanding')">Outstanding <span class="sort-indicator">{{ sortIndicator('outstanding') }}</span></th>
      <th></th>
    </tr>
    <tr v-for="row in pagedRows" :key="row.id">
      <td>{{ String(row.expenseDate).slice(0, 10) }}</td>
      <td>{{ row.vendorName || '-' }}</td>
      <td>{{ row.invoiceNumber || '-' }}</td>
      <td>{{ row.description }}</td>
      <td>₹{{ Number(row.amount).toFixed(2) }}</td>
      <td>₹{{ Number(row.paidAmount).toFixed(2) }}</td>
      <td>{{ row.paidByStaffName || '-' }}</td>
      <td>₹{{ Math.max(0, Number(row.amount)-Number(row.paidAmount)).toFixed(2) }}</td>
      <td class="actions">
        <button @click="edit(row)">Edit</button>
        <button class="danger-btn" @click="remove(row)">Delete</button>
      </td>
    </tr>
  </table>
  <div class="bill-total-box"><div class="grand"><span>Total Lab Purchases</span><b>₹{{ totalAmount.toFixed(2) }}</b></div></div>
  <Pagination :page="page" :page-count="pageCount" :total="totalCount" :page-size="pageSize" @update:page="page=$event" />

  <div v-if="show" class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="show=false">×</button>
      <h2>{{ form.id ? 'Edit' : 'Add' }} Lab Purchase</h2>
      <div class="form-grid">
        <label>Date<input type="date" v-model="form.expenseDate"></label>
        <label>Vendor<input v-model="form.vendorName"></label>
        <label>Invoice No<input v-model="form.invoiceNumber"></label>
        <label class="full">Item / Description *<input v-model="form.description"></label>
        <label>Amount *<input type="number" min="0" step="0.01" v-model.number="form.amount"></label>
        <label>Paid Amount<input type="number" min="0" :max="form.amount" step="0.01" v-model.number="form.paidAmount"></label>
        <label>Payment Date<input type="date" v-model="form.paymentDate"></label>
        <label>Mode
          <select v-model="form.paymentMode">
            <option>Cash</option><option>UPI</option><option>Bank Transfer</option><option>Cheque</option><option>Credit</option><option>Other</option>
          </select>
        </label>
        <label>Paid By Staff
          <select v-model.number="form.paidByStaffId">
            <option :value="null">Not specified</option>
            <option v-for="x in staff" :key="x.id" :value="x.id">{{ x.name }}</option>
          </select>
        </label>
        <label>Reference<input v-model="form.referenceNumber"></label>
        <label class="full">Notes<textarea rows="3" v-model="form.notes"></textarea></label>
      </div>
      <div class="modal-actions">
        <button @click="save">Save</button>
        <button class="secondary" @click="show=false">Cancel</button>
      </div>
    </div>
  </div>
</template>
