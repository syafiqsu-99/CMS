<template>
  <v-container fluid class="pa-2 h-100 d-flex flex-column" style="overflow: hidden;">
    <v-row no-gutters style="height: 12vh;">
      <v-col cols="3" v-for="card in summaryCards" :key="card.title" class="pa-1">
        <v-card class="h-100 d-flex flex-row" elevation="2">
          <div class="d-flex align-center justify-center"
               :style="{ width: '30%', backgroundColor: card.color + '20' }">
            <v-icon :color="card.color" size="40">{{ card.icon }}</v-icon>
          </div>
          <v-card-text class="d-flex flex-column justify-center py-1 px-2" style="width: 70%;">
            <div class="text-h5 font-weight-bold">{{ card.value }}</div>
            <div class="text-caption text-grey-darken-1">{{ card.title }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-row no-gutters class="flex-grow-1 flex-shrink-1" style="min-height: 0;">
      <!-- Left: machine list -->
      <v-col cols="3" style="height: 100%; overflow-y: auto;">
        <MachineList :machines="groupedMachines"
                     :selected="selectedMachine"
                     :chartData="chartData"
                     @select="onSelectMachine" />
      </v-col>

      <!-- Right: machine detail -->
      <v-col cols="9" style="height: 100%; overflow-y: auto;">
        <div v-if="selectedMachine && selectedMachineData" class="pa-2">

          <MachineInfo :machine="selectedMachine" :machineData="selectedMachineData">
            <!-- Timeline Gantt slot -->
            <template #timeline>
              <div v-if="chartData[selectedMachineData.id_machine]" style="height: 80px;">
                <Bar :data="chartData[selectedMachineData.id_machine].data"
                     :options="chartData[selectedMachineData.id_machine].options" />
              </div>
            </template>

            <!-- Utility timeline slot -->
            <template #utilities>
              <div v-if="utilityChartData.length > 0">
                <v-list dense class="py-0 bg-transparent">
                  <v-list-item v-for="(chart, index) in utilityChartData"
                               :key="chart.utility_name"
                               :class="{ 'mt-2': index > 0 }"
                               class="px-0 py-2">
                    <v-row dense align="center" no-gutters>
                      <v-col cols="2" class="text-left">
                        <div class="text-caption font-weight-medium text-truncate" :title="chart.utility_name">
                          {{ chart.utility_name }}
                        </div>
                      </v-col>
                      <v-col cols="10">
                        <div style="height: 40px;">
                          <Bar :data="chart.data" :options="chart.options" />
                        </div>
                      </v-col>
                    </v-row>
                  </v-list-item>
                </v-list>
              </div>
              <div v-else class="text-grey text-center py-4">
                <v-icon size="32" color="grey-lighten-1">mdi-flash-off-outline</v-icon>
                <div class="text-caption mt-2">No utility data available</div>
              </div>
            </template>
          </MachineInfo>

        </div>

        <!-- Empty state -->
        <div v-else class="d-flex flex-column align-center justify-center" style="height: 100%;">
          <v-icon size="80" color="grey-lighten-2">mdi-monitor-dashboard</v-icon>
          <div class="text-h6 text-grey-darken-1 mt-4 font-weight-medium">Select a machine to view details</div>
          <div class="text-caption text-grey mt-2">Choose from the list on the left</div>
        </div>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup>
  import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
  import { Bar } from 'vue-chartjs';
  import {
    Chart as ChartJS, CategoryScale, LinearScale,
    BarElement, TimeScale, Tooltip, Legend,
  } from 'chart.js';
  import 'chartjs-adapter-luxon';
  import { DateTime } from 'luxon';
  import { useMachineStore } from '@/store/machineStore';
  import { UTILITIES } from '@/utils/constant.js';
  import MachineList from '@/components/Machines/MachineList.vue';
  import MachineInfo from '@/components/Machines/MachineInfo.vue';

  ChartJS.register(CategoryScale, LinearScale, BarElement, TimeScale, Tooltip, Legend);

  const store = useMachineStore();
  const machineLogs = ref([]);
  const groupedMachines = ref([]);
  const chartData = ref({});
  const selectedMachine = ref(null);
  const utilityData = ref([]);
  const utilityChartData = ref([]);
  const utility_order = UTILITIES;

  const selectedMachineData = computed(() => {
    if (!selectedMachine.value || !store.machineData) return null;
    return store.machineData.find(m => m.id_machine === selectedMachine.value?.id_machine) ?? null;
  });

  watch(selectedMachineData, (m) => {
    if (m) fetchUtilityData(m.id_machine);
  });

  function onSelectMachine(machine) {
    selectedMachine.value = machine;
  }

  async function fetchTimelineData() {
    try {
      const res = await fetch('/api/machines/timeline');
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      machineLogs.value = data;
      groupedMachines.value = groupByMachine(data);

      if (!selectedMachine.value && groupedMachines.value.length > 0) {
        selectedMachine.value = groupedMachines.value[0];
      }
      buildChartData();
    } catch (err) {
      console.error('[Machines] fetchTimelineData:', err.message);
    }
  }

  function groupByMachine(data) {
    const grouped = {};
    for (const item of data) {
      if (!grouped[item.id_machine]) {
        grouped[item.id_machine] = {
          id_machine: item.id_machine,
          machine_name: item.machine_name ?? 'UNDEFINED',
          type: item.type ?? 'UNDEFINED',
          output: item.output ?? 0,
          plan_output: item.plan_output ?? 0,
          currentStatus: { category: item.category ?? 'N/A', color: item.color ?? '#9e9e9e' },
          data: [],
        };
      }
      if (item.start && item.finish) {
        grouped[item.id_machine].data.push({
          category: item.category ?? 'UNDEFINED',
          start: item.start,
          finish: item.finish,
          color: item.color ?? 'rgba(0,0,0,0.1)',
        });
      }
    }
    return Object.values(grouped).sort((a, b) => a.id_machine - b.id_machine);
  }

  function buildChartData() {
    const { minTime, maxTime } = getTimeBounds();
    chartData.value = {};

    groupedMachines.value.forEach((machine) => {
      const datasets = machine.data.map((item) => {
        const startTime = DateTime.fromISO(item.start, { zone: 'local' });
        const endTime = DateTime.fromISO(item.finish, { zone: 'local' });

        const getColorWithAlpha = (color, alpha) => {
          if (!color) return `rgba(0, 0, 0, ${alpha})`;
          if (color.startsWith('#')) {
            const r = parseInt(color.slice(1, 3), 16);
            const g = parseInt(color.slice(3, 5), 16);
            const b = parseInt(color.slice(5, 7), 16);
            return `rgba(${r}, ${g}, ${b}, ${alpha})`;
          }
          if (color.startsWith('rgb(')) {
            return color.replace('rgb(', 'rgba(').replace(')', `, ${alpha})`);
          }
          return color;
        };

        const backgroundColor = getColorWithAlpha(item.color, 0.8);
        const borderColor = getColorWithAlpha(item.color, 1);

        return {
          label: item.category,
          data: [
            {
              x: [startTime.toJSDate(), endTime.toJSDate()],
              y: machine.machine_name,
              category: item.category,
            },
          ],
          backgroundColor,
          borderColor,
          borderWidth: 1,
        };
      });

      chartData.value[machine.id_machine] = {
        data: { datasets },
        options: {
          animation: false,
          responsive: true,
          maintainAspectRatio: false,
          indexAxis: "y",
          barPercentage: 1,
          categoryPercentage: 1,
          plugins: {
            tooltip: {
              callbacks: {
                title: () => [],
                label: function (context) {
                  const range = context.raw.x;
                  const start = new Date(range[0]);
                  const end = new Date(range[1]);
                  return `${context.raw.category}: ${start.getHours().toString().padStart(2, "0")}:${start.getMinutes().toString().padStart(2, "0")} - ${end.getHours().toString().padStart(2, "0")}:${end.getMinutes().toString().padStart(2, "0")}`;
                },
              },
            },
            legend: { display: false },
          },
          scales: {
            x: {
              type: "time",
              time: {
                unit: "hour",
                tooltipFormat: "hh:mm a",
                displayFormats: { hour: "ha" },
              },
              min: minTime,
              max: maxTime,
              ticks: {
                stepSize: 2,
                color: "#000000",
                font: { size: 10 }
              },
              grid: { display: true, color: 'rgba(0,0,0,0.1)' },
            },
            y: {
              stacked: true,
              display: false,
            },
          },
        },
      };
    });
  }

  async function fetchUtilityData(id_machine) {
    try {
      const res = await fetch(`/api/machines/${id_machine}/utilities`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      utilityData.value = await res.json();
      buildUtilityChartData();
    } catch (err) {
      console.error('[Machines] fetchUtilityData:', err.message);
    }
  }

  function getTimeBounds() {
    const now = DateTime.now();
    let minTime, maxTime;

    const currentHour = now.hour;

    if (currentHour >= 6 && currentHour < 18) {
      minTime = now.startOf('day').plus({ hours: 6 }).toJSDate();
      maxTime = now.startOf('day').plus({ hours: 18 }).toJSDate();
    } else {
      if (currentHour >= 18) {
        minTime = now.startOf('day').plus({ hours: 18 }).toJSDate();
        maxTime = now.plus({ days: 1 }).startOf('day').plus({ hours: 6 }).toJSDate();
      } else {
        minTime = now.minus({ days: 1 }).startOf('day').plus({ hours: 18 }).toJSDate();
        maxTime = now.startOf('day').plus({ hours: 6 }).toJSDate();
      }
    }

    return { minTime, maxTime };
  }

  function buildUtilityChartData() {
    const { minTime, maxTime } = getTimeBounds();

    const groupedData = {};
    utilityData.value.forEach(item => {
      if (!groupedData[item.utility_name]) {
        groupedData[item.utility_name] = [];
      }
      groupedData[item.utility_name].push(item);
    });

    utilityChartData.value = utility_order.map(utilityName => {
      const items = groupedData[utilityName] || [];
    
      const dataPoints = items.map(item => {
        const start = DateTime.fromISO(item.start).toJSDate();
        const finish = DateTime.fromISO(item.finish).toJSDate();
        const categoryBit = item.category;
        const categoryLabel = categoryBit === 1 ? 'RUNNING' : 'OFF';
      
        return {
          x: [start, finish],
          y: utilityName,
          category: categoryBit,
          categoryLabel: categoryLabel,
          backgroundColor: categoryBit === 1 ? '#00ff00' : '#ff0000'
        };
      });
    
      return {
        utility_name: utilityName,
        data: {
          datasets: dataPoints.length > 0 ? [{
            data: dataPoints,
            backgroundColor: dataPoints.map(d => d.backgroundColor),
            borderColor: dataPoints.map(d => d.backgroundColor.replace('0.6', '1')),
            borderWidth: 1,
            barThickness: 20
          }] : []
        },
        options: {
          animation: false,
          indexAxis: "y",
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: { display: false },
            tooltip: {
              callbacks: {
                title: () => [],
                label: (ctx) => {
                  const start = new Date(ctx.raw.x[0]);
                  const end = new Date(ctx.raw.x[1]);
                  const duration = Math.round((end - start) / (1000 * 60));
                  const status = ctx.raw.categoryLabel;
                  return [
                    `${status}: ${start.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })} - ${end.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}`,
                  ];
                }
              }
            }
          },
          scales: {
            x: {
              type: "time",
              min: minTime,
              max: maxTime,
              time: {
                unit: "hour",
                tooltipFormat: "hh:mm a",
                displayFormats: { hour: "ha" },
              },
              ticks: {
                stepSize: 2,
                color: "#000000",
                font: { size: 10 }
              },
              grid: { display: true, color: 'rgba(0,0,0,0.1)' },
            },
            y: {
              stacked: true,
              display: false,
            },
          }
        }
      };
    });
  }

  const summaryCards = computed(() => [
    { title: "Total Machines", icon: "mdi-factory", color: "primary", value: store.totalMachines },
    { title: "Running", icon: "mdi-play-circle", color: "success", value: store.runningMachines },
    { title: "Stop", icon: "mdi-stop-circle", color: "error", value: store.stopMachines },
    { title: "Staff Assigned", icon: "mdi-account-group", color: "info", value: store.activeStaff },
  ]);

  const POLL_INTERVAL = 10_000;
  let timelineTimer = null;

  function startPolling() {
    if (timelineTimer) return;
    timelineTimer = setInterval(fetchTimelineData, POLL_INTERVAL);
  }

  function stopPolling() {
    if (timelineTimer) { clearInterval(timelineTimer); timelineTimer = null; }
  }

  onMounted(async () => {
    await Promise.all([
      store.loadMachineMaster(),
      fetchTimelineData(),
    ]);
    startPolling();
  });

  onUnmounted(() => { stopPolling(); });
</script>
