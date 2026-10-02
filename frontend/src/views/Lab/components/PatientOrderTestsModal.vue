<script setup>
defineProps({
  order: { type: Object, required: true },
  tests: { type: Array, required: true }
});

const emit = defineEmits(['close', 'print-all', 'edit-result', 'print-result', 'delete-test']);
</script>

<template>
  <div class="modal-bg">
    <div class="modal modal-xl">
      <button class="modal-close-x" @click="emit('close')">×</button>
      <div class="page-head">
        <h2>{{ order.patientName }} · {{ order.orderNumber }}</h2>
        <div class="actions">
          <button v-if="tests.length" @click="emit('print-all')">Print All Results</button>
          <button class="secondary" @click="emit('close')">Close</button>
        </div>
      </div>
      <table class="table">
        <tr>
          <th>Test</th><th>Status</th><th></th>
        </tr>
        <tr v-for="t in tests" :key="t.id">
          <td>{{ t.testName }}</td>
          <td>{{ t.status }}</td>
          <td class="actions">
            <button @click="emit('edit-result', t)">Edit</button>
            <button @click="emit('print-result', t)">Print</button>
            <button class="danger-btn" @click="emit('delete-test', t)">Delete</button>
          </td>
        </tr>
      </table>
      <div class="modal-actions">
        <button class="secondary" @click="emit('close')">Close</button>
      </div>
    </div>
  </div>
</template>
