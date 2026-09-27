<script setup>
import { computed, onMounted, ref } from 'vue';
import { api } from '../api';
import GridSearch from '../components/GridSearch.vue';

const today = new Date();
const from = ref(new Date(today.getFullYear(), today.getMonth(), 1).toISOString().slice(0, 10));
const to = ref(today.toISOString().slice(0, 10));
const doctorId = ref(null);
const sourceType = ref('All');
const search = ref('');
const rows = ref([]);
const doctorGroups = ref([]);
const summary = ref({});
const doctors = ref([]);
const staff = ref([]);
const payment = ref(null);
const bulkPayment = ref(null);
const expanded = ref({});

async function load() {
  const [r, d, s] = await Promise.all([
    api.get('/doctor-settlements', { params: { from: from.value, to: to.value, doctorId: doctorId.value || undefined, sourceType: sourceType.value } }),
    api.get('/doctors'),
    api.get('/staff')
  ]);
  rows.value = r.data.rows || [];
  doctorGroups.value = r.data.doctorGroups || [];
  summary.value = r.data;
  doctors.value = d.data;
  staff.value = s.data;
}

function toggle(doctorId) {
  expanded.value[doctorId] = !expanded.value[doctorId];
}

const search_ = computed(() => search.value.trim().toLowerCase());
function detailRows(doctorId) {
  const list = rows.value.filter(x => x.doctorId === doctorId);
  if (!search_.value) return list;
  return list.filter(x => JSON.stringify(x).toLowerCase().includes(search_.value));
}

const filteredGroups = computed(() => {
  if (!search_.value) return doctorGroups.value;
  const matchingDoctorIds = new Set(
    rows.value.filter(x => JSON.stringify(x).toLowerCase().includes(search_.value)).map(x => x.doctorId)
  );
  return doctorGroups.value.filter(g => matchingDoctorIds.has(g.doctorId) || g.doctorName.toLowerCase().includes(search_.value));
});

function openPayment(x) {
  payment.value = {
    id: x.id,
    doctorName: x.doctorName,
    payableAmount: x.payableAmount,
    paidAmount: x.paidAmount || 0,
    paymentDate: x.paymentDate ? String(x.paymentDate).slice(0, 10) : to.value,
    paymentMode: x.paymentMode || 'Cash',
    referenceNumber: x.referenceNumber || '',
    notes: x.notes || '',
    paidByStaffId: x.paidByStaffId || null
  };
}
async function savePayment() {
  await api.post(`/doctor-settlements/${payment.value.id}/payment`, payment.value);
  payment.value = null;
  await load();
}

function openBulkSettle(group) {
  bulkPayment.value = {
    doctorId: group.doctorId,
    doctorName: group.doctorName,
    outstanding: group.outstanding,
    paymentDate: to.value,
    paymentMode: 'Cash',
    referenceNumber: '',
    notes: '',
    paidByStaffId: null
  };
}
async function saveBulkSettle() {
  await api.post('/doctor-settlements/settle-doctor', {
    doctorId: bulkPayment.value.doctorId,
    from: from.value,
    to: to.value,
    paymentDate: bulkPayment.value.paymentDate,
    paymentMode: bulkPayment.value.paymentMode,
    referenceNumber: bulkPayment.value.referenceNumber,
    notes: bulkPayment.value.notes,
    paidByStaffId: bulkPayment.value.paidByStaffId
  });
  bulkPayment.value = null;
  await load();
}

onMounted(load);
</script>

