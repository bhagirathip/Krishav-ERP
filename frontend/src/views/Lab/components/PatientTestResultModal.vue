<script setup>
defineProps({
  data: { type: Object, required: true },
  noteHtml: { type: String, default: '' }
});

const emit = defineEmits(['close', 'save']);
</script>

<template>
  <div class="modal-bg">
    <div class="modal modal-xl">
      <button class="modal-close-x" @click="emit('close')">×</button>
      <div class="page-head">
        <div>
          <h2>{{ data.testName }} · {{ data.patient?.name }}</h2>
        </div>
        <button class="secondary" @click="emit('close')">Close</button>
      </div>

      <div v-if="!data.columns.length" class="error-box">
        No Lab Test Master columns were found for this test.
        Please open Lab Test Master, save the test again, and reopen this result.
      </div>

      <div v-else class="lab-result-dynamic-wrap">
        <table class="table lab-result-dynamic-table">
          <thead>
            <tr>
              <th v-for="column in data.columns" :key="column.id">{{ column.name }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in data.rows" :key="row.id">
              <td v-for="column in data.columns" :key="column.id">
                <textarea rows="2" class="lab-master-cell" v-model="row.values[column.id]" :placeholder="column.name"></textarea>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="data.testNote" class="note-box">
        <b>Note:</b> <span v-html="noteHtml"></span>
      </div>

      <div class="modal-actions">
        <button @click="emit('save')">Save Results</button>
        <button class="secondary" @click="emit('close')">Cancel</button>
      </div>
    </div>
  </div>
</template>
