<script setup>
import { ref, onMounted, computed, watch } from 'vue';
import { api, assetUrl } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';

const rows = ref([]);
const pageSize = 10;
const { search, page, pageCount, pagedRows, sortedRows, sortBy, sortIndicator } = useGrid(rows, pageSize);
const todayDate = () => new Date().toISOString().slice(0, 10);
const allDates = ref(false);
const dateFrom = ref(todayDate());
const dateTo = ref(todayDate());
const patients = ref([]);
const doctors = ref([]);
const beds = ref([]);
const referrers = ref([]);
const patientSources = ref([]);
const opdTypes = ref([]);
const show = ref(false);
const ipd = ref(false);
const selected = ref(null);
const errors = ref([]);

const nowLocal = () => {
  const date = new Date();
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
  return date.toISOString().slice(0, 16);
};

const defaultFollowUpDate = () => {
  const date = new Date();
  date.setDate(date.getDate() + 2);
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
  return date.toISOString().slice(0, 10);
};

const tomorrowDate = () => {
  const date = new Date();
  date.setDate(date.getDate() + 1);
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
  return date.toISOString().slice(0, 10);
};

const form = ref({
  patientId: null,
  doctorId: null,
  visitType: 'OPD',
  visitDateUtc: nowLocal(),
  bloodPressure: '',
  temperatureC: null,
  weightKg: null,
  heightCm: null,
  spo2: null,
  chiefComplaint: '',
  marketingSource: 'Walk-in',
  referrerId: null,
  followUpDate: defaultFollowUpDate(),
  createBill: true
});

// Clear a stale validation error as soon as the user changes anything,
// instead of leaving an outdated message on screen until they hit Save
// again - the next Save re-validates the fresh data from scratch anyway.
watch(form, () => {
  if (errors.value.length) errors.value = [];
}, { deep: true });

const ipdForm = ref({
  bedId: null,
  doctorId: null,
  payerType: 'Cash'
});

async function load() {
  const dateParams = allDates.value ? {} : { from: dateFrom.value, to: dateTo.value };
  const [
    opdRes,
    patientsRes,
    doctorsRes,
    bedsRes,
    referrersRes,
    sourcesRes,
    opdTypesRes
  ] = await Promise.all([
    api.get('/opd', { params: dateParams }),
    api.get('/patients'),
    api.get('/doctors'),
    api.get('/ipd/beds'),
    api.get('/referrals'),
    api.get('/patient-sources'),
    api.get('/bill-types', { params: { opdOnly: true } })
  ]);

  rows.value = opdRes.data;
  patients.value = patientsRes.data;
  doctors.value = doctorsRes.data;
  beds.value = bedsRes.data;
  referrers.value = referrersRes.data;
  patientSources.value = sourcesRes.data;
  opdTypes.value = opdTypesRes.data;
}

function typeChanged() {
  if (form.value.visitType !== 'Appointment') {
    form.value.visitDateUtc = nowLocal();

    if (!form.value.followUpDate) {
      form.value.followUpDate = defaultFollowUpDate();
    }
  } else {
    form.value.followUpDate = null;
  }
}

function patientChanged() {
  const patient = patients.value.find(
    x => x.id === form.value.patientId
  );

  if (!patient) {
    return;
  }

  form.value.marketingSource =
    patient.marketingSource || 'Walk-in';

  form.value.referrerId =
    patient.referrerId || null;
}

function add() {
  errors.value = [];
  form.value = {
    patientId: null,
    doctorId: null,
    visitType: 'OPD',
    visitDateUtc: nowLocal(),
    bloodPressure: '',
    temperatureC: null,
    weightKg: null,
    heightCm: null,
    spo2: null,
    chiefComplaint: '',
    marketingSource: 'Walk-in',
    referrerId: null,
    followUpDate: defaultFollowUpDate(),
    createBill: true
  };
  show.value = true;
}

