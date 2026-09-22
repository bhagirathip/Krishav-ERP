<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';

const rows=ref([]);
const {search,page,pageCount,pagedRows,sortedRows,sortBy,sortIndicator}=useGrid(rows,10);
const show=ref(false);
const errors=ref([]);
const form=ref(blank());
function blank(){return{id:null,name:'',discountMode:'Percent',value:0,scope:'Individual',isActive:true}}
async function load(){rows.value=(await api.get('/discounts')).data}
function add(){form.value=blank();errors.value=[];show.value=true}
function edit(row){form.value={...row};errors.value=[];show.value=true}
async function save(){errors.value=[];if(!form.value.name.trim())errors.value.push('Discount name is required.');if(form.value.value<0)errors.value.push('Discount value cannot be negative.');if(form.value.discountMode==='Percent'&&form.value.value>100)errors.value.push('Percentage cannot exceed 100%.');if(errors.value.length)return;try{if(form.value.id)await api.put('/discounts/'+form.value.id,form.value);else await api.post('/discounts',form.value);show.value=false;await load()}catch(e){errors.value=[e.response?.data?.message||'Unable to save discount.']}}
async function remove(row){if(!confirm(`Deactivate ${row.name}?`))return;await api.delete('/discounts/'+row.id);await load()}
onMounted(load)
</script>
<template>
<h1>Discount Master</h1>
<div class="toolbar"><button v-if="can('DISCOUNT','add')" @click="add">+ Add Discount Type</button></div>
<div class="grid-filter-row"><GridSearch v-model="search" placeholder="Search all discount fields..." /></div>
<table class="table"><tr>
<th class="sortable" @click="sortBy('name')">Discount Name {{sortIndicator('name')}}</th>
<th class="sortable" @click="sortBy('discountMode')">Mode {{sortIndicator('discountMode')}}</th>
<th class="sortable" @click="sortBy('value')">Value {{sortIndicator('value')}}</th>
<th class="sortable" @click="sortBy('scope')">Type {{sortIndicator('scope')}}</th>
<th class="sortable" @click="sortBy('isActive')">Status {{sortIndicator('isActive')}}</th><th></th></tr>
<tr v-for="row in pagedRows" :key="row.id"><td>{{row.name}}</td><td>{{row.discountMode}}</td><td>{{row.discountMode==='Percent'?row.value+'%':'₹'+Number(row.value).toFixed(2)}}</td><td>{{row.scope}}</td><td>{{row.isActive?'Active':'Inactive'}}</td><td class="actions"><button v-if="can('DISCOUNT','edit')" @click="edit(row)">Edit</button><button v-if="can('DISCOUNT','delete')" class="danger-btn" @click="remove(row)">Deactivate</button></td></tr></table>
<Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="10" @update:page="page=$event" />
<div v-if="show" class="modal-bg"><div class="modal"><button class="modal-close-x" @click="show = false">×</button><h2>{{form.id?'Edit':'Add'}} Discount</h2><div v-if="errors.length" class="error-box"><div v-for="e in errors" :key="e">{{e}}</div></div><div class="form-grid">
<label>Discount Name *<input v-model="form.name"></label>
<label>Discount Mode<select v-model="form.discountMode"><option>Percent</option><option>Amount</option></select></label>
<label>{{form.discountMode==='Percent'?'Discount %':'Discount Amount ₹'}}<input type="number" min="0" :max="form.discountMode==='Percent'?100:null" step="0.01" v-model.number="form.value"></label>
<label>Discount Type<select v-model="form.scope"><option>Individual</option><option>Bulk</option></select></label>
<label class="toggle-row"><input type="checkbox" v-model="form.isActive"> Active</label>
</div><div class="modal-actions"><button @click="save">Save</button><button class="secondary" @click="show=false">Cancel</button></div></div></div>
</template>
