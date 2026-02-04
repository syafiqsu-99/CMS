<template>
  <v-container fluid class="pa-2 h-100 d-flex flex-column">
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
      <!--Machines List-->
      <v-col cols="4" class="pa-1">
        <v-card variant="flat" style="height: calc(100vh - 100px); overflow-y: auto;" elevation="0" class="bg-grey-lighten-4">
          <v-list dense class="pa-2 bg-transparent">
            <v-list-item v-for="machine in groupedMachines"
                         :key="machine.id_machine"
                         @click="selectMachine(machine)"
                         :class="{ 'bg-white elevation-2': selectedMachine?.id_machine === machine.id_machine, 'bg-white': selectedMachine?.id_machine !== machine.id_machine }"
                         class="px-3 py-2 mb-2 cursor-pointer rounded-lg"
                         :style="{
                       borderLeft: selectedMachine?.id_machine === machine.id_machine ? '4px solid #1976D2' : 'none',
                       transition: 'all 0.2s ease'
                     }">
              <v-row dense align="center" no-gutters>
                <v-col cols="2" class="text-center">
                  <div class="text-body-2 font-weight-bold text-truncate" :title="machine.machine_name">
                    {{ machine.machine_name }}
                  </div>
                </v-col>

                <v-col cols="10" class="text-center">
                  <div v-if="chartData[machine.id_machine]" style="height: 40px;">
                    <Bar :data="chartData[machine.id_machine].data"
                         :options="chartData[machine.id_machine].options" />
                  </div>
                  <div v-else class="text-caption text-grey">
                    No data
                  </div>
                  <v-chip :color="machine.currentStatus?.color"
                          size="x-small"
                          class="mt-1">
                    {{ machine.currentStatus?.category || 'N/A' }}
                  </v-chip>
                </v-col>
              </v-row>
            </v-list-item>
          </v-list>
        </v-card>
      </v-col>

      <v-col cols="8" class="pa-1">
        <v-card variant="flat" class="pa-4 bg-grey-lighten-5" style="height: calc(100vh - 100px); overflow-y: auto;" elevation="0">
          <div v-if="selectedMachine && selectedMachineData">
            <div class="mb-4 d-flex align-center justify-space-between">
              <div>
                <v-card-title class="text-h5 pa-0 mb-1">
                  <strong>{{ selectedMachine.machine_name }}</strong>
                </v-card-title>
                <v-card-subtitle class="text-subtitle-2 pa-0 text-grey-darken-1">
                  {{ selectedMachineData.type }}
                </v-card-subtitle>
              </div>
              <v-chip :color="selectedMachineData.color"
                      size="large"
                      class="font-weight-bold">
                {{ selectedMachineData.category || 'No Data' }}
              </v-chip>
            </div>

            <!-- Machine Information -->
            <v-card variant="flat" class="pa-4 mb-4 rounded-lg" elevation="1">
              <div class="d-flex align-center mb-3">
                <v-icon size="20" color="primary" class="mr-2">mdi-information-outline</v-icon>
                <span class="text-subtitle-2 font-weight-bold">Machine Information</span>
              </div>
              <v-row dense>
                <v-col cols="6" sm="4" md="3">
                  <div class="text-caption text-grey-darken-1 mb-1">Model</div>
                  <div class="text-body-2 font-weight-medium">{{ selectedMachineData.type }}</div>
                </v-col>
                <v-col cols="6" sm="4" md="3">
                  <div class="text-caption text-grey-darken-1 mb-1">Packer</div>
                  <div class="text-body-2 font-weight-medium">{{ selectedMachineData.packer || 'N/A' }}</div>
                </v-col>
                <v-col cols="6" sm="4" md="3">
                  <div class="text-caption text-grey-darken-1 mb-1">SAP</div>
                  <div class="text-body-2 font-weight-medium">{{ selectedMachineData.id_type }}</div>
                </v-col>
                <v-col cols="6" sm="4" md="3">
                  <div class="text-caption text-grey-darken-1 mb-1">Mould</div>
                  <div class="text-body-2 font-weight-medium">{{ selectedMachineData.mould }}</div>
                </v-col>
                <v-col cols="6" sm="4" md="3">
                  <div class="text-caption text-grey-darken-1 mb-1">Qty per CT</div>
                  <div class="text-body-2 font-weight-medium">{{ selectedMachineData.qty_perct }}</div>
                </v-col>
                <v-col cols="6" sm="4" md="3">
                  <div class="text-caption text-grey-darken-1 mb-1">Part Weight</div>
                  <div class="text-body-2 font-weight-medium">{{ selectedMachineData.part_weight }} g</div>
                </v-col>
                <v-col cols="6" sm="4" md="3">
                  <div class="text-caption text-grey-darken-1 mb-1">Material</div>
                  <div class="text-body-2 font-weight-medium">{{ selectedMachineData.material }}</div>
                </v-col>
                <v-col cols="6" sm="4" md="3">
                  <div class="text-caption text-grey-darken-1 mb-1">Visual QC</div>
                  <div class="text-body-2 font-weight-medium">{{ selectedMachineData.visual_qc }}</div>
                </v-col>
                <v-col cols="6" sm="4" md="3">
                  <div class="text-caption text-grey-darken-1 mb-1">Measure QC</div>
                  <div class="text-body-2 font-weight-medium">{{ selectedMachineData.measure_qc }}</div>
                </v-col>
              </v-row>
            </v-card>

            <!-- Metrics Cards -->
            <v-row dense>
              <v-col cols="12" sm="6" md="3">
                <v-card variant="flat" color="primary" class="pa-3 rounded-lg white--text" elevation="2" style="height: 130px;">
                  <div class="d-flex flex-column justify-space-between" style="height: 100%;">
                    <div>
                      <div class="text-overline opacity-90">Output</div>
                      <div class="text-h4 font-weight-bold mt-1">
                        {{ selectedMachineData.output }}
                        <span class="text-body-2">pcs</span>
                      </div>
                    </div>
                    <div class="text-caption opacity-80">
                      Plan: {{ selectedMachineData.planned_output }} pcs
                    </div>
                  </div>
                </v-card>
              </v-col>

              <v-col cols="12" sm="6" md="3">
                <v-card variant="flat" color="success" class="pa-3 rounded-lg white--text" elevation="2" style="height: 130px;">
                  <div class="d-flex flex-column justify-space-between" style="height: 100%;">
                    <div>
                      <div class="text-overline opacity-90">Efficiency</div>
                      <div class="text-h4 font-weight-bold mt-1">
                        {{ calculateEfficiency(selectedMachineData) }}%
                      </div>
                    </div>
                    <v-progress-linear :model-value="parseFloat(calculateEfficiency(selectedMachineData))"
                                       color="white"
                                       bg-color="rgba(255,255,255,0.3)"
                                       height="6"
                                       rounded
                                       class="mt-2"></v-progress-linear>
                  </div>
                </v-card>
              </v-col>

              <v-col cols="12" sm="6" md="3">
                <v-card variant="flat" color="info" class="pa-3 rounded-lg white--text" elevation="2" style="height: 130px;">
                  <div class="d-flex flex-column justify-space-between" style="height: 100%;">
                    <div>
                      <div class="text-overline opacity-90">Cycle Time</div>
                      <div class="text-h4 font-weight-bold mt-1">
                        {{ selectedMachineData.act_ct.toFixed(2) || 0 }}
                        <span class="text-body-2">sec</span>
                      </div>
                    </div>
                    <div class="text-caption opacity-80">
                      SAP CT: {{ selectedMachineData.sap_ct.toFixed(2) || 0 }} sec
                    </div>
                  </div>
                </v-card>
              </v-col>

              <v-col cols="12" sm="6" md="3">
                <v-card variant="flat" color="error" class="pa-3 rounded-lg white--text" elevation="2" style="height: 130px;">
                  <div class="d-flex flex-column justify-space-between" style="height: 100%;">
                    <div>
                      <div class="text-overline opacity-90">Reject</div>
                      <div class="text-h4 font-weight-bold mt-1">
                        {{ selectedMachineData.reject_pcs || 0 }}
                        <span class="text-body-2">pcs</span>
                      </div>
                    </div>
                    <div>
                      <div class="text-caption opacity-80">Weight: {{ selectedMachineData.reject_weight.toFixed(2) || 0 }} kg</div>
                      <div class="text-caption opacity-80">Rate: {{ calculateRejectRate(selectedMachineData) }}%</div>
                    </div>
                  </div>
                </v-card>
              </v-col>

              <!-- Remark/Problem Alert -->
              <v-col cols="12" v-if="selectedMachineData.problem">
                <v-card variant="flat" class="pa-3 rounded-lg" color="amber-lighten-5" elevation="1">
                  <div class="d-flex align-start">
                    <v-icon size="20" color="warning" class="mr-2 mt-1">mdi-alert-circle-outline</v-icon>
                    <div>
                      <div class="text-subtitle-2 font-weight-bold mb-1 text-warning">Remark</div>
                      <div class="text-body-2">{{ selectedMachineData.problem }}</div>
                    </div>
                  </div>
                </v-card>
              </v-col>

              <!-- Production Timeline -->
              <v-col cols="12">
                <v-card variant="flat" class="pa-3 rounded-lg" elevation="1">
                  <div class="d-flex align-center mb-3">
                    <v-icon size="20" color="primary" class="mr-2">mdi-chart-timeline-variant</v-icon>
                    <span class="text-subtitle-2 font-weight-bold">Production Timeline</span>
                  </div>
                  <div v-if="chartData[selectedMachineData.id_machine]" style="height: 100px;" class="mb-2">
                    <Bar :data="chartData[selectedMachineData.id_machine].data"
                         :options="chartData[selectedMachineData.id_machine].options" />
                  </div>
                </v-card>
              </v-col>

              <!--Utility Data-->
              <v-col cols="12">
                <v-card variant="flat" class="pa-3 rounded-lg" elevation="1">
                  <div class="d-flex align-center mb-3">
                    <v-icon size="20" color="primary" class="mr-2">mdi-flash-outline</v-icon>
                    <span class="text-subtitle-2 font-weight-bold">Utility Timeline</span>
                  </div>
                  <div v-if="utilityChartData.length > 0">
                    <v-list dense class="py-0 bg-transparent">
                      <v-list-item v-for="(chart, index) in utilityChartData"
                                   :key="chart.utility_name"
                                   :class="{ 'mt-2': index > 0, 'border-t': index > 0 }"
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
                </v-card>
              </v-col>
            </v-row>
          </div>

          <div v-else class="d-flex flex-column align-center justify-center" style="height: 100%;">
            <v-icon size="80" color="grey-lighten-2">mdi-monitor-dashboard</v-icon>
            <div class="text-h6 text-grey-darken-1 mt-4 font-weight-medium">Select a machine to view details</div>
            <div class="text-caption text-grey mt-2">Choose from the list on the left</div>
          </div>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup>
  import { ref, onMounted, computed, watch } from 'vue';
  import { Bar } from 'vue-chartjs';
  import { Chart as ChartJS, CategoryScale, LinearScale, BarElement, TimeScale, Title, Tooltip, Legend } from 'chart.js';
  import 'chartjs-adapter-luxon';
  import { DateTime } from 'luxon';
  import { pinia } from '@/store/index'
  import { UTILITIES } from '@/store/constant.js';

  ChartJS.register(CategoryScale, LinearScale, BarElement, TimeScale, Title, Tooltip, Legend);

  const store = pinia();
  const machineLogs = ref([]);
  const groupedMachines = ref([]);
  const chartData = ref({});
  const selectedMachine = ref(null);
  const utilityData = ref([]);
  const utilityChartData = ref({});
  const utility_order = UTILITIES;

  const selectedMachineData = computed(() => {
    if (!selectedMachine.value || !store.machineData) {
      return null;
    }
    const machineData = store.machineData.find(m => m.id_machine === selectedMachine.value.id_machine) || null;

    if (machineData) {
      fetchUtilityData(machineData.id_machine);
    }
  
    return machineData;
  });

  const summaryCards = computed(() => [
    { title: "Total Machines", icon: "mdi-factory", color: "primary", value: store.totalMachines },
    { title: "Running", icon: "mdi-play-circle", color: "success", value: store.runningMachines },
    { title: "Stop", icon: "mdi-stop-circle", color: "error", value: store.stopMachines },
    { title: "Staff Assigned", icon: "mdi-account-group", color: "info", value: store.activeStaff },
  ]);

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
          currentStatus: null
        };
      }
      grouped[item.id_machine].data.push(item);
    });

    Object.values(grouped).forEach(machine => {
      machine.data.sort((a, b) => new Date(b.start) - new Date(a.start));
      machine.currentStatus = machine.data[0];
    });

    return Object.values(grouped);
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

  function renderCharts() {
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

  async function fetchTimelineData() {
    try {
      const response = await fetch('/api/MachineLog/Timeline');
      const data = await response.json();
      machineLogs.value = data;
      groupedMachines.value = groupByMachine(data);

      if (groupedMachines.value.length > 0) {
        selectedMachine.value = groupedMachines.value[0];
      }

      renderCharts();
    } catch (error) {
      console.error("Error fetching timeline data:", error);
    }
  }

  async function fetchUtilityData(id_machine) {
    try {
      const response = await fetch(`/api/MachineLog/Utilities?id_machine=${id_machine}`);

      const data = await response.json();
      utilityData.value = data;

      renderUtilityTimeline();
    } catch (err) {
      console.error("Error loading utility data:", err);
    }
  }

  function renderUtilityTimeline() {
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

  function calculateEfficiency(machineData) {
    if (!machineData || !machineData.planned_output || machineData.planned_output === 0) return '0.00';
    const efficiency = ((machineData.output || 0) / machineData.planned_output) * 100;
    return efficiency.toFixed(2);
  }

  function calculateRejectRate(machineData) {
    if (!machineData || !machineData.output || machineData.output === 0) return '0.00';
    const totalProduced = (machineData.output || 0) + (machineData.reject_pcs || 0);
    if (totalProduced === 0) return '0.00';
    const rejectRate = ((machineData.reject_pcs || 0) / totalProduced) * 100;
    return rejectRate.toFixed(2);
  }

  onMounted(async () => {
    try {
      await Promise.all([
        fetchTimelineData(),
      ]);
    } catch (error) {
      console.error("Error loading data:", error);
    }
  });
</script>
