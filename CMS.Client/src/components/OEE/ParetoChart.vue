<template>
  <v-card elevation="2" class="pa-3">
    <h4 class="mb-2 text-center text-subtitle-1 font-weight-bold">{{ title }}</h4>
    <div style="height: 420px;" class="d-flex align-center justify-center">
      <canvas v-if="data.length > 0" ref="chartCanvas" style="width: 100%;" />
      <div v-else class="d-flex flex-column align-center justify-center w-100 h-100">
        <v-icon size="48" color="grey-lighten-1">{{ emptyIcon }}</v-icon>
        <span class="text-grey text-body-2 mt-2">No data available</span>
      </div>
    </div>
  </v-card>
</template>

<script setup>
  import { ref, watch, onBeforeUnmount, nextTick } from 'vue';
  import {
    Chart, BarController, BarElement, LineController, LineElement,
    PointElement, CategoryScale, LinearScale, Tooltip, Legend,
  } from 'chart.js';

  Chart.register(BarController, BarElement, LineController, LineElement, PointElement,
    CategoryScale, LinearScale, Tooltip, Legend);

  // ── Props ─────────────────────────────────────────────────────────────────────

  const props = defineProps({
    title: { type: String, required: true },
    data: { type: Array, required: true },
    /** Key for the bar value in each data row */
    valueKey: { type: String, required: true },
    /** Key for the short label (x-axis tick) */
    labelKey: { type: String, required: true },
    /** Key for the full tooltip label */
    fullLabelKey: { type: String, default: null },
    barLabel: { type: String, required: true },
    barColor: { type: String, default: '#2196F3' },
    yAxisLabel: { type: String, default: 'Value' },
    xAxisLabel: { type: String, default: 'Category' },
    emptyIcon: { type: String, default: 'mdi-chart-bar' },
  });

  // ── Chart state ───────────────────────────────────────────────────────────────

  const chartCanvas = ref(null);
  let chart = null;

  function buildChart() {
    chart?.destroy();
    if (!chartCanvas.value || !props.data.length) return;

    let cumulative = 0;
    const total = props.data.reduce((s, d) => s + d[props.valueKey], 0);
    const cumPct = props.data.map(d => {
      cumulative += d[props.valueKey];
      return total > 0 ? (cumulative / total) * 100 : 0;
    });

    chart = new Chart(chartCanvas.value.getContext('2d'), {
      type: 'bar',
      data: {
        labels: props.data.map(d => d[props.labelKey]),
        // Store full labels for tooltip
        fullLabels: props.fullLabelKey ? props.data.map(d => d[props.fullLabelKey]) : null,
        datasets: [
          {
            label: props.barLabel,
            data: props.data.map(d => d[props.valueKey]),
            backgroundColor: props.barColor,
            yAxisID: 'y',
            order: 2,
          },
          {
            label: 'Cumulative %',
            data: cumPct,
            type: 'line',
            borderColor: '#FF9800',
            backgroundColor: '#FF9800',
            borderWidth: 2,
            pointRadius: 4,
            yAxisID: 'y1',
            order: 1,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: { display: true, position: 'top' },
          tooltip: {
            callbacks: {
              title: (ctx) => chart?.data.fullLabels?.[ctx[0].dataIndex] ?? ctx[0].label,
              label: (ctx) => {
                const v = ctx.parsed.y ?? 0;
                if (ctx.dataset.label === 'Cumulative %') return `Cumulative: ${v.toFixed(1)}%`;
                const unit = props.yAxisLabel.includes('kg') ? 'kg' : 'hrs';
                return `${ctx.dataset.label}: ${v.toFixed(2)} ${unit}`;
              },
            },
          },
        },
        scales: {
          x: {
            title: { display: true, text: props.xAxisLabel, font: { size: 12 } },
            ticks: { autoSkip: false, maxRotation: 45, minRotation: 45, font: { size: 10 } },
          },
          y: {
            beginAtZero: true,
            position: 'left',
            title: { display: true, text: props.yAxisLabel, font: { size: 12 } },
          },
          y1: {
            beginAtZero: true,
            max: 100,
            position: 'right',
            title: { display: true, text: 'Cumulative %', font: { size: 12 } },
            grid: { drawOnChartArea: false },
            ticks: { callback: v => v + '%' },
          },
        },
      },
    });
  }

  watch(
    () => props.data,
    async () => { await nextTick(); buildChart(); },
    { deep: true, immediate: true },
  );

  onBeforeUnmount(() => chart?.destroy());
</script>
