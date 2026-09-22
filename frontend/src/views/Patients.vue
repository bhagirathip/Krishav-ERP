<script setup>
import { ref, onMounted, computed, watch } from 'vue';
import { api, assetUrl } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';
import { ageText } from '../utils/age';

const rows = ref([]);
const pageSize = 10;
const { search, page, pageCount, pagedRows, sortedRows, sortBy, sortIndicator } = useGrid(rows, pageSize);
const doctors = ref([]);
const beds = ref([]);
const categories = ref([]);
const referrers = ref([]);
const marketingSources = ref([]);
const billTypes = ref([]);
const opdBillTypes = ref([]);
const showOpdBill = ref(false);
const opdBillPatient = ref(null);
const show = ref(false);
const editMode = ref(false);
const errors = ref([]);
const duplicate = ref(null);
const documentsPatient = ref(null);
const documentFiles = ref([]);
const documentCategory = ref('');
const documents = ref([]);
const categoryModal = ref(false);
const categoryForm = ref({ name: '' });
const followUpPatient = ref(null);
const patientFollowUps = ref([]);

const blank = {
  name: '',
  age: null,
  ageUnit: 'Years',
  gender: '',
  phone: '',
  address: '',
  registrationType: 'OPD',
  doctorId: null,
  bedId: null,
  payerType: 'Cash',
  otRoom: 'OT 1',
  otStartAtUtc: null,
  otEndAtUtc: null,
  visitDateUtc: null,
  bloodPressure: '',
  temperatureC: null,
  weightKg: null,
  heightCm: null,
  spo2: null,
  chiefComplaint: '',
  confirmDuplicatePhone: false,
  marketingSource: 'Walk-in',
  referrerId: null,
  createBill: true,
  billDate: null
};

const form = ref({ ...blank });

// Clear a stale validation error as soon as the user changes anything,
// instead of leaving an outdated message on screen until they hit Save
// again - the next Save re-validates the fresh data from scratch anyway.
watch(form, () => {
  if (errors.value.length) errors.value = [];
}, { deep: true });

const localDateTime = () => {
  const date = new Date();
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
  return date.toISOString().slice(0, 16);
};

const today = () => new Date().toISOString().slice(0, 10);

const defaultFollowUpDate = () => {
  const date = new Date();
  date.setDate(date.getDate() + 2);
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
  return date.toISOString().slice(0, 10);
};

