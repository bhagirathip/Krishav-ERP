<script setup>
import { ref } from 'vue';
import { api } from '../../../api';

defineProps({
  medicineTypes: { type: Array, default: () => [] }
});

const emit = defineEmits(['close', 'updated']);

const typeForm = ref({ id: null, name: '' });

async function saveType() {
  if (!typeForm.value.name.trim()) return;
  if (typeForm.value.id) {
    await api.put(`/pharmacy/medicine-types/${typeForm.value.id}`, typeForm.value);
  } else {
    await api.post('/pharmacy/medicine-types', typeForm.value);
  }
  typeForm.value = { id: null, name: '' };
  const { data } = await api.get('/pharmacy/medicine-types');
  emit('updated', data);
}

async function deleteType(row) {
  if (!confirm(`Delete medicine type "${row.name}"?`)) return;
  try {
    await api.delete(`/pharmacy/medicine-types/${row.id}`);
    const { data } = await api.get('/pharmacy/medicine-types');
    emit('updated', data);
  } catch (error) {
    alert(error.response?.data?.message || 'Unable to delete medicine type.');
  }
}
</script>

<template>
  <div class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="emit('close')">×</button>
      <h2>Medicine Type Master</h2>
      <div class="form-grid"><label>Type Name *<input v-model="typeForm.name" placeholder="Tablet, Cream, Injection, Spray"></label></div>
      <div class="toolbar"><button @click="saveType">{{ typeForm.id ? 'Update' : 'Add' }} Type</button></div>
      <table class="table"><tr><th>Type</th><th></th></tr><tr v-for="t in medicineTypes" :key="t.id"><td>{{ t.name }}</td><td class="actions"><button @click="typeForm={id:t.id,name:t.name}">Edit</button><button class="danger-btn" @click="deleteType(t)">Delete</button></td></tr></table>
      <div class="modal-actions"><button class="secondary" @click="emit('close')">Close</button></div>
    </div>
  </div>
</template>
