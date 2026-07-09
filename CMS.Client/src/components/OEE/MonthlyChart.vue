<template>
  <v-card elevation="2" class="pa-3">
    <h2 class="mb-2 text-center font-weight-bold">{{ year }}</h2>
    <div style="height: 260px;"><canvas ref="canvas" /></div>
  </v-card>
</template>

<script setup>
  import { ref, watch, onMounted, onBeforeUnmount } from 'vue';
  import Chart from 'chart.js/auto';

  const props = defineProps({
    monthly: { type: Array, default: () => [] },
    target: { type: Number, default: 65 },
    year: { type: Number, default: () => new Date().getFullYear() },
  });

  const canvas = ref(null);
  let chart = null;

  const labels = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec', 'YTD'];

  function buildSeries() {
    const utilization = Array(13).fill(null);
    const oee = Array(13).fill(null);
    for (const row of props.monthly) {
      const idx = Number(row.month_no) === 0 ? 12 : Number(row.month_no) - 1;
      if (idx < 0 || idx > 12) continue;
      utilization[idx] = Number(row.availability || 0);
      oee[idx] = Number(row.oee || 0);
    }
    return { utilization, oee };
  }

  function render() {
    if (!canvas.value) return;
    const { utilization, oee } = buildSeries();

    const data = {
      labels,
      datasets: [
        {
          type: 'line',
          label: 'OEE',
          data: oee,
          borderColor: '#1a3a6b',
          backgroundColor: '#1a3a6b',
          tension: 0.2,
          pointRadius: 3,
          spanGaps: false,
          order: 1,
        },
        {
          type: 'line',
          label: 'OEE Target',
          data: Array(13).fill(props.target),
          borderColor: '#e91e63',
          borderWidth: 2,
          pointRadius: 0,
          order: 2,
        },
        {
          type: 'bar',
          label: 'Utilization',
          data: utilization,
          backgroundColor: 'rgba(144, 202, 249, 0.7)',
          order: 3,
        },
      ],
    };

    const options = {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        y: { min: 0, max: 100, ticks: { callback: v => v + '%' } },
      },
      plugins: {
        legend: { position: 'bottom' },
        tooltip: {
          callbacks: {
            label: c => `${c.dataset.label}: ${Number(c.parsed.y).toFixed(0)}%`,
          },
        },
      },
    };

    if (chart) {
      chart.data = data;
      chart.update();
    } else {
      chart = new Chart(canvas.value, { type: 'bar', data, options });
    }
  }

  onMounted(render);
  watch(() => props.monthly, render, { deep: true });
  onBeforeUnmount(() => { chart?.destroy(); chart = null; });
</script>
