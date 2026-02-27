<template>
  <v-card>
    <v-overlay :model-value="isFetching"
               contained
               class="align-center justify-center"
               style="z-index: 10;">
      <div class="d-flex flex-column align-center ga-3">
        <v-progress-circular indeterminate color="primary" size="56" width="5" />
        <span class="text-body-2 text-white font-weight-medium">Loading machine data�</span>
      </div>
    </v-overlay>

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

      <v-row class="mb-4">
        <v-col v-for="n in 4" :key="n" cols="12" md="3">
          <v-skeleton-loader v-if="isFetching" type="card" height="96" />

          <transition v-else name="fade">
            <v-card elevation="2" class="pa-3 text-center"
                    :color="['success','error','success','warning'][n-1]"
                    variant="tonal">
              <div class="text-h4 font-weight-bold">
                {{
 [
                  kpiData.totalOutput.toLocaleString(),
                  kpiData.totalRejects.toLocaleString(),
                  kpiData.totalRuntime,
                  kpiData.totalDowntime
                ][n-1]
                }}
              </div>
              <div class="text-subtitle-2 mt-1">
                {{ ['Total Output (pcs)', 'Total Rejects (kg)', 'Total Run Time (hrs)', 'Total Down Time (hrs)'][n-1]}}
              </div>
            </v-card>
          </transition>
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
        <v-col cols="12" md="12">
          <v-skeleton-loader v-if="isFetching" type="card" height="96" />

          <transition v-else name="fade">
            <v-card elevation="2" class="pa-3 text-center"
                    color="info"
                    variant="tonal">
              <div class="text-h4 font-weight-bold">
                {{ selectedMachine?.oee + '%' }}
              </div>
              <div class="text-subtitle-2 mt-1">
                {{ 'OEE Score' }}
              </div>
          </v-card>
          </transition>
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
          <v-skeleton-loader v-if="isFetching" type="table-tbody" />
          <transition v-else name="fade">
          <v-card elevation="2">
            <v-card-title class="bg-grey-lighten-3">
              <v-icon left>mdi-package-variant</v-icon>
              Product Output Summary
            </v-card-title>
            <v-card-text>
                <v-data-table-virtual :headers="productHeaders"
                            :items="productData"
                            density="compact"
                            :items-per-page="5"
                                      class="elevation-0"
                                      style="max-height: 500px;">
                  <template v-slot:item.shift_output="{ item }">
                    <span class="font-weight-bold text-success">{{ item.shift_output.toLocaleString() }}</span>
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
                </v-data-table-virtual>
            </v-card-text>
          </v-card>
          </transition>
        </v-col>
      </v-row>

      <!-- Charts Row 1: Output & Downtime -->
      <v-row class="mb-4">
        <v-col cols="12" md="6">
          <v-skeleton-loader v-if="isFetching" type="image" height="340" />
          <transition v-else name="fade">
          <v-card elevation="2" class="pa-3">
            <h4 class="mb-3 text-center">Daily Production Output</h4>
            <div style="height: 300px;">
              <canvas ref="outputChart"></canvas>
            </div>
          </v-card>
          </transition>
        </v-col>
        <v-col cols="12" md="6">
          <v-skeleton-loader v-if="isFetching" type="image" height="340" />
          <transition v-else name="fade">
          <v-card elevation="2" class="pa-3">
            <h4 class="mb-3 text-center">Downtime Analysis by Category</h4>
            <div style="height: 300px;">
              <canvas ref="downtimeChart"></canvas>
            </div>
          </v-card>
          </transition>
        </v-col>
      </v-row>

      <!-- Downtime Details Table -->
      <v-row class="mb-4">
        <v-col cols="12">
          <v-skeleton-loader v-if="isFetching" type="table-tbody" />
          <transition v-else name="fade">
          <v-card elevation="2">
            <v-card-title class="bg-grey-lighten-3">
              <v-icon left>mdi-clock-alert-outline</v-icon>
              Downtime Events Details
            </v-card-title>
            <v-card-text>
                <v-data-table-virtual :headers="downtimeHeaders"
                                      :items="downtimeEvents"
                            density="compact"
                            :items-per-page="5"
                                      class="elevation-0"
                                      style="max-height: 500px;">
                <template v-slot:item.duration="{ item }">
                  <span class="font-weight-bold">{{ item.duration }} hrs</span>
                </template>
                <template v-slot:item.category="{ item }">
                  <v-chip :color="getCategoryColor(item.category)" size="small">
                    {{ item.category }}
                  </v-chip>
                </template>
                </v-data-table-virtual>
            </v-card-text>
          </v-card>
          </transition>
        </v-col>
      </v-row>

      <!-- Charts Row 2: Reject Analysis -->
      <v-row class="mb-4">
        <v-col cols="12" md="6">
          <v-skeleton-loader v-if="isFetching" type="image" height="340" />
          <transition v-else name="fade">
          <v-card elevation="2" class="pa-3">
            <h4 class="mb-3 text-center">Reject Types Distribution</h4>
            <div style="height: 300px;">
              <canvas ref="rejectChart"></canvas>
            </div>
          </v-card>
          </transition>
        </v-col>
        <v-col cols="12" md="6">
          <v-skeleton-loader v-if="isFetching" type="image" height="340" />
          <transition v-else name="fade">
          <v-card elevation="2" class="pa-3">
            <h4 class="mb-3 text-center">Daily Reject Trend</h4>
            <div style="height: 300px;">
              <canvas ref="rejectTrendChart"></canvas>
            </div>
          </v-card>
          </transition>
        </v-col>
      </v-row>

      <!-- Reject Details Table -->
      <v-row class="mb-4">
        <v-col cols="12">
          <v-skeleton-loader v-if="isFetching" type="table-tbody" />
          <transition v-else name="fade">
          <v-card elevation="2">
            <v-card-title class="bg-grey-lighten-3">
              <v-icon left>mdi-alert-circle-outline</v-icon>
              Reject Analysis by Type
            </v-card-title>
            <v-card-text>
                <v-data-table-virtual :headers="rejectSummaryHeaders"
                                      :items="rejectSummaryData"
                            density="compact"
                                      :items-per-page="10"
                                      class="elevation-0"
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
          <v-skeleton-loader v-if="isFetching" type="image" height="340" />
          <transition v-else name="fade">
          <v-card elevation="2" class="pa-3">
              <h4 class="mb-3 text-center">Actual vs SAP Cycle Time</h4>
            <div style="height: 300px;">
              <canvas ref="cycleTimeChart"></canvas>
            </div>
          </v-card>
          </transition>
        </v-col>
        <v-col cols="12" md="6">
          <v-skeleton-loader v-if="isFetching" type="image" height="340" />
          <transition v-else name="fade">
          <v-card elevation="2" class="pa-3">
            <h4 class="mb-3 text-center">Shift Performance Comparison</h4>
            <div style="height: 300px;">
              <canvas ref="shiftChart"></canvas>
            </div>
          </v-card>
          </transition>
        </v-col>
      </v-row>

      <!-- Utilities Usage -->
      <v-row class="mb-4">
        <v-col cols="12">
          <v-skeleton-loader v-if="isFetching" type="table-tbody" />
          <transition v-else name="fade">
          <v-card elevation="2">
            <v-card-title class="bg-grey-lighten-3">
              <v-icon left>mdi-flash</v-icon>
              Utilities Usage Timeline
            </v-card-title>
            <v-card-text>
                <v-data-table-virtual :headers="utilityHeaders"
                            :items="utilityData"
                            density="compact"
                            :items-per-page="5"
                                      class="elevation-0"
                                      style="max-height: 500px;">
                <template v-slot:item.status="{ item }">
                  <v-chip :color="item.status === 'Running' ? 'success' : 'error'" size="small">
                    {{ item.status }}
                  </v-chip>
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
  import { ref, computed, watch, nextTick } from 'vue'
  import {
    Chart, ArcElement, DoughnutController, BarController, BarElement,
    LineController, LineElement, PointElement, CategoryScale, LinearScale,
    Tooltip, Legend
  } from 'chart.js'

  Chart.register(
    ArcElement, DoughnutController, BarController, BarElement,
    LineController, LineElement, PointElement, CategoryScale, LinearScale,
    Tooltip, Legend
  )

  // ── Props / Emits ─────────────────────────────────────────────────────────────
  const props = defineProps({
    selectedMachine: { type: Object, default: null },
    detailDialog: { type: Boolean, default: false },
    startDate: { type: String, default: '' },
    endDate: { type: String, default: '' }
  })
  const emit = defineEmits(['update:detailDialog'])
  const closeDialog = () => emit('update:detailDialog', false)

  // ── Loading state ─────────────────────────────────────────────────────────────
  const isFetching = ref(false)

  // ── Chart canvas refs ─────────────────────────────────────────────────────────
  const outputChart = ref(null)
  const downtimeChart = ref(null)
  const rejectChart = ref(null)
  const rejectTrendChart = ref(null)
  const cycleTimeChart = ref(null)
  const shiftChart = ref(null)

  let charts = {}

  // ── Data refs ─────────────────────────────────────────────────────────────────
  const productData = ref([])
  const dailyOutputData = ref([])
  const runtimeData = ref([])
  const downtimeCatData = ref([])
  const downtimeEvents = ref([])
  const rejectRawData = ref([])
  const cycleTimeData = ref([])
  const shiftPerfData = ref([])
  const utilityData = ref([])

  // ── KPI computed from fetched data ────────────────────────────────────────────
  const kpiData = computed(() => {
    const totalOutput = productData.value.reduce((s, r) => s + (r.shift_output || 0), 0)
    const totalRejects = rejectRawData.value.reduce((s, r) => s + (r.total_weight || 0), 0).toFixed(1)
    const totalRuntime = runtimeData.value.reduce((s, r) => s + (r.hours || 0), 0).toFixed(1)
    const totalDowntime = downtimeCatData.value.reduce((s, r) => s + (r.hours || 0), 0).toFixed(1)
    return { totalOutput, totalRejects, totalRuntime, totalDowntime }
  })

  // ── Reject summary aggregated by type ────────────────────────────────────────
  const REJECT_TYPES = [
    { key: 'reject_panelling', label: 'Panelling' },
    { key: 'reject_lumpy', label: 'Lumpy' },
    { key: 'reject_black_dot', label: 'Black Dot' },
    { key: 'reject_burst', label: 'Burst' },
    { key: 'reject_startup', label: 'Start Up' },
    { key: 'reject_preform', label: 'Preform' },
    { key: 'reject_purging', label: 'Purging' },
    { key: 'reject_others', label: 'Others' }
  ]

  const rejectSummaryData = computed(() => {
    const totals = {}
    REJECT_TYPES.forEach(t => { totals[t.key] = 0 })
    rejectRawData.value.forEach(r => {
      REJECT_TYPES.forEach(t => { totals[t.key] += r[t.key] || 0 })
    })
    const grandTotal = Object.values(totals).reduce((s, v) => s + v, 0)
    return REJECT_TYPES
      .map(t => ({
        reject_type: t.label,
        total_weight: totals[t.key].toFixed(2),
        percentage: grandTotal > 0 ? ((totals[t.key] / grandTotal) * 100).toFixed(1) : '0.0'
      }))
      .filter(r => Number(r.total_weight) > 0)
      .sort((a, b) => b.total_weight - a.total_weight)
  })

  // ── Table headers ─────────────────────────────────────────────────────────────
  const productHeaders = [
    { title: 'Product Type', key: 'type' },
    { title: 'Total Shots', key: 'shot' },
    { title: 'Qty/Shot', key: 'qty_perct' },
    { title: 'Total Output (pcs)', key: 'shift_output' },
    { title: 'Part Weight (g)', key: 'part_weight' },
    { title: 'Avg Cycle Time (s)', key: 'act_ct' },
    { title: 'SAP Cycle Time (s)', key: 'sap_ct' },
    { title: 'Efficiency (%)', key: 'efficiency' }
  ]

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
    { title: 'Start Time', key: 'start' },
    { title: 'Finish Time', key: 'finish' },
    { title: 'Category', key: 'category' },
    { title: 'Duration', key: 'duration' },
    { title: 'Shift', key: 'shift' },
    { title: 'Remark', key: 'remark' }
  ]

  const rejectSummaryHeaders = [
    { title: 'Reject Type', key: 'reject_type' },
    { title: 'Total (kg)', key: 'total_weight' },
    { title: '% of Total', key: 'percentage' }
  ]

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
    { title: 'Utility Name', key: 'utility_name' },
    { title: 'Start Time', key: 'start' },
    { title: 'Finish Time', key: 'finish' },
    { title: 'Status', key: 'status' },
    { title: 'Duration', key: 'duration' },
    { title: 'Shift', key: 'shift' }
  ]

  // ── Fetch helpers ─────────────────────────────────────────────────────────────
  const baseParams = () => new URLSearchParams({
    id_machine: props.selectedMachine.id_machine,
    start_date: props.startDate,
    end_date: props.endDate
  })

  async function fetchAll() {
    if (!props.selectedMachine) return

    isFetching.value = true
    destroyAll()

    try {
      const p = baseParams()

      const [prod, daily, run, dtCat, dtEvt, rej, ct, shift, util] = await Promise.all([
        fetch(`/api/MachineLog/MachineDetail/ProductOutput?${p}`).then(r => r.json()),
        fetch(`/api/MachineLog/MachineDetail/DailyOutput?${p}`).then(r => r.json()),
        fetch(`/api/MachineLog/MachineDetail/Runningtime?${p}`).then(r => r.json()),
        fetch(`/api/MachineLog/MachineDetail/DowntimeCategory?${p}`).then(r => r.json()),
        fetch(`/api/MachineLog/MachineDetail/DowntimeEvents?${p}`).then(r => r.json()),
        fetch(`/api/MachineLog/MachineDetail/Reject?${p}`).then(r => r.json()),
        fetch(`/api/MachineLog/MachineDetail/CycleTime?${p}`).then(r => r.json()),
        fetch(`/api/MachineLog/MachineDetail/ShiftPerformance?${p}`).then(r => r.json()),
        fetch(`/api/MachineLog/MachineDetail/Utilities?${p}`).then(r => r.json())
      ])
      productData.value = Array.isArray(prod) ? prod : []
      dailyOutputData.value = Array.isArray(daily) ? daily : []
      runtimeData.value = Array.isArray(run) ? run: []
      downtimeCatData.value = Array.isArray(dtCat) ? dtCat : []
      downtimeEvents.value = Array.isArray(dtEvt) ? dtEvt : []
      rejectRawData.value = Array.isArray(rej) ? rej : []
      cycleTimeData.value = Array.isArray(ct) ? ct : []
      shiftPerfData.value = Array.isArray(shift) ? shift : []
      utilityData.value = Array.isArray(util) ? util : []
    } catch (err) {
      console.error('fetchAll error:', err)
    } finally {
      isFetching.value = false
      await nextTick()
      buildCharts()
    }
  }

  const CHART_COLORS = [
    '#F44336', '#E91E63', '#9C27B0', '#673AB7', '#3F51B5',
    '#2196F3', '#00BCD4', '#4CAF50', '#FF9800', '#FF5722'
  ]

  function destroyAll() {
    Object.values(charts).forEach(c => c?.destroy())
    charts = {}
    }

  function buildCharts() {
    buildOutputChart()
    buildDowntimeChart()
    buildRejectChart()
    buildRejectTrendChart()
    buildCycleTimeChart()
    buildShiftChart()
  }

  function buildOutputChart() {
    const ctx = outputChart.value?.getContext('2d')
    if (!ctx || !dailyOutputData.value.length) return
    charts.output = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: dailyOutputData.value.map(d => d.production_date),
        datasets: [{
          label: 'Daily Output (pcs)',
          data: dailyOutputData.value.map(d => d.shift_output),
          backgroundColor: 'rgba(76,175,80,0.6)',
          borderColor: 'rgba(76,175,80,1)',
          borderWidth: 1
        }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        animation: { duration: 600, easing: 'easeOutQuart' },
        plugins: { legend: { display: true, position: 'top' } },
        scales: { y: { beginAtZero: true, title: { display: true, text: 'Output (pcs)' } } }
      }
    })
    }

  function buildDowntimeChart() {
    const ctx = downtimeChart.value?.getContext('2d')
    if (!ctx || !downtimeCatData.value.length) return
    charts.downtime = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: downtimeCatData.value.map(d => d.category),
        datasets: [{
          data: downtimeCatData.value.map(d => d.hours),
          backgroundColor: CHART_COLORS.slice(0, downtimeCatData.value.length),
          borderWidth: 2
        }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        animation: { duration: 600, easing: 'easeOutQuart' },
        plugins: {
          legend: { position: 'bottom' },
          tooltip: { callbacks: { label: ctx => `${ctx.label}: ${ctx.parsed} hrs` } }
        }
      }
    })
    }

  function buildRejectChart() {
    const ctx = rejectChart.value?.getContext('2d')
    if (!ctx || !rejectSummaryData.value.length) return
    charts.reject = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: rejectSummaryData.value.map(r => r.reject_type),
        datasets: [{
          data: rejectSummaryData.value.map(r => Number(r.total_weight)),
          backgroundColor: CHART_COLORS.slice(0, rejectSummaryData.value.length),
          borderWidth: 2
        }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        animation: { duration: 600, easing: 'easeOutQuart' },
        plugins: {
          legend: { position: 'bottom' },
          tooltip: { callbacks: { label: ctx => `${ctx.label}: ${ctx.parsed} kg` } }
        }
      }
    })
    }

  function buildRejectTrendChart() {
    const ctx = rejectTrendChart.value?.getContext('2d')
    if (!ctx || !rejectRawData.value.length) return
    const byDate = {}
    rejectRawData.value.forEach(r => {
      byDate[r.production_date] = (byDate[r.production_date] || 0) + Number(r.total_weight)
    })
    const dates = Object.keys(byDate).sort()
    const values = dates.map(d => Number(byDate[d].toFixed(2)))
    charts.rejectTrend = new Chart(ctx, {
      type: 'line',
      data: {
        labels: dates,
        datasets: [{
          label: 'Daily Reject (kg)',
          data: values,
          borderColor: 'rgba(244,67,54,1)',
          backgroundColor: 'rgba(244,67,54,0.1)',
          borderWidth: 2, fill: true, tension: 0.4
        }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        animation: { duration: 600, easing: 'easeOutQuart' },
        plugins: { legend: { display: true, position: 'top' } },
        scales: { y: { beginAtZero: true, title: { display: true, text: 'Reject (kg)' } } }
      }
    })
    }

  function buildCycleTimeChart() {
    const ctx = cycleTimeChart.value?.getContext('2d')
    if (!ctx || !cycleTimeData.value.length) return
    charts.cycleTime = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: cycleTimeData.value.map(d => d.type),
        datasets: [
          {
            label: 'SAP CT (s)',
            data: cycleTimeData.value.map(d => d.sap_ct),
            backgroundColor: 'rgba(76,175,80,0.6)',
            borderColor: 'rgba(76,175,80,1)',
            borderWidth: 1
          },
          {
            label: 'Actual CT (s)',
            data: cycleTimeData.value.map(d => d.act_ct),
            backgroundColor: 'rgba(255,152,0,0.6)',
            borderColor: 'rgba(255,152,0,1)',
            borderWidth: 1
          }
        ]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        animation: { duration: 600, easing: 'easeOutQuart' },
        plugins: { legend: { display: true, position: 'top' } },
        scales: {
          x: { ticks: { maxRotation: 45, minRotation: 45, font: { size: 10 } } },
          y: { beginAtZero: true, title: { display: true, text: 'Cycle Time (s)' } }
        }
      }
    })
    }

  function buildShiftChart() {
    const ctx = shiftChart.value?.getContext('2d')
    if (!ctx || !shiftPerfData.value.length) return
    charts.shift = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: shiftPerfData.value.map(d => d.shift),
        datasets: [
          {
            label: 'Output (pcs)',
            data: shiftPerfData.value.map(d => d.shift_output),
            backgroundColor: 'rgba(33,150,243,0.6)',
            borderColor: 'rgba(33,150,243,1)',
            borderWidth: 1,
            yAxisID: 'y'
          },
          {
            label: 'Reject (pcs)',
            data: shiftPerfData.value.map(d => d.total_reject_pcs),
            backgroundColor: 'rgba(244,67,54,0.6)',
            borderColor: 'rgba(244,67,54,1)',
            borderWidth: 1,
            yAxisID: 'y1'
          }
        ]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        animation: { duration: 600, easing: 'easeOutQuart' },
        plugins: { legend: { display: true, position: 'top' } },
        scales: {
          y: { type: 'linear', position: 'left', title: { display: true, text: 'Output (pcs)' } },
          y1: { type: 'linear', position: 'right', title: { display: true, text: 'Reject (pcs)' }, grid: { drawOnChartArea: false } }
          }
        }
    })
      }

  // ── Colour helpers ────────────────────────────────────────────────────────────
  const getEfficiencyColor = v => v >= 90 ? 'success' : v >= 75 ? 'warning' : 'error'

  const CATEGORY_COLORS = {
    'MOULD CHANGE': 'primary', 'MACHINE BREAKDOWN': 'error',
    'SCHEDULED MAINTENANCE': 'info', 'QUALITY ISSUE': 'warning',
    'OTHERS MAIN': 'orange', 'OTHERS TECH': 'deep-orange',
    'NO OPERATOR': 'grey', 'MATERIAL DRYING': 'teal'
    }
  const getCategoryColor = cat => CATEGORY_COLORS[cat] || 'grey'

  // ── Watcher ───────────────────────────────────────────────────────────────────
  watch(
    [() => props.detailDialog, () => props.selectedMachine],
    async ([isOpen, machine]) => {
      if (isOpen && machine) {
        await nextTick()
        await fetchAll()
      }
      if (!isOpen) {
        destroyAll()
        isFetching.value = false
    }
    },
    { immediate: true }
  )
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
