<script setup>
import { ref, computed, onMounted } from 'vue';
import { api } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';

const today = new Date().toISOString().slice(0, 10);
const from = ref(today);
const to = ref(today);
const selectedDoctorId = ref(null);
const selectedPatientId = ref(null);
const periodPreset = ref('Today');
const executive = ref({});
const selectedYear = ref(new Date().getFullYear());
const monthlyRevenue = ref({ year: selectedYear.value, rows: [] });

const doctors = ref([]);
const patients = ref([]);
const categories = ref([]);
const doctorRows = ref([]);
const patientRows = ref([]);
const incomeShare = ref({ total: 0, items: [] });
const pageSize = 10;
const { search: doctorSearch, page: doctorPage, pageCount: doctorPageCount, pagedRows: pagedDoctorRows, sortedRows: sortedDoctorRows, sortBy: sortDoctor, sortIndicator: doctorSortIndicator } = useGrid(doctorRows, pageSize);
const { search: patientSearch, page: patientPage, pageCount: patientPageCount, pagedRows: pagedPatientRows, sortedRows: sortedPatientRows, sortBy: sortPatient, sortIndicator: patientSortIndicator } = useGrid(patientRows, pageSize);

const paymentModeShare = ref({ total: 0, items: [] });

const categoryModal = ref(false);
const categoryForm = ref({ name: '' });

const pieColors = [
  '#2563eb', '#16a34a', '#f59e0b', '#dc2626',
  '#7c3aed', '#0891b2', '#ea580c', '#4f46e5',
  '#0f766e', '#be123c'
];

async function loadMasters() {
  const [doctorsRes, patientsRes, categoriesRes] = await Promise.all([
    api.get('/doctors'),
    api.get('/patients'),
    api.get('/reporting/categories')
  ]);

  doctors.value = doctorsRes.data;
  patients.value = patientsRes.data;
  categories.value = categoriesRes.data;
}

async function loadReports() {
  const common = {
    from: from.value,
    to: to.value
  };

  const [doctorRes, patientRes, shareRes, paymentModeRes, executiveRes, monthlyRes] = await Promise.all([
    api.get('/reporting/doctor-summary', {
      params: {
        ...common,
        doctorId: selectedDoctorId.value || undefined
      }
    }),
    api.get('/reporting/patient-summary', {
      params: {
        ...common,
        patientId: selectedPatientId.value || undefined
      }
    }),
    api.get('/reporting/income-share', {
      params: {
        ...common,
        doctorId: selectedDoctorId.value || undefined,
        patientId: selectedPatientId.value || undefined
      }
    }),
    api.get('/reporting/payment-mode-share', {
      params: {
        ...common,
        doctorId: selectedDoctorId.value || undefined,
        patientId: selectedPatientId.value || undefined
      }
    }),
    api.get('/reporting/executive', { params: common }),
    api.get('/reporting/monthly-revenue', { params: { year: selectedYear.value } })
  ]);

  doctorRows.value = doctorRes.data;
  patientRows.value = patientRes.data.sort((a, b) => Number(b.total) - Number(a.total));
  incomeShare.value = shareRes.data;
  paymentModeShare.value = paymentModeRes.data;
  executive.value = executiveRes.data;
  monthlyRevenue.value = monthlyRes.data;
}

async function load() {
  await loadMasters();
  await loadReports();
}

function maxConsulted() {
  return Math.max(1, ...doctorRows.value.map(x => Number(x.patientCount || 0)));
}

function maxDoctorIncome() {
  return Math.max(1, ...doctorRows.value.map(x => Number(x.total || 0)));
}

function maxPatientIncome() {
  return Math.max(1, ...patientRows.value.map(x => Number(x.total || 0)));
}

function categoryIncome(row, category) {
  return Number(row.income?.[category.name] || 0);
}

