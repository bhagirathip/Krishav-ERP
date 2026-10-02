<script setup>
import { ref, watch, onMounted } from 'vue';

const props = defineProps({
  modelValue: { type: String, default: '' }
});
const emit = defineEmits(['update:modelValue']);

const editor = ref(null);

onMounted(() => {
  if (editor.value) editor.value.innerHTML = props.modelValue || '';
});

// Keep the editor's DOM in sync when the bound value changes from outside
// (e.g. opening a different test to edit). Skipped while the editor itself
// has focus: each keystroke's 'input' event round-trips through the parent's
// v-model and back into this watcher, and since that round trip is async,
// a burst of fast typing can have this watcher apply an already-stale prop
// value on top of DOM the user has since typed further into, wiping it out
// mid-edit. onInput already applied the user's own keystrokes to modelValue,
// so skipping the resync while focused loses nothing.
watch(() => props.modelValue, (value) => {
  if (editor.value && document.activeElement !== editor.value && editor.value.innerHTML !== (value || '')) {
    editor.value.innerHTML = value || '';
  }
});

function onInput() {
  emit('update:modelValue', editor.value.innerHTML);
}

// mousedown.prevent (rather than click) keeps the contenteditable's text
// selection intact - a click would blur the editor first and collapse the
// selection before execCommand ever runs on it.
function exec(command) {
  editor.value.focus();
  document.execCommand(command, false, null);
  onInput();
}
</script>

<template>
  <div class="rich-text-editor">
    <div class="rich-text-toolbar">
      <button type="button" class="secondary compact-button" @mousedown.prevent="exec('bold')" title="Bold"><b>B</b></button>
      <button type="button" class="secondary compact-button" @mousedown.prevent="exec('italic')" title="Italic"><i>I</i></button>
      <button type="button" class="secondary compact-button" @mousedown.prevent="exec('underline')" title="Underline"><u>U</u></button>
      <button type="button" class="secondary compact-button" @mousedown.prevent="exec('insertUnorderedList')" title="Bullet list">&bull; List</button>
      <button type="button" class="secondary compact-button" @mousedown.prevent="exec('insertOrderedList')" title="Numbered list">1. List</button>
      <button type="button" class="secondary compact-button" @mousedown.prevent="exec('removeFormat')" title="Clear formatting">Clear</button>
    </div>
    <div ref="editor" class="rich-text-area" contenteditable="true" @input="onInput"></div>
  </div>
</template>

<style scoped>
.rich-text-editor { border: 1px solid #cbd5e1; border-radius: 7px; overflow: hidden; }
.rich-text-toolbar { display: flex; gap: 6px; padding: 6px; background: #f8fafc; border-bottom: 1px solid #e2e8f0; }
.rich-text-area { min-height: 90px; padding: 8px 10px; outline: none; font-size: 14px; }
.rich-text-area ul, .rich-text-area ol { margin: 4px 0; padding-left: 22px; }
</style>
