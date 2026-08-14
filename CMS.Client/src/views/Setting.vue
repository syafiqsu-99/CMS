<template>
  <div class="setting-page d-flex flex-column" style="height: 100%; min-height: 0; overflow: hidden;">
    <v-tabs v-model="activeTab" color="primary" density="compact" show-arrows class="flex-shrink-0 border-b">
      <v-tab v-for="t in tabs" :key="t.value" :value="t.value" @click="goTo(t.route)">
        <v-icon start>{{ t.icon }}</v-icon>
        {{ t.label }}
      </v-tab>
    </v-tabs>

    <v-window v-model="activeTab" class="setting-window flex-grow-1" style="min-height: 0;">
      <v-window-item v-for="t in tabs" :key="t.value" :value="t.value" class="setting-window-item">
        <component :is="t.component" :active="t.value === activeTab" />
      </v-window-item>
    </v-window>
  </div>
</template>

<script setup>
import { ref, watch, markRaw } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import PasswordTab from '@/components/Setting/PasswordTab.vue';
import PlcSignalMonitor from '@/components/Setting/PlcSignalMonitor.vue';
import ReportTab from '@/components/Setting/ReportTab.vue';
import ProdImportTab from '@/components/Setting/ProdImportTab.vue';
import MachineNameTab from '@/components/Setting/MachineNameTab.vue';
import MaterialGroupTab from '@/components/Setting/MaterialGroupTab.vue';
import DbLogTab from '@/components/Setting/DbLogTab.vue';
import NetworkDiagramTab from '@/components/Setting/NetworkDiagramTab.vue';

const route = useRoute();
const router = useRouter();

const tabs = [
  { value: 'password', label: 'Password', icon: 'mdi-lock-outline', route: 'setting-password', component: markRaw(PasswordTab) },
  { value: 'signals', label: 'PLC Signals', icon: 'mdi-sine-wave', route: 'setting-plc-signals', component: markRaw(PlcSignalMonitor) },
  { value: 'report', label: 'Report', icon: 'mdi-file-excel-outline', route: 'setting-report', component: markRaw(ReportTab) },
  { value: 'import', label: 'Import Report', icon: 'mdi-upload', route: 'setting-import-report', component: markRaw(ProdImportTab) },
  { value: 'machines', label: 'Machine Names', icon: 'mdi-tag-text-outline', route: 'setting-machine-names', component: markRaw(MachineNameTab) },
  { value: 'material', label: 'Material Groups', icon: 'mdi-shape-outline', route: 'setting-material-groups', component: markRaw(MaterialGroupTab) },
  { value: 'dblog', label: 'DB Log', icon: 'mdi-database-eye', route: 'setting-db-log', component: markRaw(DbLogTab) },
  { value: 'network', label: 'Network Diagram', icon: 'mdi-network', route: 'setting-network-diagram', component: markRaw(NetworkDiagramTab) }
];

const activeTab = ref(route.meta.tab ?? 'password');

watch(() => route.meta.tab, (tab) => {
  if (tab && tab !== activeTab.value) activeTab.value = tab;
});

function goTo(name) {
  if (route.name !== name) router.push({ name });
}
</script>

<style scoped>
.setting-window,
.setting-window :deep(.v-window__container) {
  height: 100%;
  min-height: 0;
}

.setting-window :deep(.v-window-item) {
  height: 100%;
  min-height: 0;
}

.setting-window-item {
  height: 100%;
  min-height: 0;
}

.setting-window-item> :deep(*) {
  height: 100%;
  min-height: 0;
}
</style>