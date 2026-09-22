<script setup>
import { ref, onMounted } from 'vue';
import { api, assetUrl } from './api';
import { can } from './auth';

const logged = ref(false);
const checkingSession = ref(true);
const username = ref('');
const password = ref('');
const err = ref('');
const logo = ref(localStorage.getItem('hospitalLogo') || '');
const displayName = ref('');
const designation = ref('');
const pharmacyOpen = ref(true);
const labOpen = ref(true);
const settingsOpen = ref(false);
const masterOpen = ref(false);
const staffOpen = ref(false);
const expenseOpen = ref(false);

async function login() {
  err.value = '';

  if (!username.value.trim() || !password.value) {
    err.value = 'Username and password are required.';
    return;
  }

  try {
    const { data } = await api.post('/auth/login', {
      username: username.value.trim(),
      password: password.value
    });

    sessionStorage.setItem('token', data.token);
    sessionStorage.setItem(
      'permissions',
      JSON.stringify(data.permissions || [])
    );
    sessionStorage.setItem(
      'displayName',
      data.displayName || ''
    );
    sessionStorage.setItem(
      'designation',
      data.designation || ''
    );

    localStorage.setItem(
      'hospitalLogo',
      data.logo || ''
    );

    logo.value = data.logo || '';
    displayName.value = data.displayName || '';
    designation.value = data.designation || '';
    password.value = '';
    logged.value = true;
  } catch (e) {
    err.value =
      e.response?.data?.message ||
      'Login failed.';
  }
}

function logout() {
  sessionStorage.clear();
  location.reload();
}

const show = module => can(module, 'view');

onMounted(async () => {
  window.addEventListener('auth-expired', () => {
    logged.value = false;
    username.value = '';
    password.value = '';
  });
  try {
    const token = sessionStorage.getItem('token');

    if (token) {
      const { data } = await api.get('/auth/me');

      sessionStorage.setItem(
        'permissions',
        JSON.stringify(data.permissions || [])
      );
      sessionStorage.setItem(
        'displayName',
        data.displayName || ''
      );
      sessionStorage.setItem(
        'designation',
        data.designation || ''
      );

      displayName.value = data.displayName || '';
      designation.value = data.designation || '';
      logged.value = true;
    }
  } catch {
    sessionStorage.clear();
    logged.value = false;
  } finally {
    checkingSession.value = false;
  }

  if (!logo.value) {
    try {
      const { data } = await api.get('/settings/branding');
      const value = data.find(
        x => x.name === 'Hospital Logo'
      )?.value || '';

      logo.value = value;

      if (value) {
        localStorage.setItem('hospitalLogo', value);
      }
    } catch {
    }
  }
});
</script>

