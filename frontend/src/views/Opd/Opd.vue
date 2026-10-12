<script setup>
import { ref, onMounted, computed, watch } from 'vue';
import { api, assetUrl } from '../../api';
import { can } from '../../auth';
import Pagination from '../../components/Pagination.vue';
import GridSearch from '../../components/GridSearch.vue';
import { useServerGrid } from '../../composables/useServerGrid';
import { ageText } from '../../utils/age';

const pageSize = 10;

const todayDate = () => new Date().toISOString().slice(0, 10);
const allDates = ref(false);
const dateFrom = ref(todayDate());
const dateTo = ref(todayDate());

// All columns shown in the grid (visitNumber/visitType/visitDateUtc/
// patientName/doctorName/convertedToIpd) come straight off the join query in
// OpdController.Get, so every one of them stays sortable/searchable in SQL -
// unlike Patients/Bills there's no post-fetch enrichment step to drop
// anything for here. No searchColumn is sent - a plain search box matches
// patient name, visit number, phone, patient code and doctor together
// server-side (see OpdController's default branch).
async function fetchOpdPage(params) {
  const dateParams = allDates.value ? {} : { from: dateFrom.value, to: dateTo.value };
  const { data } = await api.get('/opd', {
    params: {
      ...dateParams,
      page: params.page,
      pageSize: params.pageSize,
      searchText: params.searchText,
      sortBy: params.sortBy,
      sortDirection: params.sortDirection
    }
  });
  return { rows: data.rows, totalCount: data.totalCount };
}

