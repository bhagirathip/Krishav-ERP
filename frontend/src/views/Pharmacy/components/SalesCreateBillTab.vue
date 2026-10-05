<script setup>
import { can } from '../../../auth';
import Pagination from '../../../components/Pagination.vue';
import { ageText } from '../../../utils/age';

defineProps({
  patients: { type: Array, default: () => [] },
  doctors: { type: Array, default: () => [] },
  discounts: { type: Array, default: () => [] },
  selectedPatient: { type: Object, default: null },
  selectedDoctorName: { type: String, default: '-' },
  pagedBillSearch: { type: Array, default: () => [] },
  sortedBillSearch: { type: Array, default: () => [] },
  billSearchPageCount: { type: Number, default: 1 },
  pageSize: { type: Number, required: true },
  sortBillSearch: { type: Function, required: true },
  billSortIndicator: { type: Function, required: true },
  addToCart: { type: Function, required: true },
  cart: { type: Array, default: () => [] },
  updateUnit: { type: Function, required: true },
  allowedUnits: { type: Function, required: true },
  maxQuantity: { type: Function, required: true },
  rowGross: { type: Function, required: true },
  rowTotal: { type: Function, required: true },
  removeCart: { type: Function, required: true },
  totals: { type: Object, required: true },
  createSale: { type: Function, required: true }
});

const patientId = defineModel('patientId');
const doctorId = defineModel('doctorId');
const doctorMode = defineModel('doctorMode');
const outsideDoctorName = defineModel('outsideDoctorName');
const walkInPatientName = defineModel('walkInPatientName');
const walkInPhone = defineModel('walkInPhone');
const paymentMode = defineModel('paymentMode');
const roundOff = defineModel('roundOff');
const billSearch = defineModel('billSearch');
const billSearchPage = defineModel('billSearchPage');
</script>

<template>
  <div class="card">
    <h2>Create Pharmacy Bill</h2>

    <div class="form-grid">
      <label>Patient<select v-model.number="patientId"><option :value="null">Walk-in Patient</option><option v-for="p in patients" :key="p.id" :value="p.id">{{ p.name }} · {{ p.patientCode }} · {{ p.phone }}</option></select></label>
      <label>Payment Mode<select v-model="paymentMode"><option>Cash</option><option>UPI</option><option>Card</option><option>Bank Transfer</option><option>Other</option></select></label>
    </div>

    <div v-if="patientId && selectedPatient" class="card patient-summary-card">
      <b>{{ selectedPatient.name }}</b> · {{ selectedPatient.patientCode }}<br>
      Age/Gender: {{ ageText(selectedPatient) }}/{{ selectedPatient.gender }} · Phone: {{ selectedPatient.phone }} · Doctor: {{ selectedPatient.doctorName || '-' }}
    </div>

    <div v-else class="form-grid">
      <label>Walk-in Patient Name *<input v-model="walkInPatientName"></label>
      <label>Phone<input v-model="walkInPhone"></label>
    </div>

    <div class="form-grid">
      <label>Doctor Source
        <select v-model="doctorMode" @change="if(doctorMode!=='list') doctorId=null; if(doctorMode!=='outside') outsideDoctorName=''">
          <option value="none">No Doctor</option>
          <option value="list">Select Existing Doctor</option>
          <option value="outside">Outside Doctor</option>
        </select>
      </label>
      <label v-if="doctorMode==='list'">Doctor
        <select v-model.number="doctorId">
          <option :value="null">Select Doctor</option>
          <option v-for="d in doctors.filter(x=>x.isActive)" :key="d.id" :value="d.id">{{ d.name }}</option>
        </select>
      </label>
      <label v-if="doctorMode==='outside'">Outside Doctor Name
        <input v-model="outsideDoctorName" placeholder="Enter doctor name">
      </label>
    </div>

    <div class="toolbar"><input style="max-width:520px" v-model="billSearch" placeholder="Search product, MF, invoice, type, batch or HSN"></div>
    <table class="table">
      <tr><th class="sortable" @click="sortBillSearch('medicineType')">Type <span class="sort-indicator">{{billSortIndicator('medicineType')}}</span></th><th class="sortable" @click="sortBillSearch('productName')">Product <span class="sort-indicator">{{billSortIndicator('productName')}}</span></th><th class="sortable" @click="sortBillSearch('manufacturer')">MF <span class="sort-indicator">{{billSortIndicator('manufacturer')}}</span></th><th class="sortable" @click="sortBillSearch('batchNo')">Batch <span class="sort-indicator">{{billSortIndicator('batchNo')}}</span></th><th class="sortable" @click="sortBillSearch('mrp')">MRP <span class="sort-indicator">{{billSortIndicator('mrp')}}</span></th><th>Stock</th><th></th></tr>
      <tr v-for="row in pagedBillSearch" :key="row.id"><td>{{ row.medicineType }}</td><td>{{ row.productName }}</td><td>{{ row.manufacturer }}</td><td>{{ row.batchNo }}</td><td>₹{{ Number(row.mrp).toFixed(2) }}</td><td>{{ row.availablePacks }} pack(s) + {{ row.looseUnits }} loose</td><td><button v-if="can('PHARMACY_SALES','add')" @click="addToCart(row)">Add to Bill</button></td></tr>
    </table>
    <Pagination :page="billSearchPage" :page-count="billSearchPageCount" :total="sortedBillSearch.length" :page-size="pageSize" @update:page="billSearchPage=$event" />

    <h3>Selected Medicines</h3>
    <table class="table">
      <tr><th>Product</th><th>Type</th><th>Batch</th><th>Sale Unit</th><th>Quantity</th><th>Price</th><th>Gross</th><th>Approved Discount</th><th>Total</th><th></th></tr>
      <tr v-for="(row,index) in cart" :key="row.purchaseItemId">
        <td>{{ row.productName }}<div class="muted">{{ row.packing }}</div></td><td>{{ row.medicineType }}</td><td>{{ row.batchNo }}</td>
        <td><select v-model="row.unitType" @change="updateUnit(row)"><option v-for="u in allowedUnits(row)" :key="u">{{ u }}</option></select></td>
        <td><input type="number" min="1" :max="maxQuantity(row)" v-model.number="row.quantity"><div class="muted">Max {{ maxQuantity(row) }} {{ row.unitType }}</div></td>
        <td>₹{{ Number(row.unitPrice).toFixed(2) }}</td><td>₹{{ rowGross(row).toFixed(2) }}</td>
        <td><select v-model.number="row.discountTypeId"><option :value="null">No Discount</option><option v-for="d in discounts" :key="d.id" :value="d.id">{{d.name}} · {{d.discountMode==='Percent'?d.value+'%':'₹'+Number(d.value).toFixed(2)}}</option></select></td>
        <td><b>₹{{ rowTotal(row).toFixed(2) }}</b></td><td><button class="danger-btn" @click="removeCart(index)">×</button></td>
      </tr>
    </table>

    <div class="bill-total-box"><div><span>Before Discount</span><b>₹{{ totals.gross.toFixed(2) }}</b></div><div><span>Discount</span><b>₹{{ totals.discount.toFixed(2) }}</b></div><div><span>Round Off (-9 to 9)</span><input v-model.number="roundOff" type="number" min="-9" max="9" step="0.01" style="width:80px;display:inline-block"></div><div class="grand"><span>Total</span><b>₹{{ totals.net.toFixed(2) }}</b></div></div>
    <div class="modal-actions"><button v-if="can('PHARMACY_SALES','add')" @click="createSale">Complete Sale</button></div>
  </div>
</template>