<template>
  <div v-if="checkingSession" class="login"><div class="card login-card"><h2>Checking session...</h2></div></div>

  <div v-else-if="!logged" class="login">
    <div class="card login-card">
      <img v-if="logo" :src="assetUrl(logo)" class="login-logo">
      <div v-else class="logo-placeholder">+</div>
      <h1>Krishav Health Care ERP</h1>
      <input v-model.trim="username" autocomplete="username" placeholder="Username">
      <input v-model="password" type="password" autocomplete="current-password" placeholder="Password" @keyup.enter="login">
      <button @click="login">Login</button>
      <div class="danger">{{ err }}</div>
    </div>
  </div>

  <div v-else class="layout">
    <aside>
      <div class="brand">
        <img v-if="logo" :src="assetUrl(logo)" class="menu-logo">
        <div><h2>Krishav ERP</h2><small v-if="displayName">{{ displayName }}<span v-if="designation"> · {{ designation }}</span></small></div>
      </div>

      <router-link v-if="show('DASHBOARD')" to="/">Dashboard</router-link>
      <router-link v-if="show('PATIENT')" to="/patients">Patient</router-link>
      <router-link v-if="show('OPD')" to="/opd">OPD</router-link>
      <router-link v-if="show('BILL')" to="/bills">Bill</router-link>
      <router-link v-if="show('IPD')" to="/ipd">IPD</router-link>
      <router-link v-if="show('OT')" to="/ot">OT Management</router-link>
      <router-link v-if="show('FOLLOWUP')" to="/followups">Follow-up</router-link>

      <div v-if="show('PHARMACY')" class="menu-group collapsible-menu">
        <button type="button" class="menu-collapse-button" @click="pharmacyOpen = !pharmacyOpen">
          <span>Pharmacy</span>
          <span>{{ pharmacyOpen ? '▾' : '▸' }}</span>
        </button>
        <div v-show="pharmacyOpen" class="submenu">
          <router-link class="submenu-link" to="/pharmacy/distributors">Distributor Master</router-link>
          <router-link class="submenu-link" to="/pharmacy/purchase">Purchase</router-link>
          <router-link class="submenu-link" to="/pharmacy/sales">Sales</router-link>
          <router-link class="submenu-link" to="/pharmacy/expiry">Expiry Medicine</router-link>
        </div>
      </div>

      <div v-if="show('LAB')" class="menu-group collapsible-menu">
        <button type="button" class="menu-collapse-button" @click="labOpen = !labOpen">
          <span>Lab</span>
          <span>{{ labOpen ? '▾' : '▸' }}</span>
        </button>
        <div v-show="labOpen" class="submenu">
          <router-link class="submenu-link" to="/lab/master">Add Lab Test Master</router-link>
          <router-link class="submenu-link" to="/lab/patient-tests">Add Test Patient</router-link>
        </div>
      </div>

      <div v-if="show('STAFF')" class="menu-group collapsible-menu">
        <button type="button" class="menu-collapse-button" @click="staffOpen = !staffOpen">
          <span>Staff</span>
          <span>{{ staffOpen ? '▾' : '▸' }}</span>
        </button>
        <div v-show="staffOpen" class="submenu">
          <router-link class="submenu-link" to="/staff/master">Staff Master</router-link>
          <router-link class="submenu-link" to="/staff/designations">Designation Master</router-link>
          <router-link class="submenu-link" to="/staff/attendance">Attendance</router-link>
          <router-link class="submenu-link" to="/staff/salary">Salary / Payroll</router-link>
          <router-link class="submenu-link" to="/staff/report">Staff Report</router-link>
        </div>
      </div>

      <div v-if="show('EXPENSE')" class="menu-group collapsible-menu">
        <button type="button" class="menu-collapse-button" @click="expenseOpen = !expenseOpen">
          <span>Expense</span>
          <span>{{ expenseOpen ? '▾' : '▸' }}</span>
        </button>
        <div v-show="expenseOpen" class="submenu">
          <router-link class="submenu-link" to="/expenses/daily">Daily Expense</router-link>
          <router-link class="submenu-link" to="/expenses/lab">Lab Expense</router-link>
          <router-link class="submenu-link" to="/expenses/pharmacy">Pharmacy Expense</router-link>
          <router-link class="submenu-link" to="/expenses/doctor-settlements">Doctor Settlement</router-link>
          <router-link class="submenu-link" to="/expenses/report">Expense Report</router-link>
        </div>
      </div>

      <router-link v-if="show('REPORTING')" to="/reporting">Reporting</router-link>

      <div
        v-if="
          show('DOCTOR') ||
          show('BED') ||
          show('DISCOUNT') ||
          show('REFERRAL') ||
          show('PATIENT_SOURCE') ||
          show('BILL_TYPE')
        "
        class="menu-group collapsible-menu"
      >
        <button
          type="button"
          class="menu-collapse-button"
          @click="masterOpen = !masterOpen"
        >
          <span>Master</span>
          <span>{{ masterOpen ? '▾' : '▸' }}</span>
        </button>

        <div v-show="masterOpen" class="submenu">
          <router-link
            v-if="show('DOCTOR')"
            class="submenu-link"
            to="/doctors"
          >
            Doctor Master
          </router-link>

          <router-link
            v-if="show('BED')"
            class="submenu-link"
            to="/beds"
          >
            Bed / Cabin Master
          </router-link>

          <router-link
            v-if="show('PATIENT_SOURCE')"
            class="submenu-link"
            to="/patient-sources"
          >
            Patient Source Master
          </router-link>

          <router-link
            v-if="show('BILL_TYPE')"
            class="submenu-link"
            to="/bill-types"
          >
            Bill Type Master
          </router-link>

          <router-link
            v-if="show('REFERRAL')"
            class="submenu-link"
            to="/referrals"
          >
            Referral Master
          </router-link>

          <router-link
            v-if="show('DISCOUNT')"
            class="submenu-link"
            to="/discounts"
          >
            Discount Master
          </router-link>
        </div>
      </div>

      <div
        v-if="
          show('SETTINGS') ||
          show('ROLE') ||
          show('PERMISSION') ||
          show('REFERRAL')
        "
        class="menu-group collapsible-menu"
      >
        <button
          type="button"
          class="menu-collapse-button"
          @click="settingsOpen = !settingsOpen"
        >
          <span>Settings</span>
          <span>{{ settingsOpen ? '▾' : '▸' }}</span>
        </button>

        <div v-show="settingsOpen" class="submenu">
          <router-link
            v-if="show('SETTINGS')"
            class="submenu-link"
            to="/settings"
          >
            Settings
          </router-link>

          <router-link
            v-if="show('ROLE')"
            class="submenu-link"
            to="/designations"
          >
            Designation
          </router-link>

          <router-link
            v-if="show('PERMISSION')"
            class="submenu-link"
            to="/permissions"
          >
            Permission
          </router-link>

          <router-link
            v-if="show('REFERRAL')"
            class="submenu-link"
            to="/payouts"
          >
            Payout
          </router-link>
        </div>
      </div>

      <button class="secondary" @click="logout">Logout</button>
    </aside>
    <main><router-view /></main>
  </div>
</template>