const pieStyle = computed(() => {
  const items = incomeShare.value.items || [];
  if (!items.length) return { background: '#e5e7eb' };

  let current = 0;
  const parts = items.map((item, index) => {
    const start = current;
    current += Number(item.percentage || 0);
    return `${pieColors[index % pieColors.length]} ${start}% ${current}%`;
  });

  return {
    background: `conic-gradient(${parts.join(',')})`
  };
});

const paymentPieStyle = computed(() => {
  const items = paymentModeShare.value.items || [];
  if (!items.length) return { background: '#e5e7eb' };

  let current = 0;
  const parts = items.map((item, index) => {
    const start = current;
    current += Number(item.percentage || 0);
    return `${pieColors[index % pieColors.length]} ${start}% ${current}%`;
  });

  return {
    background: `conic-gradient(${parts.join(',')})`
  };
});


function formatDate(date) {
  const d = new Date(date);
  d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
  return d.toISOString().slice(0, 10);
}

function applyPreset() {
  const now = new Date();
  let start = new Date(now);
  let end = new Date(now);

  if (periodPreset.value === 'Yesterday') {
    start.setDate(start.getDate() - 1);
    end = new Date(start);
  } else if (periodPreset.value === 'This Week') {
    const day = start.getDay() || 7;
    start.setDate(start.getDate() - day + 1);
  } else if (periodPreset.value === 'This Month') {
    start = new Date(now.getFullYear(), now.getMonth(), 1);
  } else if (periodPreset.value === 'Last Month') {
    start = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    end = new Date(now.getFullYear(), now.getMonth(), 0);
  } else if (periodPreset.value === 'Today') {
    start = new Date(now);
    end = new Date(now);
  } else {
    return;
  }

  from.value = formatDate(start);
  to.value = formatDate(end);
  loadReports();
}

function money(value) {
  return Number(value || 0).toFixed(2);
}

function maxMonthlyNet() {
  return Math.max(1, ...(monthlyRevenue.value.rows || []).map(x => Number(x.net || 0)));
}

function newCategory() {
  categoryForm.value = { name: '' };
  categoryModal.value = true;
}

function editCategory(category) {
  categoryForm.value = { ...category };
  categoryModal.value = true;
}

async function saveCategory() {
  if (!categoryForm.value.name?.trim()) return;

  if (categoryForm.value.id) {
    await api.put(`/reporting/categories/${categoryForm.value.id}`, categoryForm.value);
  } else {
    await api.post('/reporting/categories', categoryForm.value);
  }

  categoryModal.value = false;
  await load();
}

async function deleteCategory(category) {
  if (!confirm(`Delete report category ${category.name}?`)) return;
  await api.delete(`/reporting/categories/${category.id}`);
  await load();
}

onMounted(load);
</script>

