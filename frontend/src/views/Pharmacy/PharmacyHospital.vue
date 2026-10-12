<script setup>
import { ref, onMounted, computed } from 'vue';
import { api } from '../../api';
import { can } from '../../auth';
import Pagination from '../../components/Pagination.vue';
import GridSearch from '../../components/GridSearch.vue';
import { useServerGrid } from '../../composables/useServerGrid';

const activeTab = ref('issue');
const departments = ['Lab', 'Dental', 'OT', 'Others'];

// --- Issue tab: pick a department, build a cart from available stock, same
// pack/unit math as Pharmacy Sales but no patient/bill involved. ---
const department = ref('Lab');
const cart = ref([]);
const stockPageSize = 10;

async function fetchStockPage(params) {
  const { data } = await api.get('/pharmacy/sales/search', {
    params: { q: params.searchText, page: params.page, pageSize: params.pageSize }
  });
  return { rows: data.rows, totalCount: data.totalCount };
}

const {
  page: stockPage, pageCount: stockPageCount, rows: stockResults, totalCount: stockTotalCount,
  searchText: stockSearch, reload: reloadStock
} = useServerGrid(fetchStockPage, stockPageSize);

function packLabel(row) {
  if (row.medicineType === 'Tablet') return 'Strip';
  if (row.medicineType === 'Injection') return 'Pack';
  return 'Unit';
}

function looseLabel(row) {
  if (row.medicineType === 'Tablet') return 'Tablet';
  if (row.medicineType === 'Injection') return 'Vial';
  return 'Unit';
}

function allowedUnits(row) {
  const pack = packLabel(row);
  const loose = looseLabel(row);
  return pack === loose ? [pack] : [pack, loose];
}

function addToCart(row) {
  if (cart.value.some(x => x.purchaseItemId === row.id)) return;
  const unitType = packLabel(row);
  cart.value.push({
    purchaseItemId: row.id,
    productName: row.productName,
    batchNo: row.batchNo,
    medicineType: row.medicineType,
    mrp: Number(row.mrp),
    unitsPerPack: Number(row.unitsPerPack),
    remainingUnits: Number(row.remainingTablets),
    unitType,
    quantity: 1,
    unitPrice: Number(row.mrp)
  });
}

function updateUnit(row) {
  row.unitPrice = row.unitType === packLabel(row)
    ? Number(row.mrp)
    : Number(row.mrp) / Math.max(Number(row.unitsPerPack), 1);
  row.quantity = 1;
}

function maxQuantity(row) {
  if (row.unitType === packLabel(row)) {
    return Math.floor(Number(row.remainingUnits) / Math.max(Number(row.unitsPerPack), 1));
  }
  return Number(row.remainingUnits);
}

function rowTotal(row) {
  return Number(row.unitPrice || 0) * Number(row.quantity || 0);
}

function removeCart(index) {
  cart.value.splice(index, 1);
}

const cartTotal = computed(() => cart.value.reduce((sum, row) => sum + rowTotal(row), 0));

async function createIssue() {
  if (!cart.value.length) {
    alert('Add at least one medicine.');
    return;
  }

  for (const row of cart.value) {
    if (row.quantity <= 0 || row.quantity > maxQuantity(row)) {
      alert(`Invalid ${row.unitType.toLowerCase()} quantity for ${row.productName}.`);
      return;
    }
  }

  try {
    const { data } = await api.post('/pharmacy/hospital/issues', {
      department: department.value,
      items: cart.value.map(x => ({
        purchaseItemId: x.purchaseItemId,
        unitType: x.unitType,
        quantity: x.quantity
      }))
    });

    alert(`Issued to ${department.value}. Issue ${data.issueNumber}, ₹${Number(data.totalAmount).toFixed(2)}.`);
    cart.value = [];
    await reloadStock();
    await reloadIssues();
    await loadOutstanding();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to create this issue.');
  }
}

// --- History tab: every past issue, server-paged, plain search. ---
const historyFrom = ref('');
const historyTo = ref('');
const historyDepartment = ref('All');
const pageSize = 10;

