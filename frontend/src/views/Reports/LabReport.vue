<script setup>
import { onMounted, ref } from 'vue';
import { api } from '../../api';

const now = new Date();
const today = now.toISOString().slice(0, 10);
const from = ref(today);
const to = ref(today);
const year = ref(now.getFullYear());
const data = ref({ monthly: [] });

async function load() {
  data.value = (await api.get('/module-reports/lab', { params: { from: from.value, to: to.value, year: year.value } })).data;
}

function money(v) {
  return Number(v || 0).toFixed(2);
}

onMounted(load);
</script>

<template>
  <h1>Lab Report</h1>
  <div class="toolbar">
    <label>From<input type="date" v-model="from"></label>
    <label>To<input type="date" v-model="to"></label>
    <label>Trend Year<input type="number" v-model.number="year"></label>
    <button @click="load">Apply</button>
  </div>

  <div class="grid">
    <div class="card">
      <div class="muted">Lab Orders</div>
      <div class="metric">{{ data.labOrders || 0 }}</div>
    </div>
    <div class="card">
      <div class="muted">Lab Revenue</div>
      <div class="metric">₹{{ money(data.labIncome) }}</div>
    </div>
    <div class="card">
      <div class="muted">Lab Purchases</div>
      <div class="metric">₹{{ money(data.labPurchaseExpense) }}</div>
    </div>
    <div class="card">
      <div class="muted">Lab Paid</div>
      <div class="metric">₹{{ money(data.labExpensePaid) }}</div>
    </div>
    <div class="card">
      <div class="muted">Contribution</div>
      <div class="metric">₹{{ money(data.labOperatingContribution) }}</div>
    </div>
    <div class="card">
      <div class="muted">Total Individual Tests</div>
      <div class="metric">{{ data.totalIndividualTests || 0 }}</div>
    </div>
  </div>

  <div class="card" style="margin-top:18px">
    <h2>Month-wise Lab Revenue</h2>
    <table class="table">
      <tr>
        <th>Month</th>
        <th>Revenue</th>
        <th>Purchase Expense</th>
        <th>Difference vs Previous Month</th>
      </tr>
      <tr v-for="x in data.monthly || []" :key="x.month">
        <td>{{ x.monthName }}</td>
        <td><b>₹{{ money(x.revenue) }}</b></td>
        <td>₹{{ money(x.expense) }}</td>
        <td :class="Number(x.differenceFromPreviousMonth) >= 0 ? 'positive-text' : 'danger'">{{ Number(x.differenceFromPreviousMonth) >= 0 ? '+' : '' }}₹{{ money(x.differenceFromPreviousMonth) }}</td>
      </tr>
    </table>
  </div>

  <div class="card" style="margin-top:18px">
    <h2>Test-wise Count ({{ from }} to {{ to }})</h2>
    <table class="table">
      <tr>
        <th>Test</th>
        <th>Times Done</th>
      </tr>
      <tr v-for="x in data.testBreakdown || []" :key="x.testName">
        <td>{{ x.testName }}</td>
        <td><b>{{ x.count }}</b></td>
      </tr>
      <tr v-if="!(data.testBreakdown || []).length">
        <td colspan="2" class="muted">No tests in this date range.</td>
      </tr>
    </table>
  </div>
</template>
