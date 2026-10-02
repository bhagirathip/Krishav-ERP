<script setup>
defineProps({
  categories: { type: Array, default: () => [] }
});

defineEmits(['close', 'edit', 'cancel-edit', 'save', 'delete']);

const form = defineModel('form', { required: true });
</script>

<template>
  <div class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="$emit('close')">×</button>
      <h2>Document Category Master</h2>

      <div class="toolbar">
        <input v-model="form.name" style="max-width: 360px" placeholder="Category name">
        <button @click="$emit('save')">{{ form.id ? 'Update' : 'Add Category' }}</button>
        <button v-if="form.id" class="secondary" @click="$emit('cancel-edit')">Cancel Edit</button>
      </div>

      <table class="table">
        <tr><th>Category</th><th></th></tr>
        <tr v-for="category in categories" :key="category.id">
          <td>{{ category.name }}</td>
          <td class="actions">
            <button @click="$emit('edit', category)">Edit</button>
            <button class="danger-btn" @click="$emit('delete', category)">Delete</button>
          </td>
        </tr>
      </table>

      <div class="modal-actions">
        <button class="secondary" @click="$emit('close')">Close</button>
      </div>
    </div>
  </div>
</template>