async function fetchIssuesPage(params) {
  const { data } = await api.get('/pharmacy/hospital/issues', {
    params: {
      from: historyFrom.value || undefined,
      to: historyTo.value || undefined,
      department: historyDepartment.value === 'All' ? undefined : historyDepartment.value,
      page: params.page,
      pageSize: params.pageSize,
      sortBy: params.sortBy,
      sortDirection: params.sortDirection,
      searchText: params.searchText
    }
  });
  return { rows: data.rows, totalCount: data.totalCount };
}

const {
  page: issuePage, pageCount: issuePageCount, rows: issueRows, totalCount: issueTotalCount,
  sortBy: sortIssues, sortIndicator: issueSortIndicator, searchText: issueSearchText, reload: reloadIssues
} = useServerGrid(fetchIssuesPage, pageSize);

// --- Settlement tab: outstanding-to-date breakdown, Settle Now, past
// settlements list, mark-payment modal. ---
const outstanding = ref({ rows: [], grandTotal: 0 });
const settlementDetail = ref(null);
const payment = ref(null);

async function loadOutstanding() {
  outstanding.value = (await api.get('/pharmacy/hospital/outstanding')).data;
}

async function fetchSettlementsPage(params) {
  const { data } = await api.get('/pharmacy/hospital/settlements', {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      sortBy: params.sortBy,
      sortDirection: params.sortDirection,
      searchText: params.searchText
    }
  });
  return { rows: data.rows, totalCount: data.totalCount };
}

const {
  page: settlementPage, pageCount: settlementPageCount, rows: settlementRows, totalCount: settlementTotalCount,
  sortBy: sortSettlements, sortIndicator: settlementSortIndicator, searchText: settlementSearchText, reload: reloadSettlements
} = useServerGrid(fetchSettlementsPage, pageSize);

async function settleNow() {
  if (!outstanding.value.rows?.length) return;
  if (!confirm(`Settle everything outstanding (₹${Number(outstanding.value.grandTotal).toFixed(2)}) in one lump sum?`)) return;

  try {
    const { data } = await api.post('/pharmacy/hospital/settle');
    alert(`Settlement ${data.settlementNumber} created for ₹${Number(data.totalAmount).toFixed(2)}.`);
    await loadOutstanding();
    await reloadSettlements();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to settle.');
  }
}

async function viewSettlement(row) {
  settlementDetail.value = (await api.get(`/pharmacy/hospital/settlements/${row.id}`)).data;
}

function openPayment(settlement) {
  payment.value = { id: settlement.id, settlementNumber: settlement.settlementNumber, totalAmount: settlement.totalAmount, paymentMode: 'Cash', referenceNumber: '' };
}

async function savePayment() {
  try {
    await api.post(`/pharmacy/hospital/settlements/${payment.value.id}/pay`, {
      paymentMode: payment.value.paymentMode,
      referenceNumber: payment.value.referenceNumber
    });
    payment.value = null;
    await reloadSettlements();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to record this payment.');
  }
}

onMounted(async () => {
  await reloadStock();
  await reloadIssues();
  await loadOutstanding();
  await reloadSettlements();
});
</script>

