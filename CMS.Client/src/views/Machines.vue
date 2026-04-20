<!-- CMS.Client/src/views/Machines.vue -->
<template>
  <v-container fluid class="pa-2 h-100 d-flex flex-column">
    <v-row no-gutters align="center" justify="center">
      <v-col cols="12" class="text-center">
        <h2 class="font-weight-bold">MACHINES</h2>
      </v-col>
    </v-row>

    <!-- Summary cards -->
    <v-row dense class="mb-2">
      <v-col v-for="card in summaryCards" :key="card.title" cols="6" md="3">
        <v-card variant="flat" elevation="1" class="pa-3 text-center rounded-lg">
          <v-icon :color="card.color" size="28">{{ card.icon }}</v-icon>
          <div class="text-h6 font-weight-bold mt-1">{{ card.value }}</div>
          <div class="text-caption text-grey">{{ card.title }}</div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Main split layout -->
    <v-row class="flex-grow-1" no-gutters>
      <!-- Machine list -->
      <v-col cols="4" class="pa-1">
        <MachineList :machines="groupedMachines"
                     :chart-data="chartData"
                     :selected-id="selectedMachine?.id_machine ?? null"
                     @select="selectMachine" />
      </v-col>

      <!-- Machine detail -->
      <v-col cols="8" class="pa-1">
        <v-card variant="flat"
                class="pa-4 bg-grey-lighten-5"
                style="height: calc(100vh - 100px); overflow-y: auto;"
                elevation="0">
          <MachineInfo :machine="selectedMachineData">
            <!-- Info slot -->
            <template #info>
              <v-row dense v-if="selectedMachineData">
                <v-col cols="6" md="3" v-for="field in infoFields" :key="field.label">
                  <div class="text-caption text-grey">{{ field.label }}</div>
                  <div class="text-body-2 font-weight-bold">{{ field.value }}</div>
                </v-col>
              </v-row>
            </template>

            <!-- Timeline slot -->
            <template #timeline>
              <div v-if="selectedMachineData && chartData[selectedMachineData.id_machine]"
                   style="height: 60px;">
                <Bar :data="chartData[selectedMachineData.id_machine].data"
                     :options="chartData[selectedMachineData.id_machine].options" />
              </div>
            </template>

            <!-- Utilities slot -->
            <template #utilities>
              <div v-if="utilityChartData.length > 0">
                <v-list dense class="py-0 bg-transparent">
                  <v-list-item v-for="(chart, index) in utilityChartData"
                               :key="chart.utility_name"
                               :class="{ 'mt-2': index > 0 }"
                               class="px-0 py-2">
                    <v-row dense align="center" no-gutters>
                      <v-col cols="2" class="text-left">
                        <div class="text-caption font-weight-medium text-truncate"
                             :title="chart.utility_name">
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
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup>
  import { ref, computed, onMounted } from 'vue';
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

  // ── Computed ────────────────────────────────────────────────────────────────

  const selectedMachineData = computed(() => {
    if (!selectedMachine.value || !store.machineData) return null;
    const m = store.machineData.find(m => m.id_machine === selectedMachine.value.id_machine);
    if (m) fetchUtilityData(m.id_machine);
    return m ?? null;
  });

  const summaryCards = computed(() => [
    { title: 'Total Machines', icon: 'mdi-factory', color: 'primary', value: store.totalMachines },
    { title: 'Running', icon: 'mdi-play-circle', color: 'success', value: store.runningMachines },
    { title: 'Stop', icon: 'mdi-stop-circle', color: 'error', value: store.stopMachines },
    { title: 'Staff Assigned', icon: 'mdi-account-group', color: 'info', value: store.activeStaff },
  ]);

  const infoFields = computed(() => {
    const m = selectedMachineData.value;
    if (!m) return [];
    return [
      { label: 'Machine', value: m.machine_name },
      { label: 'Material', value: m.material || '—' },
      { label: 'SAP Code', value: m.id_type || '—' },
      { label: 'Mould', value: m.mould || '—' },
      { label: 'Packer', value: m.packer || '—' },
      { label: 'Product', value: m.type || '—' },
    ];
  });

  // ── Methods ─────────────────────────────────────────────────────────────────

  function selectMachine(machine) {
    selectedMachine.value = machine.currentStatus;
  }

  function groupByMachine(data) {
    const grouped = {};
    data.forEach(item => {
      if (!grouped[item.id_machine]) {
        grouped[item.id_machine] = {
          id_machine: item.id_machine,
          machine_name: item.machine_name,
          data: [],
          currentStatus: null,
        };
      }
      grouped[item.id_machine].data.push(item);
    });
    Object.values(grouped).forEach(m => {
      m.data.sort((a, b) => new Date(b.start) - new Date(a.start));
      m.currentStatus = m.data[0];
    });
    return Object.values(grouped);
  }

  function getTimeBounds() {
    const now = DateTime.now();
    const hour = now.hour;
    let minTime, maxTime;
    if (hour >= 6 && hour < 18) {
      minTime = now.set({ hour: 6, minute: 0, second: 0 }).toJSDate();
      maxTime = now.set({ hour: 18, minute: 0, second: 0 }).toJSDate();
    } else {
      minTime = (hour >= 18
        ? now.set({ hour: 18, minute: 0, second: 0 })
        : now.minus({ days: 1 }).set({ hour: 18, minute: 0, second: 0 })
      ).toJSDate();
      maxTime = (hour < 6
        ? now.set({ hour: 6, minute: 0, second: 0 })
        : now.plus({ days: 1 }).set({ hour: 6, minute: 0, second: 0 })
      ).toJSDate();
    }
    return { minTime, maxTime };
  }

  function renderCharts() {
    const { minTime, maxTime } = getTimeBounds();
    groupedMachines.value.forEach(machine => {
      const datasets = machine.data.map(item => ({
        data: [{ x: [new Date(item.start), item.finish ? new Date(item.finish) : new Date()], y: machine.machine_name }],
        backgroundColor: item.color || '#808080',
        barThickness: 20,
      }));
      chartData.value[machine.id_machine] = {
        data: { datasets },
        options: {
          indexAxis: 'y',
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: { display: false }, tooltip: {
              callbacks: {
                label: ctx => {
                  const [s, e] = ctx.raw.x;
                  const fmt = d => `${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}`;
                  return `${ctx.raw.category}: ${fmt(s)} - ${fmt(e)}`;
                },
              }
            }
          },
          scales: {
            x: {
              type: 'time', min: minTime, max: maxTime, time: { unit: 'hour', displayFormats: { hour: 'ha' } },
              ticks: { stepSize: 2, color: '#000', font: { size: 10 } },
              grid: { color: 'rgba(0,0,0,0.1)' }
            },
            y: { stacked: true, display: false },
          },
        },
      };
    });
  }

  function renderUtilityTimeline() {
    const { minTime, maxTime } = getTimeBounds();
    const grouped = {};
    utilityData.value.forEach(item => {
      if (!grouped[item.utility_name]) grouped[item.utility_name] = [];
      grouped[item.utility_name].push(item);
    });
    utilityChartData.value = utility_order.map(name => {
      const items = grouped[name] || [];
      const datasets = items.length
        ? [{
          data: items.map(i => ({
            x: [DateTime.fromISO(i.start).toJSDate(), DateTime.fromISO(i.finish).toJSDate()],
            y: name, category: i.category,
            backgroundColor: i.category === 1 ? '#00ff00' : '#ff0000',
          })), barThickness: 20
        }]
        : [{ data: [{ x: [minTime, minTime], y: name, backgroundColor: 'transparent' }], barThickness: 20 }];
      return {
        utility_name: name,
        data: { datasets },
        options: {
          indexAxis: 'y', responsive: true, maintainAspectRatio: false,
          plugins: { legend: { display: false } },
          scales: {
            x: {
              type: 'time', min: minTime, max: maxTime,
              time: { unit: 'hour', displayFormats: { hour: 'ha' } },
              ticks: { display: false }, grid: { display: false }
            },
            y: { stacked: true, display: false },
          },
        },
      };
    });
  }

  async function fetchTimelineData() {
    try {
      const res = await fetch('/api/base/Timeline');
      const data = await res.json();
      machineLogs.value = data;
      groupedMachines.value = groupByMachine(data);
      if (groupedMachines.value.length > 0)
        selectedMachine.value = groupedMachines.value[0].currentStatus;
      renderCharts();
    } catch (err) {
      console.error('Error fetching timeline:', err);
    }
  }

  async function fetchUtilityData(id_machine) {
    try {
      const res = await fetch(`/api/machines/${id_machine}/utilities`);
      const data = await res.json();
      utilityData.value = data;
      renderUtilityTimeline();
    } catch (err) {
      console.error('Error fetching utilities:', err);
    }
  }

  onMounted(async () => {
    await Promise.all([store.loadMachineMaster(), store.loadAttendance()]);
    await fetchTimelineData();
  });
</script>
