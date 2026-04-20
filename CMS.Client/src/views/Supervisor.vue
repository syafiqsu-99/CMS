<template>
  <v-container fluid class="pa-2 h-100 d-flex flex-column">
    <v-row no-gutters align="center" justify="center">
      <v-col cols="12" class="text-center">
        <h2 class="font-weight-bold">SUPERVISOR</h2>
      </v-col>
    </v-row>

    <v-row no-gutters class="flex-grow-1 flex-shrink-1" style="height: 90vh;">
      <v-col cols="12" class="pa-1 d-flex" style="height: 100%;">
        <v-card variant="text"
                class="d-flex flex-column"
                elevation="2"
                style="width: 100%; height: 100%; overflow: hidden;">

          <v-card-text class="d-flex flex-column pa-2" style="height: 100%; overflow: hidden;">

            <v-tabs v-model="activeTab"
                    color="primary"
                    density="compact"
                    grow
                    class="flex-grow-0 flex-shrink-0">
              <v-tab v-for="(tab, index) in tabs" :key="index" :value="index" class="px-3 text-caption">
                <v-icon start size="16">{{ tab.icon }}</v-icon>
                {{ tab.label }}
              </v-tab>
            </v-tabs>

            <v-tabs-window v-model="activeTab" style="min-height: 0; flex: 1; overflow: hidden;">

              <!-- Production Report -->
              <v-tabs-window-item :value="0">
                <ProdSchedule :SAP-data="store.SAPData" />
              </v-tabs-window-item>

              <!-- Machine Management — mounts/unmounts with the tab,
                   so the polling lifecycle hook fires correctly -->
              <v-tabs-window-item :value="1">
                <MachineManagement :SAP-data="store.SAPData"
                                   @refresh-data="store.loadMachineMaster" />
              </v-tabs-window-item>

              <!-- Staff Assignment -->
              <v-tabs-window-item :value="2">
                <StaffSchedule />
              </v-tabs-window-item>

              <!-- Product Database — static, loaded once -->
              <v-tabs-window-item :value="3">
                <ProductDatabase :SAP-data="store.SAPData"
                                 @refresh-data="store.loadSAP" />
              </v-tabs-window-item>

              <!-- Shift Calendar -->
              <v-tabs-window-item :value="4">
                <ShiftCalendar />
              </v-tabs-window-item>

            </v-tabs-window>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup>
  import { ref, onMounted } from 'vue';
  import { useMachineStore } from '@/store/machineStore';

  import ProdSchedule from '@/components/Supervisor/ProdSchedule.vue';
  import MachineManagement from '@/components/Supervisor/MachineSchedule.vue';
  import StaffSchedule from '@/components/Supervisor/StaffSchedule.vue';
  import ProductDatabase from '@/components/Supervisor/SAPSchedule.vue';
  import ShiftCalendar from '@/components/Supervisor/ShiftCalendar.vue';

  const store = useMachineStore();
  const activeTab = ref(0);

  onMounted(() => {
    store.loadInitialData();
  });

  const tabs = [
    { label: 'Production Report', icon: 'mdi-cog-outline' },
    { label: 'Machine Management', icon: 'mdi-robot-industrial' },
    { label: 'Staff Assignment', icon: 'mdi-account-clock' },
    { label: 'Product Database', icon: 'mdi-archive' },
    { label: 'Shift Calendar', icon: 'mdi-calendar-clock' },
  ];
</script>