<template>
  <h1>Pharmacy · Hospital</h1>
  <div class="muted" style="margin-bottom:12px">
    Stock issued here goes to an internal hospital department (Lab, Dental, OT, Others), not to a patient - it never shows up on any patient bill. The hospital settles its running tab with the pharmacy periodically instead.
  </div>

  <div class="toolbar">
    <button :class="activeTab === 'issue' ? '' : 'secondary'" @click="activeTab = 'issue'">Issue Items</button>
    <button :class="activeTab === 'history' ? '' : 'secondary'" @click="activeTab = 'history'">Issue History</button>
    <button :class="activeTab === 'settlement' ? '' : 'secondary'" @click="activeTab = 'settlement'">Settlement</button>
  </div>

  <div v-if="activeTab === 'issue'" class="card">
    <h2>Issue to Department</h2>
    <div class="form-grid">
      <label>Department
        <select v-model="department">
          <option v-for="d in departments" :key="d" :value="d">{{ d }}</option>
        </select>
      </label>
    </div>

    <GridSearch v-model="stockSearch" placeholder="Search product, MF, batch or HSN" />
    <table class="table">
      <tr><th>Type</th><th>Product</th><th>Batch</th><th>MRP</th><th>Stock</th><th></th></tr>
      <tr v-for="row in stockResults" :key="row.id">
        <td>{{ row.medicineType }}</td>
        <td>{{ row.productName }}</td>
        <td>{{ row.batchNo }}</td>
        <td>₹{{ Number(row.mrp).toFixed(2) }}</td>
        <td>{{ row.availablePacks }} pack(s) + {{ row.looseUnits }} loose</td>
        <td><button v-if="can('PHARMACY_HOSPITAL', 'add')" @click="addToCart(row)">Add</button></td>
      </tr>
    </table>
    <Pagination :page="stockPage" :page-count="stockPageCount" :total="stockTotalCount" :page-size="stockPageSize" @update:page="stockPage = $event" />

    <h3>Selected Medicines</h3>
    <table class="table">
      <tr><th>Product</th><th>Batch</th><th>Unit</th><th>Quantity</th><th>Price</th><th>Total</th><th></th></tr>
      <tr v-for="(row, index) in cart" :key="row.purchaseItemId">
        <td>{{ row.productName }}</td>
        <td>{{ row.batchNo }}</td>
        <td><select v-model="row.unitType" @change="updateUnit(row)"><option v-for="u in allowedUnits(row)" :key="u">{{ u }}</option></select></td>
        <td><input type="number" min="1" :max="maxQuantity(row)" v-model.number="row.quantity"><div class="muted">Max {{ maxQuantity(row) }} {{ row.unitType }}</div></td>
        <td>₹{{ Number(row.unitPrice).toFixed(2) }}</td>
        <td><b>₹{{ rowTotal(row).toFixed(2) }}</b></td>
        <td><button class="danger-btn" @click="removeCart(index)">×</button></td>
      </tr>
    </table>
    <div class="bill-total-box"><div class="grand"><span>Total</span><b>₹{{ cartTotal.toFixed(2) }}</b></div></div>
    <div class="modal-actions"><button v-if="can('PHARMACY_HOSPITAL', 'add')" @click="createIssue">Issue to {{ department }}</button></div>
  </div>

  <div v-if="activeTab === 'history'">
    <div class="toolbar report-filter">
      <label>From<input type="date" v-model="historyFrom"></label>
      <label>To<input type="date" v-model="historyTo"></label>
      <label>Department
        <select v-model="historyDepartment">
          <option>All</option>
          <option v-for="d in departments" :key="d" :value="d">{{ d }}</option>
        </select>
      </label>
      <button @click="reloadIssues">Apply</button>
    </div>
    <GridSearch v-model="issueSearchText" placeholder="Search issue number, department..." />
    <table class="table">
      <tr>
        <th class="sortable" @click="sortIssues('issueNumber')">Issue <span class="sort-indicator">{{ issueSortIndicator('issueNumber') }}</span></th>
        <th class="sortable" @click="sortIssues('department')">Department <span class="sort-indicator">{{ issueSortIndicator('department') }}</span></th>
        <th>Date</th>
        <th class="sortable" @click="sortIssues('totalAmount')">Amount <span class="sort-indicator">{{ issueSortIndicator('totalAmount') }}</span></th>
        <th>Status</th>
      </tr>
      <tr v-for="row in issueRows" :key="row.id">
        <td>{{ row.issueNumber }}</td>
        <td>{{ row.department }}</td>
        <td>{{ new Date(row.issueDateUtc).toLocaleDateString() }}</td>
        <td>₹{{ Number(row.totalAmount).toFixed(2) }}</td>
        <td>{{ row.isSettled ? 'Settled' : 'Outstanding' }}</td>
      </tr>
    </table>
    <Pagination :page="issuePage" :page-count="issuePageCount" :total="issueTotalCount" :page-size="pageSize" @update:page="issuePage = $event" />
  </div>

  <div v-if="activeTab === 'settlement'">
    <div class="card">
      <h2>Outstanding</h2>
      <div class="grid">
        <div class="card"><div class="muted">Total Outstanding</div><div class="metric">₹{{ Number(outstanding.grandTotal || 0).toFixed(2) }}</div></div>
      </div>
      <div v-if="!outstanding.rows?.length" class="muted">Nothing outstanding right now.</div>
      <template v-else>
        <div v-for="dateGroup in outstanding.rows" :key="dateGroup.date" class="patient-group">
          <div class="patient-group-header">
            <b>{{ new Date(dateGroup.date).toLocaleDateString() }}</b>
            <span><b>₹{{ Number(dateGroup.dateTotal).toFixed(2) }}</b></span>
          </div>
          <table class="table">
            <tr><th>Issue</th><th>Department</th><th>Amount</th></tr>
            <tr v-for="issue in dateGroup.issues" :key="issue.id">
              <td>{{ issue.issueNumber }}</td>
              <td>{{ issue.department }}</td>
              <td>₹{{ Number(issue.totalAmount).toFixed(2) }}</td>
            </tr>
          </table>
        </div>
        <div class="modal-actions"><button v-if="can('PHARMACY_HOSPITAL', 'add')" @click="settleNow">Settle Now (₹{{ Number(outstanding.grandTotal).toFixed(2) }})</button></div>
      </template>
    </div>

    <h2 class="report-section-heading">Settlement History</h2>
    <GridSearch v-model="settlementSearchText" placeholder="Search settlement number..." />
    <table class="table">
      <tr>
        <th class="sortable" @click="sortSettlements('settlementNumber')">Settlement <span class="sort-indicator">{{ settlementSortIndicator('settlementNumber') }}</span></th>
        <th>Date</th>
        <th class="sortable" @click="sortSettlements('totalAmount')">Amount <span class="sort-indicator">{{ settlementSortIndicator('totalAmount') }}</span></th>
        <th>Status</th>
        <th>Payment</th>
        <th></th>
      </tr>
      <tr v-for="row in settlementRows" :key="row.id">
        <td>{{ row.settlementNumber }}</td>
        <td>{{ new Date(row.settlementDateUtc).toLocaleDateString() }}</td>
        <td>₹{{ Number(row.totalAmount).toFixed(2) }}</td>
        <td>{{ row.isPaid ? 'Paid' : 'Unpaid' }}</td>
        <td>{{ row.paymentMode || '-' }}<div class="muted">{{ row.paidAtUtc ? new Date(row.paidAtUtc).toLocaleDateString() : '' }}</div></td>
        <td class="actions">
          <button @click="viewSettlement(row)">View</button>
          <button v-if="!row.isPaid && can('PHARMACY_HOSPITAL', 'edit')" @click="openPayment(row)">Mark Payment</button>
        </td>
      </tr>
    </table>
    <Pagination :page="settlementPage" :page-count="settlementPageCount" :total="settlementTotalCount" :page-size="pageSize" @update:page="settlementPage = $event" />
  </div>

  <div v-if="settlementDetail" class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="settlementDetail = null">×</button>
      <h2>{{ settlementDetail.settlement.settlementNumber }}</h2>
      <div class="card"><b>Total: ₹{{ Number(settlementDetail.settlement.totalAmount).toFixed(2) }}</b> · {{ settlementDetail.settlement.isPaid ? 'Paid' : 'Unpaid' }}</div>
      <div v-for="dateGroup in settlementDetail.rows" :key="dateGroup.date" class="patient-group">
        <div class="patient-group-header">
          <b>{{ new Date(dateGroup.date).toLocaleDateString() }}</b>
          <span><b>₹{{ Number(dateGroup.dateTotal).toFixed(2) }}</b></span>
        </div>
        <table class="table">
          <tr><th>Issue</th><th>Department</th><th>Amount</th></tr>
          <tr v-for="issue in dateGroup.issues" :key="issue.id">
            <td>{{ issue.issueNumber }}</td>
            <td>{{ issue.department }}</td>
            <td>₹{{ Number(issue.totalAmount).toFixed(2) }}</td>
          </tr>
        </table>
      </div>
    </div>
  </div>

  <div v-if="payment" class="modal-bg">
    <div class="modal">
      <button class="modal-close-x" @click="payment = null">×</button>
      <h2>Mark Payment · {{ payment.settlementNumber }}</h2>
      <div class="card"><b>Amount: ₹{{ Number(payment.totalAmount).toFixed(2) }}</b></div>
      <div class="form-grid" style="margin-top:14px">
        <label>Mode
          <select v-model="payment.paymentMode">
            <option>Cash</option>
            <option>UPI</option>
            <option>Bank Transfer</option>
          </select>
        </label>
        <label>Reference<input v-model="payment.referenceNumber"></label>
      </div>
      <div class="modal-actions"><button @click="savePayment">Save Payment</button><button class="secondary" @click="payment = null">Cancel</button></div>
    </div>
  </div>
</template>
