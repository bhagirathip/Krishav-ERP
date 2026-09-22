<script setup>
import {ref,onMounted,computed} from 'vue';import {api,assetUrl} from '../api';import {can} from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';
const rows=ref([]),show=ref(false),form=ref({name:'',value:'',type:'Billing',isActive:true}),imageFile=ref(null);
const pageSize=10;const {search,page,pageCount,pagedRows,sortedRows,sortBy,sortIndicator}=useGrid(rows,pageSize);
const headerNames=['OPD Header','Hospital Logo','Lab Header','Pharmacy Header','Lab Signature'];
async function load(){rows.value=(await api.get('/settings')).data}
function add(){form.value={name:'',value:'',type:'Billing',isActive:true};imageFile.value=null;show.value=true}function edit(x){form.value={...x};imageFile.value=null;show.value=true}
async function save(){if(imageFile.value){const fd=new FormData();fd.append('file',imageFile.value);fd.append('name',form.value.name);await api.post('/settings/image',fd)}else if(form.value.id)await api.put('/settings/'+form.value.id,form.value);else await api.post('/settings',form.value);show.value=false;load()}
async function uploadNamed(name,e){const f=e.target.files[0];if(!f)return;const fd=new FormData();fd.append('file',f);fd.append('name',name);await api.post('/settings/image',fd);load()}
async function del(x){if(confirm('Remove this setting?')){await api.delete('/settings/'+x.id);load()}}
function get(name){return rows.value.find(x=>x.name===name)?.value||''}
onMounted(load)
</script><template><h1>Settings</h1><div class="toolbar"><button v-if="can('SETTINGS','add')" @click="add">+ Add Default Value</button></div>
<div class="grid-filter-row"><GridSearch v-model="search" placeholder="Search all setting fields..." /></div>
<div class="grid header-grid"><div v-for="n in headerNames" class="card"><b>{{n}}</b><div class="header-preview"><img v-if="get(n)" :src="assetUrl(get(n))"></div><input type="file" accept=".png,.jpg,.jpeg,.webp" @change="uploadNamed(n,$event)"></div></div>
<table class="table" style="margin-top:20px"><tr><th class="sortable" @click="sortBy('name')">Name <span class="sort-indicator">{{sortIndicator('name')}}</span></th><th class="sortable" @click="sortBy('value')">Value <span class="sort-indicator">{{sortIndicator('value')}}</span></th><th class="sortable" @click="sortBy('type')">Type <span class="sort-indicator">{{sortIndicator('type')}}</span></th><th></th></tr><tr v-for="x in pagedRows"><td>{{x.name}}</td><td><img v-if="x.type==='Branding'&&x.value.startsWith('/')" :src="assetUrl(x.value)" style="max-height:45px;max-width:180px"><span v-else>{{x.value}}</span></td><td>{{x.type}}</td><td class="actions"><button v-if="can('SETTINGS','edit')" @click="edit(x)">Edit</button><button v-if="can('SETTINGS','delete')" class="danger-btn" @click="del(x)">Delete</button></td></tr></table><Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="pageSize" @update:page="page=$event" />
<div v-if="show" class="modal-bg"><div class="modal"><button class="modal-close-x" @click="show=false">×</button><h2>{{form.id?'Edit':'Add'}} Default Value</h2><div class="form-grid"><label>Name *<input v-model="form.name"></label><label>Type<select v-model="form.type"><option>Billing</option><option>Branding</option><option>Generic</option></select></label><label class="full">Value<textarea rows="3" v-model="form.value"></textarea></label><label class="full">Optional Image<input type="file" accept=".png,.jpg,.jpeg,.webp" @change="imageFile=$event.target.files[0]"></label></div><div class="modal-actions"><button @click="save">Save</button><button class="secondary" @click="show=false">Cancel</button></div></div></div></template>