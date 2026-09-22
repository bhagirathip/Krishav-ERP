<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../api';
import { can } from '../auth';
import Pagination from '../components/Pagination.vue';
import GridSearch from '../components/GridSearch.vue';
import { useGrid } from '../composables/useGrid';

const rows = ref([]);
const modules = ref([]);
const users = ref([]);
const show = ref(false);
const userShow = ref(false);
const userForm = ref({ displayName: '', username: '', password: '', roleId: null });
const form = ref({ name: '', description: '', permissions: [] });
const pageSize = 10;

const {
  search: userSearch,
  page: userPage,
  pageCount: userPageCount,
  pagedRows: pagedUsers,
  sortedRows: sortedUsers,
  sortBy: sortUser,
  sortIndicator: userSortIndicator
} = useGrid(users, pageSize);

const {
  search: roleSearch,
  page: rolePage,
  pageCount: rolePageCount,
  pagedRows: pagedRoles,
  sortedRows: sortedRoles,
  sortBy: sortRole,
  sortIndicator: roleSortIndicator
} = useGrid(rows, pageSize);

async function load() {
  rows.value = (await api.get('/roles')).data;
  modules.value = (await api.get('/roles/modules')).data;
  users.value = (await api.get('/roles/users')).data;
}

function blankPerms() {
  return modules.value.map(module => ({
    module,
    canView: false,
    canAdd: false,
    canEdit: false,
    canDelete: false
  }));
}

function add() {
  form.value = { name: '', description: '', permissions: blankPerms() };
  show.value = true;
}

function edit(role) {
  const map = new Map((role.permissions || []).map(permission => [permission.module, permission]));
  form.value = {
    id: role.id,
    name: role.name,
    description: role.description,
    permissions: modules.value.map(module =>
      map.get(module)
        ? { ...map.get(module) }
        : { module, canView: false, canAdd: false, canEdit: false, canDelete: false }
    )
  };
  show.value = true;
}

async function saveUser() {
  if (!userForm.value.displayName || !userForm.value.username || !userForm.value.password || !userForm.value.roleId) {
    alert('Name, username, password and role are required.');
    return;
  }

  await api.post('/roles/users', userForm.value);
  userShow.value = false;
  userForm.value = { displayName: '', username: '', password: '', roleId: null };
  await load();
}

async function save() {
  if (form.value.id) await api.put('/roles/' + form.value.id, form.value);
  else await api.post('/roles', form.value);
  show.value = false;
  await load();
}

async function assign(user, event) {
  await api.put(`/roles/users/${user.id}/role/${Number(event.target.value)}`);
  await load();
}

async function del(role) {
  await api.delete('/roles/' + role.id);
  await load();
}

onMounted(load);
</script>

<template>
  <h1>Role & Permission</h1>

  <div class="toolbar">
    <button v-if="can('ROLE', 'add')" @click="add">+ Create Role</button>
    <button v-if="can('ROLE', 'add')" @click="userShow = true">+ Add User</button>
  </div>

  <h2>User Role Assignment</h2>
  <div class="grid-filter-row">
    <GridSearch v-model="userSearch" placeholder="Search all user fields..." />
  </div>

  <table class="table">
    <tr>
      <th class="sortable" @click="sortUser('username')">User <span class="sort-indicator">{{ userSortIndicator('username') }}</span></th>
      <th class="sortable" @click="sortUser('displayName')">Display Name <span class="sort-indicator">{{ userSortIndicator('displayName') }}</span></th>
      <th class="sortable" @click="sortUser('roleId')">Role <span class="sort-indicator">{{ userSortIndicator('roleId') }}</span></th>
    </tr>
    <tr v-for="user in pagedUsers" :key="user.id">
      <td>{{ user.username }}</td>
      <td>{{ user.displayName }}</td>
      <td>
        <select :value="user.roleId" @change="assign(user, $event)">
          <option v-for="role in rows" :key="role.id" :value="role.id">{{ role.name }}</option>
        </select>
      </td>
    </tr>
  </table>

  <Pagination
    :page="userPage"
    :page-count="userPageCount"
    :total="sortedUsers.length"
    :page-size="pageSize"
    @update:page="userPage = $event"
  />

  <h2 style="margin-top:28px">Roles</h2>
  <div class="grid-filter-row">
    <GridSearch v-model="roleSearch" placeholder="Search all role fields..." />
  </div>

  <table class="table">
    <tr>
      <th class="sortable" @click="sortRole('name')">Role <span class="sort-indicator">{{ roleSortIndicator('name') }}</span></th>
      <th class="sortable" @click="sortRole('description')">Description <span class="sort-indicator">{{ roleSortIndicator('description') }}</span></th>
      <th></th>
    </tr>
    <tr v-for="role in pagedRoles" :key="role.id">
      <td>{{ role.name }}</td>
      <td>{{ role.description }}</td>
      <td class="actions">
        <button v-if="can('ROLE', 'edit')" @click="edit(role)">Edit</button>
        <button v-if="can('ROLE', 'delete')" class="danger-btn" @click="del(role)">Delete</button>
      </td>
    </tr>
  </table>

  <Pagination
    :page="rolePage"
    :page-count="rolePageCount"
    :total="sortedRoles.length"
    :page-size="pageSize"
    @update:page="rolePage = $event"
  />

  <div v-if="show" class="modal-bg">
    <div class="modal modal-xl">
      <h2>{{ form.id ? 'Edit' : 'Create' }} Role</h2>
      <div class="form-grid">
        <label>Role Name *<input v-model="form.name"></label>
        <label>Description<input v-model="form.description"></label>
      </div>
      <table class="table" style="margin-top:18px">
        <tr><th>Module</th><th>View</th><th>Add</th><th>Edit</th><th>Delete</th></tr>
        <tr v-for="permission in form.permissions" :key="permission.module">
          <td>{{ permission.module }}</td>
          <td><input type="checkbox" v-model="permission.canView"></td>
          <td><input type="checkbox" v-model="permission.canAdd"></td>
          <td><input type="checkbox" v-model="permission.canEdit"></td>
          <td><input type="checkbox" v-model="permission.canDelete"></td>
        </tr>
      </table>
      <div class="modal-actions">
        <button @click="save">Save Role</button>
        <button class="secondary" @click="show = false">Cancel</button>
      </div>
    </div>
  </div>

  <div v-if="userShow" class="modal-bg">
    <div class="modal">
      <h2>Add User</h2>
      <div class="form-grid">
        <label>Name *<input v-model="userForm.displayName"></label>
        <label>Username *<input v-model="userForm.username"></label>
        <label>Password *<input type="password" v-model="userForm.password"></label>
        <label>Role *
          <select v-model.number="userForm.roleId">
            <option :value="null">Select</option>
            <option v-for="role in rows" :key="role.id" :value="role.id">{{ role.name }}</option>
          </select>
        </label>
      </div>
      <div class="modal-actions">
        <button @click="saveUser">Create User</button>
        <button class="secondary" @click="userShow = false">Cancel</button>
      </div>
    </div>
  </div>
</template>
