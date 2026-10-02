<script setup>
import { computed, ref } from 'vue';
import { api } from '../../../api';
import SearchableSelect from '../../../components/SearchableSelect.vue';

const props = defineProps({
  patients: { type: Array, required: true },
  tests: { type: Array, required: true },
  discounts: { type: Array, required: true }
});

const emit = defineEmits(['close', 'saved']);

const order = ref({ patientId: null, roundOff: 0, tests: [{ labTestId: null, discountTypeId: null }] });

const patientOptions = computed(() => props.patients.map(p => ({ value: p.id, label: `${p.name} · ${p.patientCode}` })));
const testOptions = computed(() => props.tests.map(t => ({ value: t.id, label: t.name })));

function addTestLine() {
  order.value.tests.push({ labTestId: null, discountTypeId: null });
}

function removeTestLine(i) {
  order.value.tests.splice(i, 1);
  if (!order.value.tests.length) addTestLine();
}

function testObj(l) {
  return props.tests.find(t => t.id === l.labTestId);
}

function linePrice(l) {
  return Number(testObj(l)?.price || 0);
}

function lineDisc(l) {
  const p = linePrice(l);
  const d = props.discounts.find(x => x.id === l.discountTypeId);
  if (!d) return 0;
  const v = Number(d.value || 0);
  return d.discountMode === 'Amount' ? Math.min(p, v) : p * Math.min(100, Math.max(0, v)) / 100;
}

function beforeTotal() {
  return order.value.tests.reduce((a, l) => a + linePrice(l), 0);
}

function afterTotal() {
  return order.value.tests.reduce((a, l) => a + linePrice(l) - lineDisc(l), 0);
}

// Small manual nudge (-9..9) folded into the bill total, e.g. 448 -> 450; never a separate print line.
function finalTotal() {
  return afterTotal() + Number(order.value.roundOff || 0);
}

async function saveOrder() {
  if (!order.value.patientId) return alert('Select patient.');
  if (order.value.tests.some(x => !x.labTestId)) return alert('Select test for every row.');
  if (order.value.roundOff < -9 || order.value.roundOff > 9) return alert('Round off must be between -9 and 9.');
  await api.post('/lab/orders', order.value);
  emit('saved');
}
</script>

<template>
  <div class="modal-bg">
    <div class="modal modal-xl">
      <button class="modal-close-x" @click="emit('close')">×</button>
      <h2>Add Tests for Patient</h2>
      <label>
        Patient *
        <SearchableSelect v-model="order.patientId" :options="patientOptions" placeholder="Type patient name or code..." />
      </label>
      <table class="table" style="margin-top:16px">
        <tr>
          <th>Test</th><th>Price</th><th>Approved Discount</th><th>Discount</th><th>After Discount</th><th></th>
        </tr>
        <tr v-for="(l,i) in order.tests" :key="i">
          <td>
            <SearchableSelect v-model="l.labTestId" :options="testOptions" placeholder="Type test name..." />
          </td>
          <td>₹{{ linePrice(l).toFixed(2) }}</td>
          <td>
            <select v-model.number="l.discountTypeId">
              <option :value="null">No Discount</option>
              <option v-for="d in discounts" :key="d.id" :value="d.id">
                {{ d.name }} · {{ d.discountMode==='Percent' ? d.value+'%' : '₹'+Number(d.value).toFixed(2) }}
              </option>
            </select>
          </td>
          <td>₹{{ lineDisc(l).toFixed(2) }}</td>
          <td>₹{{ (linePrice(l)-lineDisc(l)).toFixed(2) }}</td>
          <td><button class="danger-btn" @click="removeTestLine(i)">×</button></td>
        </tr>
      </table>
      <button @click="addTestLine">+ Add Test</button>
      <div class="bill-total-box">
        <div><span>Before Discount</span><b>₹{{ beforeTotal().toFixed(2) }}</b></div>
        <div class="grand"><span>After Discount</span><b>₹{{ afterTotal().toFixed(2) }}</b></div>
        <div>
          <span>Round Off (-9 to 9)</span>
          <input v-model.number="order.roundOff" type="number" min="-9" max="9" step="1" style="width:80px;display:inline-block">
        </div>
        <div class="grand"><span>Final Amount</span><b>₹{{ finalTotal().toFixed(2) }}</b></div>
      </div>
      <div class="modal-actions">
        <button @click="saveOrder">Create Tests + Bill</button>
        <button class="secondary" @click="emit('close')">Cancel</button>
      </div>
    </div>
  </div>
</template>
