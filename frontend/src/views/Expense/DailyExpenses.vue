<script setup>
import { onMounted, ref } from 'vue';
import { api } from '../../api';
import { can, role } from '../../auth';
import GridSearch from '../../components/GridSearch.vue';
import Pagination from '../../components/Pagination.vue';
import { useServerGrid } from '../../composables/useServerGrid';

// Only an Administrator can settle expenses (see
// ExpensesController.SettleOneDaily) - hidden entirely for everyone else
// rather than shown and rejected. Even for an Administrator, the button
// still won't show on a row they added themselves (x.addedByAdministrator,
// see the template) - an admin can't review/approve their own entry.
const isAdministrator = role() === 'Administrator';

const today = new Date().toISOString().slice(0, 10);
const pageSize = 10;
const staff = ref([]);
const show = ref(false);
const form = ref(blank());
const totalAmount = ref(0);

const tab = ref('Unpaid');
const allDates = ref(true);
const dateFrom = ref(today);
const dateTo = ref(today);

async function fetchDailyExpensesPage(params) {
  const dateParams = allDates.value ? {} : { from: dateFrom.value, to: dateTo.value };
  const { data } = await api.get('/expenses/daily', {
    params: {
      status: tab.value,
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

const { page, pageCount, rows: pagedRows, totalCount, sortBy, sortIndicator, searchText, reload } = useServerGrid(fetchDailyExpensesPage, pageSize);

async function changeTab(value) {
  tab.value = value;
  page.value = 1;
  await reload();
}

function blank() {
  return {
    id: null,
    expenseDate: today,
    category: 'General',
    description: '',
    amount: 0,
    paymentMode: 'Cash',
    paidByStaffId: null,
    referenceNumber: '',
    notes: ''
  };
}

// Settle - locks this one expense from further Edit/Delete. If it was paid
// out-of-pocket by a staff member, its amount becomes a Reimbursement on
// that staff member's salary for the month it falls in.
const settlingId = ref(null);

async function settleOne(expense) {
  if (!confirm(`Settle ₹${Number(expense.amount).toFixed(2)} - ${expense.description}?${expense.paidByStaffName ? ` This will be added as a Reimbursement on ${expense.paidByStaffName}'s salary.` : ''}`)) return;
  settlingId.value = expense.id;
  try {
    await api.post(`/expenses/daily/${expense.id}/settle`);
    await load();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to settle this expense.');
  } finally {
    settlingId.value = null;
  }
}

async function load() {
  const [s] = await Promise.all([api.get('/staff'), reload()]);
  staff.value = s.data;
}

function add() {
  form.value = blank();
  show.value = true;
}

function edit(x) {
  form.value = { ...x, expenseDate: String(x.expenseDate).slice(0, 10) };
  show.value = true;
}

async function save() {
  if (!form.value.description.trim() || Number(form.value.amount) <= 0) {
    alert('Description and amount are required.');
    return;
  }
  try {
    if (form.value.id) await api.put(`/expenses/daily/${form.value.id}`, form.value);
    else await api.post('/expenses/daily', form.value);
    show.value = false;
    await load();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to save this expense.');
  }
}

async function remove(x) {
  if (!confirm('Delete this expense?')) return;
  try {
    await api.delete(`/expenses/daily/${x.id}`);
    await load();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to delete this expense.');
  }
}

onMounted(load);
</script>
<template>
  <h1>Daily Expenses</h1>
  <div class="toolbar">
    <button @click="add">+ Add Expense</button>
    <button :class="tab === 'Unpaid' ? '' : 'secondary'" @click="changeTab('Unpaid')">Unpaid</button>
    <button :class="tab === 'Paid' ? '' : 'secondary'" @click="changeTab('Paid')">Paid</button>
    <button :class="tab === 'All' ? '' : 'secondary'" @click="changeTab('All')">All</button>
  </div>

  <div class="toolbar report-filter">
    <label class="toggle-row"><input type="checkbox" v-model="allDates" @change="load"> All Dates</label>
    <label v-if="!allDates">From<input type="date" v-model="dateFrom"></label>
    <label v-if="!allDates">To<input type="date" v-model="dateTo"></label>
    <button v-if="!allDates" @click="load">Apply</button>
  </div>

  <GridSearch v-model="searchText" placeholder="Search category, description, payment mode, reference..." />

  <table class="table">
    <tr>
      <th class="sortable" @click="sortBy('expenseDate')">Date <span class="sort-indicator">{{ sortIndicator('expenseDate') }}</span></th>
      <th class="sortable" @click="sortBy('category')">Category <span class="sort-indicator">{{ sortIndicator('category') }}</span></th>
      <th>Description</th>
      <th>Mode</th>
      <th>Paid By Staff</th>
      <th>Reference</th>
      <th class="sortable" @click="sortBy('amount')">Amount <span class="sort-indicator">{{ sortIndicator('amount') }}</span></th>
      <th>Status</th>
      <th></th>
    </tr>
    <tr v-for="x in pagedRows" :key="x.id">
      <td>{{ String(x.expenseDate).slice(0, 10) }}</td>
      <td>{{ x.category }}</td>
      <td>{{ x.description }}</td>
      <td>{{ x.paymentMode }}</td>
      <td>{{ x.paidByStaffName || '-' }}</td>
      <td>{{ x.referenceNumber || '-' }}</td>
      <td>₹{{ Number(x.amount).toFixed(2) }}</td>
      <td>{{ x.isSettled ? 'Paid' : 'Unpaid' }}</td>
      <td class="actions">
        <template v-if="!x.isSettled">
          <button @click="edit(x)">Edit</button>
          <button class="danger-btn" @click="remove(x)">Delete</button>
          <button v-if="can('EXPENSE_DAILY', 'edit') && isAdministrator && !x.addedByAdministrator" :disabled="settlingId === x.id" @click="settleOne(x)">Settle</button>
        </template>
        <span v-else class="muted">Settled</span>
      </td>
    </tr>
  </table>
  <div class="bill-total-box"><div class="grand"><span>Total Expenses</span><b>₹{{ totalAmount.toFixed(2) }}</b></div></div>
  <Pagination :page="page" :page-count="pageCount" :total="totalCount" :page-size="pageSize" @update:page="page=$event" />

  <div v-if="show" class="modal-bg">
    <div class="modal">
      <button class="modal-close-x" @click="show = false">×</button>
      <h2>{{ form.id ? 'Edit' : 'Add' }} Daily Expense</h2>
      <div class="form-grid">
        <label>Date *<input type="date" v-model="form.expenseDate"></label>
        <label>Category<input v-model="form.category"></label>
        <label class="full">Description *<input v-model="form.description"></label>
        <label>Amount *<input type="number" min="0" step="0.01" v-model.number="form.amount"></label>
        <label>Payment Mode
          <select v-model="form.paymentMode">
            <option>Cash</option><option>UPI</option><option>Card</option><option>Bank Transfer</option><option>Cheque</option><option>Other</option>
          </select>
        </label>
        <label>Paid By Staff
          <select v-model.number="form.paidByStaffId">
            <option :value="null">Not specified</option>
            <option v-for="x in staff" :key="x.id" :value="x.id">{{ x.name }} · {{ x.designation || '-' }}</option>
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
