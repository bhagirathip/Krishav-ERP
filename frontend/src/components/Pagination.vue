<script setup>
const props = defineProps({
  page: { type: Number, required: true },
  pageCount: { type: Number, required: true },
  total: { type: Number, required: true },
  pageSize: { type: Number, default: 10 }
});

const emit = defineEmits(['update:page']);

function go(page) {
  const next = Math.min(Math.max(page, 1), Math.max(props.pageCount, 1));
  emit('update:page', next);
}
</script>

<template>
  <div v-if="pageCount > 1" class="pagination-bar">
    <div class="muted">
      Showing {{ ((page - 1) * pageSize) + 1 }}–{{ Math.min(page * pageSize, total) }} of {{ total }}
    </div>
    <div class="pagination-actions">
      <button class="secondary" :disabled="page <= 1" @click="go(page - 1)">Previous</button>
      <span>Page {{ page }} of {{ pageCount }}</span>
      <button class="secondary" :disabled="page >= pageCount" @click="go(page + 1)">Next</button>
    </div>
  </div>
</template>
