<script setup>
import { onMounted, ref } from 'vue';
import { api } from '../../api';

const today = new Date();
const todayText = today.toISOString().slice(0, 10);
const from = ref(todayText);
const to = ref(todayText);
const year = ref(today.getFullYear());
const data = ref({ monthly: [], distributorBreakdown: [] });
const paymentMode = ref({ rows: [], totalCash: 0, totalOnline: 0, grandTotal: 0 });

async function load() {
  const [reportRes, paymentModeRes] = await Promise.all([
    api.get('/module-reports/pharmacy', { params: { from: from.value, to: to.value, year: year.value } }),
    api.get('/reporting/payment-mode-by-bill-type-pharmacy', { params: { from: from.value, to: to.value } })
  ]);
  data.value = reportRes.data;
  paymentMode.value = paymentModeRes.data;
}

function money(v) {
  return Number(v || 0).toFixed(2);
}

onMounted(load);
</script>

<template>
  <h1>Pharmacy Report</h1>
  <div class="toolbar">
    <label>From<input type="date" v-model="from"></label>
    <label>To<input type="date" v-model="to"></label>
    <label>Trend Year<input type="number" min="2020" max="2100" v-model.number="year"></label>
    <button @click="load">Apply</button>
  </div>

  <div class="grid">
    <div class="card">
      <div class="muted">Purchase Amount</div>
      <div class="metric">₹{{ money(data.purchaseAmount) }}</div>
    </div>
    <div class="card">
      <div class="muted">Cash Purchase</div>
      <div class="metric">₹{{ money(data.cashPurchaseAmount) }}</div>
    </div>
    <div class="card">
      <div class="muted">Credit Purchase</div>
      <div class="metric">₹{{ money(data.creditPurchaseAmount) }}</div>
    </div>
    <div class="card">
      <div class="muted">Supplier Payments</div>
      <div class="metric">₹{{ money(data.supplierPayments) }}</div>
    </div>
    <div class="card">
      <div class="muted">Sales Revenue</div>
      <div class="metric">₹{{ money(data.salesAmount) }}</div>
    </div>
    <div class="card">
      <div class="muted">Customer Returns</div>
      <div class="metric">₹{{ money(data.customerReturnAmount) }}</div>
    </div>
    <div class="card">
      <div class="muted">Expiry Return</div>
      <div class="metric">₹{{ money(data.expiryReturnAmount) }}</div>
    </div>
    <div class="card">
      <div class="muted">Expiry Loss</div>
      <div class="metric">₹{{ money(data.expiryDiscardLoss) }}</div>
    </div>
  </div>

  <div class="card" style="margin-top:18px">
    <h2>Distributor-wise Purchase</h2>
    <table class="table">
      <tr>
        <th>Distributor</th>
        <th>Invoices</th>
        <th>Total Purchase</th>
        <th>Cash</th>
        <th>Credit</th>
      </tr>
      <tr v-for="x in data.distributorBreakdown || []" :key="x.distributor">
        <td><b>{{ x.distributor }}</b></td>
        <td>{{ x.invoiceCount }}</td>
        <td>₹{{ money(x.purchaseAmount) }}</td>
        <td>₹{{ money(x.cashAmount) }}</td>
        <td>₹{{ money(x.creditAmount) }}</td>
      </tr>
    </table>
  </div>

  <div class="card" style="margin-top:18px">
    <h2>Cash vs Online by Bill Type</h2>
    <div class="muted">Showing {{ from }} to {{ to }}</div>
    <div v-if="!paymentMode.rows?.length" class="muted">No payments received for this period.</div>
    <table v-else class="table">
      <tr><th>Bill Type</th><th>Cash</th><th>Online</th><th>Total</th></tr>
      <tr v-for="row in paymentMode.rows" :key="row.billType">
        <td>{{ row.billType }}</td>
        <td>₹{{ money(row.cash) }}</td>
        <td>₹{{ money(row.online) }}</td>
        <td><b>₹{{ money(row.total) }}</b></td>
      </tr>
      <tr>
        <td><b>Total</b></td>
        <td><b>₹{{ money(paymentMode.totalCash) }}</b></td>
        <td><b>₹{{ money(paymentMode.totalOnline) }}</b></td>
        <td><b>₹{{ money(paymentMode.grandTotal) }}</b></td>
      </tr>
    </table>
  </div>

  <div class="card" style="margin-top:18px">
    <h2>Month-wise Pharmacy Revenue · {{ data.year }}</h2>
    <table class="table">
      <tr>
        <th>Month</th>
        <th>Purchase</th>
        <th>Sales Revenue</th>
        <th>Difference vs Previous Month</th>
      </tr>
      <tr v-for="x in data.monthly || []" :key="x.month">
        <td>{{ x.monthName }}</td>
        <td>₹{{ money(x.purchaseAmount) }}</td>
        <td><b>₹{{ money(x.revenue) }}</b></td>
        <td :class="Number(x.differenceFromPreviousMonth) >= 0 ? 'positive-text' : 'danger'">{{ Number(x.differenceFromPreviousMonth) >= 0 ? '+' : '' }}₹{{ money(x.differenceFromPreviousMonth) }}</td>
      </tr>
    </table>
  </div>
</template>
