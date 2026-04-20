<template>
  <v-container fluid class="pa-0 h-100 d-flex flex-column" style="overflow: hidden;">
    <v-row no-gutters class="flex-grow-1" style="height: 100%; overflow: hidden;">
      <v-col cols="12" style="height: 70vh">
        <v-window v-model="machine_layout" show-arrows="hover" continuous class="h-100">
          <!--<v-window-item :value="1">
            <v-card class="h-100 d-flex flex-column">-->
              <!--<div ref="machLayout" class="w-100 h-100" style="min-height: 65vh;"></div>-->
            <!--</v-card>

            <v-card width="15%" variant="text" class="position-absolute top-0 right-0">
              <v-card-title align="center" justify="center"><strong>LEGENDS</strong></v-card-title>
              <v-card-text>
                <v-row>
                  <v-col cols="12" class="d-flex align-center">
                    <v-icon color="green" class="me-2">mdi-circle</v-icon>
                    <span>Prod. Run</span>
                  </v-col>
                  <v-col cols="12" class="d-flex align-center">
                    <v-icon color="yellow" class="me-2">mdi-circle</v-icon>
                    <span>Prod. Issue</span>
                  </v-col>
                  <v-col cols="12" class="d-flex align-center">
                    <v-icon color="red" class="me-2">mdi-circle</v-icon>
                    <span>Tech. Issue</span>
                  </v-col>
                  <v-col cols="12" class="d-flex align-center">
                    <v-icon color="orange" class="me-2">mdi-circle</v-icon>
                    <span>Maint. Issue</span>
                  </v-col>
                  <v-col cols="12" class="d-flex align-center">
                    <v-icon color="gray" class="me-2">mdi-circle</v-icon>
                    <span>QC Issue</span>
                  </v-col>
                  <v-col cols="12" class="d-flex align-center">
                    <v-icon color="white" class="me-2">mdi-circle</v-icon>
                    <span>Unidentified</span>
                  </v-col>
                </v-row>
              </v-card-text>
            </v-card>
          </v-window-item>-->

          <v-window-item :value="1" class="h-100">
            <Attendance :machine-status="machineColor" />
          </v-window-item>
        </v-window>
      </v-col>
      <v-col cols="12" style="height: 30vh; max-height: 30vh;">
        <v-window v-model="running_slideshow" show-arrows="hover" continuous class="h-100 overflow-hidden">
          <v-window-item v-for="(group, index) in paginatedMachines" :key="index" class="h-100">
            <v-row class="fill-height ma-0">
              <v-col v-for="machine in group" :key="machine.id_machine" cols="2" class="pa-1 d-flex flex-column">
                <v-card rounded="xl"
                        variant="elevated"
                        elevation="8"
                        :color="machine.data[0]?.color"
                        class="flex-fill d-flex flex-column overflow-hidden"
                        style="max-height: 100%;">
                  <v-card-title class="text-center pa-1 text-h6 font-weight-bold flex-shrink-0" style="min-height: 40px;">
                    {{ machine.machine_name }}
                  </v-card-title>
                  <v-card-text class="pa-1 flex-fill d-flex flex-column overflow-hidden" style="flex: 1 1 0; min-height: 0;">
                    <div class="marquee-wrapper flex-grow-1 overflow-hidden">
                      <div class="marquee-content">
                        <span class="text-caption font-weight-bold">{{ machine.type }}</span>
                        <span class="text-caption font-weight-bold">{{ machine.type }}</span>
                      </div>
                    </div>

                    <v-divider class="my-1 flex-shrink-0"></v-divider>

                    <v-row dense class="text-caption flex-shrink-0 ma-0" style="height: 55px;">
                      <v-col cols="6" class="text-right pa-1">
                        <div>Output:</div>
                        <div>Plan:</div>
                        <div>Eff:</div>
                      </v-col>
                      <v-col cols="6" class="text-left pa-1">
                        <div>{{ machine.output }}</div>
                        <div>{{ machine.plan_output.toFixed(0) }}</div>
                        <div>{{ machine.eff.toFixed(2) }}%</div>
                      </v-col>
                    </v-row>

                    <v-divider class="my-1 flex-shrink-0"></v-divider>

                    <div class="d-flex justify-center align-center flex-shrink-0">
                      <v-chip :color="machine.data[0]?.color"
                              size="small"
                              variant="elevated"
                              class="text-caption px-2"
                              style="height: 24px;">
                        {{ machine.data[0]?.category || 'No Data' }}
                      </v-chip>
                    </div>
                  </v-card-text>
                </v-card>
              </v-col>
            </v-row>
          </v-window-item>
        </v-window>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup>
  import Attendance from '../components/Dashboard/Attendance.vue';
  import {
    Chart as ChartJS,
    CategoryScale,
    LinearScale,
    BarElement,
    Title,
    Tooltip,
    Legend,
  } from 'chart.js'
  import { ref, onMounted, onUnmounted, computed, watch, nextTick } from 'vue'
  import { initMachLayout, updateMachLayout } from '@/Machines_Layout'
  import { Bar } from "vue-chartjs";
  import { DateTime } from 'luxon';

  ChartJS.register(CategoryScale, LinearScale, BarElement, Title, Tooltip, Legend);

  const machLayout = ref(null);
  const machine_layout = ref(1);
  const machines = ref([]);
  const previousMachineData = ref([]);
  const running_slideshow = ref(0);
  const chartData = ref({});
  const card_timer = ref(null);
  const layout_timer = ref(null);
  const machineColor = ref({})
  let sceneManager = null;
  let chartInstances = {};

  const paginatedMachines = computed(() => {
    const chunkSize = 6;
    return Array.from(
      { length: Math.ceil(machines.value.length / chunkSize) },
      (_, i) => machines.value.slice(i * chunkSize, (i + 1) * chunkSize)
    );
  });

  onMounted(async () => {
    if (machLayout.value) {
      sceneManager = await initMachLayout(machLayout.value)
      await refreshMachineLayout()
    }
    fetchTimelineData();
    startDataRefresh();
  })

  onUnmounted(() => {
    clearInterval(card_timer.value);
    //clearInterval(layout_timer.value);
    destroyExistingCharts();
  })

  function startDataRefresh() {
    card_timer.value = setInterval(() => {
      running_slideshow.value =
        (running_slideshow.value + 1) % paginatedMachines.value.length;
      fetchTimelineData();
    }, 10000);
    //layout_timer.value = setInterval(() => {
    //  machine_layout.value = machine_layout.value >= 2 ? 1 : machine_layout.value + 1;
    //}, 60000);
  }

  function destroyExistingCharts() {
    Object.values(chartInstances).forEach((chart) => {
      if (chart) chart.destroy();
    });
    chartInstances = {};
  }

  async function refreshMachineLayout() {
    try {
      if (sceneManager?.scene) {
        previousMachineData.value = await updateMachLayout(sceneManager.scene, previousMachineData.value)
      }
    } catch (error) {
      console.error('Error during machine layout refresh:', error)
    }
  }

  async function fetchTimelineData() {
    try {
      const response = await fetch('/api/base/Timeline');
      const data = await response.json();

      machines.value = groupByMachine(data);

      const statusMap = {};
      data.forEach(item => {
        const machineName = item.machine_name;

        if (!statusMap[machineName] ||
          new Date(item.start) > new Date(statusMap[machineName].start)) {
          statusMap[machineName] = {
            start: item.start,
            color: item.color
          };
        }
      });

      const colorMap = {};
      Object.keys(statusMap).forEach(machine => {
        colorMap[machine] = statusMap[machine].color;
      });

      machineColor.value = colorMap;
    } catch (error) {
      console.error("Error fetching timeline data:", error);
    }
  }

  function groupByMachine(data) {
    const grouped = {};
    data.forEach((item) => {
      if (!grouped[item.id_machine]) {
        grouped[item.id_machine] = {
          id_machine: item.id_machine,
          machine_name: item.machine_name || "UNDEFINED",
          type: item.type || "UNDEFINED",
          output: item.output || 0,
          plan_output: item.plan_output || 0,
          eff: item.efficiency || 0.0,
          data: [],
        };
      }
      grouped[item.id_machine].data.push({
        category: item.category || "UNDEFINED",
        start: item.start || null,
        finish: item.finish || null,
        color: item.color || "rgba(0,0,0,0.1)",
      });
    });

    return Object.values(grouped);
  }
</script>
<style scoped>
  .marquee-wrapper {
    overflow: hidden;
    position: relative;
    width: 100%;
  }

  .marquee-content {
    display: flex;
    animation: marquee 10s linear infinite;
    width: fit-content;
    will-change: transform;
  }

    .marquee-content span {
      white-space: nowrap;
      padding-right: 3em;
      flex-shrink: 0;
    }

  @keyframes marquee {
    0% {
      transform: translateX(0);
    }

    100% {
      transform: translateX(-50%);
    }
  }
</style>