function validate() {
  const list = [];

  if (!form.value.patientId) list.push('Patient is required.');
  if (!form.value.doctorId) list.push('Doctor is required.');

  if (!form.value.marketingSource) {
    list.push('Patient source is required.');
  }

  if (
    form.value.visitType !== 'Appointment' &&
    !form.value.followUpDate
  ) {
    list.push('Follow-up date is required.');
  }

  if (form.value.visitType === 'Appointment') {
    if (!form.value.visitDateUtc) list.push('Appointment date/time is required.');
    if (form.value.visitDateUtc && new Date(form.value.visitDateUtc) < new Date()) {
      list.push('Appointment cannot be before current date/time.');
    }
  }

  if (form.value.weightKg != null && (form.value.weightKg < 1 || form.value.weightKg > 500)) list.push('Weight must be 1–500 kg.');
  if (form.value.heightCm != null && (form.value.heightCm < 30 || form.value.heightCm > 250)) list.push('Height must be 30–250 cm.');
  if (form.value.spo2 != null && (form.value.spo2 < 50 || form.value.spo2 > 100)) list.push('SpO₂ must be 50–100%.');

  errors.value = list;
  return list.length === 0;
}

async function save() {
  if (!validate()) return;

  try {
    await api.post('/opd', {
      ...form.value,
      visitDateUtc: form.value.visitType === 'Appointment'
        ? form.value.visitDateUtc
        : nowLocal()
    });

    show.value = false;
    await load();
  } catch (error) {
    errors.value = [error.response?.data?.message || 'Unable to create OPD visit.'];
  }
}

function convertToIpd(row) {
  selected.value = row;
  ipdForm.value = {
    bedId: null,
    doctorId: row.doctorId,
    payerType: 'Cash'
  };
  ipd.value = true;
}

async function saveIpd() {
  try {
    await api.post('/ipd/convert/' + selected.value.id, ipdForm.value);
    ipd.value = false;
    await load();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to convert to IPD.');
  }
}

function printableVital(label, value, suffix = '') {
  // Leave it blank (no underscores) when not recorded, so there is clean
  // empty space for the doctor to write the value by hand instead of a
  // placeholder line.
  const text = value === null || value === undefined || value === ''
    ? ''
    : `${value}${suffix}`;
  return `<div><b>${label}:</b> ${text}</div>`;
}

function printHtml(html) {
  const iframe = document.createElement('iframe');
  iframe.style.position = 'fixed';
  iframe.style.right = '0';
  iframe.style.bottom = '0';
  iframe.style.width = '0';
  iframe.style.height = '0';
  iframe.style.border = '0';
  document.body.appendChild(iframe);

  const doc = iframe.contentWindow.document;
  doc.open();
  doc.write(html);
  doc.close();

  iframe.onload = () => {
    iframe.contentWindow.focus();
    iframe.contentWindow.print();
    iframe.contentWindow.onafterprint = () => iframe.remove();
    setTimeout(() => {
      if (document.body.contains(iframe)) iframe.remove();
    }, 60000);
  };
}

async function printPrescription(row) {
  const { data } = await api.get(`/opd/${row.id}/prescription`);

  const header = data.hospital.opdHeader
    ? `<img class="header-image" src="${assetUrl(data.hospital.opdHeader)}">`
    : '';

  const vitals = [
    printableVital('BP', data.visit?.bloodPressure),
    printableVital('Temperature', data.visit?.temperatureC, ' °F'),
    printableVital('Weight', data.visit?.weightKg, ' kg'),
    printableVital('Height', data.visit?.heightCm, ' cm'),
    printableVital('SpO₂', data.visit?.spo2, '%')
  ].join('');

  printHtml(`<!doctype html>
<html>
<head>
  <title></title>
  <style>
    @page { size: A4; margin: 0; }
    * { box-sizing: border-box; }
    html, body { height: 100%; }
    body { margin: 0; width: 210mm; min-height: 297mm; display: flex; flex-direction: column; font-family: Arial, sans-serif; color: #000; }
    .header-image { display: block; width: 210mm; height: 42mm; object-fit: fill; margin: 0; }
    .content { flex: 1 0 auto; display: flex; flex-direction: column; padding: 0 12mm 10mm; }
    .patient-info { border-top: 2px solid #000; margin-bottom: 10px; }
    .pi-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0 20px; padding: 6px 0; font-size: 15px; }
    .pi-row:last-child { border-bottom: 2px solid #000; }
    .pi-row b { display: inline-block; min-width: 110px; }
    .vitals { display: grid; grid-template-columns: repeat(5, 1fr); gap: 0 12px; margin-top: 10px; padding-bottom: 14px; border-bottom: 1px solid #999; font-size: 13px; }
    .blank-space { flex: 1 0 auto; }
    .rx-footer { flex: 0 0 auto; display: flex; justify-content: flex-end; }
    .signature-block { text-align: center; min-width: 200px; }
    .signature-block .sig-name { font-weight: 700; font-size: 13px; border-top: 1px solid #333; padding-top: 4px; margin-top: 30px; }
    .signature-block .sig-qual { font-size: 12px; color: #333; }
    .signature-block .sig-caption { font-size: 11px; color: #555; margin-top: 2px; }
  </style>
</head>
<body>
  ${header}
  <div class="content">
    <div class="patient-info">
      <div class="pi-row"><span><b>Patient:</b> ${data.patient.name}</span><span><b>Patient ID:</b> ${data.patient.patientCode}</span></div>
      <div class="pi-row"><span><b>Age / Gender:</b> ${data.patient.age} / ${data.patient.gender}</span><span><b>Phone:</b> ${data.patient.phone}</span></div>
      <div class="pi-row"><span><b>Doctor:</b> ${data.doctor?.name || '-'}</span><span><b>Specialisation:</b> ${data.doctor?.specialisation || '-'}</span></div>
    </div>
    <div class="vitals">${vitals}</div>
    <div class="blank-space"></div>
    <div class="rx-footer">
      <div class="signature-block">
        <div class="sig-name">${data.doctor?.name || ''}</div>
        <div class="sig-qual">${data.doctor?.specialisation || ''}</div>
        <div class="sig-caption">Doctor Signature</div>
      </div>
    </div>
  </div>
</body>
</html>`);
}

