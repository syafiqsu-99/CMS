<template>
  <v-container fluid class="pa-2 h-100 d-flex flex-column" style="overflow-y: auto; overflow-x: hidden;">
    <v-row no-gutters align="center" justify="center" class="mb-1">
      <v-col cols="12" class="text-center">
        <h2 class="font-weight-bold">OEE Dashboard</h2>
      </v-col>
    </v-row>

    <!-- Date Filters -->
    <v-row no-gutters align="center" justify="center" class="mb-2">
      <v-col cols="12" md="4" class="px-1">
        <v-date-input v-model="startDate"
                      label="Start Date"
                      :max="endDate"
                      variant="outlined"
                      density="compact"
                      hide-details
                      display-format="fullDate" />
      </v-col>
      <v-col cols="12" md="4" class="px-1">
        <v-date-input v-model="endDate"
                      label="End Date"
                      :min="startDate"
                      variant="outlined"
                      density="compact"
                      hide-details
                      display-format="fullDate" />
      </v-col>
    </v-row>

    <!-- Summary Doughnut Cards -->
    <v-row class="mb-2">
      <v-col cols="12" md="3" v-for="(metric, index) in summaryMetrics" :key="metric.title" class="px-1">
        <v-card class="pa-2 d-flex flex-column align-center justify-center text-center" elevation="2"
                :style="{ borderTop: `3px solid ${getMetricColor(metric.type, metric.value)}` }">
          <div class="text-subtitle-2 font-weight-bold mb-1" :style="{ color: getMetricColor(metric.type, metric.value) }">
            {{ metric.title }}
          </div>
          <div class="relative d-flex align-center justify-center" style="width: 140px; height: 140px;">
            <canvas :ref="el => doughnutRefs[index] = el"></canvas>
          </div>
          <div class="text-caption mt-1" style="color: #888;">
            Target: {{ metricTargets[metric.type] }}%
          </div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Machine Data Table -->
    <v-row class="mb-2">
      <v-col cols="12">
        <v-card elevation="2" class="overflow-hidden w-100">
          <!-- Main Table -->
          <v-data-table-virtual :headers="tableHeaders"
                                :items="machineData"
                                fixed-header
                                density="compact"
                                hover
                                @click:row="handleRowClick"
                                style="height: 50vh;">

            <template v-slot:item.run_time="{ item }">
              {{ Number(item.run_time).toFixed(2) }}
            </template>
            <template v-slot:item.down_time="{ item }">
              {{ Number(item.down_time).toFixed(2) }}
            </template>
            <template v-slot:item.material_used="{ item }">
              {{ Number(item.material_used).toFixed(2) }}
            </template>
            <template v-slot:item.reject_weight="{ item }">
              {{ Number(item.reject_weight).toFixed(2) }}
            </template>
            <template v-slot:item.oee="{ item }">
              <span :style="{ color: getMetricColor('oee', item.oee), fontWeight: '600' }">
                {{ item.oee }}%
              </span>
            </template>
            <template v-slot:item.performance="{ item }">
              <span :style="{ color: getMetricColor('performance', item.performance), fontWeight: '600' }">
                {{ item.performance }}%
              </span>
            </template>
            <template v-slot:item.availability="{ item }">
              <span :style="{ color: getMetricColor('availability', item.availability), fontWeight: '600' }">
                {{ item.availability }}%
              </span>
            </template>
            <template v-slot:item.quality="{ item }">
              <span :style="{ color: getMetricColor('quality', item.quality), fontWeight: '600' }">
                {{ item.quality }}%
              </span>
            </template>
          </v-data-table-virtual>

          <v-divider />
          <v-table density="compact" class="totals-footer">
            <tbody>
              <tr style="background-color: #f5f5f5; font-weight: 700;">
                <td :style="{ width: '18%' }">TOTAL</td>
                <td :style="{ width: '8%', textAlign: 'center' }"></td>
                <td :style="{ width: '8%', textAlign: 'center' }"></td>
                <td :style="{ width: '8%', textAlign: 'center' }"></td>
                <td :style="{ width: '8%', textAlign: 'center' }"></td>
                <td :style="{ width: '10%', textAlign: 'center' }">{{ totalsRow.run_time }}</td>
                <td :style="{ width: '10%', textAlign: 'center' }">{{ totalsRow.down_time }}</td>
                <td :style="{ width: '10%', textAlign: 'center' }">{{ totalsRow.material_used }}</td>
                <td :style="{ width: '10%', textAlign: 'center' }">{{ totalsRow.reject_weight }}</td>
              </tr>
            </tbody>
          </v-table>
        </v-card>
      </v-col>
    </v-row>

    <!-- Pareto Charts -->
    <v-row>
      <v-col cols="12" md="4" class="px-1">
        <v-card elevation="2" class="pa-3">
          <h4 class="mb-2 text-center text-subtitle-1 font-weight-bold">Total Reject</h4>
          <div style="height: 420px;" class="d-flex align-center justify-center">
            <canvas v-if="rejectData.length > 0" ref="rejectChart" style="width:100%;"></canvas>
            <div v-else class="d-flex flex-column align-center justify-center w-100 h-100">
              <v-icon size="48" color="grey-lighten-1">mdi-chart-bar</v-icon>
              <span class="text-grey text-body-2 mt-2">No reject data available</span>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="4" class="px-1">
        <v-card elevation="2" class="pa-3">
          <h4 class="mb-2 text-center text-subtitle-1 font-weight-bold">Total Output</h4>
          <div style="height: 420px;" class="d-flex align-center justify-center">
            <canvas v-if="outputData.length > 0" ref="outputChart" style="width:100%;"></canvas>
            <div v-else class="d-flex flex-column align-center justify-center w-100 h-100">
              <v-icon size="48" color="grey-lighten-1">mdi-chart-bar</v-icon>
              <span class="text-grey text-body-2 mt-2">No output data available</span>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="4" class="px-1">
        <v-card elevation="2" class="pa-3">
          <h4 class="mb-2 text-center text-subtitle-1 font-weight-bold">Total Downtime</h4>
          <div style="height: 420px;" class="d-flex align-center justify-center">
            <canvas v-if="downtimeData.length > 0" ref="downtimeChart" style="width:100%;"></canvas>
            <div v-else class="d-flex flex-column align-center justify-center w-100 h-100">
              <v-icon size="48" color="grey-lighten-1">mdi-chart-timeline-variant</v-icon>
              <span class="text-grey text-body-2 mt-2">No downtime data available</span>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Machine Detail Dialog -->
    <v-dialog v-model="detailDialog" max-width="1400px" scrollable>
      <MachineOEE :selectedMachine="selectedMachine"
                  :detailDialog="detailDialog"
                  :startDate="startDate instanceof Date ? startDate.toLocaleDateString('en-CA') : startDate"
                  :endDate="endDate instanceof Date ? endDate.toLocaleDateString('en-CA') : endDate"
                  @update:detailDialog="detailDialog = $event" />
    </v-dialog>
  </v-container>
