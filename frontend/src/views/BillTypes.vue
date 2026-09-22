<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../api';
import { can } from '../auth';
import GridSearch from '../components/GridSearch.vue';
import Pagination from '../components/Pagination.vue';
import { useGrid } from '../composables/useGrid';
const rows=ref([]),show=ref(false),form=ref({id:null,name:'',isBillable:true,isOpdType:false,isActive:true});
const {search,page,pageCount,pagedRows,sortedRows,sortBy,sortIndicator}=useGrid(rows,10);
async function load(){rows.value=(await api.get('/bill-types',{params:{includeInactive:true}})).data}
function add(){form.value={id:null,name:'',isBillable:true,isOpdType:false,isActive:true};show.value=true}
function edit(x){form.value={...x};show.value=true}
async function save(){if(!form.value.name.trim()){alert('Bill type name is required.');return} if(form.value.id) await api.put(`/bill-types/${form.value.id}`,form.value); else await api.post('/bill-types',form.value);show.value=false;await load()}
async function remove(x){if(confirm(`Deactivate ${x.name}?`)){await api.delete(`/bill-types/${x.id}`);await load()}}
onMounted(load);
</script>
<template>
<h1>Bill Type Master</h1>
<div class="toolbar"><button v-if="can('BILL_TYPE','add')" @click="add">+ Add Bill Type</button></div>
<GridSearch v-model="search" placeholder="Search bill types..." />
<table class="table"><tr><th class="sortable" @click="sortBy('name')">Name {{sortIndicator('name')}}</th><th>Use in OPD</th><th>Billable</th><th>Status</th><th></th></tr>
<tr v-for="x in pagedRows" :key="x.id"><td>{{x.name}}</td><td>{{x.isOpdType?'Yes':'No'}}</td><td>{{x.isBillable?'Yes':'No'}}</td><td>{{x.isActive?'Active':'Inactive'}}</td><td class="actions"><button v-if="can('BILL_TYPE','edit')" @click="edit(x)">Edit</button><button v-if="x.isActive&&can('BILL_TYPE','delete')" class="danger-btn" @click="remove(x)">Deactivate</button></td></tr></table>
<Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="10" @update:page="page=$event" />
<div v-if="show" class="modal-bg"><div class="modal"><button class="modal-close-x" @click="show = false">×</button><h2>{{form.id?'Edit':'Add'}} Bill Type</h2><div class="form-grid"><label>Name *<input v-model="form.name"></label><label class="toggle-row"><input type="checkbox" v-model="form.isOpdType"> Use in OPD</label><label class="toggle-row"><input type="checkbox" v-model="form.isBillable"> Billable</label><label class="toggle-row"><input type="checkbox" v-model="form.isActive"> Active</label></div><div class="modal-actions"><button @click="save">Save</button><button class="secondary" @click="show=false">Cancel</button></div></div></div>
</template>
