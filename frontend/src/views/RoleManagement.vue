<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';

const roles = ref([]);
const users = ref([]);
const showRole = ref(false);
const showUser = ref(false);
const roleForm = ref({
  id: null,
  name: '',
  description: '',
  permissions: []
});
const userForm = ref({
  id: null,
  displayName: '',
  username: '',
  password: '',
  roleId: null,
  isActive: true
});
const roleError = ref('');
const userError = ref('');

const {
  search: roleSearch,
  page: rolePage,
  pageCount: rolePageCount,
  pagedRows: pagedRoles,
  sortedRows: sortedRoles
} = useGrid(roles, 10);

const {
  search: userSearch,
  page: userPage,
  pageCount: userPageCount,
  pagedRows: pagedUsers,
  sortedRows: sortedUsers
} = useGrid(users, 10);

async function load() {
  roles.value = (await api.get('/roles')).data;
  users.value = (await api.get('/roles/users')).data;
}

function addRole() {
  roleError.value = '';
  roleForm.value = {
    id: null,
    name: '',
    description: '',
    permissions: []
  };
  showRole.value = true;
}

function editRole(role) {
  roleError.value = '';
  roleForm.value = {
    id: role.id,
    name: role.name,
    description: role.description,
    permissions: role.permissions || []
  };
  showRole.value = true;
}

async function saveRole() {
  roleError.value = '';

  if (!roleForm.value.name.trim()) {
    roleError.value = 'Designation name is required.';
    return;
  }

  try {
    if (roleForm.value.id) {
      await api.put(
        '/roles/' + roleForm.value.id,
        roleForm.value
      );
    } else {
      await api.post(
        '/roles',
        roleForm.value
      );
    }
  } catch (error) {
    roleError.value = error.response?.data?.message || 'Unable to save designation.';
    return;
  }

  showRole.value = false;
  await load();
}

async function deleteRole(role) {
  if (!confirm(`Delete designation ${role.name}?`)) {
    return;
  }

  await api.delete('/roles/' + role.id);
  await load();
}

function addUser() {
  userError.value = '';
  userForm.value = {
    id: null,
    displayName: '',
    username: '',
    password: '',
    roleId: null,
    isActive: true
  };
  showUser.value = true;
}

function editUser(user) {
  userError.value = '';
  userForm.value = {
    id: user.id,
    displayName: user.displayName,
    username: user.username,
    password: '',
    roleId: user.roleId,
    isActive: user.isActive
  };
  showUser.value = true;
}

async function saveUser() {
  userError.value = '';

  if (userForm.value.id) {
    if (!userForm.value.displayName || !userForm.value.roleId) {
      userError.value = 'Name and designation are required.';
      return;
    }
  } else if (
    !userForm.value.displayName ||
    !userForm.value.username ||
    !userForm.value.password ||
    !userForm.value.roleId
  ) {
    userError.value = 'Name, username, password and designation are required.';
    return;
  }

  try {
    if (userForm.value.id) {
      await api.put('/roles/users/' + userForm.value.id, userForm.value);
    } else {
      await api.post('/roles/users', userForm.value);
    }
  } catch (error) {
    userError.value = error.response?.data?.message || 'Unable to save user.';
    return;
  }

  showUser.value = false;
  await load();
}

async function assign(user, event) {
  await api.put(
    `/roles/users/${user.id}/role/${Number(event.target.value)}`
  );

  await load();
}

onMounted(load);
</script>

