<script setup>
import { ref, computed, onMounted } from 'vue';
import { api } from '../api';
import GridSearch from '../components/GridSearch.vue';
import Pagination from '../components/Pagination.vue';
import { useGrid } from '../composables/useGrid';

const referrers = ref([]);
const categories = ref([]);
const summary = ref({
  rows: [],
  categories: [],
  canApprovePayout: false
});

const from = ref(
  new Date().toISOString().slice(0, 10)
);
const to = ref(
  new Date().toISOString().slice(0, 10)
);
const selectedReferrerId = ref(null);
const department = ref('All');

const flatRows = computed(() => {
  const result = [];

  for (const referrer of summary.value.rows || []) {
    for (const patient of referrer.patients || []) {
      result.push({
        ...patient,
        referrerId: referrer.referrerId,
        referrerCode: referrer.referrerCode,
        referrerName: referrer.name
      });
    }
  }

  return result;
});

const {
  search,
  page,
  pageCount,
  pagedRows,
  sortedRows,
  sortBy,
  sortIndicator
} = useGrid(flatRows, 10);

const payout = ref(null);

async function load() {
  const [refsRes, summaryRes] =
    await Promise.all([
      api.get('/referrals'),
      api.get('/referrals/summary', {
        params: {
          from: from.value,
          to: to.value,
          referrerId:
            selectedReferrerId.value ||
            undefined,
          department:
            department.value
        }
      })
    ]);

  referrers.value = refsRes.data;
  summary.value = summaryRes.data;
  categories.value =
    summaryRes.data.categories || [];
}

function money(value) {
  return Number(value || 0).toFixed(2);
}

function openPayout(row) {
  payout.value = {
    referrerId: row.referrerId,
    patientId: row.patientId,
    referrerName: row.referrerName,
    patientName: row.name,
    amount: Number(
      row.approvedPayout || 0
    ),
    paymentDate:
      row.paymentDate
        ? String(row.paymentDate).slice(0, 10)
        : new Date().toISOString().slice(0, 10),
    paymentMode:
      row.paymentMode || 'Cash',
    referenceNumber:
      row.referenceNumber || '',
    notes:
      row.payoutNotes || ''
  };
}

async function savePayout() {
  await api.post(
    '/referrals/payout',
    payout.value
  );

  payout.value = null;
  await load();
}

onMounted(load);
</script>

<template>
  <h1>Referral Payout</h1>

  <div class="toolbar">
    <label>
      From
      <input
        type="date"
        v-model="from"
      >
    </label>

    <label>
      To
      <input
        type="date"
        v-model="to"
      >
    </label>

    <label>
      Referrer
      <select
        v-model.number="
          selectedReferrerId
        "
      >
        <option :value="null">
          All Referrers
        </option>

        <option
          v-for="row in referrers"
          :key="row.id"
          :value="row.id"
        >
          {{ row.name }}
        </option>
      </select>
    </label>

    <label>
      Department
      <select v-model="department">
        <option>All</option>
        <option
          v-for="category in categories"
          :key="category"
        >
          {{ category }}
        </option>
      </select>
    </label>

    <button @click="load">
      Apply
    </button>
  </div>

  <div class="grid-filter-row">
    <GridSearch
      v-model="search"
      placeholder="Search referrer, patient, source, phone..."
    />
  </div>

  <table class="table">
    <tr>
      <th
        class="sortable"
        @click="sortBy('referrerName')"
      >
        Referrer
        {{ sortIndicator('referrerName') }}
      </th>

      <th
        class="sortable"
        @click="sortBy('name')"
      >
        Patient
        {{ sortIndicator('name') }}
      </th>

      <th>Source</th>

      <th
        v-for="category in categories"
        :key="category"
      >
        {{ category }}
      </th>

      <th>Total Income</th>
      <th>Paid / Approved</th>
      <th>Payment Date</th>
      <th>Mode</th>
      <th>Reference</th>
      <th></th>
    </tr>

    <tr
      v-for="row in pagedRows"
      :key="
        row.referrerId +
        '-' +
        row.patientId
      "
    >
      <td>
        {{ row.referrerName }}
        <div class="muted">
          {{ row.referrerCode }}
        </div>
      </td>

      <td>
        {{ row.name }}
        <div class="muted">
          {{ row.patientCode }}
          ·
          {{ row.phone }}
        </div>
      </td>

      <td>
        {{ row.marketingSource }}
      </td>

      <td
        v-for="category in categories"
        :key="category"
      >
        ₹{{
          money(
            row.income?.[category]
          )
        }}
      </td>

      <td>
        <b>
          ₹{{ money(row.totalIncome) }}
        </b>
      </td>

      <td>
        ₹{{ money(row.approvedPayout) }}
      </td>

      <td>
        {{
          row.paymentDate
            ? String(row.paymentDate)
                .slice(0, 10)
            : '-'
        }}
      </td>

      <td>
        {{ row.paymentMode || '-' }}
      </td>

      <td>
        {{ row.referenceNumber || '-' }}
      </td>

      <td>
        <button
          v-if="
            summary.canApprovePayout
          "
          @click="openPayout(row)"
        >
          Payment Details
        </button>
      </td>
    </tr>
  </table>

  <Pagination
    :page="page"
    :page-count="pageCount"
    :total="sortedRows.length"
    :page-size="10"
    @update:page="page = $event"
  />

  <div
    v-if="payout"
    class="modal-bg"
  >
    <div class="modal">
      <button class="modal-close-x" @click="payout = null">×</button>
      <h2>Referral Payment</h2>

      <div class="card">
        <b>{{ payout.referrerName }}</b>
        <div class="muted">
          Patient:
          {{ payout.patientName }}
        </div>
      </div>

      <div
        class="form-grid"
        style="margin-top:14px"
      >
        <label>
          Amount *
          <input
            type="number"
            min="0"
            step="0.01"
            v-model.number="
              payout.amount
            "
          >
        </label>

        <label>
          Payment Date
          <input
            type="date"
            v-model="
              payout.paymentDate
            "
          >
        </label>

        <label>
          Payment Mode
          <select
            v-model="
              payout.paymentMode
            "
          >
            <option>Cash</option>
            <option>UPI</option>
            <option>Card</option>
            <option>Bank Transfer</option>
            <option>Cheque</option>
            <option>Other</option>
          </select>
        </label>

        <label>
          Reference / Transaction No
          <input
            v-model="
              payout.referenceNumber
            "
          >
        </label>

        <label class="full">
          Notes
          <textarea
            rows="3"
            v-model="payout.notes"
          ></textarea>
        </label>
      </div>

      <div class="modal-actions">
        <button @click="savePayout">
          Save Payment
        </button>

        <button
          class="secondary"
          @click="payout = null"
        >
          Cancel
        </button>
      </div>
    </div>
  </div>
</template>
