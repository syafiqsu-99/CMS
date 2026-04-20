<template>
  <v-card>
    <!-- Loading overlay -->
    <v-overlay :model-value="isFetching"
               contained
               class="align-center justify-center"
               style="z-index: 10;">
      <div class="d-flex flex-column align-center ga-3">
        <v-progress-circular indeterminate color="primary" size="56" width="5" />
        <span class="text-body-2 text-white font-weight-medium">Loading machine data…</span>
      </div>
    </v-overlay>

    <!-- Title bar -->
    <v-card-title class="bg-primary text-white d-flex justify-space-between align-center">
      <div>
        <span class="text-h5">Machine Analytics — {{ selectedMachine?.machine_name }}</span>
        <div class="text-caption mt-1">{{ startDate }} to {{ endDate }}</div>
      </div>
      <v-btn icon variant="text" @click="closeDialog">
        <v-icon color="white">mdi-close</v-icon>
      </v-btn>
    </v-card-title>

    <v-card-text class="pa-4">

      <!-- KPI summary row -->
      <v-row class="mb-4">
        <v-col v-for="n in 4" :key="n" cols="12" md="3">
          <v-skeleton-loader v-if="isFetching" type="card" height="96" />
          <transition v-else name="fade">
            <v-card elevation="2"
                    class="pa-3 text-center"
                    :color="['success','error','warning','info'][n-1]"
                    variant="tonal">
              <div class="text-h4 font-weight-bold">
                {{
                  [
                    kpiData.totalOutput.toLocaleString(),
                    kpiData.totalRejects.toLocaleString(),
                    kpiData.totalDowntime,
                    selectedMachine?.oee + '%'
                  ][n-1]
                }}
              </div>
              <div class="text-subtitle-2 mt-1">
                {{ ['Total Output (pcs)', 'Total Rejects (kg)', 'Total Downtime (hrs)', 'OEE Score'][n-1] }}
              </div>
            </v-card>
          </transition>
        </v-col>
      </v-row>

      <!-- Product Output Table -->
      <v-row class="mb-4">
        <v-col cols="12">
          <v-skeleton-loader v-if="isFetching" type="table-tbody" />
          <transition v-else name="fade">
            <v-card elevation="2">
              <v-card-title class="bg-grey-lighten-3">Product Output Summary</v-card-title>
              <v-card-text>
                <v-data-table-virtual :headers="productHeaders"
                                      :items="productData"
                                      density="compact"
                                      style="max-height: 500px;">
                  <template v-slot:item.shift_output="{ item }">
                    <span class="font-weight-bold text-success">{{ item.shift_output.toLocaleString() }}</span>
                  </template>
                  <template v-slot:item.efficiency="{ item }">
                    <v-chip :color="getEfficiencyColor(item.efficiency)" size="small">{{ item.efficiency }}%</v-chip>
                  </template>
                </v-data-table-virtual>
              </v-card-text>
            </v-card>
          </transition>
        </v-col>
      </v-row>

      <!-- Charts row 1 -->
      <v-row class="mb-4">
        <v-col cols="12" md="6">
          <v-skeleton-loader v-if="isFetching" type="image" height="340" />
          <transition v-else name="fade">
            <v-card elevation="2" class="pa-3">
              <h4 class="mb-3 text-center">Daily Production Output</h4>
              <div style="height: 300px;"><canvas ref="outputChart" /></div>
            </v-card>
          </transition>
        </v-col>
        <v-col cols="12" md="6">
          <v-skeleton-loader v-if="isFetching" type="image" height="340" />
          <transition v-else name="fade">
            <v-card elevation="2" class="pa-3">
              <h4 class="mb-3 text-center">Downtime Analysis by Category</h4>
              <div style="height: 300px;"><canvas ref="downtimeChart" /></div>
            </v-card>
          </transition>
        </v-col>
      </v-row>

      <!-- Downtime Events Table -->
      <v-row class="mb-4">
        <v-col cols="12">
          <v-skeleton-loader v-if="isFetching" type="table-tbody" />
          <transition v-else name="fade">
            <v-card elevation="2">
              <v-card-title class="bg-grey-lighten-3">Downtime Events Details</v-card-title>
              <v-card-text>
                <v-data-table-virtual :headers="downtimeHeaders"
                                      :items="downtimeEvents"
                                      density="compact"
                                      style="max-height: 500px;">
                  <template v-slot:item.duration="{ item }">
                    <span class="font-weight-bold">{{ item.duration }} hrs</span>
                  </template>
                  <template v-slot:item.category="{ item }">
                    <v-chip :color="getCategoryColor(item.category)" size="small">{{ item.category }}</v-chip>
                  </template>
                </v-data-table-virtual>
              </v-card-text>
            </v-card>
          </transition>
        </v-col>
      </v-row>

      <!-- Charts row 2 -->
      <v-row class="mb-4">
        <v-col cols="12" md="6">
          <v-skeleton-loader v-if="isFetching" type="image" height="340" />
          <transition v-else name="fade">
            <v-card elevation="2" class="pa-3">
              <h4 class="mb-3 text-center">Reject Types Distribution</h4>
              <div style="height: 300px;"><canvas ref="rejectChart" /></div>
            </v-card>
          </transition>
        </v-col>
        <v-col cols="12" md="6">
          <v-skeleton-loader v-if="isFetching" type="image" height="340" />
          <transition v-else name="fade">
            <v-card elevation="2" class="pa-3">
              <h4 class="mb-3 text-center">Daily Reject Trend</h4>
              <div style="height: 300px;"><canvas ref="rejectTrendChart" /></div>
            </v-card>
          </transition>
        </v-col>
      </v-row>

      <!-- Reject Summary Table -->
      <v-row class="mb-4">
        <v-col cols="12">
          <v-skeleton-loader v-if="isFetching" type="table-tbody" />
          <transition v-else name="fade">
            <v-card elevation="2">
              <v-card-title class="bg-grey-lighten-3">Reject Analysis by Type</v-card-title>
              <v-card-text>
                <v-data-table-virtual :headers="rejectSummaryHeaders"
                                      :items="rejectSummaryData"
                                      density="compact"
                                      style="max-height: 500px;">
                  <template v-slot:item.total_weight="{ item }">
                    <span class="font-weight-bold text-error">{{ item.total_weight }} kg</span>
                  </template>
                </v-data-table-virtual>
              </v-card-text>
            </v-card>
          </transition>
        </v-col>
      </v-row>

      <!-- Charts row 3 -->
      <v-row class="mb-4">
        <v-col cols="12" md="6">
          <v-skeleton-loader v-if="isFetching" type="image" height="340" />
          <transition v-else name="fade">
            <v-card elevation="2" class="pa-3">
              <h4 class="mb-3 text-center">Actual vs Standard Cycle Time</h4>
              <div style="height: 300px;"><canvas ref="cycleTimeChart" /></div>
            </v-card>
          </transition>
        </v-col>
        <v-col cols="12" md="6">
          <v-skeleton-loader v-if="isFetching" type="image" height="340" />
          <transition v-else name="fade">
            <v-card elevation="2" class="pa-3">
              <h4 class="mb-3 text-center">Shift Performance Comparison</h4>
              <div style="height: 300px;"><canvas ref="shiftChart" /></div>
            </v-card>
          </transition>
        </v-col>
      </v-row>

      <!-- Utilities Table -->
      <v-row class="mb-4">
        <v-col cols="12">
          <v-skeleton-loader v-if="isFetching" type="table-tbody" />
          <transition v-else name="fade">
            <v-card elevation="2">
              <v-card-title class="bg-grey-lighten-3">Utilities Usage Timeline</v-card-title>
              <v-card-text>
                <v-data-table-virtual :headers="utilityHeaders"
                                      :items="utilityData"
                                      density="compact"
                                      style="max-height: 500px;">
                  <template v-slot:item.status="{ item }">
                    <v-chip :color="item.status === 'Running' ? 'success' : 'error'" size="small">{{ item.status }}</v-chip>
                  </template>
                  <template v-slot:item.duration="{ item }">
                    {{ item.duration }} hrs
                  </template>
                </v-data-table-virtual>
              </v-card-text>
            </v-card>
          </transition>
        </v-col>
      </v-row>

    </v-card-text>

    <v-card-actions class="pa-4 bg-grey-lighten-4">
      <v-spacer />
      <v-btn variant="text" @click="closeDialog">Close</v-btn>
    </v-card-actions>
  </v-card>
