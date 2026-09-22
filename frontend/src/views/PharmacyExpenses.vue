<script setup>
import { onMounted, ref } from 'vue';
import { api } from '../api';
import GridSearch from '../components/GridSearch.vue';
import Pagination from '../components/Pagination.vue';
import { useGrid } from '../composables/useGrid';

const rows=ref([]); const payment=ref(null); const history=ref([]); const staff=ref([]);
const {search,page,pageCount,pagedRows,sortedRows,sortBy,sortIndicator}=useGrid(rows,10);
async function load(){const [r,s]=await Promise.all([api.get('/expenses/pharmacy'),api.get('/staff')]);rows.value=r.data;staff.value=s.data;}
async function open(row){history.value=(await api.get(`/expenses/pharmacy/${row.id}/payments`)).data;payment.value={purchaseInvoiceId:row.id,invoiceNumber:row.invoiceNumber,outstanding:row.outstanding,amount:row.outstanding,paymentDate:new Date().toISOString().slice(0,10),paymentMode:'Cash',referenceNumber:'',notes:'',paidByStaffId:null};}
async function save(){await api.post('/expenses/pharmacy/payment',payment.value);payment.value=null;await load();}
onMounted(load);
</script>
<template>
<h1>Pharmacy Expenses</h1>
<div class="muted">Purchase invoices automatically appear here. Record supplier payments as they are made.</div>
<GridSearch v-model="search" placeholder="Search invoice, distributor, payment type, amount..." />
<table class="table"><tr><th>Invoice</th><th>Date</th><th>Distributor</th><th>Type</th><th>Total</th><th>Paid</th><th>Outstanding</th><th></th></tr>
<tr v-for="row in pagedRows" :key="row.id"><td>{{row.invoiceNumber}}</td><td>{{String(row.invoiceDate).slice(0,10)}}</td><td>{{row.distributorName}}</td><td>{{row.paymentType}}</td><td>₹{{Number(row.totalAmount).toFixed(2)}}</td><td>₹{{Number(row.paidAmount).toFixed(2)}}</td><td><b>₹{{Number(row.outstanding).toFixed(2)}}</b></td><td><button @click="open(row)">Manage Payment</button></td></tr></table>
<Pagination :page="page" :page-count="pageCount" :total="sortedRows.length" :page-size="10" @update:page="page=$event" />
<div v-if="payment" class="modal-bg"><div class="modal modal-lg"><button class="modal-close-x" @click="payment=null">×</button><h2>Supplier Payment · {{payment.invoiceNumber}}</h2><div class="card"><b>Outstanding: ₹{{Number(payment.outstanding).toFixed(2)}}</b></div><div class="form-grid" style="margin-top:14px"><label>Amount *<input type="number" min="0" :max="payment.outstanding" step="0.01" v-model.number="payment.amount"></label><label>Date<input type="date" v-model="payment.paymentDate"></label><label>Mode<select v-model="payment.paymentMode"><option>Cash</option><option>UPI</option><option>Bank Transfer</option><option>Cheque</option><option>Other</option></select></label><label>Paid By Staff<select v-model.number="payment.paidByStaffId"><option :value="null">Not specified</option><option v-for="x in staff" :key="x.id" :value="x.id">{{x.name}}</option></select></label><label>Reference<input v-model="payment.referenceNumber"></label><label class="full">Notes<textarea rows="3" v-model="payment.notes"></textarea></label></div><h3>Previous Payments</h3><table class="table"><tr><th>Date</th><th>Amount</th><th>Mode</th><th>Paid By</th><th>Reference</th></tr><tr v-for="x in history" :key="x.id"><td>{{String(x.paymentDate).slice(0,10)}}</td><td>₹{{Number(x.amount).toFixed(2)}}</td><td>{{x.paymentMode}}</td><td>{{x.paidByStaffName||'-'}}</td><td>{{x.referenceNumber||'-'}}</td></tr></table><div class="modal-actions"><button @click="save">Save Payment</button><button class="secondary" @click="payment=null">Close</button></div></div></div>
</template>
