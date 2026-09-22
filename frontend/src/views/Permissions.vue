<script setup>
import { ref, onMounted } from 'vue';
import { api } from '../api';

const roles = ref([]);
const modules = ref([]);
const selectedRoleId = ref(null);
const permissions = ref([]);

async function load() {
  const [rolesRes, modulesRes] =
    await Promise.all([
      api.get('/roles'),
      api.get('/roles/modules')
    ]);

  roles.value = rolesRes.data;
  modules.value = modulesRes.data;

  if (
    !selectedRoleId.value &&
    roles.value.length
  ) {
    selectedRoleId.value =
      roles.value[0].id;
  }

  loadRolePermissions();
}

function loadRolePermissions() {
  const role = roles.value.find(
    x => x.id === selectedRoleId.value
  );

  const existing = new Map(
    (role?.permissions || []).map(
      permission => [
        permission.module,
        permission
      ]
    )
  );

  permissions.value =
    modules.value.map(module => ({
      module,
      canView:
        existing.get(module)?.canView ||
        false,
      canAdd:
        existing.get(module)?.canAdd ||
        false,
      canEdit:
        existing.get(module)?.canEdit ||
        false,
      canDelete:
        existing.get(module)?.canDelete ||
        false
    }));
}

async function save() {
  const role = roles.value.find(
    x => x.id === selectedRoleId.value
  );

  if (!role) {
    return;
  }

  await api.put(
    '/roles/' + role.id,
    {
      name: role.name,
      description: role.description,
      permissions: permissions.value
    }
  );

  alert('Designation permissions saved.');
  await load();
}

onMounted(load);
</script>

<template>
  <h1>Permission by Designation</h1>

  <div class="toolbar">
    <label style="min-width:260px">
      Designation
      <select
        v-model.number="selectedRoleId"
        @change="loadRolePermissions"
      >
        <option
          v-for="role in roles"
          :key="role.id"
          :value="role.id"
        >
          {{ role.name }}
        </option>
      </select>
    </label>

    <button @click="save">
      Save Permissions
    </button>
  </div>

  <table class="table">
    <tr>
      <th>Module</th>
      <th>View</th>
      <th>Add</th>
      <th>Edit</th>
      <th>Delete</th>
    </tr>

    <tr
      v-for="permission in permissions"
      :key="permission.module"
    >
      <td>
        <b>{{ permission.module }}</b>
      </td>

      <td>
        <input
          type="checkbox"
          v-model="permission.canView"
        >
      </td>

      <td>
        <input
          type="checkbox"
          v-model="permission.canAdd"
        >
      </td>

      <td>
        <input
          type="checkbox"
          v-model="permission.canEdit"
        >
      </td>

      <td>
        <input
          type="checkbox"
          v-model="permission.canDelete"
        >
      </td>
    </tr>
  </table>
</template>
