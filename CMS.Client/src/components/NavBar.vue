<template>
  <v-layout class="h-screen">
    <v-navigation-drawer v-model:rail="isRail" expand-on-hover permanent class="nav-bar">
      <v-list class="flex-shrink-0 py-1">
        <v-list-item :prepend-avatar="'/JJlogo.png'" class="brand-item">
          <v-list-item-title class="font-weight-bold">CMS</v-list-item-title>
          <v-list-item-subtitle class="brand-sub">Central Monitoring System</v-list-item-subtitle>
        </v-list-item>
      </v-list>

      <v-divider></v-divider>

      <div class="nav-scroll flex-grow-1">
        <v-list nav v-model:opened="openGroups">
          <v-list-item prepend-icon="mdi-monitor-dashboard" title="Dashboard" :to="{ name: 'dashboard' }"
            link></v-list-item>

          <v-list-item prepend-icon="mdi-view-comfy" title="Machines" :to="{ name: 'machines' }" link></v-list-item>

          <v-list-item prepend-icon="mdi-database" title="OEE" :to="{ name: 'oee' }" link></v-list-item>

          <!-- Supervisor -->
          <template v-if="loggedIn">
            <v-menu v-if="isRail" location="end" open-on-hover :close-on-content-click="true">
              <template #activator="{ props }">
                <v-list-item v-bind="props" prepend-icon="mdi-account" title="Supervisor"
                  :active="isGroupActive(supervisorChildren)" />
              </template>
              <v-list nav density="compact" class="flyout-list">
                <v-list-subheader>Supervisor</v-list-subheader>
                <v-list-item v-for="item in supervisorChildren" :key="item.name" :prepend-icon="item.icon"
                  :title="item.label" :to="{ name: item.name }" link />
              </v-list>
            </v-menu>

            <v-list-group v-else value="supervisor">
              <template #activator="{ props }">
                <v-list-item v-bind="props" prepend-icon="mdi-account" title="Supervisor"
                  :active="isGroupActive(supervisorChildren)" />
              </template>
              <v-list-item v-for="item in supervisorChildren" :key="item.name" :prepend-icon="item.icon"
                :title="item.label" :to="{ name: item.name }" link />
            </v-list-group>
          </template>

          <!-- Setting -->
          <template v-if="loggedIn">
            <v-menu v-if="isRail" location="end" open-on-hover :close-on-content-click="true">
              <template #activator="{ props }">
                <v-list-item v-bind="props" prepend-icon="mdi-cog" title="Setting"
                  :active="isGroupActive(settingChildren)" />
              </template>
              <v-list nav density="compact" class="flyout-list">
                <v-list-subheader>Setting</v-list-subheader>
                <v-list-item v-for="item in settingChildren" :key="item.name" :prepend-icon="item.icon"
                  :title="item.label" :to="{ name: item.name }" link />
              </v-list>
            </v-menu>

            <v-list-group v-else value="setting">
              <template #activator="{ props }">
                <v-list-item v-bind="props" prepend-icon="mdi-cog" title="Setting"
                  :active="isGroupActive(settingChildren)" />
              </template>
              <v-list-item v-for="item in settingChildren" :key="item.name" :prepend-icon="item.icon"
                :title="item.label" :to="{ name: item.name }" link />
            </v-list-group>
          </template>
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
      <div class="flex-grow-1 overflow-hidden" style="min-height: 0;">
        <router-view />
      </div>
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
import { ref, inject, watch } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { ADMIN_PASSWORD } from '@/utils/constant.js';

const router = useRouter();
const route = useRoute();

const showSnackbar = inject("showSnackbar");
const dialog = ref(false);
const password = ref('');
const loggedIn = ref(localStorage.getItem('logged_in') === 'true');
const isRail = ref(true);
const openGroups = ref([]);

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

function isGroupActive(children) {
  return children.some(c => c.name === route.name);
}

watch(isRail, (railed) => {
  if (railed) {
    openGroups.value = [];
  }
});

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
  openGroups.value = [];

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

.brand-item :deep(.v-list-item__prepend) {
  margin-inline-end: 12px;
}

.brand-sub {
  font-size: 0.7rem;
  line-height: 1.1;
  white-space: normal;
  opacity: 0.7;
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

.flyout-list {
  min-width: 220px;
}

.page-header {
  padding: 10px 20px;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.12);
  background: rgb(var(--v-theme-surface));
}
</style>