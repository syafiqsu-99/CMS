<template>
  <v-card>
    <v-card-title class="bg-primary text-white d-flex justify-space-between align-center">
      <div>
        <span class="text-h5">Machine Analytics - {{ selectedMachine?.machine_name }}</span>
        <div class="text-caption mt-1">{{ dateRange }}</div>
      </div>
      <v-btn icon variant="text" @click="closeDialog">
        <v-icon color="white">mdi-close</v-icon>
      </v-btn>
    </v-card-title>

    <v-card-text class="pa-4">
      <!-- Date Range Selector -->
      <v-row class="mb-4">
        <v-col cols="12" md="4">
          <v-text-field v-model="startDate"
                        label="Start Date"
                        type="date"
                        variant="outlined"
                        density="compact"
                        hide-details></v-text-field>
        </v-col>
        <v-col cols="12" md="4">
          <v-text-field v-model="endDate"
                        label="End Date"
                        type="date"
                        variant="outlined"
                        density="compact"
                        hide-details></v-text-field>
        </v-col>
        <v-col cols="12" md="4">
          <v-btn color="primary" block @click="fetchData">
            <v-icon left>mdi-refresh</v-icon>
            Update Data
          </v-btn>
        </v-col>
      </v-row>

      <!-- KPI Summary Cards -->
      <v-row class="mb-4">
        <v-col cols="12" md="3">
          <v-card elevation="2" class="pa-3 text-center" color="success" variant="tonal">
            <div class="text-h4 font-weight-bold">{{ kpiData.totalOutput.toLocaleString() }}</div>
            <div class="text-subtitle-2 mt-1">Total Output (pcs)</div>
          </v-card>
        </v-col>
        <v-col cols="12" md="3">
          <v-card elevation="2" class="pa-3 text-center" color="error" variant="tonal">
            <div class="text-h4 font-weight-bold">{{ kpiData.totalRejects.toLocaleString() }}</div>
            <div class="text-subtitle-2 mt-1">Total Rejects (kg)</div>
          </v-card>
        </v-col>
        <v-col cols="12" md="3">
          <v-card elevation="2" class="pa-3 text-center" color="warning" variant="tonal">
            <div class="text-h4 font-weight-bold">{{ kpiData.totalDowntime }}</div>
            <div class="text-subtitle-2 mt-1">Total Downtime (hrs)</div>
          </v-card>
        </v-col>
        <v-col cols="12" md="3">
          <v-card elevation="2" class="pa-3 text-center" color="info" variant="tonal">
            <div class="text-h4 font-weight-bold">{{ kpiData.oeePercentage }}%</div>
            <div class="text-subtitle-2 mt-1">OEE Score</div>
          </v-card>
        </v-col>
      </v-row>

      <!-- Product Output Table and Chart -->
      <v-row class="mb-4">
        <v-col cols="12">
          <v-card elevation="2">
            <v-card-title class="bg-grey-lighten-3">
              <v-icon left>mdi-package-variant</v-icon>
              Product Output Summary
            </v-card-title>
            <v-card-text>
              <v-data-table :headers="productHeaders"
                            :items="productData"
                            density="compact"
                            :items-per-page="5"
                            class="elevation-0">
                <template v-slot:item.output="{ item }">
                  <span class="font-weight-bold text-success">{{ item.output.toLocaleString() }}</span>
                </template>
                <template v-slot:item.shots="{ item }">
                  {{ item.shots.toLocaleString() }}
                </template>
                <template v-slot:item.cycleTime="{ item }">
                  {{ item.cycleTime }} sec
                </template>
                <template v-slot:item.efficiency="{ item }">
                  <v-chip :color="getEfficiencyColor(item.efficiency)" size="small">
                    {{ item.efficiency }}%
                  </v-chip>
                </template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <!-- Charts Row 1: Output & Downtime -->
      <v-row class="mb-4">
        <v-col cols="12" md="6">
          <v-card elevation="2" class="pa-3">
            <h4 class="mb-3 text-center">Daily Production Output</h4>
            <div style="height: 300px;">
              <canvas ref="outputChart"></canvas>
            </div>
          </v-card>
        </v-col>
        <v-col cols="12" md="6">
          <v-card elevation="2" class="pa-3">
            <h4 class="mb-3 text-center">Downtime Analysis by Category</h4>
            <div style="height: 300px;">
              <canvas ref="downtimeChart"></canvas>
            </div>
          </v-card>
        </v-col>
      </v-row>

      <!-- Downtime Details Table -->
      <v-row class="mb-4">
        <v-col cols="12">
          <v-card elevation="2">
            <v-card-title class="bg-grey-lighten-3">
              <v-icon left>mdi-clock-alert-outline</v-icon>
              Downtime Events Details
            </v-card-title>
            <v-card-text>
              <v-data-table :headers="downtimeHeaders"
                            :items="downtimeData"
                            density="compact"
                            :items-per-page="5"
                            class="elevation-0">
                <template v-slot:item.duration="{ item }">
                  <span class="font-weight-bold">{{ item.duration }} hrs</span>
                </template>
                <template v-slot:item.category="{ item }">
                  <v-chip :color="getCategoryColor(item.category)" size="small">
                    {{ item.category }}
                  </v-chip>
                </template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <!-- Charts Row 2: Reject Analysis -->
      <v-row class="mb-4">
        <v-col cols="12" md="6">
          <v-card elevation="2" class="pa-3">
            <h4 class="mb-3 text-center">Reject Types Distribution</h4>
            <div style="height: 300px;">
              <canvas ref="rejectChart"></canvas>
            </div>
          </v-card>
        </v-col>
        <v-col cols="12" md="6">
          <v-card elevation="2" class="pa-3">
            <h4 class="mb-3 text-center">Daily Reject Trend</h4>
            <div style="height: 300px;">
              <canvas ref="rejectTrendChart"></canvas>
            </div>
          </v-card>
        </v-col>
      </v-row>

      <!-- Reject Details Table -->
      <v-row class="mb-4">
        <v-col cols="12">
          <v-card elevation="2">
            <v-card-title class="bg-grey-lighten-3">
              <v-icon left>mdi-alert-circle-outline</v-icon>
              Reject Analysis by Type
            </v-card-title>
            <v-card-text>
              <v-data-table :headers="rejectHeaders"
                            :items="rejectDetailData"
                            density="compact"
                            :items-per-page="5"
                            class="elevation-0">
                <template v-slot:item.totalWeight="{ item }">
                  <span class="font-weight-bold text-error">{{ item.totalWeight }} kg</span>
                </template>
                <template v-slot:item.percentage="{ item }">
                  {{ item.percentage }}%
                </template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <!-- Hourly Production Chart -->
      <v-row class="mb-4">
        <v-col cols="12">
          <v-card elevation="2" class="pa-3">
            <h4 class="mb-3 text-center">Hourly Production Pattern</h4>
            <div style="height: 300px;">
              <canvas ref="hourlyChart"></canvas>
            </div>
          </v-card>
        </v-col>
      </v-row>

      <!-- Cycle Time Analysis -->
      <v-row class="mb-4">
        <v-col cols="12" md="6">
          <v-card elevation="2" class="pa-3">
            <h4 class="mb-3 text-center">Actual vs Standard Cycle Time</h4>
            <div style="height: 300px;">
              <canvas ref="cycleTimeChart"></canvas>
            </div>
          </v-card>
        </v-col>
        <v-col cols="12" md="6">
          <v-card elevation="2" class="pa-3">
            <h4 class="mb-3 text-center">Shift Performance Comparison</h4>
            <div style="height: 300px;">
              <canvas ref="shiftChart"></canvas>
            </div>
          </v-card>
        </v-col>
      </v-row>

      <!-- Utilities Usage -->
      <v-row class="mb-4">
        <v-col cols="12">
          <v-card elevation="2">
            <v-card-title class="bg-grey-lighten-3">
              <v-icon left>mdi-flash</v-icon>
              Utilities Usage Timeline
            </v-card-title>
            <v-card-text>
              <v-data-table :headers="utilityHeaders"
                            :items="utilityData"
                            density="compact"
                            :items-per-page="5"
                            class="elevation-0">
                <template v-slot:item.status="{ item }">
                  <v-chip :color="item.status === 'Running' ? 'success' : 'error'" size="small">
                    {{ item.status }}
                  </v-chip>
                </template>
                <template v-slot:item.duration="{ item }">
                  {{ item.duration }} hrs
                </template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </v-card-text>

    <v-card-actions class="pa-4 bg-grey-lighten-4">
      <v-spacer></v-spacer>
      <v-btn color="primary" variant="elevated" prepend-icon="mdi-download">
        Export Report
      </v-btn>
      <v-btn color="secondary" variant="elevated" prepend-icon="mdi-printer">
        Print
      </v-btn>
      <v-btn variant="text" @click="closeDialog">Close</v-btn>
    </v-card-actions>
  </v-card>
