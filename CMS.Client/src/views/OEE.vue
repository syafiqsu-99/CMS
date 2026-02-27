<template>
  <v-container fluid class="pa-2 h-100 d-flex flex-column" style="overflow-y: auto; overflow-x: hidden;">
    <v-row no-gutters align="center" justify="center">
      <v-col cols="12" class="text-center">
        <h2 class="font-weight-bold">OEE</h2>
      </v-col>
    </v-row>
    <v-row no-gutters align="center" justify="center">
      <v-col cols="12" md="4">
        <v-date-input v-model="startDate"
                      label="Start Date"
                      :max="endDate"
                      variant="outlined"
                      density="compact"
                      hide-details
                      display-format="fullDate" />
      </v-col>
      <v-col cols="12" md="4">
        <v-date-input v-model="endDate"
                      label="End Date"
                      :min="startDate"
                      variant="outlined"
                      density="compact"
                      hide-details
                      display-format="fullDate" />
      </v-col>
    </v-row>

    <v-row>
      <v-col cols="12" md="3" v-for="(metric, index) in summaryMetrics" :key="metric.title">
        <v-card class="pa-2 d-flex flex-column align-center justify-center text-center" elevation="2">
          <h3>{{ metric.title }}</h3>
          <div class="relative flex items-center justify-center w-full" style="max-width: 180px; max-height: 180px;">
            <canvas :ref="el => doughnutRefs[index] = el"></canvas>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <v-row>
      <v-col cols="12">
        <v-card elevation="2" class="overflow-hidden w-100">
          <v-data-table-virtual :headers="tableHeaders"
                                fixed-header
                                :items="machineData"
                                density="compact"
                                hover
                                item-selectable
                                @click:row="handleRowClick"
                                style="height: 60vh;">
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
        </v-card>
      </v-col>
    </v-row>

    <v-row>
      <v-col cols="12" md="4">
        <v-card elevation="2" class="pa-3">
          <h4 class="mb-3 text-center">Total Reject</h4>
          <div style="height: 500px;" class="d-flex align-center justify-center">
            <canvas v-if="rejectData.length > 0" ref="rejectChart"></canvas>
            <div v-else class="d-flex flex-column align-center justify-center w-100 h-100">
              <v-icon size="48" color="grey-lighten-1">mdi-chart-bar</v-icon>
              <span class="text-grey text-body-2 mt-2">No reject data available</span>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card elevation="2" class="pa-3">
          <h4 class="mb-3 text-center">Total Output</h4>
          <div style="height: 500px;" class="d-flex align-center justify-center">
            <canvas v-if="outputData.length > 0" ref="outputChart"></canvas>
            <div v-else class="d-flex flex-column align-center justify-center w-100 h-100">
              <v-icon size="48" color="grey-lighten-1">mdi-chart-bar</v-icon>
              <span class="text-grey text-body-2 mt-2">No output data available</span>
            </div>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card elevation="2" class="pa-3">
          <h4 class="mb-3 text-center">Total Downtime</h4>
          <div style="height: 500px;" class="d-flex align-center justify-center">
            <canvas v-if="downtimeData.length > 0" ref="downtimeChart"></canvas>
            <div v-else class="d-flex flex-column align-center justify-center w-100 h-100">
              <v-icon size="48" color="grey-lighten-1">mdi-chart-timeline-variant</v-icon>
              <span class="text-grey text-body-2 mt-2">No downtime data available</span>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>

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
  import { ref, onMounted, watch, nextTick } from "vue";
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

  const centerTextPlugin = {
    id: "centerText",
    beforeDraw(chart) {
      const { width, height, ctx } = chart;
      const dataset = chart.config.data.datasets[0];
      const value = dataset.data[0];
      ctx.save();
      const fontSize = (height / 120).toFixed(2);
      ctx.font = `${fontSize}em sans-serif`;
      ctx.textBaseline = "middle";
      ctx.fillStyle = "#000";
      const text = `${value.toFixed(1)}%`;
      const textX = Math.round(width / 2);
      const textY = Math.round(height / 2);
      ctx.textAlign = "center";
      ctx.fillText(text, textX, textY);
      ctx.restore();
    },
  };

  const tableHeaders = [
    { title: "Machine", key: "machine_name", width: "60%" },
    { title: "OEE (%)", key: "oee", width: "10%", align: "center" },
    { title: "Performance (%)", key: "performance", width: "10%", align: "center" },
    { title: "Availability (%)", key: "availability", width: "10%", align: "center" },
    { title: "Quality (%)", key: "quality", width: "10%", align: "center" },
  ];

  const machineData = ref([]);
  const summaryMetrics = ref([
    { title: "Overall OEE", value: 0 },
    { title: "Performance", value: 0 },
    { title: "Availability", value: 0 },
    { title: "Quality", value: 0 },
  ]);

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
        }))
        : [];

      if (machineData.value.length > 0) {
        const avg = (key) => {
          const validMachines = data.filter(m =>
            m.id_machine >= 1 &&
            m.id_machine <= 16 &&
            Number(m.oee) > 0 
          );

          if (validMachines.length === 0) return 0;

          const total = validMachines.reduce(
            (sum, m) => sum + (Number(m[key]) || 0),
            0
          );

          return total / validMachines.length;
        };

        summaryMetrics.value = [
          { title: "Overall OEE", value: Number(avg("oee").toFixed(2)), type: 'oee' },
          { title: "Performance", value: Number(avg("performance").toFixed(2)), type: 'performance' },
          { title: "Availability", value: Number(avg("availability").toFixed(2)), type: 'availability' },
          { title: "Quality", value: Number(avg("quality").toFixed(2)), type: 'quality' },
        ];
      } else {
        summaryMetrics.value = summaryMetrics.value.map((m) => ({ ...m, value: 0 }));
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

  const createDoughnutChart = (ctx, value) => {
    const color = value >= 90 ? "#4caf50" : value >= 80 ? "#ff9800" : "#f44336";

    return new Chart(ctx, {
      type: "doughnut",
      data: {
        datasets: [
          {
            data: [value, 100 - value],
            backgroundColor: [color, "#e0e0e0"],
            borderWidth: 0,
            cutout: "60%",
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
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
          doughnutCharts.push(createDoughnutChart(ctx, metric.value || 0));
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
          x: { title: { display: true, text: xAxisLabel, font: { size: 12 } }, ticks: { autoSkip: false, maxRotation: 45, minRotation: 45, font: { size: 10 } } },
          y: { beginAtZero: true, position: 'left', title: { display: true, text: yAxisLabel, font: { size: 12 } } },
          y1: { beginAtZero: true, max: 100, position: 'right', title: { display: true, text: 'Cumulative %', font: { size: 12 } }, grid: { drawOnChartArea: false }, ticks: { callback: v => v + '%' } }
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
              { label: 'Downtime (Hours)', data: downtimeData.value.map(d => d.hours), backgroundColor: '#2196F3', yAxisID: 'y', order: 2 },
              { label: 'Cumulative %', data: cumPct, type: 'line', borderColor: '#FF9800', backgroundColor: '#FF9800', borderWidth: 2, pointRadius: 4, yAxisID: 'y1', order: 1 }
            ]
          },
          options: getCommonOptions('Time (hrs)', 'Downtime')
        });
      }
    } catch (err) {
      console.error("Error updating charts:", err);
    }
  };

  const handleRowClick = (event, { item }) => {
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

  const destroyDetailCharts = () => {
    Object.values(detailChartInstances).forEach(chart => {
      if (chart) {
        chart.destroy();
      }
    });
    detailChartInstances = {
      output: null,
      downtime: null,
      reject: null,
      hourly: null
    };
  };

  const getMetricColor = (metricType, value) => {
    const v = Number(value);
    switch (metricType.toLowerCase()) {
      case 'oee': return v >= 65 ? '#4caf50' : v >= 60 ? '#ff9800' : '#f44336';
      case 'performance': return v >= 90 ? '#4caf50' : v >= 80 ? '#ff9800' : '#f44336';
      case 'availability': return v >= 65 ? '#4caf50' : v >= 60 ? '#ff9800' : '#f44336';
      case 'quality': return v >= 98 ? '#4caf50' : v >= 95 ? '#ff9800' : '#f44336';
      default: return '#e0e0e0';
    }
  };
</script>