</template>

<script setup>
  import { ref, computed, watch, nextTick } from 'vue';
  import {
    Chart, ArcElement, DoughnutController, BarController, BarElement,
    LineController, LineElement, PointElement, CategoryScale, LinearScale,
    Tooltip, Legend,
  } from 'chart.js';

  Chart.register(
    ArcElement, DoughnutController, BarController, BarElement,
    LineController, LineElement, PointElement, CategoryScale, LinearScale,
    Tooltip, Legend,
  );

  // ── Props / Emits ─────────────────────────────────────────────────────────────

  const props = defineProps({
    selectedMachine: { type: Object, default: null },
    detailDialog: { type: Boolean, default: false },
    startDate: { type: String, default: '' },
    endDate: { type: String, default: '' },
  });
  const emit = defineEmits(['update:detailDialog']);
  const closeDialog = () => emit('update:detailDialog', false);

  // ── State ─────────────────────────────────────────────────────────────────────

  const isFetching = ref(false);
  const productData = ref([]);
  const dailyOutputData = ref([]);
  const downtimeCatData = ref([]);
  const downtimeEvents = ref([]);
  const rejectRawData = ref([]);
  const cycleTimeData = ref([]);
  const shiftPerfData = ref([]);
  const utilityData = ref([]);

  // Canvas refs
  const outputChart = ref(null);
  const downtimeChart = ref(null);
  const rejectChart = ref(null);
  const rejectTrendChart = ref(null);
  const cycleTimeChart = ref(null);
  const shiftChart = ref(null);

  let charts = {};

  // ── KPI ───────────────────────────────────────────────────────────────────────

  const kpiData = computed(() => ({
    totalOutput: productData.value.reduce((s, r) => s + (r.shift_output || 0), 0),
    totalRejects: rejectRawData.value.reduce((s, r) => s + (r.total_weight || 0), 0).toFixed(1),
    totalDowntime: downtimeCatData.value.reduce((s, r) => s + (r.hours || 0), 0).toFixed(1),
  }));

  // ── Reject summary ────────────────────────────────────────────────────────────

  const REJECT_TYPES = [
    { key: 'reject_panelling', label: 'Panelling' },
    { key: 'reject_lumpy', label: 'Lumpy' },
    { key: 'reject_black_dot', label: 'Black Dot' },
    { key: 'reject_burst', label: 'Burst' },
    { key: 'reject_startup', label: 'Start Up' },
    { key: 'reject_preform', label: 'Preform' },
    { key: 'reject_purging', label: 'Purging' },
    { key: 'reject_others', label: 'Others' },
  ];

  const rejectSummaryData = computed(() => {
    const totals = Object.fromEntries(REJECT_TYPES.map(t => [t.key, 0]));
    rejectRawData.value.forEach(r => REJECT_TYPES.forEach(t => { totals[t.key] += r[t.key] || 0; }));
    const grand = Object.values(totals).reduce((s, v) => s + v, 0);
    return REJECT_TYPES
      .map(t => ({
        reject_type: t.label,
        total_weight: totals[t.key].toFixed(2),
        percentage: grand > 0 ? ((totals[t.key] / grand) * 100).toFixed(1) : '0.0',
      }))
      .filter(r => Number(r.total_weight) > 0)
      .sort((a, b) => b.total_weight - a.total_weight);
  });

  // ── Table headers ─────────────────────────────────────────────────────────────

  const productHeaders = [
    { title: 'Product Type', key: 'type' },
    { title: 'Total Shots', key: 'shot' },
    { title: 'Qty/Shot', key: 'qty_perct' },
    { title: 'Total Output (pcs)', key: 'shift_output' },
    { title: 'Part Weight (g)', key: 'part_weight' },
    { title: 'Avg CT (s)', key: 'act_ct' },
    { title: 'SAP CT (s)', key: 'sap_ct' },
    { title: 'Efficiency (%)', key: 'efficiency' },
  ];
  const downtimeHeaders = [
    { title: 'Start', key: 'start' },
    { title: 'Finish', key: 'finish' },
    { title: 'Category', key: 'category' },
    { title: 'Duration', key: 'duration' },
    { title: 'Shift', key: 'shift' },
    { title: 'Remark', key: 'remark' },
  ];
  const rejectSummaryHeaders = [
    { title: 'Reject Type', key: 'reject_type' },
    { title: 'Total (kg)', key: 'total_weight' },
    { title: '% of Total', key: 'percentage' },
  ];
  const utilityHeaders = [
    { title: 'Utility', key: 'utility_name' },
    { title: 'Start', key: 'start' },
    { title: 'Finish', key: 'finish' },
    { title: 'Status', key: 'status' },
    { title: 'Duration', key: 'duration' },
    { title: 'Shift', key: 'shift' },
  ];

  // ── Data fetch ────────────────────────────────────────────────────────────────

  function baseParams() {
    return new URLSearchParams({
      start_date: props.startDate,
      end_date: props.endDate,
    });
  }

  const id = () => props.selectedMachine?.id_machine;

  async function fetchAll() {
    if (!props.selectedMachine) return;
    isFetching.value = true;
    destroyAll();

    try {
      const p = baseParams();
      const base = `/api/oee/machine/${id()}`;

      // All detail endpoints now live under /api/oee/machine/{id}/
      const [prod, daily, dtCat, dtEvt, rej, ct, shift, util] = await Promise.all([
        fetch(`${base}/product-output?${p}`).then(r => r.json()),
        fetch(`${base}/daily-output?${p}`).then(r => r.json()),
        fetch(`${base}/downtime-category?${p}`).then(r => r.json()),
        fetch(`${base}/downtime-events?${p}`).then(r => r.json()),
        fetch(`${base}/reject?${p}`).then(r => r.json()),
        fetch(`${base}/cycle-time?${p}`).then(r => r.json()),
        fetch(`${base}/shift-performance?${p}`).then(r => r.json()),
        fetch(`${base}/utilities?${p}`).then(r => r.json()),
      ]);

      productData.value = Array.isArray(prod) ? prod : [];
      dailyOutputData.value = Array.isArray(daily) ? daily : [];
      downtimeCatData.value = Array.isArray(dtCat) ? dtCat : [];
      downtimeEvents.value = Array.isArray(dtEvt) ? dtEvt : [];
      rejectRawData.value = Array.isArray(rej) ? rej : [];
      cycleTimeData.value = Array.isArray(ct) ? ct : [];
      shiftPerfData.value = Array.isArray(shift) ? shift : [];
      utilityData.value = Array.isArray(util) ? util : [];
    } catch (err) {
      console.error('[MachineOEE] fetchAll:', err);
    } finally {
      isFetching.value = false;
      await nextTick();
      buildCharts();
    }
  }

  // ── Charts ────────────────────────────────────────────────────────────────────

  const COLORS = [
    '#F44336', '#E91E63', '#9C27B0', '#673AB7', '#3F51B5',
    '#2196F3', '#00BCD4', '#4CAF50', '#FF9800', '#FF5722',
  ];

  function destroyAll() { Object.values(charts).forEach(c => c?.destroy()); charts = {}; }

  function buildCharts() {
    buildBarChart('output', outputChart, dailyOutputData.value,
      d => d.production_date, d => d.shift_output,
      'Daily Output (pcs)', 'rgba(76,175,80,0.6)', 'Output (pcs)');

    buildDoughnut('downtime', downtimeChart, downtimeCatData.value,
      d => d.category, d => d.hours,
      ctx => `${ctx.label}: ${ctx.parsed} hrs`);

    buildDoughnut('reject', rejectChart, rejectSummaryData.value,
      d => d.reject_type, d => Number(d.total_weight),
      ctx => `${ctx.label}: ${ctx.parsed} kg`);

    buildLineTrend();
    buildCycleTimeChart();
    buildShiftChart();
  }

  function buildBarChart(key, canvasRef, data, labelFn, valueFn, label, color, yTitle) {
    const ctx = canvasRef.value?.getContext('2d');
    if (!ctx || !data.length) return;
    charts[key] = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: data.map(labelFn),
        datasets: [{ label, data: data.map(valueFn), backgroundColor: color, borderColor: color.replace('0.6', '1'), borderWidth: 1 }],
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        animation: { duration: 500 },
        plugins: { legend: { position: 'top' } },
        scales: { y: { beginAtZero: true, title: { display: true, text: yTitle } } },
      },
    });
  }

  function buildDoughnut(key, canvasRef, data, labelFn, valueFn, tooltipLabel) {
    const ctx = canvasRef.value?.getContext('2d');
    if (!ctx || !data.length) return;
    charts[key] = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: data.map(labelFn),
        datasets: [{ data: data.map(valueFn), backgroundColor: COLORS.slice(0, data.length), borderWidth: 2 }],
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        animation: { duration: 500 },
        plugins: { legend: { position: 'bottom' }, tooltip: { callbacks: { label: tooltipLabel } } },
      },
    });
  }

  function buildLineTrend() {
    const ctx = rejectTrendChart.value?.getContext('2d');
    if (!ctx || !rejectRawData.value.length) return;
    const byDate = {};
    rejectRawData.value.forEach(r => {
      byDate[r.production_date] = (byDate[r.production_date] || 0) + Number(r.total_weight);
    });
    const dates = Object.keys(byDate).sort();
    const values = dates.map(d => Number(byDate[d].toFixed(2)));
    charts.rejectTrend = new Chart(ctx, {
      type: 'line',
      data: { labels: dates, datasets: [{ label: 'Daily Reject (kg)', data: values, borderColor: '#F44336', backgroundColor: 'rgba(244,67,54,0.1)', borderWidth: 2, fill: true, tension: 0.4 }] },
      options: { responsive: true, maintainAspectRatio: false, animation: { duration: 500 }, plugins: { legend: { position: 'top' } }, scales: { y: { beginAtZero: true, title: { display: true, text: 'Reject (kg)' } } } },
    });
  }

  function buildCycleTimeChart() {
    const ctx = cycleTimeChart.value?.getContext('2d');
    if (!ctx || !cycleTimeData.value.length) return;
    charts.cycleTime = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: cycleTimeData.value.map(d => d.type),
        datasets: [
          { label: 'SAP CT (s)', data: cycleTimeData.value.map(d => d.sap_ct), backgroundColor: 'rgba(76,175,80,0.6)', borderWidth: 1 },
          { label: 'Actual CT (s)', data: cycleTimeData.value.map(d => d.act_ct), backgroundColor: 'rgba(255,152,0,0.6)', borderWidth: 1 },
        ],
      },
      options: { responsive: true, maintainAspectRatio: false, animation: { duration: 500 }, plugins: { legend: { position: 'top' } }, scales: { y: { beginAtZero: true, title: { display: true, text: 'Cycle Time (s)' } } } },
    });
  }

  function buildShiftChart() {
    const ctx = shiftChart.value?.getContext('2d');
    if (!ctx || !shiftPerfData.value.length) return;
    charts.shift = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: shiftPerfData.value.map(d => d.shift),
        datasets: [
          { label: 'Output (pcs)', data: shiftPerfData.value.map(d => d.shift_output), backgroundColor: 'rgba(33,150,243,0.6)', yAxisID: 'y' },
          { label: 'Reject (pcs)', data: shiftPerfData.value.map(d => d.total_reject_pcs), backgroundColor: 'rgba(244,67,54,0.6)', yAxisID: 'y1' },
        ],
      },
      options: {
        responsive: true, maintainAspectRatio: false, animation: { duration: 500 },
        plugins: { legend: { position: 'top' } },
        scales: {
          y: { type: 'linear', position: 'left', title: { display: true, text: 'Output (pcs)' } },
          y1: { type: 'linear', position: 'right', title: { display: true, text: 'Reject (pcs)' }, grid: { drawOnChartArea: false } },
        },
      },
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────────

  const getEfficiencyColor = v => v >= 90 ? 'success' : v >= 75 ? 'warning' : 'error';

  const CATEGORY_COLORS = {
    'MOULD CHANGE': 'primary', 'MACHINE BREAKDOWN': 'error',
    'SCHEDULED MAINTENANCE': 'info', 'QUALITY ISSUE': 'warning',
  };
  const getCategoryColor = cat => CATEGORY_COLORS[cat] || 'grey';

  // ── Watcher ───────────────────────────────────────────────────────────────────

  watch(
    [() => props.detailDialog, () => props.selectedMachine],
    async ([isOpen]) => {
      if (isOpen && props.selectedMachine) { await nextTick(); await fetchAll(); }
      if (!isOpen) { destroyAll(); isFetching.value = false; }
    },
    { immediate: true },
  );
</script>

<style scoped>
  canvas {
    max-height: 300px;
  }

  .fade-enter-active {
    transition: opacity 0.4s ease, transform 0.4s ease;
  }

  .fade-enter-from {
    opacity: 0;
    transform: translateY(6px);
  }

  .fade-enter-to {
    opacity: 1;
    transform: translateY(0);
  }
</style>
