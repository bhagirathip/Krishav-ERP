<script setup>
import { ref, computed, onMounted } from 'vue';
import { api } from '../api';
import GridSearch from '../components/GridSearch.vue';
import Pagination from '../components/Pagination.vue';
import { useGrid } from '../composables/useGrid';
const months=ref(1),rows=ref([]),report=ref({returnedAmount:0,lossAmount:0,rows:[]}),show=ref(false),selected=ref(null);
const action=ref({actionType:'Return',unitType:'Pack',quantity:1,notes:''});
const {search,page,pageCount,pagedRows,sortedRows,sortBy,sortIndicator}=useGrid(rows,10);
async function load(){rows.value=(await api.get('/pharmacy/expiry',{params:{months:months.value}})).data;report.value=(await api.get('/pharmacy/expiry/report')).data}
function baseLabel(r){if(r.medicineType==='Tablet')return 'Tablet';if(r.medicineType==='Injection')return 'Vial';return 'Unit'}
function openAction(r,type){selected.value=r;action.value={actionType:type,unitType:'Pack',quantity:1,notes:''};show.value=true}
const amount=computed(()=>{if(!selected.value)return 0;const units=action.value.unitType==='Pack'?Number(action.value.quantity||0)*Math.max(selected.value.tabletsPerStrip,1):Number(action.value.quantity||0);return (Number(selected.value.rate||0)/Math.max(selected.value.tabletsPerStrip,1))*units})
async function save(){try{await api.post('/pharmacy/expiry/action',{purchaseItemId:selected.value.id,...action.value});show.value=false;await load()}catch(e){alert(e.response?.data?.message||'Unable to save expiry action.')}}
onMounted(load);
</script>
<template>
<h1>Expiry Medicine</h1>
<div class="toolbar"><label>Show medicines expiring within<select v-model.number="months" @change="load"><option v-for="n in 6" :key="n" :value="n">Next {{n}} Month{{n>1?'s':''}}</option></select></label></div>
<div class="grid" style="margin-bottom:18px"><div class="card"><div class="muted">Amount Returned Due to Expiry</div><div class="metric">₹{{Number(report.returnedAmount||0).toFixed(2)}}</div></div><div class="card"><div class="muted">Loss Due to Expiry / Discard</div><div class="metric">₹{{Number(report.lossAmount||0).toFixed(2)}}</div></div></div>
<GridSearch v-model="search" placeholder="Search medicine, batch, distributor, invoice..." />
<table class="table"><tr><th class="sortable" @click="sortBy('productName')">Medicine {{sortIndicator('productName')}}</th><th>Batch</th><th class="sortable" @click="sortBy('expiryDate')">Expiry {{sortIndicator('expiryDate')}}</th><th>Distributor</th><th>Invoice</th><th>Stock</th><th>Purchase Rate</th><th>Returnable</th><th>Status</th><th></th></tr>
<tr v-for="r in pagedRows" :key="r.id"><td>{{r.productName}}<div class="muted">{{r.medicineType}} · {{r.packing}}</div></td><td>{{r.batchNo}}</td><td>{{String(r.expiryDate).slice(0,10)}}</td><td>{{r.distributorName}}</td><td>{{r.invoiceNumber}}</td><td>{{r.remainingPacks}} pack(s) + {{r.looseUnits}} {{baseLabel(r).toLowerCase()}}(s)</td><td>₹{{Number(r.rate).toFixed(2)}}</td><td>{{r.isReturnableOnExpiry?'Yes':'No'}}</td><td>{{r.status}}</td><td class="actions"><button v-if="r.isReturnableOnExpiry" @click="openAction(r,'Return')">Return</button><button class="danger-btn" @click="openAction(r,'Discard')">Discard</button></td></tr></table>
<Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="10" @update:page="page=$event" />
<h2 style="margin-top:28px">Expiry Action Report</h2>
<table class="table"><tr><th>Date</th><th>Medicine</th><th>Batch</th><th>Action</th><th>Quantity</th><th>Amount</th><th>Notes</th></tr><tr v-for="r in report.rows||[]" :key="r.id"><td>{{new Date(r.actionDateUtc).toLocaleString()}}</td><td>{{r.productName}}</td><td>{{r.batchNo}}</td><td>{{r.actionType}}</td><td>{{r.quantityInBaseUnits}}</td><td>₹{{Number(r.amount).toFixed(2)}}</td><td>{{r.notes||'-'}}</td></tr></table>
<div v-if="show" class="modal-bg"><div class="modal"><button class="modal-close-x" @click="show=false">×</button><h2>{{action.actionType}} Expiry Medicine</h2><div class="card"><b>{{selected.productName}}</b><div class="muted">Batch {{selected.batchNo}} · Available {{selected.remainingPacks}} pack(s) + {{selected.looseUnits}} {{baseLabel(selected).toLowerCase()}}(s)</div></div><div class="form-grid" style="margin-top:14px"><label>Action<select v-model="action.actionType"><option v-if="selected.isReturnableOnExpiry">Return</option><option>Discard</option></select></label><label>Unit<select v-model="action.unitType"><option value="Pack">Pack / Strip</option><option value="BaseUnit">{{baseLabel(selected)}}</option></select></label><label>Quantity<input type="number" min="1" v-model.number="action.quantity"></label><label>Calculated Amount<input :value="'₹'+amount.toFixed(2)" readonly></label><label class="full">Notes<textarea rows="3" v-model="action.notes"></textarea></label></div><div class="modal-actions"><button @click="save">Save {{action.actionType}}</button><button class="secondary" @click="show=false">Cancel</button></div></div></div>
</template>