</template>

<script setup>
  import { ref, computed, onMounted, watch, nextTick } from 'vue';
  import {
    Chart,
    ArcElement,
    DoughnutController,
    BarController,
    BarElement,
    LineController,
    LineElement,
    PointElement,
    CategoryScale,
    LinearScale,
    Tooltip,
    Legend,
  } from 'chart.js';

  Chart.register(
    ArcElement,
    DoughnutController,
    BarController,
    BarElement,
    LineController,
    LineElement,
    PointElement,
    CategoryScale,
    LinearScale,
    Tooltip,
    Legend
  );

  // Props
  const props = defineProps({
    selectedMachine: {
      type: Object,
      default: null
    },
    detailDialog: {
      type: Boolean,
      default: false
    }
  });

  // Emits
  const emit = defineEmits(['update:detailDialog']);

  // Refs for charts
  const outputChart = ref(null);
  const downtimeChart = ref(null);
  const rejectChart = ref(null);
  const rejectTrendChart = ref(null);
  const hourlyChart = ref(null);
  const cycleTimeChart = ref(null);
  const shiftChart = ref(null);

  // Chart instances
  let outputChartInstance = null;
  let downtimeChartInstance = null;
  let rejectChartInstance = null;
  let rejectTrendChartInstance = null;
  let hourlyChartInstance = null;
  let cycleTimeChartInstance = null;
  let shiftChartInstance = null;

  // Date range
  const startDate = ref('2026-01-20');
  const endDate = ref('2026-01-27');

  const dateRange = computed(() => {
    return `${startDate.value} to ${endDate.value}`;
  });

  // KPI Data
  const kpiData = ref({
    totalOutput: 45780,
    totalRejects: 342.5,
    totalDowntime: '18.5',
    oeePercentage: 82.3
  });

  // Product Headers
  const productHeaders = [
    { title: 'Product Type', key: 'productType' },
    { title: 'Mould No', key: 'mouldNo' },
    { title: 'Total Shots', key: 'shots' },
    { title: 'Qty/Shot', key: 'qtyPerShot' },
    { title: 'Total Output (pcs)', key: 'output' },
    { title: 'Part Weight (g)', key: 'partWeight' },
    { title: 'Avg Cycle Time', key: 'cycleTime' },
    { title: 'Efficiency', key: 'efficiency' }
  ];

  // Product Data (dummy)
  const productData = ref([
    {
      productType: 'Bottle Cap 28mm',
      mouldNo: 'M-2801',
      shots: 12450,
      qtyPerShot: 2,
      output: 24900,
      partWeight: 2.3,
      cycleTime: 8.2,
      efficiency: 94
    },
    {
      productType: 'Bottle Cap 38mm',
      mouldNo: 'M-3802',
      shots: 8920,
      qtyPerShot: 2,
      output: 17840,
      partWeight: 3.1,
      cycleTime: 10.5,
      efficiency: 87
    },
    {
      productType: 'Preform 500ml',
      mouldNo: 'P-5004',
      shots: 1520,
      qtyPerShot: 2,
      output: 3040,
      partWeight: 18.5,
      cycleTime: 15.3,
      efficiency: 78
    }
  ]);

  // Downtime Headers
  const downtimeHeaders = [
    { title: 'Start Time', key: 'startTime' },
    { title: 'Finish Time', key: 'finishTime' },
    { title: 'Category', key: 'category' },
    { title: 'Duration', key: 'duration' },
    { title: 'Shift', key: 'shift' },
    { title: 'Remark', key: 'remark' }
  ];

  // Downtime Data (dummy)
  const downtimeData = ref([
    {
      startTime: '2026-01-27 08:15',
      finishTime: '2026-01-27 10:45',
      category: 'Mould Change',
      duration: 2.5,
      shift: 'Day',
      remark: 'Changed from M-2801 to M-3802'
    },
    {
      startTime: '2026-01-27 13:30',
      finishTime: '2026-01-27 15:00',
      category: 'Quality Issue',
      duration: 1.5,
      shift: 'Day',
      remark: 'Adjusting temperature settings'
    },
    {
      startTime: '2026-01-26 22:00',
      finishTime: '2026-01-27 01:30',
      category: 'Scheduled Maintenance',
      duration: 3.5,
      shift: 'Night',
      remark: 'Preventive maintenance'
    },
    {
      startTime: '2026-01-26 16:45',
      finishTime: '2026-01-26 17:30',
      category: 'Material Shortage',
      duration: 0.75,
      shift: 'Day',
      remark: 'Waiting for resin delivery'
    },
    {
      startTime: '2026-01-25 10:00',
      finishTime: '2026-01-25 11:15',
      category: 'Machine Breakdown',
      duration: 1.25,
      shift: 'Day',
      remark: 'Hydraulic pump issue'
    }
  ]);

  // Reject Headers
  const rejectHeaders = [
    { title: 'Date', key: 'date' },
    { title: 'Shift', key: 'shift' },
    { title: 'Reject Type', key: 'rejectType' },
    { title: 'Weight (kg)', key: 'totalWeight' },
    { title: '% of Total', key: 'percentage' }
  ];

  // Reject Detail Data (dummy)
  const rejectDetailData = ref([
    { date: '2026-01-27', shift: 'Day', rejectType: 'Panelling', totalWeight: 45.2, percentage: 13.2 },
    { date: '2026-01-27', shift: 'Day', rejectType: 'Black Dot', totalWeight: 32.8, percentage: 9.6 },
    { date: '2026-01-27', shift: 'Night', rejectType: 'Lumpy', totalWeight: 28.5, percentage: 8.3 },
    { date: '2026-01-26', shift: 'Day', rejectType: 'Start Up', totalWeight: 52.3, percentage: 15.3 },
    { date: '2026-01-26', shift: 'Night', rejectType: 'Burst', totalWeight: 18.7, percentage: 5.5 },
    { date: '2026-01-25', shift: 'Day', rejectType: 'Preform', totalWeight: 38.9, percentage: 11.4 },
    { date: '2026-01-25', shift: 'Night', rejectType: 'Purging', totalWeight: 41.2, percentage: 12.0 }
  ]);

  // Utility Headers
  const utilityHeaders = [
    { title: 'Utility Name', key: 'utilityName' },
    { title: 'Start Time', key: 'startTime' },
    { title: 'Finish Time', key: 'finishTime' },
    { title: 'Status', key: 'status' },
    { title: 'Duration', key: 'duration' },
    { title: 'Shift', key: 'shift' }
  ];

  // Utility Data (dummy)
  const utilityData = ref([
    {
      utilityName: 'Chiller Unit 1',
      startTime: '2026-01-27 08:00',
      finishTime: '2026-01-27 20:00',
      status: 'Running',
      duration: 12.0,
      shift: 'Day'
    },
    {
      utilityName: 'Compressor A',
      startTime: '2026-01-27 08:00',
      finishTime: '2026-01-27 20:00',
      status: 'Running',
      duration: 12.0,
      shift: 'Day'
    },
    {
      utilityName: 'Dryer Unit 2',
      startTime: '2026-01-27 13:00',
      finishTime: '2026-01-27 14:30',
      status: 'Stop',
      duration: 1.5,
      shift: 'Day'
    }
  ]);

  // Helper functions
  const getEfficiencyColor = (efficiency) => {
    if (efficiency >= 90) return 'success';
    if (efficiency >= 75) return 'warning';
    return 'error';
  };

  const getCategoryColor = (category) => {
    const colors = {
      'Mould Change': 'primary',
      'Quality Issue': 'warning',
      'Scheduled Maintenance': 'info',
      'Material Shortage': 'orange',
      'Machine Breakdown': 'error'
    };
    return colors[category] || 'grey';
  };

  const closeDialog = () => {
    emit('update:detailDialog', false);
  };

  const fetchData = () => {
    // This would fetch real data from your API
    console.log('Fetching data for date range:', startDate.value, 'to', endDate.value);
    // For now, we're using dummy data
    nextTick(() => {
      createCharts();
    });
  };

  // Create all charts
  const createCharts = () => {
    createOutputChart();
    createDowntimeChart();
    createRejectChart();
    createRejectTrendChart();
    createHourlyChart();
    createCycleTimeChart();
    createShiftChart();
  };

  // Output Chart (Bar)
  const createOutputChart = () => {
    if (outputChartInstance) {
      outputChartInstance.destroy();
    }

    const ctx = outputChart.value?.getContext('2d');
    if (!ctx) return;

    outputChartInstance = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: ['Jan 21', 'Jan 22', 'Jan 23', 'Jan 24', 'Jan 25', 'Jan 26', 'Jan 27'],
        datasets: [{
          label: 'Daily Output (pcs)',
          data: [6200, 6850, 6420, 6980, 6150, 6580, 6600],
          backgroundColor: 'rgba(76, 175, 80, 0.6)',
          borderColor: 'rgba(76, 175, 80, 1)',
          borderWidth: 1
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            display: true,
            position: 'top'
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            title: {
              display: true,
              text: 'Output (pieces)'
            }
          }
        }
      }
    });
  };

  // Downtime Chart (Doughnut)
  const createDowntimeChart = () => {
    if (downtimeChartInstance) {
      downtimeChartInstance.destroy();
    }

    const ctx = downtimeChart.value?.getContext('2d');
    if (!ctx) return;

    downtimeChartInstance = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: ['Mould Change', 'Quality Issue', 'Scheduled Maintenance', 'Material Shortage', 'Machine Breakdown'],
        datasets: [{
          data: [6.5, 3.2, 4.8, 2.1, 1.9],
          backgroundColor: [
            'rgba(33, 150, 243, 0.7)',
            'rgba(255, 152, 0, 0.7)',
            'rgba(156, 39, 176, 0.7)',
            'rgba(255, 87, 34, 0.7)',
            'rgba(244, 67, 54, 0.7)'
          ],
          borderWidth: 2
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'bottom'
          },
          tooltip: {
            callbacks: {
              label: function (context) {
                return context.label + ': ' + context.parsed + ' hrs';
              }
            }
          }
        }
      }
    });
  };

  // Reject Chart (Doughnut)
  const createRejectChart = () => {
    if (rejectChartInstance) {
      rejectChartInstance.destroy();
    }

    const ctx = rejectChart.value?.getContext('2d');
    if (!ctx) return;

    rejectChartInstance = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: ['Panelling', 'Black Dot', 'Lumpy', 'Start Up', 'Burst', 'Preform', 'Purging'],
        datasets: [{
          data: [45.2, 32.8, 28.5, 52.3, 18.7, 38.9, 41.2],
          backgroundColor: [
            'rgba(244, 67, 54, 0.7)',
            'rgba(233, 30, 99, 0.7)',
            'rgba(156, 39, 176, 0.7)',
            'rgba(103, 58, 183, 0.7)',
            'rgba(63, 81, 181, 0.7)',
            'rgba(33, 150, 243, 0.7)',
            'rgba(0, 188, 212, 0.7)'
          ],
          borderWidth: 2
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'bottom'
          },
          tooltip: {
            callbacks: {
              label: function (context) {
                return context.label + ': ' + context.parsed + ' kg';
              }
            }
          }
        }
      }
    });
  };

  // Reject Trend Chart (Line)
  const createRejectTrendChart = () => {
    if (rejectTrendChartInstance) {
      rejectTrendChartInstance.destroy();
    }

    const ctx = rejectTrendChart.value?.getContext('2d');
    if (!ctx) return;

    rejectTrendChartInstance = new Chart(ctx, {
      type: 'line',
      data: {
        labels: ['Jan 21', 'Jan 22', 'Jan 23', 'Jan 24', 'Jan 25', 'Jan 26', 'Jan 27'],
        datasets: [{
          label: 'Daily Reject (kg)',
          data: [52.3, 48.7, 45.2, 51.8, 49.3, 47.1, 48.9],
          borderColor: 'rgba(244, 67, 54, 1)',
          backgroundColor: 'rgba(244, 67, 54, 0.1)',
          borderWidth: 2,
          fill: true,
          tension: 0.4
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            display: true,
            position: 'top'
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            title: {
              display: true,
              text: 'Reject Weight (kg)'
            }
          }
        }
      }
    });
  };

  // Hourly Chart (Line)
  const createHourlyChart = () => {
    if (hourlyChartInstance) {
      hourlyChartInstance.destroy();
    }

    const ctx = hourlyChart.value?.getContext('2d');
    if (!ctx) return;

    hourlyChartInstance = new Chart(ctx, {
      type: 'line',
      data: {
        labels: ['00:00', '02:00', '04:00', '06:00', '08:00', '10:00', '12:00', '14:00', '16:00', '18:00', '20:00', '22:00'],
        datasets: [{
          label: 'Hourly Production (pcs)',
          data: [280, 295, 310, 290, 320, 315, 305, 325, 310, 300, 285, 275],
          borderColor: 'rgba(33, 150, 243, 1)',
          backgroundColor: 'rgba(33, 150, 243, 0.1)',
          borderWidth: 2,
          fill: true,
          tension: 0.4
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            display: true,
            position: 'top'
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            title: {
              display: true,
              text: 'Output (pieces)'
            }
          },
          x: {
            title: {
              display: true,
              text: 'Time'
            }
          }
        }
      }
    });
  };

  // Cycle Time Chart (Bar - Grouped)
  const createCycleTimeChart = () => {
    if (cycleTimeChartInstance) {
      cycleTimeChartInstance.destroy();
    }

    const ctx = cycleTimeChart.value?.getContext('2d');
    if (!ctx) return;

    cycleTimeChartInstance = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: ['Bottle Cap 28mm', 'Bottle Cap 38mm', 'Preform 500ml'],
        datasets: [
          {
            label: 'Standard Cycle Time (sec)',
            data: [8.0, 10.0, 15.0],
            backgroundColor: 'rgba(76, 175, 80, 0.6)',
            borderColor: 'rgba(76, 175, 80, 1)',
            borderWidth: 1
          },
          {
            label: 'Actual Cycle Time (sec)',
            data: [8.2, 10.5, 15.3],
            backgroundColor: 'rgba(255, 152, 0, 0.6)',
            borderColor: 'rgba(255, 152, 0, 1)',
            borderWidth: 1
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            display: true,
            position: 'top'
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            title: {
              display: true,
              text: 'Cycle Time (seconds)'
            }
          }
        }
      }
    });
  };

  // Shift Chart (Bar)
  const createShiftChart = () => {
    if (shiftChartInstance) {
      shiftChartInstance.destroy();
    }

    const ctx = shiftChart.value?.getContext('2d');
    if (!ctx) return;

    shiftChartInstance = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: ['Day Shift', 'Night Shift'],
        datasets: [
          {
            label: 'Output (pcs)',
            data: [24500, 21280],
            backgroundColor: 'rgba(33, 150, 243, 0.6)',
            borderColor: 'rgba(33, 150, 243, 1)',
            borderWidth: 1,
            yAxisID: 'y'
          },
          {
            label: 'Reject (kg)',
            data: [185.3, 157.2],
            backgroundColor: 'rgba(244, 67, 54, 0.6)',
            borderColor: 'rgba(244, 67, 54, 1)',
            borderWidth: 1,
            yAxisID: 'y1'
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            display: true,
            position: 'top'
          }
        },
        scales: {
          y: {
            type: 'linear',
            display: true,
            position: 'left',
            title: {
              display: true,
              text: 'Output (pieces)'
            }
          },
          y1: {
            type: 'linear',
            display: true,
            position: 'right',
            title: {
              display: true,
              text: 'Reject (kg)'
            },
            grid: {
              drawOnChartArea: false
            }
          }
        }
      }
    });
  };

  // Watch for dialog open/close
  watch(() => props.detailDialog, (newVal) => {
    if (newVal) {
      nextTick(() => {
        createCharts();
      });
    } else {
      // Destroy all chart instances when dialog closes
      if (outputChartInstance) outputChartInstance.destroy();
      if (downtimeChartInstance) downtimeChartInstance.destroy();
      if (rejectChartInstance) rejectChartInstance.destroy();
      if (rejectTrendChartInstance) rejectTrendChartInstance.destroy();
      if (hourlyChartInstance) hourlyChartInstance.destroy();
      if (cycleTimeChartInstance) cycleTimeChartInstance.destroy();
      if (shiftChartInstance) shiftChartInstance.destroy();
    }
  });

  // Initialize charts on mount
  onMounted(() => {
    if (props.detailDialog) {
      nextTick(() => {
        createCharts();
      });
    }
  });
</script>

<style scoped>
  .v-card-title {
    font-weight: 500;
  }

  .v-data-table {
    font-size: 0.875rem;
  }

  canvas {
    max-height: 300px;
  }
</style>
