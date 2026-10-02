<script setup>
import { assetUrl } from '../../../api';

defineProps({
  patient: { type: Object, required: true },
  categories: { type: Array, default: () => [] },
  documents: { type: Array, default: () => [] }
});

defineEmits(['close', 'upload']);

const documentCategory = defineModel('documentCategory');
const documentFiles = defineModel('documentFiles');
</script>

<template>
  <div class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="$emit('close')">×</button>
      <h2>Documents · {{ patient.name }}</h2>

      <div class="form-grid">
        <label>
          Category
          <select v-model="documentCategory">
            <option v-for="category in categories" :key="category.id" :value="category.name">{{ category.name }}</option>
          </select>
        </label>
        <label>
          Files
          <input type="file" multiple accept=".pdf,.jpg,.jpeg,.png" @change="documentFiles = [...$event.target.files]">
        </label>
      </div>

      <div class="modal-actions left-actions">
        <button @click="$emit('upload')">Upload Selected Documents</button>
      </div>

      <table class="table">
        <tr><th>File</th><th>Category</th><th>Date</th></tr>
        <tr v-for="document in documents" :key="document.id">
          <td><a :href="assetUrl(document.storedPath)" target="_blank">{{ document.fileName }}</a></td>
          <td>{{ document.category }}</td>
          <td>{{ new Date(document.uploadedAtUtc).toLocaleString() }}</td>
        </tr>
      </table>

      <div class="modal-actions">
        <button class="secondary" @click="$emit('close')">Close</button>
      </div>
    </div>
  </div>
</template>
