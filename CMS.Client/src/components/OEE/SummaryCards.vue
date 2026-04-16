<template>
  <v-row class="mb-2">
    <v-col v-for="(metric, index) in metrics"
           :key="metric.title"
           cols="12"
           md="3"
           class="px-1">
      <v-card class="pa-2 d-flex flex-column align-center justify-center text-center"
              elevation="2"
              :style="{ borderTop: `3px solid ${metricColor(metric)}` }">
        <div class="text-subtitle-2 font-weight-bold mb-1"
             :style="{ color: metricColor(metric) }">
          {{ metric.title }}
        </div>

        <div class="d-flex align-center justify-center" style="width: 140px; height: 140px;">
          <canvas :ref="(el) => (canvasRefs[index] = el)" />
        </div>

        <div class="text-caption mt-1" style="color: #888;">
          Target: {{ TARGETS[metric.type] }}%
        </div>
      </v-card>
    </v-col>
  </v-row>
</template>

<script setup>
import { ref, watch, onBeforeUnmount, nextTick } from 'vue';
import {
  Chart, ArcElement, DoughnutController, Tooltip, Legend,
} from 'chart.js';

Chart.register(ArcElement, DoughnutController, Tooltip, Legend);

// ── Props ─────────────────────────────────────────────────────────────────────

const props = defineProps({
  /** Array of { title, value, type } objects */
  metrics: { type: Array, required: true },
});

// ── Constants ─────────────────────────────────────────────────────────────────

const TARGETS = { oee: 65, performance: 95, availability: 70, quality: 97 };

// ── State ─────────────────────────────────────────────────────────────────────

const canvasRefs = ref([]);
let charts = [];

// ── Helpers ───────────────────────────────────────────────────────────────────

function metricColor({ type, value }) {
  return Number(value) >= TARGETS[type] ? '#4caf50' : '#f44336';
}

const centerTextPlugin = {
  id: 'centerText',
  beforeDraw(chart) {
    const { width, height, ctx } = chart;
    const value = chart.config.data.datasets[0].data[0];
    ctx.save();
    const fontSize = (height / 120).toFixed(2);
    ctx.font = `bold ${fontSize}em sans-serif`;
    ctx.textBaseline = 'middle';
    ctx.textAlign = 'center';
    ctx.fillStyle = chart.config.data.datasets[0].backgroundColor[0];
    ctx.fillText(`${Number(value).toFixed(0)}%`, Math.round(width / 2), Math.round(height / 2));
    ctx.restore();
  },
};

function buildCharts() {
  charts.forEach(c => c?.destroy());
  charts = [];

  props.metrics.forEach((metric, i) => {
    const canvas = canvasRefs.value[i];
    if (!canvas) return;
    const color = metricColor(metric);
    charts.push(
      new Chart(canvas.getContext('2d'), {
        type: 'doughnut',
        data: {
          datasets: [{
            data: [metric.value, Math.max(0, 100 - metric.value)],
            backgroundColor: [color, '#e0e0e0'],
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
      }),
    );
  });
}

// Rebuild charts whenever metrics data changes
watch(
  () => props.metrics,
  async () => {
    await nextTick();
    buildCharts();
  },
  { deep: true, immediate: true },
);

onBeforeUnmount(() => charts.forEach(c => c?.destroy()));
</script>