<template>
  <h1>Doctor Settlement</h1>
  <div class="toolbar">
    <label>From<input type="date" v-model="from"></label>
    <label>To<input type="date" v-model="to"></label>
    <label>Doctor<select v-model.number="doctorId"><option :value="null">All Doctors</option><option v-for="d in doctors" :key="d.id" :value="d.id">{{ d.name }}</option></select></label>
    <label>Type<select v-model="sourceType"><option>All</option><option>Consultation</option><option>OT</option></select></label>
    <button @click="load">Apply</button>
  </div>
  <GridSearch v-model="search" placeholder="Search doctor, consultation, OT, patient, amount..." />

  <div class="grid">
    <div class="card"><div class="muted">Doctor Payable</div><div class="metric">₹{{ Number(summary.totalPayable || 0).toFixed(2) }}</div></div>
    <div class="card"><div class="muted">Paid</div><div class="metric">₹{{ Number(summary.totalPaid || 0).toFixed(2) }}</div></div>
    <div class="card"><div class="muted">Outstanding</div><div class="metric">₹{{ Number(summary.totalOutstanding || 0).toFixed(2) }}</div></div>
  </div>

  <div v-for="group in filteredGroups" :key="group.doctorId" class="card doctor-settlement-group">
    <div class="doctor-settlement-head" @click="toggle(group.doctorId)">
      <div>
        <span class="collapse-caret">{{ expanded[group.doctorId] ? '▾' : '▸' }}</span>
        <b>{{ group.doctorName }}</b>
      </div>
      <div class="doctor-settlement-totals">
        <span>Payable <b>₹{{ Number(group.payable).toFixed(2) }}</b></span>
        <span>Paid <b>₹{{ Number(group.paid).toFixed(2) }}</b></span>
        <span :class="{ danger: group.outstanding > 0 }">Outstanding <b>₹{{ Number(group.outstanding).toFixed(2) }}</b></span>
        <button
          v-if="group.outstanding > 0"
          @click.stop="openBulkSettle(group)"
        >
          Settle All
        </button>
      </div>
    </div>

    <table v-if="expanded[group.doctorId]" class="table">
      <tr><th>Date</th><th>Type</th><th>Patient</th><th>Reference</th><th>Description</th><th>Payable</th><th>Paid</th><th>Outstanding</th><th>Payment</th><th></th></tr>
      <tr v-for="x in detailRows(group.doctorId)" :key="x.id">
        <td>{{ String(x.earnedDate).slice(0, 10) }}</td>
        <td>{{ x.sourceType }}</td>
        <td>{{ x.patientName || '-' }}</td>
        <td>{{ x.referenceCode || '-' }}</td>
        <td>{{ x.description }}</td>
        <td>₹{{ Number(x.payableAmount).toFixed(2) }}</td>
        <td>₹{{ Number(x.paidAmount).toFixed(2) }}</td>
        <td><b>₹{{ Number(x.outstanding).toFixed(2) }}</b></td>
        <td>{{ x.paymentMode || '-' }}<div class="muted">{{ x.paymentDate ? String(x.paymentDate).slice(0, 10) : '' }}</div></td>
        <td><button @click="openPayment(x)">Manage Payment</button></td>
      </tr>
    </table>
  </div>
  <div v-if="!filteredGroups.length" class="muted">No doctor settlements for the selected filters.</div>

  <div v-if="payment" class="modal-bg">
    <div class="modal">
      <button class="modal-close-x" @click="payment = null">×</button>
      <h2>Doctor Payment &middot; {{ payment.doctorName }}</h2>
      <div class="card"><b>Payable: ₹{{ Number(payment.payableAmount).toFixed(2) }}</b></div>
      <div class="form-grid" style="margin-top:14px">
        <label>Paid Amount<input type="number" min="0" :max="payment.payableAmount" step="0.01" v-model.number="payment.paidAmount"></label>
        <label>Payment Date<input type="date" v-model="payment.paymentDate"></label>
        <label>Mode<select v-model="payment.paymentMode"><option>Cash</option><option>UPI</option><option>Bank Transfer</option><option>Cheque</option><option>Other</option></select></label>
        <label>Paid By Staff<select v-model.number="payment.paidByStaffId"><option :value="null">Not specified</option><option v-for="x in staff" :key="x.id" :value="x.id">{{ x.name }}</option></select></label>
        <label>Reference<input v-model="payment.referenceNumber"></label>
        <label class="full">Notes<textarea rows="3" v-model="payment.notes"></textarea></label>
      </div>
      <div class="modal-actions"><button @click="savePayment">Save Payment</button><button class="secondary" @click="payment = null">Cancel</button></div>
    </div>
  </div>

  <div v-if="bulkPayment" class="modal-bg">
    <div class="modal">
      <button class="modal-close-x" @click="bulkPayment = null">×</button>
      <h2>Settle All &middot; {{ bulkPayment.doctorName }}</h2>
      <div class="card"><b>Outstanding: ₹{{ Number(bulkPayment.outstanding).toFixed(2) }}</b><div class="muted">Settles every unpaid consultation and OT payout for this doctor in the selected date range at once.</div></div>
      <div class="form-grid" style="margin-top:14px">
        <label>Payment Date<input type="date" v-model="bulkPayment.paymentDate"></label>
        <label>Mode<select v-model="bulkPayment.paymentMode"><option>Cash</option><option>UPI</option><option>Bank Transfer</option><option>Cheque</option><option>Other</option></select></label>
        <label>Paid By Staff<select v-model.number="bulkPayment.paidByStaffId"><option :value="null">Not specified</option><option v-for="x in staff" :key="x.id" :value="x.id">{{ x.name }}</option></select></label>
        <label>Reference<input v-model="bulkPayment.referenceNumber"></label>
        <label class="full">Notes<textarea rows="3" v-model="bulkPayment.notes"></textarea></label>
      </div>
      <div class="modal-actions"><button @click="saveBulkSettle">Settle All</button><button class="secondary" @click="bulkPayment = null">Cancel</button></div>
    </div>
  </div>
</template>