const opdBillForm = ref({
  patientId: null,
  doctorId: null,
  visitType: 'OPD',
  visitDateUtc: localDateTime(),
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

async function load() {
  const [
    patientsRes,
    doctorsRes,
    bedsRes,
    categoriesRes,
    referrersRes,
    sourcesRes,
    billTypesRes,
    opdBillTypesRes
  ] = await Promise.all([
    api.get('/patients'),
    api.get('/doctors'),
    api.get('/ipd/beds'),
    api.get('/document-categories'),
    api.get('/referrals'),
    api.get('/referrals/sources'),
    api.get('/bill-types'),
    api.get('/bill-types', {
      params: {
        opdOnly: true,
        billableOnly: true
      }
    })
  ]);

  rows.value = patientsRes.data;
  doctors.value = doctorsRes.data;
  beds.value = bedsRes.data;
  categories.value = categoriesRes.data;
  referrers.value = referrersRes.data;
  marketingSources.value = sourcesRes.data;
  billTypes.value = billTypesRes.data;
  opdBillTypes.value = opdBillTypesRes.data;

  if (!documentCategory.value && categories.value.length) {
    documentCategory.value = categories.value[0].name;
  }
}

function add() {
  editMode.value = false;
  errors.value = [];
  form.value = {
    ...blank,
    registrationType:
      billTypes.value.find(x => x.name === 'OPD')?.name ||
      billTypes.value[0]?.name ||
      'OPD',
    billDate: today()
  };
  show.value = true;
}

// Defaults the appointment date/time to right now (like OPD/Emergency
// implicitly use the current moment) so the field never starts blank -
// the user can then move it to whenever the appointment is actually for.
function registrationTypeChanged() {
  if (form.value.registrationType === 'Appointment' && !form.value.visitDateUtc) {
    form.value.visitDateUtc = localDateTime();
  }
}

function edit(patient) {
  editMode.value = true;
  errors.value = [];
  form.value = {
    ...blank,
    id: patient.id,
    name: patient.name,
    age: patient.age,
    ageUnit: patient.ageUnit || 'Years',
    gender: patient.gender,
    phone: patient.phone,
    address: patient.address || '',
    registrationType: patient.registrationType,
    doctorId: patient.doctorId,
    bloodPressure: patient.bloodPressure || '',
    temperatureC: patient.temperatureC,
    weightKg: patient.weightKg,
    heightCm: patient.heightCm,
    spo2: patient.spo2,
    chiefComplaint: patient.chiefComplaint || '',
    marketingSource: patient.marketingSource || 'Walk-in',
    referrerId: patient.referrerId || null
  };
  show.value = true;
}

function validate() {
  const list = [];

  if (!form.value.name?.trim()) list.push('Patient name is required.');
  if (form.value.age === null || form.value.age < 0) list.push('Valid age is required.');
  if (!form.value.gender) list.push('Gender is required.');
  if (!/^[6-9]\d{9}$/.test(form.value.phone || '')) list.push('Phone number must be a valid 10-digit Indian mobile number.');
  if (['OPD', 'Emergency', 'Dental', 'IPD', 'OT', 'Appointment'].includes(form.value.registrationType) && !form.value.doctorId) list.push('Doctor is required.');
  if (form.value.registrationType === 'IPD' && !form.value.bedId && !editMode.value) list.push('Bed/Cabin is required.');
  if (form.value.registrationType === 'OT' && (!form.value.otStartAtUtc || !form.value.otEndAtUtc)) list.push('OT start and end time are required.');
  if (form.value.registrationType === 'Appointment') {
    if (!form.value.visitDateUtc) list.push('Appointment date/time is required.');
    else if (new Date(form.value.visitDateUtc) < new Date()) list.push('Appointment cannot be before current date/time.');
  }
  if (form.value.heightCm != null && (form.value.heightCm < 30 || form.value.heightCm > 250)) list.push('Height must be between 30 and 250 cm.');
  if (form.value.weightKg != null && (form.value.weightKg < 1 || form.value.weightKg > 500)) list.push('Weight must be between 1 and 500 kg.');
  if (form.value.spo2 != null && (form.value.spo2 < 50 || form.value.spo2 > 100)) list.push('SpO₂ must be between 50 and 100%.');

  errors.value = list;
  return list.length === 0;
}

function payload() {
  const isOpd = form.value.registrationType === 'OPD';

  return {
    ...form.value,
    otStartAtUtc: form.value.registrationType === 'OT' && form.value.otStartAtUtc ? form.value.otStartAtUtc : null,
    otEndAtUtc: form.value.registrationType === 'OT' && form.value.otEndAtUtc ? form.value.otEndAtUtc : null,
    visitDateUtc: form.value.registrationType === 'Appointment' ? form.value.visitDateUtc : null,
    bedId: form.value.registrationType === 'IPD' ? form.value.bedId : null,
    createBill: isOpd ? form.value.createBill : false,
    billDate: isOpd && form.value.createBill && form.value.billDate ? form.value.billDate : null
  };
}

async function save() {
  if (!validate()) return;

  try {
    if (editMode.value) {
      await api.put('/patients/' + form.value.id, payload());
    } else {
      await api.post('/patients', payload());
    }

    show.value = false;
    duplicate.value = null;
    await load();
  } catch (error) {
    if (error.response?.status === 409 && error.response?.data?.code === 'DUPLICATE_PHONE') {
      duplicate.value = error.response.data;
      return;
    }

    errors.value = error.response?.data?.errors || [error.response?.data?.message || 'Unable to save patient.'];
  }
}

async function addAnyway() {
  form.value.confirmDuplicatePhone = true;
  duplicate.value = null;
  await save();
}

function existingOpd(patient) {
  show.value = false;
  duplicate.value = null;
  location.href = '/opd?patientId=' + patient.id;
}

async function deletePatient(patient) {
  if (!confirm('Remove patient from active list?')) return;
  await api.delete('/patients/' + patient.id);
  await load();
}

async function openDocuments(patient) {
  documentsPatient.value = patient;
  documents.value = (await api.get(`/patients/${patient.id}/documents`)).data;
  documentFiles.value = [];
}

async function uploadDocuments() {
  if (!documentFiles.value.length || !documentCategory.value) return;

  const data = new FormData();
  for (const file of documentFiles.value) data.append('files', file);
  data.append('category', documentCategory.value);

  await api.post(`/patients/${documentsPatient.value.id}/documents`, data);
  documents.value = (await api.get(`/patients/${documentsPatient.value.id}/documents`)).data;
  documentFiles.value = [];
}

function addCategory() {
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
    await api.put(`/document-categories/${categoryForm.value.id}`, categoryForm.value);
  } else {
    await api.post('/document-categories', categoryForm.value);
  }

  categoryModal.value = false;
  await load();
}

async function deleteCategory(category) {
  if (!confirm(`Delete category ${category.name}?`)) return;
  await api.delete(`/document-categories/${category.id}`);
  await load();
}


async function openFollowUps(patient) {
  followUpPatient.value = patient;
  const { data } = await api.get(`/followups/patient/${patient.id}`);
  patientFollowUps.value = data.followUps || [];
}


function openOpdBill(patient) {
  opdBillPatient.value = patient;

  opdBillForm.value = {
    patientId: patient.id,
    doctorId: patient.doctorId || null,
    visitType:
      opdBillTypes.value.find(x => x.name === 'OPD')?.name ||
      opdBillTypes.value[0]?.name ||
      'OPD',
    visitDateUtc: localDateTime(),
    bloodPressure: patient.bloodPressure || '',
    temperatureC: patient.temperatureC,
    weightKg: patient.weightKg,
    heightCm: patient.heightCm,
    spo2: patient.spo2,
    chiefComplaint: patient.chiefComplaint || '',
    marketingSource: patient.marketingSource || 'Walk-in',
    referrerId: patient.referrerId || null,
    followUpDate: defaultFollowUpDate(),
    createBill: true
  };

  showOpdBill.value = true;
}

async function createOpdBillFromPatient() {
  if (!opdBillForm.value.doctorId) {
    alert('Doctor is required.');
    return;
  }

  if (!opdBillForm.value.visitType) {
    alert('OPD bill type is required.');
    return;
  }

  try {
    await api.post('/opd', {
      ...opdBillForm.value,
      visitDateUtc: localDateTime(),
      createBill: true
    });

    showOpdBill.value = false;
    await load();
    alert('OPD visit and bill created successfully.');
  } catch (error) {
    alert(
      error.response?.data?.message ||
      'Unable to create OPD bill.'
    );
  }
}

onMounted(load);
</script>

<template>
  <h1>Patient</h1>

  <div class="toolbar">
    <button v-if="can('PATIENT', 'add')" @click="add">+ Add Patient</button>
    <button v-if="can('PATIENT', 'edit')" class="secondary" @click="addCategory">Document Category Master</button>

  </div>

  <div class="grid-filter-row"><GridSearch v-model="search" placeholder="Search all patient fields..." /></div>

  <table class="table">
    <tr>
      <th class="sortable" @click="sortBy('patientCode')">ID <span class="sort-indicator">{{ sortIndicator('patientCode') }}</span></th>
      <th class="sortable" @click="sortBy('name')">Name <span class="sort-indicator">{{ sortIndicator('name') }}</span></th>
      <th class="sortable" @click="sortBy('age')">Age/Gender <span class="sort-indicator">{{ sortIndicator('age') }}</span></th>
      <th class="sortable" @click="sortBy('phone')">Phone <span class="sort-indicator">{{ sortIndicator('phone') }}</span></th>
      <th class="sortable" @click="sortBy('registrationType')">Type <span class="sort-indicator">{{ sortIndicator('registrationType') }}</span></th>
      <th class="sortable" @click="sortBy('doctorName')">Doctor <span class="sort-indicator">{{ sortIndicator('doctorName') }}</span></th>
      <th class="sortable" @click="sortBy('marketingSource')">Source <span class="sort-indicator">{{ sortIndicator('marketingSource') }}</span></th>
      <th class="sortable" @click="sortBy('referrerName')">Referrer <span class="sort-indicator">{{ sortIndicator('referrerName') }}</span></th>
      <th>Bed / OT</th>
      <th></th>
    </tr>
    <tr v-for="patient in pagedRows" :key="patient.id">
      <td>{{ patient.patientCode }}</td>
      <td>{{ patient.name }}</td>
      <td>{{ ageText(patient) }} / {{ patient.gender }}</td>
      <td>{{ patient.phone }}</td>
      <td>{{ patient.registrationType }}</td>
      <td>{{ patient.doctorName || '-' }}</td>
      <td>{{ patient.marketingSource || 'Walk-in' }}</td>
      <td>{{ patient.referrerName || '-' }}</td>
      <td>
        {{ patient.bedNumber || patient.otRoom || '-' }}
        <div v-if="patient.otStartAtUtc" class="muted">{{ new Date(patient.otStartAtUtc).toLocaleString() }}</div>
      </td>
      <td class="actions">
        <button v-if="can('PATIENT', 'edit')" @click="edit(patient)">Edit</button>
        <button @click="openDocuments(patient)">Documents</button>
        <button @click="openFollowUps(patient)">Follow-ups</button>
        <button v-if="can('OPD', 'add')" @click="openOpdBill(patient)">Create OPD Bill</button>
        <button v-if="can('PATIENT', 'delete')" class="danger-btn" @click="deletePatient(patient)">Delete</button>
      </td>
    </tr>
  </table>
  <Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="pageSize" @update:page="page = $event" />

  <div v-if="show" class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="show = false">×</button>
      <h2>{{ editMode ? 'Edit Patient' : 'Add Patient' }}</h2>

      <div v-if="errors.length" class="error-box">
        <div v-for="error in errors" :key="error">{{ error }}</div>
      </div>

      <div class="form-grid">
        <label>Name *<input v-model="form.name"></label>
        <label>Age *<input v-model.number="form.age" type="number" min="0"></label>
        <label>
          Age Unit
          <select v-model="form.ageUnit">
            <option>Years</option>
            <option>Months</option>
            <option>Weeks</option>
            <option>Days</option>
          </select>
        </label>
        <label>
          Gender *
          <select v-model="form.gender">
            <option value="">Select</option>
            <option>Male</option>
            <option>Female</option>
            <option>Other</option>
          </select>
        </label>
        <label>Phone *<input v-model="form.phone" maxlength="10" placeholder="10-digit mobile"></label>
        <label class="full">Address<textarea v-model="form.address" rows="2"></textarea></label>
        <label>
          Patient Source *
          <select v-model="form.marketingSource">
            <option v-for="source in marketingSources" :key="source" :value="source">{{ source }}</option>
          </select>
        </label>
        <label>
          Referrer
          <select v-model.number="form.referrerId">
            <option :value="null">No Referrer</option>
            <option v-for="referrer in referrers" :key="referrer.id" :value="referrer.id">{{ referrer.referrerCode }} · {{ referrer.name }}</option>
          </select>
        </label>

        <label>
          Type *
          <select v-model="form.registrationType" :disabled="editMode" @change="registrationTypeChanged">
            <option
              v-for="type in billTypes"
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
            <option v-for="doctor in doctors.filter(x => x.isActive)" :key="doctor.id" :value="doctor.id">{{ doctor.name }}</option>
          </select>
        </label>

        <label v-if="form.registrationType === 'Appointment' && !editMode">
          Appointment Date / Time *
          <input v-model="form.visitDateUtc" type="datetime-local" :min="localDateTime()">
        </label>

        <template v-if="form.registrationType === 'IPD' && !editMode">
          <label>
            Bed/Cabin *
            <select v-model.number="form.bedId">
              <option :value="null">Select</option>
              <option v-for="bed in beds.filter(x => !x.isOccupied)" :key="bed.id" :value="bed.id">{{ bed.bedNumber }} · {{ bed.roomType }}</option>
            </select>
          </label>
          <label>
            Payer Type
            <select v-model="form.payerType">
              <option>Cash</option>
              <option>Insurance</option>
              <option>Ayushman</option>
            </select>
          </label>
        </template>

        <template v-if="form.registrationType === 'OT' && !editMode">
          <label>
            OT Number
            <select v-model="form.otRoom">
              <option>OT 1</option>
              <option>OT 2</option>
            </select>
          </label>
          <label>OT Start<input v-model="form.otStartAtUtc" type="datetime-local"></label>
          <label>OT End<input v-model="form.otEndAtUtc" type="datetime-local"></label>
        </template>

        <template v-if="form.registrationType === 'OPD' && !editMode">
          <label class="toggle-row"><input v-model="form.createBill" type="checkbox"> Create Bill</label>
          <label v-if="form.createBill">
            Bill Date
            <input v-model="form.billDate" type="date" :max="today()">
          </label>
        </template>

        <label>BP<input v-model="form.bloodPressure" placeholder="120/80"></label>
        <label>Temperature °F<input v-model.number="form.temperatureC" type="number" step="0.1"></label>
        <label>Weight (kg)<input v-model.number="form.weightKg" type="number" min="1" max="500" step="0.1"></label>
        <label>Height (cm)<input v-model.number="form.heightCm" type="number" min="30" max="250" step="0.1"></label>
        <label>SpO₂ (%)<input v-model.number="form.spo2" type="number" min="50" max="100"></label>
        <label class="full">Chief Complaint<textarea v-model="form.chiefComplaint" rows="4"></textarea></label>
      </div>

      <div class="modal-actions">
        <button @click="save">Save</button>
        <button class="secondary" @click="show = false">Cancel</button>
      </div>
    </div>
  </div>

  <div v-if="duplicate" class="modal-bg">
    <div class="modal">
      <button class="modal-close-x" @click="duplicate = null">×</button>
      <h2>Phone Number Already Exists</h2>
      <p>This mobile number already belongs to:</p>

      <div v-for="patient in duplicate.patients" :key="patient.id" class="card">
        <b>{{ patient.name }}</b> · {{ patient.patientCode }}
        <div class="muted">{{ ageText(patient) }} / {{ patient.gender }} · {{ patient.phone }}</div>
        <button @click="existingOpd(patient)">Book New OPD for Existing Patient</button>
      </div>

      <div class="modal-actions">
        <button @click="addAnyway">Add New Patient Anyway</button>
        <button class="secondary" @click="duplicate = null">Cancel</button>
      </div>
    </div>
  </div>

  <div v-if="documentsPatient" class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="documentsPatient = null">×</button>
      <h2>Documents · {{ documentsPatient.name }}</h2>

      <div class="form-grid">
        <label>
          Category
          <select v-model="documentCategory">
            <option v-for="category in categories" :key="category.id" :value="category.name">{{ category.name }}</option>
          </select>
        </label>
        <label>
          Files
          <input type="file" multiple accept=".pdf,.jpg,.jpeg,.png" @change="documentFiles = [...$event.target.files]">
        </label>
      </div>

      <div class="modal-actions left-actions">
        <button @click="uploadDocuments">Upload Selected Documents</button>
      </div>

      <table class="table">
        <tr><th>File</th><th>Category</th><th>Date</th></tr>
        <tr v-for="document in documents" :key="document.id">
          <td><a :href="assetUrl(document.storedPath)" target="_blank">{{ document.fileName }}</a></td>
          <td>{{ document.category }}</td>
          <td>{{ new Date(document.uploadedAtUtc).toLocaleString() }}</td>
        </tr>
      </table>

      <div class="modal-actions">
        <button class="secondary" @click="documentsPatient = null">Close</button>
      </div>
    </div>
  </div>



  <div v-if="showOpdBill" class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="showOpdBill = false">×</button>
      <div class="page-head">
        <div>
          <h2>Create OPD Bill</h2>
          <div class="muted" v-if="opdBillPatient">
            {{ opdBillPatient.patientCode }} ·
            {{ opdBillPatient.name }} ·
            {{ ageText(opdBillPatient) }}/{{ opdBillPatient.gender }} ·
            {{ opdBillPatient.phone }}
          </div>
        </div>

        <button
          class="secondary"
          @click="showOpdBill = false"
        >
          Close
        </button>
      </div>

      <div class="form-grid">
        <label>
          OPD Type *
          <select v-model="opdBillForm.visitType">
            <option
              v-for="type in opdBillTypes"
              :key="type.id"
              :value="type.name"
            >
              {{ type.name }}
            </option>
          </select>
        </label>

        <label>
          Doctor *
          <select v-model.number="opdBillForm.doctorId">
            <option :value="null">Select Doctor</option>
            <option
              v-for="doctor in doctors.filter(x => x.isActive)"
              :key="doctor.id"
              :value="doctor.id"
            >
              {{ doctor.name }}
            </option>
          </select>
        </label>

        <label>
          Patient Source
          <select v-model="opdBillForm.marketingSource">
            <option
              v-for="source in marketingSources"
              :key="source"
              :value="source"
            >
              {{ source }}
            </option>
          </select>
        </label>

        <label>
          Referrer
          <select v-model.number="opdBillForm.referrerId">
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

        <label>
          BP
          <input v-model="opdBillForm.bloodPressure" placeholder="120/80">
        </label>

        <label>
          Temperature °F
          <input type="number" step="0.1" v-model.number="opdBillForm.temperatureC">
        </label>

        <label>
          Weight Kg
          <input type="number" step="0.1" v-model.number="opdBillForm.weightKg">
        </label>

        <label>
          Height cm
          <input type="number" step="0.1" v-model.number="opdBillForm.heightCm">
        </label>

        <label>
          SpO₂ %
          <input type="number" v-model.number="opdBillForm.spo2">
        </label>

        <label>
          Follow-up Date
          <input type="date" v-model="opdBillForm.followUpDate">
        </label>

        <label class="full">
          Chief Complaint
          <textarea rows="4" v-model="opdBillForm.chiefComplaint"></textarea>
        </label>
      </div>

      <div class="modal-actions">
        <button @click="createOpdBillFromPatient">
          Create OPD Bill
        </button>

        <button
          class="secondary"
          @click="showOpdBill = false"
        >
          Cancel
        </button>
      </div>
    </div>
  </div>

  <div v-if="followUpPatient" class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="followUpPatient = null">×</button>
      <div class="page-head">
        <div>
          <h2>Follow-ups · {{ followUpPatient.name }}</h2>
          <div class="muted">
            {{ followUpPatient.patientCode }} · {{ followUpPatient.phone }}
          </div>
        </div>
        <button class="secondary" @click="followUpPatient = null">Close</button>
      </div>

      <table class="table">
        <tr>
          <th>Follow-up Date</th>
          <th>Status</th>
          <th>Comment</th>
          <th>Next Follow-up</th>
          <th>Completed</th>
        </tr>

        <tr v-for="row in patientFollowUps" :key="row.id">
          <td>{{ String(row.followUpDate).slice(0, 10) }}</td>
          <td>{{ row.status }}</td>
          <td>{{ row.comment || '-' }}</td>
          <td>
            {{
              row.nextFollowUpDate
                ? String(row.nextFollowUpDate).slice(0, 10)
                : '-'
            }}
          </td>
          <td>
            {{
              row.completedAtUtc
                ? new Date(row.completedAtUtc).toLocaleString()
                : '-'
            }}
          </td>
        </tr>
      </table>

      <div v-if="!patientFollowUps.length" class="muted">
        No follow-up records for this patient.
      </div>

      <div class="modal-actions">
        <button class="secondary" @click="followUpPatient = null">Close</button>
      </div>
    </div>
  </div>

  <div v-if="categoryModal" class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="categoryModal = false">×</button>
      <h2>Document Category Master</h2>

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
