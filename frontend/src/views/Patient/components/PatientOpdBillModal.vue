<script setup>
import { ageText } from '../../../utils/age';

defineProps({
  patient: { type: Object, default: null },
  opdBillTypes: { type: Array, default: () => [] },
  doctors: { type: Array, default: () => [] },
  marketingSources: { type: Array, default: () => [] },
  referrers: { type: Array, default: () => [] }
});

defineEmits(['close', 'submit']);

const form = defineModel('form', { required: true });
</script>

<template>
  <div class="modal-bg">
    <div class="modal modal-lg">
      <button class="modal-close-x" @click="$emit('close')">×</button>
      <div class="page-head">
        <div>
          <h2>Create OPD Bill</h2>
          <div class="muted" v-if="patient">
            {{ patient.patientCode }} ·
            {{ patient.name }} ·
            {{ ageText(patient) }}/{{ patient.gender }} ·
            {{ patient.phone }}
          </div>
        </div>

        <button
          class="secondary"
          @click="$emit('close')"
        >
          Close
        </button>
      </div>

      <div class="form-grid">
        <label>
          OPD Type *
          <select v-model="form.visitType">
            <option
              v-for="type in opdBillTypes"
              :key="type.id"
              :value="type.name"
            >
              {{ type.name }}
            </option>
          </select>
        </label>

        <label>
          Doctor *
          <select v-model.number="form.doctorId">
            <option :value="null">Select Doctor</option>
            <option
              v-for="doctor in doctors.filter(x => x.isActive)"
              :key="doctor.id"
              :value="doctor.id"
            >
              {{ doctor.name }}
            </option>
          </select>
        </label>

        <label>
          Patient Source
          <select v-model="form.marketingSource">
            <option
              v-for="source in marketingSources"
              :key="source"
              :value="source"
            >
              {{ source }}
            </option>
          </select>
        </label>

        <label>
          Referrer
          <select v-model.number="form.referrerId">
            <option :value="null">No Referrer</option>
            <option
              v-for="referrer in referrers"
              :key="referrer.id"
              :value="referrer.id"
            >
              {{ referrer.referrerCode }} · {{ referrer.name }}
            </option>
          </select>
        </label>

        <label>
          BP
          <input v-model="form.bloodPressure" placeholder="120/80">
        </label>

        <label>
          Temperature °F
          <input type="number" step="0.1" v-model.number="form.temperatureC">
        </label>

        <label>
          Pulse (bpm)
          <input type="number" min="30" max="250" v-model.number="form.pulse">
        </label>

        <label>
          Weight Kg
          <input type="number" step="0.1" v-model.number="form.weightKg">
        </label>

        <label>
          Height cm
          <input type="number" step="0.1" v-model.number="form.heightCm">
        </label>

        <label>
          SpO₂ %
          <input type="number" v-model.number="form.spo2">
        </label>

        <label>
          Follow-up Date
          <input type="date" v-model="form.followUpDate">
        </label>

        <label class="full">
          Chief Complaint
          <textarea rows="4" v-model="form.chiefComplaint"></textarea>
        </label>
      </div>

      <div class="modal-actions">
        <button @click="$emit('submit')">
          Create OPD Bill
        </button>

        <button
          class="secondary"
          @click="$emit('close')"
        >
          Cancel
        </button>
      </div>
    </div>
  </div>
</template>