onMounted(async () => {
  await load();
  const patientId = Number(new URLSearchParams(location.search).get('patientId'));
  if (patientId) {
    form.value.patientId = patientId;
    show.value = true;
  }
});
</script>

<template>
  <h1>OPD</h1>

  <div class="toolbar">
    <button v-if="can('OPD', 'add')" @click="add">+ Add Visit / Appointment</button>

  </div>

  <div class="toolbar report-filter">
    <label class="toggle-row"><input type="checkbox" v-model="allDates" @change="load"> All Dates</label>
    <label v-if="!allDates">From<input type="date" v-model="dateFrom"></label>
    <label v-if="!allDates">To<input type="date" v-model="dateTo"></label>
    <button v-if="!allDates" @click="load">Apply</button>
  </div>

  <div class="grid-filter-row"><GridSearch v-model="search" placeholder="Search all OPD fields..." /></div>

  <table class="table">
    <tr>
      <th class="sortable" @click="sortBy('visitNumber')">Visit <span class="sort-indicator">{{ sortIndicator('visitNumber') }}</span></th>
      <th class="sortable" @click="sortBy('visitType')">Type <span class="sort-indicator">{{ sortIndicator('visitType') }}</span></th>
      <th class="sortable" @click="sortBy('visitDateUtc')">Date <span class="sort-indicator">{{ sortIndicator('visitDateUtc') }}</span></th>
      <th class="sortable" @click="sortBy('patientName')">Patient <span class="sort-indicator">{{ sortIndicator('patientName') }}</span></th>
      <th class="sortable" @click="sortBy('doctorName')">Doctor <span class="sort-indicator">{{ sortIndicator('doctorName') }}</span></th>
      <th class="sortable" @click="sortBy('convertedToIpd')">Status <span class="sort-indicator">{{ sortIndicator('convertedToIpd') }}</span></th>
      <th></th>
    </tr>
    <tr v-for="row in pagedRows" :key="row.id">
      <td>{{ row.visitNumber }}</td>
      <td>{{ row.visitType }}</td>
      <td>{{ new Date(row.visitDateUtc).toLocaleString() }}</td>
      <td>
        {{ row.patientName }}
        <div class="muted">{{ row.patientCode }} · {{ row.phone }}</div>
      </td>
      <td>{{ row.doctorName || '-' }}</td>
      <td>{{ row.convertedToIpd ? 'Moved to IPD' : 'Active' }}</td>
      <td class="actions">
        <button @click="printPrescription(row)">Blank Prescription</button>
        <button v-if="!row.convertedToIpd" @click="convertToIpd(row)">OPD → IPD</button>
      </td>
    </tr>
  </table>
  <Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="pageSize" @update:page="page = $event" />

  <div v-if="show" class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="show = false">×</button>
      <h2>Add OPD / Emergency / Dental / Appointment</h2>

      <div v-if="errors.length" class="error-box">
        <div v-for="error in errors" :key="error">{{ error }}</div>
      </div>

      <div class="form-grid">
        <label>
          Patient *
          <select v-model.number="form.patientId" @change="patientChanged">
            <option :value="null">Select Patient</option>
            <option v-for="patient in patients" :key="patient.id" :value="patient.id">{{ patient.name }} · {{ patient.patientCode }}</option>
          </select>
        </label>

        <label>
          Type *
          <select v-model="form.visitType" @change="typeChanged">
            <option
              v-for="type in opdTypes"
              :key="type.id"
              :value="type.name"
            >
              {{ type.name }}
            </option>
          </select>
        </label>

        <label>
          Doctor *
          <select v-model.number="form.doctorId">
            <option :value="null">Select Doctor</option>
            <option v-for="doctor in doctors.filter(x => x.isActive)" :key="doctor.id" :value="doctor.id">{{ doctor.name }} · ₹{{ doctor.consultationCharge }}</option>
          </select>
        </label>

        <label>
          Date / Time
          <input
            v-model="form.visitDateUtc"
            type="datetime-local"
            :readonly="form.visitType !== 'Appointment'"
            :min="form.visitType === 'Appointment' ? nowLocal() : null"
          >
        </label>

        <label>
          Patient Source *
          <select v-model="form.marketingSource">
            <option
              v-for="source in patientSources"
              :key="source.id"
              :value="source.name"
            >
              {{ source.name }}
            </option>
          </select>
        </label>

        <label>
          Referral
          <select v-model.number="form.referrerId">
            <option :value="null">No Referrer</option>
            <option
              v-for="referrer in referrers"
              :key="referrer.id"
              :value="referrer.id"
            >
              {{ referrer.referrerCode }} · {{ referrer.name }}
            </option>
          </select>
        </label>

        <label v-if="form.visitType !== 'Appointment'">
          Follow-up Date *
          <input
            type="date"
            :min="tomorrowDate()"
            v-model="form.followUpDate"
          >
          <span class="muted">
            Default is 2 calendar days after today's consultation. Change it if the doctor requests another date.
          </span>
        </label>

        <label>BP<input v-model="form.bloodPressure" placeholder="120/80"></label>
        <label>Temperature °F<input v-model.number="form.temperatureC" type="number" step="0.1"></label>
        <label>Weight kg<input v-model.number="form.weightKg" type="number" step="0.1"></label>
        <label>Height cm<input v-model.number="form.heightCm" type="number" step="0.1"></label>
        <label>SpO₂ %<input v-model.number="form.spo2" type="number"></label>
        <label class="full">Chief Complaint<textarea v-model="form.chiefComplaint" rows="4"></textarea></label>
        <label class="toggle-row"><input v-model="form.createBill" type="checkbox"> Create bill</label>
      </div>

      <div class="modal-actions">
        <button @click="save">Save</button>
        <button class="secondary" @click="show = false">Cancel</button>
      </div>
    </div>
  </div>

  <div v-if="ipd" class="modal-bg">
    <div class="modal">
      <button class="modal-close-x" @click="ipd = false">×</button>
      <h2>Convert to IPD</h2>
      <div class="form-grid">
        <label>
          Bed/Cabin *
          <select v-model.number="ipdForm.bedId">
            <option :value="null">Select</option>
            <option v-for="bed in beds.filter(x => !x.isOccupied)" :key="bed.id" :value="bed.id">{{ bed.bedNumber }}</option>
          </select>
        </label>
        <label>
          Payer Type
          <select v-model="ipdForm.payerType">
            <option>Cash</option>
            <option>Insurance</option>
            <option>Ayushman</option>
          </select>
        </label>
        <label>
          IPD Doctor *
          <select v-model.number="ipdForm.doctorId">
            <option v-for="doctor in doctors.filter(x => x.isActive)" :key="doctor.id" :value="doctor.id">{{ doctor.name }}</option>
          </select>
        </label>
      </div>
      <div class="modal-actions">
        <button @click="saveIpd">Convert</button>
        <button class="secondary" @click="ipd = false">Cancel</button>
      </div>
    </div>
  </div>
</template>
