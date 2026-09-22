<script setup>
import {ref,onMounted} from 'vue';import {api} from '../api';import {can} from '../auth';
const today=new Date().toISOString().slice(0,10);const from=ref(today),to=ref(today),d=ref({});
async function load(){const r=await api.get('/dashboard',{params:{from:from.value,to:to.value}});d.value=r.data}onMounted(load);
</script>
<template><div class="page-wide"><div class="page-head"><div><h1>Dashboard</h1><div class="muted">Hospital activity and collection summary</div></div><div class="toolbar report-filter"><label>From<input type="date" v-model="from"></label><label>To<input type="date" v-model="to"></label><button @click="load">Apply</button></div></div>
<div class="dashboard-grid">
<div v-if="can('OPD','view')" class="card metric-card"><div class="muted">OPD(s)</div><div class="metric">{{d.opdCount??0}}</div></div>
<div v-if="can('OPD','view')" class="card metric-card"><div class="muted">Emergency(es)</div><div class="metric">{{d.emergencyCount??0}}</div></div>
<div v-if="!d.isMultiDay && can('IPD','view')" class="card metric-card"><div class="muted">Currently Admitted</div><div class="metric">{{d.currentlyAdmitted??0}}</div></div>
<div v-if="!d.isMultiDay && can('BED','view')" class="card metric-card"><div class="muted">Beds</div><div class="metric">{{d.beds?.occupied??0}} / {{d.beds?.total??18}}</div><div class="muted">Occupied / Total · {{d.beds?.available??18}} available</div></div>
<div v-if="can('LAB','view')" class="card metric-card"><div class="muted">Number of Lab Tests</div><div class="metric">{{d.numberOfLabTests??0}}</div></div>
<div v-if="can('BILL','view')" class="card metric-card money"><div class="muted">Total Unpaid Amount</div><div class="metric">₹{{Number(d.totalUnpaidAmount??0).toFixed(2)}}</div></div>
<div v-if="can('BILL','view')" class="card metric-card money"><div class="muted">Total Paid Amount</div><div class="metric">₹{{Number(d.totalPaidAmount??0).toFixed(2)}}</div></div>
<div v-if="can('LAB','view')" class="card metric-card money"><div class="muted">Total Lab Amount</div><div class="metric">₹{{Number(d.totalLabAmount??0).toFixed(2)}}</div></div>
<div v-if="can('PHARMACY','view')" class="card metric-card money"><div class="muted">Total Pharmacy Amount</div><div class="metric">₹{{Number(d.totalPharmacyAmount??0).toFixed(2)}}</div></div>
<div v-if="can('OPD','view')" class="card metric-card money"><div class="muted">Total OPD Amount</div><div class="metric">₹{{Number(d.totalOpdAmount??0).toFixed(2)}}</div></div>
<div v-if="can('OPD','view')" class="card metric-card money"><div class="muted">Doctor Consultation (Settled Separately)</div><div class="metric">₹{{Number(d.doctorConsultationAmount??0).toFixed(2)}}</div></div>
</div></div></template>