<template>
  <h1>Designation</h1>

  <div class="toolbar">
    <button
      v-if="can('ROLE', 'add')"
      @click="addRole"
    >
      + Create Designation
    </button>

    <button
      v-if="can('ROLE', 'add')"
      @click="addUser"
    >
      + Add User
    </button>
  </div>

  <h2>User Designation Assignment</h2>

  <GridSearch
    v-model="userSearch"
    placeholder="Search users..."
  />

  <table class="table">
    <tr>
      <th>User</th>
      <th>Display Name</th>
      <th>Designation</th>
      <th>Status</th>
      <th></th>
    </tr>

    <tr
      v-for="user in pagedUsers"
      :key="user.id"
    >
      <td>{{ user.username }}</td>
      <td>{{ user.displayName }}</td>
      <td>
        <select
          :value="user.roleId"
          @change="assign(user, $event)"
        >
          <option
            v-for="role in roles"
            :key="role.id"
            :value="role.id"
          >
            {{ role.name }}
          </option>
        </select>
      </td>
      <td>{{ user.isActive ? 'Active' : 'Inactive' }}</td>
      <td class="actions">
        <button
          v-if="can('ROLE', 'edit')"
          @click="editUser(user)"
        >
          Edit
        </button>
      </td>
    </tr>
  </table>

  <Pagination
    :page="userPage"
    :page-count="userPageCount"
    :total="sortedUsers.length"
    :page-size="10"
    @update:page="userPage = $event"
  />

  <h2 style="margin-top:28px">
    Designations
  </h2>

  <GridSearch
    v-model="roleSearch"
    placeholder="Search designations..."
  />

  <table class="table">
    <tr>
      <th>Designation</th>
      <th>Description</th>
      <th></th>
    </tr>

    <tr
      v-for="role in pagedRoles"
      :key="role.id"
    >
      <td>{{ role.name }}</td>
      <td>{{ role.description }}</td>
      <td class="actions">
        <button
          v-if="can('ROLE', 'edit')"
          @click="editRole(role)"
        >
          Edit
        </button>

        <button
          v-if="can('ROLE', 'delete')"
          class="danger-btn"
          @click="deleteRole(role)"
        >
          Delete
        </button>
      </td>
    </tr>
  </table>

  <Pagination
    :page="rolePage"
    :page-count="rolePageCount"
    :total="sortedRoles.length"
    :page-size="10"
    @update:page="rolePage = $event"
  />

  <div
    v-if="showRole"
    class="modal-bg"
  >
    <div class="modal">
      <button class="modal-close-x" @click="showRole = false">×</button>
      <h2>
        {{ roleForm.id ? 'Edit' : 'Create' }}
        Designation
      </h2>

      <div v-if="roleError" class="error-box">{{ roleError }}</div>

      <div class="form-grid">
        <label>
          Designation Name *
          <input
            v-model="roleForm.name"
          >
        </label>

        <label>
          Description
          <input
            v-model="roleForm.description"
          >
        </label>
      </div>

      <div class="modal-actions">
        <button @click="saveRole">
          Save
        </button>

        <button
          class="secondary"
          @click="showRole = false"
        >
          Cancel
        </button>
      </div>
    </div>
  </div>

  <div
    v-if="showUser"
    class="modal-bg"
  >
    <div class="modal">
      <button class="modal-close-x" @click="showUser = false">×</button>
      <h2>{{ userForm.id ? 'Edit' : 'Add' }} User</h2>

      <div v-if="userError" class="error-box">{{ userError }}</div>

      <div class="form-grid">
        <label>
          Name *
          <input
            v-model="
              userForm.displayName
            "
          >
        </label>

        <label>
          Username *
          <input
            v-model="
              userForm.username
            "
            :disabled="!!userForm.id"
          >
        </label>

        <label>
          Password{{ userForm.id ? ' (leave blank to keep current)' : ' *' }}
          <input
            type="password"
            v-model="
              userForm.password
            "
          >
        </label>

        <label v-if="userForm.id" class="toggle-row">
          <input type="checkbox" v-model="userForm.isActive"> Active
        </label>

        <label>
          Designation *
          <select
            v-model.number="
              userForm.roleId
            "
          >
            <option :value="null">
              Select Designation
            </option>

            <option
              v-for="role in roles"
              :key="role.id"
              :value="role.id"
            >
              {{ role.name }}
            </option>
          </select>
        </label>
      </div>

      <div class="modal-actions">
        <button @click="saveUser">
          {{ userForm.id ? 'Save' : 'Create User' }}
        </button>

        <button
          class="secondary"
          @click="showUser = false"
        >
          Cancel
        </button>
      </div>
    </div>
  </div>
</template>
