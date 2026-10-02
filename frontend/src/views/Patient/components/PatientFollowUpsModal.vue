<script setup>
defineProps({
  patient: { type: Object, required: true },
  followUps: { type: Array, default: () => [] }
});

defineEmits(['close']);
</script>

<template>
  <div class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="$emit('close')">×</button>
      <div class="page-head">
        <div>
          <h2>Follow-ups · {{ patient.name }}</h2>
          <div class="muted">
            {{ patient.patientCode }} · {{ patient.phone }}
          </div>
        </div>
        <button class="secondary" @click="$emit('close')">Close</button>
      </div>

      <table class="table">
        <tr>
          <th>Follow-up Date</th>
          <th>Status</th>
          <th>Comment</th>
          <th>Next Follow-up</th>
          <th>Completed</th>
        </tr>

        <tr v-for="row in followUps" :key="row.id">
          <td>{{ String(row.followUpDate).slice(0, 10) }}</td>
          <td>{{ row.status }}</td>
          <td>{{ row.comment || '-' }}</td>
          <td>
            {{
              row.nextFollowUpDate
                ? String(row.nextFollowUpDate).slice(0, 10)
                : '-'
            }}
          </td>
          <td>
            {{
              row.completedAtUtc
                ? new Date(row.completedAtUtc).toLocaleString()
                : '-'
            }}
          </td>
        </tr>
      </table>

      <div v-if="!followUps.length" class="muted">
        No follow-up records for this patient.
      </div>

      <div class="modal-actions">
        <button class="secondary" @click="$emit('close')">Close</button>
      </div>
    </div>
  </div>
</template>