</template>

<script setup>
  import MachineOEE from '@/components/MachineOEE.vue';
  import { ref, computed, onMounted, watch, nextTick } from "vue";
  import {
    Chart, ArcElement, DoughnutController, BarController, BarElement,
    LineController, LineElement, PointElement, CategoryScale, LinearScale,
    Tooltip, Legend,
  } from "chart.js";

  Chart.register(
    ArcElement, DoughnutController, BarController, BarElement,
    LineController, LineElement, PointElement, CategoryScale, LinearScale,
    Tooltip, Legend
  );

  const doughnutRefs = ref([]);
  const outputChart = ref(null);
  const downtimeChart = ref(null);
  const rejectChart = ref(null);
  const detailDialog = ref(false);
  const selectedMachine = ref(null);

  const rejectData = ref([]);
  const outputData = ref([]);
  const downtimeData = ref([]);

  let doughnutCharts = [];
  let pageChartInstances = { output: null, downtime: null, reject: null };

  const startDate = ref(new Date());
  const endDate = ref(new Date());
  const currentHour = new Date().getHours();
  const currentShift = currentHour >= 8 && currentHour < 20 ? 1 : 2;

  const metricTargets = {
    oee: 65,
    performance: 95,
    availability: 70,
    quality: 97,
  };

  const getMetricColor = (metricType, value) => {
    const v = Number(value);
    switch ((metricType || '').toLowerCase()) {
      case 'oee': return v >= metricTargets.oee ? '#4caf50' : '#f44336';
      case 'performance': return v >= metricTargets.performance ? '#4caf50' : '#f44336';
      case 'availability': return v >= metricTargets.availability ? '#4caf50' : '#f44336';
      case 'quality': return v >= metricTargets.quality ? '#4caf50' : '#f44336';
      default: return '#9e9e9e';
    }
  };

  const centerTextPlugin = {
    id: "centerText",
    beforeDraw(chart) {
      const { width, height, ctx } = chart;
      const value = chart.config.data.datasets[0].data[0];
      ctx.save();
      const fontSize = (height / 120).toFixed(2);
      ctx.font = `bold ${fontSize}em sans-serif`;
      ctx.textBaseline = "middle";
      ctx.textAlign = "center";
      ctx.fillStyle = chart.config.data.datasets[0].backgroundColor[0];
      ctx.fillText(`${value.toFixed(0)}%`, Math.round(width / 2), Math.round(height / 2));
      ctx.restore();
    },
  };

  const tableHeaders = [
    { title: "Machine", key: "machine_name", width: "18%" },
    { title: "OEE (%)", key: "oee", width: "8%", align: "center" },
    { title: "Performance (%)", key: "performance", width: "8%", align: "center" },
    { title: "Availability (%)", key: "availability", width: "8%", align: "center" },
    { title: "Quality (%)", key: "quality", width: "8%", align: "center" },
    { title: "Run Time (hrs)", key: "run_time", width: "10%", align: "center" },
    { title: "Down Time (hrs)", key: "down_time", width: "10%", align: "center" },
    { title: "Material Used (kg)", key: "material_used", width: "10%", align: "center" },
    { title: "Reject (kg)", key: "reject_weight", width: "10%", align: "center" },
  ];

  const machineData = ref([]);
  const summaryMetrics = ref([
    { title: "Overall OEE", value: 0, type: 'oee' },
    { title: "Performance", value: 0, type: 'performance' },
    { title: "Availability", value: 0, type: 'availability' },
    { title: "Quality", value: 0, type: 'quality' },
  ]);

  const totalsRow = computed(() => {
    const rows = machineData.value;
    const sum = (key) => rows.reduce((s, r) => s + (Number(r[key]) || 0), 0);
    return {
      run_time: sum('run_time').toFixed(2),
      down_time: sum('down_time').toFixed(2),
      material_used: sum('material_used').toFixed(2),
      reject_weight: sum('reject_weight').toFixed(2),
    };
  });

  const fetchOEEData = async () => {
    try {
      const params = new URLSearchParams({
        start_date: startDate.value.toLocaleDateString("en-CA"),
        end_date: endDate.value.toLocaleDateString("en-CA"),
        shift: currentShift,
      });

      const [oeeRes, rejectRes, outputRes, downtimeRes] = await Promise.all([
        fetch(`/api/MachineLog/OEE?${params}`),
        fetch(`/api/MachineLog/Reject?${params}`),
        fetch(`/api/MachineLog/Output?${params}`),
        fetch(`/api/MachineLog/Downtime?${params}`)
      ]);

      const data = await oeeRes.json();
      machineData.value = Array.isArray(data)
        ? data.map(item => ({
          ...item,
          oee: Number(item.oee || 0).toFixed(2),
          performance: Number(item.performance || 0).toFixed(2),
          availability: Number(item.availability || 0).toFixed(2),
          quality: Number(item.quality || 0).toFixed(2),
          run_time: Number(item.run_time || 0),
          down_time: Number(item.down_time || 0),
          material_used: Number(item.material_used || 0),
          reject_weight: Number(item.reject_weight || 0),
        }))
        : [];

      if (machineData.value.length > 0) {
        const validMachines = data.filter(m => m.id_machine >= 1 && m.id_machine <= 16 && Number(m.oee) > 0);
        const avg = (key) => {
          if (!validMachines.length) return 0;
          return validMachines.reduce((s, m) => s + (Number(m[key]) || 0), 0) / validMachines.length;
        };
        summaryMetrics.value = [
          { title: "Overall OEE", value: Number(avg("oee").toFixed(0)), type: 'oee' },
          { title: "Performance", value: Number(avg("performance").toFixed(0)), type: 'performance' },
          { title: "Availability", value: Number(avg("availability").toFixed(0)), type: 'availability' },
          { title: "Quality", value: Number(avg("quality").toFixed(0)), type: 'quality' },
        ];
      } else {
        summaryMetrics.value = summaryMetrics.value.map(m => ({ ...m, value: 0 }));
      }

      rejectData.value = await rejectRes.json();
      outputData.value = await outputRes.json();
      downtimeData.value = await downtimeRes.json();

      await nextTick();
      updateCharts();
    } catch (err) {
      console.error("Error fetching data:", err);
    }
  };

  const createDoughnutChart = (ctx, value, metricType) => {
    const color = getMetricColor(metricType, value);
    return new Chart(ctx, {
      type: "doughnut",
      data: {
        datasets: [{
          data: [value, Math.max(0, 100 - value)],
          backgroundColor: [color, "#e0e0e0"],
          borderWidth: 0,
        }],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: '65%',
        plugins: { legend: { display: false }, tooltip: { enabled: false } },
      },
      plugins: [centerTextPlugin],
    });
  };

  const updateCharts = () => {
    try {
      doughnutCharts.forEach(c => c?.destroy());
      doughnutCharts = [];
      Object.keys(pageChartInstances).forEach(key => {
        pageChartInstances[key]?.destroy();
        pageChartInstances[key] = null;
      });

      summaryMetrics.value.forEach((metric, i) => {
        if (doughnutRefs.value[i]) {
          const ctx = doughnutRefs.value[i].getContext("2d");
          doughnutCharts.push(createDoughnutChart(ctx, metric.value || 0, metric.type));
        }
      });

      const getCommonOptions = (yAxisLabel, xAxisLabel) => ({
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: { display: true, position: 'top' },
          tooltip: {
            callbacks: {
              title: (context) => context[0].chart.data.fullLabels?.[context[0].dataIndex] || context[0].label,
              label: (context) => {
                const v = context.parsed.y ?? 0;
                if (context.dataset.label === 'Cumulative %') return `Cumulative: ${v.toFixed(1)}%`;
                const unit = yAxisLabel.includes('kg') ? 'kg' : 'hrs';
                return `${context.dataset.label}: ${v.toFixed(2)} ${unit}`;
              }
            }
          }
        },
        scales: {
          x: {
            title: { display: true, text: xAxisLabel, font: { size: 12 } },
            ticks: { autoSkip: false, maxRotation: 45, minRotation: 45, font: { size: 10 } }
          },
          y: {
            beginAtZero: true,
            position: 'left',
            title: { display: true, text: yAxisLabel, font: { size: 12 } }
          },
          y1: {
            beginAtZero: true,
            max: 100,
            position: 'right',
            title: { display: true, text: 'Cumulative %', font: { size: 12 } },
            grid: { drawOnChartArea: false },
            ticks: { callback: v => v + '%' }
          }
        }
      });

      if (rejectChart.value && rejectData.value.length > 0) {
        let cum = 0;
        const total = rejectData.value.reduce((s, d) => s + d.total_reject, 0);
        const cumPct = rejectData.value.map(d => { cum += d.total_reject; return (cum / total) * 100; });
        pageChartInstances.reject = new Chart(rejectChart.value.getContext('2d'), {
          type: 'bar',
          data: {
            labels: rejectData.value.map(r => r.id_type),
            fullLabels: rejectData.value.map(r => r.type),
            datasets: [
              { label: 'Total Reject (kg)', data: rejectData.value.map(r => r.total_reject), backgroundColor: '#F44336', yAxisID: 'y', order: 2 },
              { label: 'Cumulative %', data: cumPct, type: 'line', borderColor: '#FF9800', backgroundColor: '#FF9800', borderWidth: 2, pointRadius: 4, yAxisID: 'y1', order: 1 }
            ]
          },
          options: getCommonOptions('Weight (kg)', 'Product Type')
        });
      }

      if (outputChart.value && outputData.value.length > 0) {
        let cum = 0;
        const total = outputData.value.reduce((s, d) => s + d.total_output, 0);
        const cumPct = outputData.value.map(d => { cum += d.total_output; return (cum / total) * 100; });
        pageChartInstances.output = new Chart(outputChart.value.getContext('2d'), {
          type: 'bar',
          data: {
            labels: outputData.value.map(d => d.id_type),
            fullLabels: outputData.value.map(d => d.type),
            datasets: [
              { label: 'Total Output (kg)', data: outputData.value.map(d => d.total_output), backgroundColor: '#2196F3', yAxisID: 'y', order: 2 },
              { label: 'Cumulative %', data: cumPct, type: 'line', borderColor: '#FF9800', backgroundColor: '#FF9800', borderWidth: 2, pointRadius: 4, yAxisID: 'y1', order: 1 }
            ]
          },
          options: getCommonOptions('Weight (kg)', 'Product Type')
        });
      }

      if (downtimeChart.value && downtimeData.value.length > 0) {
        let cum = 0;
        const total = downtimeData.value.reduce((s, d) => s + d.hours, 0);
        const cumPct = downtimeData.value.map(d => { cum += d.hours; return (cum / total) * 100; });
        pageChartInstances.downtime = new Chart(downtimeChart.value.getContext('2d'), {
          type: 'bar',
          data: {
            labels: downtimeData.value.map(d => d.id_type),
            fullLabels: downtimeData.value.map(d => d.type),
            datasets: [
              { label: 'Downtime (Hours)', data: downtimeData.value.map(d => d.hours), backgroundColor: '#FF7043', yAxisID: 'y', order: 2 },
              { label: 'Cumulative %', data: cumPct, type: 'line', borderColor: '#FF9800', backgroundColor: '#FF9800', borderWidth: 2, pointRadius: 4, yAxisID: 'y1', order: 1 }
            ]
          },
          options: getCommonOptions('Time (hrs)', 'Downtime Category')
        });
      }
    } catch (err) {
      console.error("Error updating charts:", err);
    }
  };

  const handleRowClick = (event, { item }) => {
    if (item._isTotal) return;
    selectedMachine.value = item;
    detailDialog.value = true;
  };

  watch([startDate, endDate], () => {
    fetchOEEData();
    if (detailDialog.value) detailDialog.value = false;
  });

  onMounted(async () => {
    await fetchOEEData();
  });
</script>
