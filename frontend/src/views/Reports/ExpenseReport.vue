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
  data.value = (await api.get('/module-reports/expenses', { params: { from: from.value, to: to.value, year: year.value } })).data;
}

function money(v) {
  return Number(v || 0).toFixed(2);
}

onMounted(load);
</script>

<template>
  <h1>Expense Report</h1>
  <div class="toolbar">
    <label>From<input type="date" v-model="from"></label>
    <label>To<input type="date" v-model="to"></label>
    <label>Trend Year<input type="number" v-model.number="year"></label>
    <button @click="load">Apply</button>
  </div>

  <div class="grid">
    <div class="card">
      <div class="muted">Daily Expenses</div>
      <div class="metric">₹{{ money(data.dailyExpenses) }}</div>
    </div>
    <div class="card">
      <div class="muted">Salary Payments</div>
      <div class="metric">₹{{ money(data.salaryPayments) }}</div>
    </div>
    <div class="card">
      <div class="muted">Pharmacy Payments</div>
      <div class="metric">₹{{ money(data.pharmacyPayments) }}</div>
    </div>
    <div class="card">
      <div class="muted">Lab Payments</div>
      <div class="metric">₹{{ money(data.labPayments) }}</div>
    </div>
    <div class="card">
      <div class="muted">Doctor Payments</div>
      <div class="metric">₹{{ money(data.doctorPayments) }}</div>
    </div>
    <div class="card">
      <div class="muted">Total Cash Expenses</div>
      <div class="metric">₹{{ money(data.totalExpenses) }}</div>
    </div>
  </div>

  <div class="card" style="margin-top:18px">
    <h2>Hospital Expenses · Date-wise</h2>
    <div class="muted">Showing {{ from }} to {{ to }}</div>
    <div v-if="!data.hospitalExpenseRows?.length" class="muted">No hospital expenses in this date range.</div>
    <table v-else class="table">
      <tr><th>Date</th><th>Total</th></tr>
      <tr v-for="row in data.hospitalExpenseRows" :key="row.date">
        <td>{{ String(row.date).slice(0, 10) }}</td>
        <td><b>₹{{ money(row.total) }}</b></td>
      </tr>
      <tr>
        <td><b>Total</b></td>
        <td><b>₹{{ money(data.hospitalExpenseTotal) }}</b></td>
      </tr>
    </table>
  </div>

  <div class="card" style="margin-top:18px">
    <h2>Month-wise Expense</h2>
    <table class="table">
      <tr>
        <th>Month</th>
        <th>Total Expense</th>
        <th>Difference vs Previous Month</th>
      </tr>
      <tr v-for="x in data.monthly || []" :key="x.month">
        <td>{{ x.monthName }}</td>
        <td><b>₹{{ money(x.amount) }}</b></td>
        <td :class="Number(x.differenceFromPreviousMonth) <= 0 ? 'positive-text' : 'danger'">{{ Number(x.differenceFromPreviousMonth) >= 0 ? '+' : '' }}₹{{ money(x.differenceFromPreviousMonth) }}</td>
      </tr>
    </table>
  </div>
</template>
