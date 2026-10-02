<script setup>
// A type-to-filter combobox: unlike a free-text input, the model only ever
// changes when an option is actually clicked - typing just filters the list,
// so the user can't submit something that isn't one of the given options.
import { ref, computed, watch } from 'vue';

const props = defineProps({
  modelValue: { type: [Number, String, null], default: null },
  options: { type: Array, required: true }, // [{ value, label }]
  placeholder: { type: String, default: 'Type to search...' }
});
const emit = defineEmits(['update:modelValue']);

const query = ref('');
const open = ref(false);
const root = ref(null);

function labelFor(value) {
  return props.options.find(o => o.value === value)?.label || '';
}

// Keep the visible text in sync whenever the selected value changes from
// outside (e.g. the form is reset) rather than just on first mount.
watch(() => [props.modelValue, props.options], () => {
  query.value = labelFor(props.modelValue);
}, { immediate: true });

const filtered = computed(() => {
  const q = query.value.trim().toLowerCase();
  const pool = q ? props.options.filter(o => o.label.toLowerCase().includes(q)) : props.options;
  return pool.slice(0, 50);
});

function select(option) {
  emit('update:modelValue', option.value);
  query.value = option.label;
  open.value = false;
}

function onBlur() {
  // Give a mousedown-selected option time to register before we decide
  // whether to snap the text back to the last real selection.
  setTimeout(() => {
    open.value = false;
    query.value = labelFor(props.modelValue);
  }, 150);
}
</script>

<template>
  <div class="searchable-select" ref="root">
    <input
      type="text"
      v-model="query"
      :placeholder="placeholder"
      autocomplete="off"
      @focus="open = true"
      @input="open = true"
      @blur="onBlur"
    >
    <div v-if="open && filtered.length" class="searchable-select-options">
      <div v-for="opt in filtered" :key="opt.value" @mousedown.prevent="select(opt)">{{ opt.label }}</div>
    </div>
    <div v-else-if="open && query.trim()" class="searchable-select-options">
      <div class="searchable-select-empty">No match - pick from the list</div>
    </div>
  </div>
</template>

<style scoped>
.searchable-select { position: relative; }
.searchable-select-options {
  position: absolute;
  z-index: 30;
  top: 100%;
  left: 0;
  right: 0;
  margin-top: 2px;
  background: #fff;
  border: 1px solid #cbd5e1;
  border-radius: 7px;
  max-height: 220px;
  overflow: auto;
  box-shadow: 0 4px 14px rgba(0, 0, 0, .12);
}
.searchable-select-options div { padding: 8px 10px; cursor: pointer; }
.searchable-select-options div:hover { background: #eef2ff; }
.searchable-select-empty { color: #6b7280; cursor: default; font-size: 13px; }
.searchable-select-empty:hover { background: transparent; }
</style>
