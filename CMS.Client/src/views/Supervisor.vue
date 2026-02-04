<template>
  <v-container fluid class="pa-2 h-100 d-flex flex-column">
    <v-row no-gutters align="center" justify="center">
      <v-col cols="12" class="text-center">
        <h2 class="font-weight-bold">SUPERVISOR</h2>
      </v-col>
    </v-row>

    <v-row no-gutters class="flex-grow-1 flex-shrink-1" style="height: 90vh;">
      <v-col cols="12" class="pa-1 d-flex" style="height: 100%;">
        <v-card variant="text" class="d-flex flex-column" elevation="2" style="width: 100%; height: 100%; overflow: hidden;">
          <v-card-text class="d-flex flex-column pa-2" style="height: 100%; overflow: hidden;">
            <v-tabs v-model="activeTab"
                    background-color="white"
                    color="primary"
                    density="compact"
                    grow
                    class="flex-grow-0 flex-shrink-0">
              <v-tab v-for="(tab, index) in tabs" :key="index" class="px-3 text-caption">
                <v-icon start size="16">{{ tab.icon }}</v-icon>
                {{ tab.label }}
              </v-tab>
            </v-tabs>
            <v-tabs-window v-model="activeTab" style="min-height: 0; ">
              <v-tabs-window-item value="0">
                  <ProdSchedule :SAP-data="store.SAPData" />
              </v-tabs-window-item>
              <v-tabs-window-item value="1">
                  <MachineSchedule :machine-data="store.machineData" :SAP-data="store.SAPData" @refresh-data="handleRefreshData" />
              </v-tabs-window-item>
              <v-tabs-window-item value="2">
                  <StaffSchedule />
              </v-tabs-window-item>
              <v-tabs-window-item value="3">
                  <SAPSchedule :SAP-data="store.SAPData" @refresh-data="handleRefreshSAP"/>
              </v-tabs-window-item>
            </v-tabs-window>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup>
  import { ref, onMounted, onUnmounted, computed, watch, nextTick } from 'vue';
  import { pinia } from '@/store/index'
  import StaffSchedule from '@/components/StaffSchedule.vue';
  import MachineSchedule from '@/components/MachineSchedule.vue';
  import SAPSchedule from '@/components/SAPSchedule.vue';
  import ProdSchedule from '@/components/ProdSchedule.vue';

  const activeTab = ref(0);
  const store = pinia();

  const summaryCards = computed(() => [
    { title: "Total Machines", icon: "mdi-factory", color: "primary", value: store.totalMachines },
    { title: "Running", icon: "mdi-play-circle", color: "success", value: store.runningMachines },
    { title: "Stop", icon: "mdi-stop-circle", color: "error", value: store.stopMachines },
    { title: "Staff Assigned", icon: "mdi-account-group", color: "info", value: store.activeStaff },
  ]);

  const tabs = [
    { label: "Production Report", icon: "mdi-cog-outline" },
    { label: "Machine Management", icon: "mdi-robot-industrial" },
    { label: "Staff Assignment", icon: "mdi-account-clock" },
    { label: "Product Database", icon: "mdi-archive" },
  ];

  async function handleRefreshData() {
    await store.loadMachineMaster();
  }

  async function handleRefreshSAP() {
    await store.loadSAP();
  }
</script>
