<script setup>
defineProps({
  modelValue: { type: String, default: '' },
  placeholder: { type: String, default: 'Search all fields...' }
});

defineEmits(['update:modelValue']);
</script>

<template>
  <div class="grid-search-box">
    <span>🔎</span>
    <input
      :value="modelValue"
      :placeholder="placeholder"
      @input="$emit('update:modelValue', $event.target.value)"
    >
  </div>
</template>