const { page, pageCount, rows, totalCount, sortBy, sortIndicator, searchText, reload } = useServerGrid(fetchOpdPage, pageSize);
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
const editingId = ref(null);

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
  pulse: null,
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
  const [
    patientsRes,
    doctorsRes,
    bedsRes,
    referrersRes,
    sourcesRes,
    opdTypesRes
  ] = await Promise.all([
    api.get('/patients'),
    api.get('/doctors'),
    api.get('/ipd/beds'),
    api.get('/referrals'),
    api.get('/patient-sources'),
    api.get('/bill-types', { params: { opdOnly: true } }),
    reload()
  ]);

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
  editingId.value = null;
  form.value = {
    patientId: null,
    doctorId: null,
    visitType: 'OPD',
    visitDateUtc: nowLocal(),
    bloodPressure: '',
    temperatureC: null,
    pulse: null,
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

// Only the doctor/vitals/complaint/referral fields are editable afterwards -
// the visit type, date/time and any bill it already produced stay as they
// were when it was created (patient and bill amounts aren't safe to shift
// around after the fact from here).
function editVisit(row) {
  errors.value = [];
  editingId.value = row.id;
  form.value = {
    patientId: row.patientId,
    doctorId: row.doctorId,
    visitType: row.visitType,
    visitDateUtc: nowLocal(),
    bloodPressure: row.bloodPressure || '',
    temperatureC: row.temperatureC,
    pulse: row.pulse,
    weightKg: row.weightKg,
    heightCm: row.heightCm,
    spo2: row.spo2,
    chiefComplaint: row.chiefComplaint || '',
    marketingSource: row.marketingSource || 'Walk-in',
    referrerId: row.referrerId,
    followUpDate: null,
    createBill: false
  };
  show.value = true;
}

async function deleteVisit(row) {
  if (!confirm(`Delete visit ${row.visitNumber} for ${row.patientName}?`)) return;
  try {
    await api.delete(`/opd/${row.id}`);
    await load();
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to delete this visit.');
  }
}

function validate() {
  const list = [];

  if (!editingId.value && !form.value.patientId) list.push('Patient is required.');
  if (!form.value.doctorId) list.push('Doctor is required.');

  if (!form.value.marketingSource) {
    list.push('Patient source is required.');
  }

  if (
    !editingId.value &&
    form.value.visitType !== 'Appointment' &&
    !form.value.followUpDate
  ) {
    list.push('Follow-up date is required.');
  }

  if (!editingId.value && form.value.visitType === 'Appointment') {
    if (!form.value.visitDateUtc) list.push('Appointment date/time is required.');
    if (form.value.visitDateUtc && new Date(form.value.visitDateUtc) < new Date()) {
      list.push('Appointment cannot be before current date/time.');
    }
  }

  if (form.value.weightKg != null && (form.value.weightKg < 1 || form.value.weightKg > 500)) list.push('Weight must be 1–500 kg.');
  if (form.value.heightCm != null && (form.value.heightCm < 30 || form.value.heightCm > 250)) list.push('Height must be 30–250 cm.');
  if (form.value.spo2 != null && (form.value.spo2 < 50 || form.value.spo2 > 100)) list.push('SpO₂ must be 50–100%.');
  if (form.value.pulse != null && (form.value.pulse < 30 || form.value.pulse > 250)) list.push('Pulse must be 30–250 bpm.');

  errors.value = list;
  return list.length === 0;
}

async function save() {
  if (!validate()) return;

  try {
    if (editingId.value) {
      await api.put(`/opd/${editingId.value}`, form.value);
    } else {
      await api.post('/opd', {
        ...form.value,
        visitDateUtc: form.value.visitType === 'Appointment'
          ? form.value.visitDateUtc
          : nowLocal()
      });
    }

    show.value = false;
    await load();
  } catch (error) {
    errors.value = [error.response?.data?.message || `Unable to ${editingId.value ? 'update' : 'create'} OPD visit.`];
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

function printableNow() {
  const d = new Date();
  const pad = n => String(n).padStart(2, '0');
  return `${pad(d.getDate())}-${pad(d.getMonth() + 1)}-${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function printableVital(label, value, suffix = '') {
  // Leave the number blank when not recorded, so there is clean empty space
  // for the doctor to write it in by hand - but the unit always prints
  // regardless (with a blank writable gap before it when there's no value
  // yet), so filling it in by hand only ever means writing the number
  // itself, not the unit too.
  const isBlank = value === null || value === undefined || value === '';
  const text = isBlank ? '<span class="vital-blank">&nbsp;</span>' : value;
  return `<div><b>${label}:</b> ${text}${suffix}</div>`;
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
    printableVital('BP', data.visit?.bloodPressure, ' mmHg'),
    printableVital('Temperature', data.visit?.temperatureC, ' °F'),
    printableVital('Pulse', data.visit?.pulse, ' bpm'),
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
    .patient-info { border-top: 2px solid #000; border-bottom: 2px solid #000; margin-bottom: 10px; padding: 6px 0; display: grid; grid-template-columns: 1fr 1fr; gap: 0 20px; font-size: 15px; }
    .pi-col div { padding: 3px 0; }
    .pi-col b { display: inline-block; min-width: 110px; }
    .vitals { display: grid; grid-template-columns: repeat(6, 1fr); gap: 0 8px; margin-top: 10px; padding-bottom: 14px; border-bottom: 1px solid #999; font-size: 12px; white-space: nowrap; }
    .vital-blank { display: inline-block; min-width: 9mm; }
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
      <div class="pi-col">
        <div><b>Patient:</b> ${data.patient.name}</div>
        <div><b>Patient ID:</b> ${data.patient.patientCode}</div>
        <div><b>Age / Gender:</b> ${ageText(data.patient)} / ${data.patient.gender}</div>
        <div><b>Phone:</b> ${data.patient.phone}</div>
      </div>
      <div class="pi-col">
        <div><b>Appointment Time:</b> ${printableNow()}</div>
        <div><b>Doctor:</b> ${data.doctor?.name || '-'}</div>
        <div><b>Specialization:</b> ${data.doctor?.specialisation || '-'}</div>
      </div>
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

function vitalFieldHtml(label, unit) {
  return `<div><div class="vf-label">${label}</div><div class="vf-line"></div><div class="vf-unit">${unit}</div></div>`;
}

// A true blank prescription PAD, not a filled-in record of this specific
// visit - only the doctor's identity is printed (known in advance for this
// visit's doctor slot), every patient/vitals/appointment field is left blank
// for the doctor to write in by hand during consultation. Kept as a
// separate button/function from printPrescription (which fills in the
// patient's actual details) rather than replacing it.
async function printNewBlankPrescription(row) {
  const { data } = await api.get(`/opd/${row.id}/prescription`);

  // No pre-made header image here (unlike every other print in this app) -
  // the hospital logo and trust logo are two separate uploaded images
  // placed left/right of an HTML-built name/address/contact block, so the
  // text stays editable via Settings without needing a new banner image
  // exported every time it changes.
  const hospitalLogo = data.hospital.logo
    ? `<img src="${assetUrl(data.hospital.logo)}">`
    : '';
  const trustLogo = data.hospital.trustLogo
    ? `<img src="${assetUrl(data.hospital.trustLogo)}">`
    : '';

  const watermark = data.hospital.logo
    ? `<img class="watermark" src="${assetUrl(data.hospital.logo)}">`
    : '';

  const vitals = [
    vitalFieldHtml('BP', 'mmHg'),
    vitalFieldHtml('Temp', '°F'),
    vitalFieldHtml('Pulse', 'bpm'),
    vitalFieldHtml('Weight', 'kg'),
    vitalFieldHtml('Height', 'cm'),
    vitalFieldHtml('SpO₂', '%')
  ].join('');

  printHtml(`<!doctype html>
<html>
<head>
  <title></title>
  <style>
    @page { size: A4; margin: 0; }
    * { box-sizing: border-box; }
    html, body { height: 100%; }
    body { margin: 0; width: 210mm; min-height: 297mm; display: flex; flex-direction: column; font-family: Arial, sans-serif; color: #000; position: relative; }
    .rx-header { display: flex; align-items: center; gap: 10px; padding: 6mm 10mm 4mm; border-bottom: 3px solid #2e7d32; }
    .rx-header .logo-box { flex: 0 0 32mm; height: 24mm; display: flex; align-items: center; justify-content: center; }
    .rx-header .logo-box img { max-width: 100%; max-height: 100%; object-fit: contain; }
    .rx-header .hospital-info { flex: 1; text-align: center; padding: 0 6px; }
    .rx-header .hospital-name { font-size: 26px; font-weight: 800; color: #14387a; margin: 0; }
    .rx-header .tagline { font-size: 12px; color: #2e7d32; font-weight: 700; margin: 2px 0 6px; letter-spacing: 0.5px; }
    .rx-header .address-row { font-size: 11.5px; color: #333; margin-bottom: 2px; }
    .rx-header .contact-row { font-size: 11.5px; color: #333; display: flex; justify-content: center; gap: 18px; flex-wrap: wrap; }
    .content { flex: 1 0 auto; display: flex; flex-direction: column; padding: 6mm 12mm 10mm; position: relative; z-index: 1; }
    .watermark { position: absolute; left: -20mm; bottom: -10mm; width: 90mm; opacity: 0.08; pointer-events: none; z-index: 0; }
    .info-box { background: #eef3f9; border-radius: 10px; padding: 12px 18px; display: grid; grid-template-columns: 1fr 1fr; gap: 8px 24px; font-size: 14px; margin-bottom: 14px; }
    .info-box .field { display: flex; align-items: baseline; gap: 6px; }
    .info-box .field b { min-width: 120px; flex: 0 0 auto; }
    .info-box .field .line { flex: 1; border-bottom: 1px solid #333; min-height: 14px; }
    .info-box .field .filled { flex: 1; border-bottom: 1px solid #333; padding-bottom: 1px; }
    .vitals-box { display: flex; align-items: stretch; border: 1px solid #ccc; border-radius: 8px; margin-bottom: 20px; overflow: hidden; }
    .vitals-badge { background: #2d6cdf; color: #fff; font-weight: 700; font-size: 13px; display: flex; align-items: center; justify-content: center; padding: 0 16px; white-space: nowrap; }
    .vitals-fields { flex: 1; display: grid; grid-template-columns: repeat(6, 1fr); gap: 0 10px; padding: 10px 14px; font-size: 12px; }
    .vitals-fields .vf-label { font-weight: 700; margin-bottom: 20px; }
    .vitals-fields .vf-line { border-bottom: 1px solid #333; height: 14px; }
    .vitals-fields .vf-unit { text-align: center; color: #555; margin-top: 2px; }
    .rx-symbol { font-family: Georgia, 'Times New Roman', serif; font-size: 44px; font-weight: 700; color: #1a4b96; margin: 2px 0 0; }
    .blank-space { flex: 1 0 auto; }
    .rx-footer { flex: 0 0 auto; display: flex; justify-content: flex-end; position: relative; z-index: 1; }
    .signature-block { text-align: center; min-width: 220px; }
    .signature-block .sig-line { border-top: 1px solid #333; margin-top: 40px; padding-top: 4px; }
    .signature-block .sig-caption { font-size: 12px; color: #555; }
    .signature-block .sig-name { font-weight: 700; font-size: 15px; margin-top: 10px; }
    .signature-block .sig-qual { font-size: 11.5px; color: #333; max-width: 260px; }
  </style>
</head>
<body>
  <div class="rx-header">
    <div class="logo-box">${hospitalLogo}</div>
    <div class="hospital-info">
      <div class="hospital-name">${data.hospital.name || ''}</div>
      <div class="tagline">A Multi-Speciality Hospital</div>
      <div class="address-row">📍 ${data.hospital.address || ''}</div>
      <div class="contact-row">
        <span>📞 ${data.hospital.phone || ''}</span>
        <span>✉ ${data.hospital.email || ''}</span>
      </div>
    </div>
    <div class="logo-box">${trustLogo}</div>
  </div>
  <div class="content">
    ${watermark}
    <div class="info-box">
      <div class="field"><b>Patient Name</b><span class="line"></span></div>
      <div class="field"><b>Appt. Dt. &amp; Time</b><span class="line"></span></div>
      <div class="field"><b>Patient ID</b><span class="line"></span></div>
      <div class="field"><b>Doctor</b><span class="filled">${data.doctor?.name || ''}</span></div>
      <div class="field"><b>Age / Gender</b><span class="line"></span></div>
      <div class="field"><b>Specialization</b><span class="filled">${data.doctor?.specialisation || ''}</span></div>
      <div class="field"><b>Phone</b><span class="line"></span></div>
      <div></div>
    </div>
    <div class="vitals-box">
      <div class="vitals-badge">VITALS</div>
      <div class="vitals-fields">${vitals}</div>
    </div>
    <div class="rx-symbol">℞</div>
    <div class="blank-space"></div>
    <div class="rx-footer">
      <div class="signature-block">
        <div class="sig-line"><div class="sig-caption">Doctor's Signature</div></div>
        <div class="sig-name">${data.doctor?.name || ''}</div>
        <div class="sig-qual">${data.doctor?.specialisation || ''}</div>
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

  <div class="grid-filter-row">
    <GridSearch v-model="searchText" placeholder="Search patient name, visit number, phone, patient code, doctor..." />
  </div>

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
    <tr v-for="row in rows" :key="row.id">
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
        <!-- Temporarily hidden while the new design is finalized - printNewBlankPrescription(row) still works, just re-add this button to bring it back.
        <button @click="printNewBlankPrescription(row)">New Blank Prescription</button>
        -->
        <button v-if="!row.convertedToIpd" @click="convertToIpd(row)">OPD → IPD</button>
        <button v-if="can('OPD', 'edit')" @click="editVisit(row)">Edit</button>
        <button v-if="can('OPD', 'delete')" class="danger-btn" @click="deleteVisit(row)">Delete</button>
      </td>
    </tr>
  </table>
  <Pagination :page="page" :page-count="pageCount" :total="totalCount" :page-size="pageSize" @update:page="page = $event" />

  <div v-if="show" class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="show = false">×</button>
      <h2>{{ editingId ? 'Edit Visit' : 'Add OPD / Emergency / Dental / Appointment' }}</h2>

      <div v-if="errors.length" class="error-box">
        <div v-for="error in errors" :key="error">{{ error }}</div>
      </div>

      <div class="form-grid">
        <label>
          Patient *
          <select v-model.number="form.patientId" @change="patientChanged" :disabled="!!editingId">
            <option :value="null">Select Patient</option>
            <option v-for="patient in patients" :key="patient.id" :value="patient.id">{{ patient.name }} · {{ patient.patientCode }}</option>
          </select>
        </label>

        <label>
          Type *
          <select v-model="form.visitType" @change="typeChanged" :disabled="!!editingId">
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

        <label v-if="!editingId">
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

        <label v-if="!editingId && form.visitType !== 'Appointment'">
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

        <label>BP (mmHg)<input v-model="form.bloodPressure" placeholder="120/80"></label>
        <label>Temperature °F<input v-model.number="form.temperatureC" type="number" step="0.1"></label>
        <label>Pulse (bpm)<input v-model.number="form.pulse" type="number" min="30" max="250"></label>
        <label>Weight kg<input v-model.number="form.weightKg" type="number" step="0.1"></label>
        <label>Height cm<input v-model.number="form.heightCm" type="number" step="0.1"></label>
        <label>SpO₂ %<input v-model.number="form.spo2" type="number"></label>
        <label class="full">Chief Complaint<textarea v-model="form.chiefComplaint" rows="4"></textarea></label>
        <label v-if="!editingId" class="toggle-row"><input v-model="form.createBill" type="checkbox"> Create bill</label>
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
