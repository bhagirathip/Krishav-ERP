<script setup>
import { onMounted, ref } from 'vue';
import { api } from '../api';
import GridSearch from '../components/GridSearch.vue';
import Pagination from '../components/Pagination.vue';
import { useGrid } from '../composables/useGrid';

const rows=ref([]),show=ref(false),form=ref({id:null,name:'',isActive:true});
const {search,page,pageCount,pagedRows,sortedRows,sortBy,sortIndicator}=useGrid(rows,10);
async function load(){rows.value=(await api.get('/staff-designations',{params:{includeInactive:true}})).data;}
function add(){form.value={id:null,name:'',isActive:true};show.value=true;}
function edit(x){form.value={...x};show.value=true;}
async function save(){if(!form.value.name.trim()){alert('Designation name is required.');return;} if(form.value.id)await api.put(`/staff-designations/${form.value.id}`,form.value);else await api.post('/staff-designations',form.value);show.value=false;await load();}
async function remove(x){if(!confirm(`Deactivate ${x.name}?`))return;await api.delete(`/staff-designations/${x.id}`);await load();}
onMounted(load);
</script>
<template>
<h1>Designation Master</h1>
<div class="toolbar"><button @click="add">+ Add Designation</button></div>
<GridSearch v-model="search" placeholder="Search designation..." />
<table class="table"><tr><th class="sortable" @click="sortBy('name')">Designation {{sortIndicator('name')}}</th><th>Status</th><th></th></tr>
<tr v-for="x in pagedRows" :key="x.id"><td><b>{{x.name}}</b></td><td>{{x.isActive?'Active':'Inactive'}}</td><td class="actions"><button @click="edit(x)">Edit</button><button v-if="x.isActive" class="danger-btn" @click="remove(x)">Deactivate</button></td></tr></table>
<Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="10" @update:page="page=$event" />
<div v-if="show" class="modal-bg"><div class="modal"><button class="modal-close-x" @click="show = false">×</button><h2>{{form.id?'Edit':'Add'}} Designation</h2><label>Designation Name *<input v-model="form.name"></label><label class="toggle-row"><input type="checkbox" v-model="form.isActive"> Active</label><div class="modal-actions"><button @click="save">Save</button><button class="secondary" @click="show=false">Cancel</button></div></div></div>
</template>