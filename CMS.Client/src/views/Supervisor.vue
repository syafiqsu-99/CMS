<template>
  <div class="d-flex flex-column" style="height: 100%; min-height: 0; overflow: hidden;">
    <v-tabs v-model="activeTab" color="primary" density="compact" grow show-arrows class="flex-shrink-0 border-b">
      <v-tab v-for="(tab, index) in tabs" :key="index" :value="index" class="px-3 text-caption"
        @click="goTo(tab.route)">
        <v-icon start size="16">{{ tab.icon }}</v-icon>
        {{ tab.label }}
      </v-tab>
    </v-tabs>

    <v-tabs-window v-model="activeTab" class="flex-grow-1" style="min-height: 0; overflow: hidden;">
      <v-tabs-window-item :value="0" style="height: 100%; overflow: hidden;">
        <ProdSchedule :SAP-data="store.SAPData" />
      </v-tabs-window-item>

      <v-tabs-window-item :value="1" style="height: 100%; overflow: hidden;">
        <MachineManagement :SAP-data="store.SAPData" @refresh-data="store.loadMachineMaster" />
      </v-tabs-window-item>

      <v-tabs-window-item :value="2" style="height: 100%; overflow: hidden;">
        <StaffSchedule />
      </v-tabs-window-item>

      <v-tabs-window-item :value="3" style="height: 100%; overflow: hidden;">
        <ProductDatabase @refresh-data="store.loadSAP" />
      </v-tabs-window-item>

      <v-tabs-window-item :value="4" style="height: 100%; overflow: hidden;">
        <ShiftCalendar />
      </v-tabs-window-item>
    </v-tabs-window>
  </div>
</template>

<script setup>
import { ref, watch, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useMachineStore } from '@/store/machineStore';

import ProdSchedule from '@/components/Supervisor/ProdSchedule.vue';
import MachineManagement from '@/components/Supervisor/MachineSchedule.vue';
import StaffSchedule from '@/components/Supervisor/StaffSchedule.vue';
import ProductDatabase from '@/components/Supervisor/SAPSchedule.vue';
import ShiftCalendar from '@/components/Supervisor/ShiftCalendar.vue';

const route = useRoute();
const router = useRouter();
const store = useMachineStore();

const tabs = [
  { label: 'Production Report', icon: 'mdi-cog-outline', route: 'supervisor-prod-report' },
  { label: 'Machine Management', icon: 'mdi-robot-industrial', route: 'supervisor-machine-management' },
  { label: 'Staff Assignment', icon: 'mdi-account-clock', route: 'supervisor-staff-assignment' },
  { label: 'Product Database', icon: 'mdi-archive', route: 'supervisor-product-database' },
  { label: 'Shift Calendar', icon: 'mdi-calendar-clock', route: 'supervisor-shift-calendar' },
];

const activeTab = ref(route.meta.tab ?? 0);

watch(() => route.meta.tab, (tab) => {
  if (tab !== undefined && tab !== activeTab.value) activeTab.value = tab;
});

function goTo(name) {
  if (route.name !== name) router.push({ name });
}

onMounted(() => {
  store.loadMachineMaster();
  store.loadSAP();
});
</script>