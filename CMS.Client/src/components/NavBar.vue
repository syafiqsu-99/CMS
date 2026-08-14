<template>
  <v-layout class="h-screen">
    <v-navigation-drawer expand-on-hover permanent rail class="nav-bar" @update:rail="isRail = $event">
      <v-list class="flex-shrink-0">
        <v-list-item prepend-avatar="/JJlogo.png" subtitle="CMS" title="Central Monitoring System"></v-list-item>
      </v-list>

      <v-divider></v-divider>

      <div class="nav-scroll flex-grow-1">
        <v-list nav>
          <v-list-item prepend-icon="mdi-monitor-dashboard" title="Dashboard" :to="{ name: 'dashboard' }"
            link></v-list-item>

          <v-list-item prepend-icon="mdi-view-comfy" title="Machines" :to="{ name: 'machines' }" link></v-list-item>

          <v-list-item prepend-icon="mdi-database" title="OEE" :to="{ name: 'oee' }" link></v-list-item>

          <v-list-group v-if="loggedIn" value="supervisor">
            <template #activator="{ props }">
              <v-list-item v-bind="props" prepend-icon="mdi-account" title="Supervisor" />
            </template>
            <v-list-item v-for="item in supervisorChildren" :key="item.name" :prepend-icon="item.icon"
              :title="item.label" :to="{ name: item.name }" link />
          </v-list-group>

          <v-list-group v-if="loggedIn" value="setting">
            <template #activator="{ props }">
              <v-list-item v-bind="props" prepend-icon="mdi-cog" title="Setting" />
            </template>
            <v-list-item v-for="item in settingChildren" :key="item.name" :prepend-icon="item.icon" :title="item.label"
              :to="{ name: item.name }" link />
          </v-list-group>
        </v-list>
      </div>

      <template #append>
        <v-divider />

        <v-list class="flex-shrink-0">
          <v-list-item prepend-icon="mdi-login" title="Login" v-if="!loggedIn" @click="dialog = true" />

          <v-list-item prepend-icon="mdi-logout" title="Logout" v-else @click="logout" />
        </v-list>
      </template>
    </v-navigation-drawer>

    <v-main class="d-flex flex-column" style="height: 100vh;">
      <router-view />
    </v-main>

    <v-dialog v-model="dialog" width="400px">
      <v-card class="pa-4">
        <h3 class="mb-4">Enter Password</h3>

        <v-text-field v-model="password" variant="outlined" label="Password" type="password" @keyup.enter="login" />

        <v-btn block color="primary" class="mt-3" @click="login">
          Login
        </v-btn>
      </v-card>
    </v-dialog>
  </v-layout>
</template>

<script setup>
import { ref, inject } from 'vue';
import { useRouter } from 'vue-router';
import { ADMIN_PASSWORD } from '@/utils/constant.js';

const router = useRouter();

const showSnackbar = inject("showSnackbar");
const dialog = ref(false);
const password = ref('');
const loggedIn = ref(localStorage.getItem('logged_in') === 'true');
const isRail = ref(true);

const supervisorChildren = [
  { name: 'supervisor-prod-report', label: 'Production Report', icon: 'mdi-cog-outline' },
  { name: 'supervisor-machine-management', label: 'Machine Management', icon: 'mdi-robot-industrial' },
  { name: 'supervisor-staff-assignment', label: 'Staff Assignment', icon: 'mdi-account-clock' },
  { name: 'supervisor-product-database', label: 'Product Database', icon: 'mdi-archive' },
];

const settingChildren = [
  { name: 'setting-password', label: 'Password', icon: 'mdi-lock-outline' },
  { name: 'setting-plc-signals', label: 'PLC Signals', icon: 'mdi-sine-wave' },
  { name: 'setting-report', label: 'Report', icon: 'mdi-file-excel-outline' },
  { name: 'setting-import-report', label: 'Import Report', icon: 'mdi-upload' },
  { name: 'setting-machine-names', label: 'Machine Names', icon: 'mdi-tag-text-outline' },
  { name: 'setting-material-groups', label: 'Material Groups', icon: 'mdi-shape-outline' },
  { name: 'setting-network', label: 'Network', icon: 'mdi-lan' },
  { name: 'setting-db-log', label: 'DB Log', icon: 'mdi-database-eye' },
];

const login = () => {
  if (password.value === ADMIN_PASSWORD) {
    localStorage.setItem('logged_in', 'true');
    loggedIn.value = true;
    dialog.value = false;
    password.value = '';
  } else {
    showSnackbar(`Invalid password!`, "error");
  }
};

const logout = () => {
  localStorage.removeItem('logged_in');
  loggedIn.value = false;

  if (router.currentRoute.value.meta.requireAuth) {
    router.push({ name: "dashboard" });
  }
};
</script>

<style scoped>
.nav-bar :deep(.v-navigation-drawer__content) {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.nav-scroll {
  overflow-y: auto;
  overflow-x: hidden;
  min-height: 0;
  scrollbar-width: thin;
  scrollbar-color: rgba(0, 0, 0, 0.2) transparent;
}

.nav-scroll::-webkit-scrollbar {
  width: 6px;
}

.nav-scroll::-webkit-scrollbar-thumb {
  background: rgba(0, 0, 0, 0.2);
  border-radius: 3px;
}

.nav-scroll::-webkit-scrollbar-thumb:hover {
  background: rgba(0, 0, 0, 0.35);
}
</style>