<template>
  <h1>Reporting</h1>

  <div class="toolbar report-filter-wide">
    <label>
      Period
      <select v-model="periodPreset" @change="applyPreset">
        <option>Today</option><option>Yesterday</option><option>This Week</option><option>This Month</option><option>Last Month</option><option>Custom Date</option>
      </select>
    </label>
    <label>From<input v-model="from" type="date" @change="periodPreset='Custom Date'"></label>
    <label>To<input v-model="to" type="date" @change="periodPreset='Custom Date'"></label>
    <label>
      Doctor
      <select v-model.number="selectedDoctorId">
        <option :value="null">All Doctors</option>
        <option v-for="doctor in doctors.filter(x => x.isActive)" :key="doctor.id" :value="doctor.id">{{ doctor.name }}</option>
      </select>
    </label>
    <label>
      Patient
      <select v-model.number="selectedPatientId">
        <option :value="null">All Patients</option>
        <option v-for="patient in patients" :key="patient.id" :value="patient.id">{{ patient.name }} · {{ patient.patientCode }}</option>
      </select>
    </label>
    <button @click="loadReports">Apply</button>
    
  </div>

  <h2>Executive Dashboard</h2>
  <div class="executive-card-grid">
    <div class="card executive-card"><span>OPD</span><b>{{executive.opd || 0}}</b></div>
    <div class="card executive-card"><span>IPD</span><b>{{executive.ipd || 0}}</b></div>
    <div class="card executive-card"><span>Emergency</span><b>{{executive.emergency || 0}}</b></div>
    <div class="card executive-card"><span>Admissions</span><b>{{executive.admissions || 0}}</b></div>
    <div class="card executive-card"><span>Discharges</span><b>{{executive.discharges || 0}}</b></div>
    <div class="card executive-card"><span>Surgeries</span><b>{{executive.surgeries || 0}}</b></div>
    <div class="card executive-card"><span>Bed Occupancy</span><b>{{executive.bedOccupancy?.percentage || 0}}%</b><small>{{executive.bedOccupancy?.occupied || 0}} / {{executive.bedOccupancy?.total || 0}}</small></div>
    <div class="card executive-card"><span>Lab Tests</span><b>{{executive.labTests || 0}}</b></div>
    <div class="card executive-card"><span>Pharmacy Bills</span><b>{{executive.pharmacyBills || 0}}</b></div>
    <div class="card executive-card money-card"><span>Gross Revenue</span><b>₹{{money(executive.grossRevenue)}}</b></div>
    <div class="card executive-card money-card"><span>Discounts</span><b>₹{{money(executive.discounts)}}</b></div>
    <div class="card executive-card money-card"><span>Net Revenue</span><b>₹{{money(executive.netRevenue)}}</b></div>
    <div class="card executive-card money-card"><span>Collections</span><b>₹{{money(executive.collections)}}</b></div>
    <div class="card executive-card money-card"><span>Outstanding</span><b>₹{{money(executive.outstanding)}}</b></div>
    <div class="card executive-card money-card"><span>Expenses</span><b>₹{{money(executive.expenses)}}</b><small>Recorded pharmacy purchases</small></div>
    <div class="card executive-card money-card"><span>Expiry Returns</span><b>₹{{money(executive.expiryReturnedAmount)}}</b></div>
    <div class="card executive-card money-card"><span>Expiry Loss</span><b>₹{{money(executive.expiryLossAmount)}}</b></div>
    <div class="card executive-card money-card"><span>Operating Result</span><b>₹{{money(executive.operatingResult)}}</b></div>
  </div>

  <div class="card monthly-revenue-card">
    <div class="page-head"><div><h2>Month-wise Revenue Progress</h2><div class="muted">Gross, discount, net revenue, collections and recorded expenses.</div></div><label>Year<input type="number" min="2020" max="2100" v-model.number="selectedYear"><button @click="loadReports">Apply</button></label></div>
    <div class="monthly-vertical-chart">
      <div class="monthly-y-axis">
        <span>₹{{ money(maxMonthlyNet()) }}</span>
        <span>₹{{ money(maxMonthlyNet() * 0.75) }}</span>
        <span>₹{{ money(maxMonthlyNet() * 0.50) }}</span>
        <span>₹{{ money(maxMonthlyNet() * 0.25) }}</span>
        <span>₹0.00</span>
      </div>

      <div class="monthly-plot-area">
        <div
          v-for="row in monthlyRevenue.rows"
          :key="row.month"
          class="monthly-column"
        >
          <div class="monthly-column-value">
            ₹{{ Number(row.net || 0).toFixed(0) }}
          </div>

          <div class="monthly-column-track">
            <div
              class="monthly-column-bar"
              :style="{
                height:
                  (Number(row.net || 0) / maxMonthlyNet() * 100) + '%'
              }"
              :title="`${row.monthName}: ₹${money(row.net)}`"
            ></div>
          </div>

          <b class="monthly-column-label">
            {{ row.monthName }}
          </b>
        </div>
      </div>
    </div>
    <table class="table"><tr><th>Month</th><th>Gross</th><th>Discount</th><th>Net</th><th>Collections</th><th>Expenses</th><th>Operating Result</th></tr><tr v-for="row in monthlyRevenue.rows" :key="'m-'+row.month"><td>{{row.monthName}}</td><td>₹{{money(row.gross)}}</td><td>₹{{money(row.discount)}}</td><td><b>₹{{money(row.net)}}</b></td><td>₹{{money(row.collections)}}</td><td>₹{{money(row.expenses)}}</td><td>₹{{money(row.operatingResult)}}</td></tr></table>
  </div>

  <div class="report-grid">
    <section class="card">
      <h2>Patients Consulted by Doctor</h2>
      <div v-if="!doctorRows.length" class="muted">No consultation data for this period.</div>
      <div v-for="row in pagedDoctorRows" :key="row.doctorId" class="bar-row">
        <div class="bar-label">{{ row.doctorName }}</div>
        <div class="bar-track">
          <div class="bar-fill" :style="{ width: (row.patientCount / maxConsulted() * 100) + '%' }"></div>
        </div>
        <div class="bar-value">{{ row.patientCount }} patient(s)</div>
      </div>
    </section>

    <section class="card">
      <h2>Income Source %</h2>
      <div class="pie-layout">
        <div class="pie-chart" :style="pieStyle">
          <div class="pie-hole">₹{{ Number(incomeShare.total || 0).toFixed(0) }}</div>
        </div>
        <div class="pie-legend">
          <div v-for="(item, index) in incomeShare.items" :key="item.category" class="legend-row">
            <span class="legend-color" :style="{ background: pieColors[index % pieColors.length] }"></span>
            <span>{{ item.category }}</span>
            <b>{{ Number(item.percentage).toFixed(1) }}%</b>
            <span class="muted">₹{{ Number(item.amount).toFixed(2) }}</span>
          </div>
        </div>
      </div>
    </section>

    <section class="card">
      <h2>Mode of Payment %</h2>
      <div v-if="!paymentModeShare.items?.length" class="muted">No payments received for this period.</div>
      <div v-else class="pie-layout">
        <div class="pie-chart" :style="paymentPieStyle">
          <div class="pie-hole">₹{{ Number(paymentModeShare.total || 0).toFixed(0) }}</div>
        </div>
        <div class="pie-legend">
          <div v-for="(item, index) in paymentModeShare.items" :key="item.mode" class="legend-row">
            <span class="legend-color" :style="{ background: pieColors[index % pieColors.length] }"></span>
            <span>{{ item.mode }}</span>
            <b>{{ Number(item.percentage).toFixed(1) }}%</b>
            <span class="muted">₹{{ Number(item.amount).toFixed(2) }}</span>
          </div>
        </div>
      </div>
    </section>
  </div>

  <h2>Doctor-wise Report</h2>
  <div class="chart-card card">
    <div v-for="row in pagedDoctorRows" :key="row.doctorId" class="bar-row">
      <div class="bar-label">{{ row.doctorName }}</div>
      <div class="bar-track">
        <div class="bar-fill" :style="{ width: (row.total / maxDoctorIncome() * 100) + '%' }"></div>
      </div>
      <div class="bar-value">₹{{ Number(row.total).toFixed(2) }}</div>
    </div>
  </div>

  <div class="grid-filter-row"><GridSearch v-model="doctorSearch" placeholder="Search all doctor report fields..." /></div>
  <div class="table-scroll">
    <table class="table report-table">
      <tr>
        <th class="sortable" @click="sortDoctor('doctorName')">Doctor <span class="sort-indicator">{{doctorSortIndicator('doctorName')}}</span></th>
        <th class="sortable" @click="sortDoctor('patientCount')">Unique Patients <span class="sort-indicator">{{doctorSortIndicator('patientCount')}}</span></th>
        <th class="sortable" @click="sortDoctor('consultationCount')">Consultations <span class="sort-indicator">{{doctorSortIndicator('consultationCount')}}</span></th>
        <th v-for="category in categories" :key="category.id" class="sortable" @click="sortDoctor('income.' + category.name)">{{ category.name }} <span class="sort-indicator">{{ doctorSortIndicator('income.' + category.name) }}</span></th>
        <th class="sortable" @click="sortDoctor('total')">Total <span class="sort-indicator">{{doctorSortIndicator('total')}}</span></th>
      </tr>
      <tr v-for="row in pagedDoctorRows" :key="row.doctorId">
        <td>{{ row.doctorName }}</td>
        <td>{{ row.patientCount }}</td>
        <td>{{ row.consultationCount }}</td>
        <td v-for="category in categories" :key="category.id">₹{{ categoryIncome(row, category).toFixed(2) }}</td>
        <td><b>₹{{ Number(row.total).toFixed(2) }}</b></td>
      </tr>
    </table>
    <Pagination :page="doctorPage" :page-count="doctorPageCount" :total="sortedDoctorRows.length" :page-size="pageSize" @update:page="doctorPage = $event" />
  </div>

  <h2 style="margin-top: 30px">Patient-wise Report</h2>
  <div class="chart-card card">
    <div v-for="row in patientRows.slice(0, 15)" :key="row.patientId" class="bar-row">
      <div class="bar-label">{{ row.name }}</div>
      <div class="bar-track">
        <div class="bar-fill" :style="{ width: (row.total / maxPatientIncome() * 100) + '%' }"></div>
      </div>
      <div class="bar-value">₹{{ Number(row.total).toFixed(2) }}</div>
    </div>
  </div>

  <div class="grid-filter-row"><GridSearch v-model="patientSearch" placeholder="Search all patient report fields..." /></div>
  <div class="table-scroll">
    <table class="table report-table">
      <tr>
        <th class="sortable" @click="sortPatient('name')">Patient <span class="sort-indicator">{{patientSortIndicator('name')}}</span></th>
        <th class="sortable" @click="sortPatient('patientCode')">ID <span class="sort-indicator">{{patientSortIndicator('patientCode')}}</span></th>
        <th class="sortable" @click="sortPatient('phone')">Phone <span class="sort-indicator">{{patientSortIndicator('phone')}}</span></th>
        <th v-for="category in categories" :key="category.id" class="sortable" @click="sortPatient('income.' + category.name)">{{ category.name }} <span class="sort-indicator">{{ patientSortIndicator('income.' + category.name) }}</span></th>
        <th class="sortable" @click="sortPatient('total')">Total <span class="sort-indicator">{{patientSortIndicator('total')}}</span></th>
      </tr>
      <tr v-for="row in pagedPatientRows" :key="row.patientId">
        <td>{{ row.name }}</td>
        <td>{{ row.patientCode }}</td>
        <td>{{ row.phone }}</td>
        <td v-for="category in categories" :key="category.id">₹{{ categoryIncome(row, category).toFixed(2) }}</td>
        <td><b>₹{{ Number(row.total).toFixed(2) }}</b></td>
      </tr>
    </table>
    <Pagination :page="patientPage" :page-count="patientPageCount" :total="sortedPatientRows.length" :page-size="pageSize" @update:page="patientPage = $event" />
  </div>

  <div v-if="categoryModal" class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="categoryModal = false">×</button>
      <h2>Report Category Master</h2>
      <p class="muted">These categories are used by Billing and by Doctor/Patient income reports.</p>

      <div class="toolbar">
        <input v-model="categoryForm.name" style="max-width: 360px" placeholder="Category name">
        <button @click="saveCategory">{{ categoryForm.id ? 'Update' : 'Add Category' }}</button>
        <button v-if="categoryForm.id" class="secondary" @click="categoryForm = { name: '' }">Cancel Edit</button>
      </div>

      <table class="table">
        <tr><th>Category</th><th></th></tr>
        <tr v-for="category in categories" :key="category.id">
          <td>{{ category.name }}</td>
          <td class="actions">
            <button @click="editCategory(category)">Edit</button>
            <button class="danger-btn" @click="deleteCategory(category)">Delete</button>
          </td>
        </tr>
      </table>

      <div class="modal-actions">
        <button class="secondary" @click="categoryModal = false">Close</button>
      </div>
    </div>
  </div>
</template>
