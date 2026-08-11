<template>
  <v-card elevation="2" class="pa-3">
    <h2 class="mb-2 text-center font-weight-bold">
      {{ year }}<span v-if="selectedType && selectedType !== 'All'"> · {{ selectedType }}</span>
    </h2>
    <div style="height: 260px;"><canvas ref="canvas" /></div>
  </v-card>
</template>

<script setup>
import { ref, watch, onMounted, onBeforeUnmount, computed } from 'vue';
import Chart from 'chart.js/auto';
import { machinesOfType } from '@/utils/machineType';

const props = defineProps({
  monthly: { type: Array, default: () => [] },
  target: { type: Number, default: 65 },
  year: { type: Number, default: () => new Date().getFullYear() },
  selectedType: { type: String, default: 'All' },
});

const canvas = ref(null);
let chart = null;

const labels = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec', 'YTD'];

const valueLabelPlugin = {
  id: 'valueLabel',
  afterDatasetsDraw(c) {
    const { ctx } = c;
    c.data.datasets.forEach((dataset, di) => {
      if (dataset.label === 'OEE Target') return;
      const meta = c.getDatasetMeta(di);
      if (meta.hidden) return;
      meta.data.forEach((element, i) => {
        const raw = dataset.data[i];
        if (raw === null || raw === undefined) return;
        ctx.save();
        ctx.font = 'bold 10px sans-serif';
        ctx.textAlign = 'center';

        if (dataset.type === 'line') {
          ctx.textBaseline = 'bottom';
          ctx.fillStyle = dataset.borderColor;
          ctx.fillText(`${Number(raw).toFixed(0)}%`, element.x, element.y - 6);
        } else {
          ctx.textBaseline = 'bottom';
          ctx.fillStyle = '#0d47a1';
          ctx.fillText(`${Number(raw).toFixed(0)}%`, element.x, element.base - 4);
        }
        ctx.restore();
      });
    });
  },
};

function sum(rows, key) {
  return rows.reduce((s, r) => s + (Number(r[key]) || 0), 0);
}

function metricsOf(rows) {
  const run = sum(rows, 'run_time');
  const unplanned = sum(rows, 'unplanned_dt');
  const operating = run + unplanned;
  const sap = sum(rows, 'total_sap_time');
  const act = sum(rows, 'total_actual_time');
  const mat = sum(rows, 'material_used');
  const rej = sum(rows, 'reject_weight');
  const good = mat - rej;

  const availability = operating > 0 ? (run / operating) * 100 : 0;
  const performance = act > 0 ? (sap / act) * 100 : 0;
  const quality = mat > 0 && good >= 0 ? (good / mat) * 100 : 0;
  const oee = (availability * performance * quality) / 10000;
  return { availability, oee };
}

const filteredMonthly = computed(() =>
  machinesOfType(props.monthly, props.selectedType)
);

function buildSeries() {
  const utilization = Array(13).fill(null);
  const oee = Array(13).fill(null);

  const currentYear = props.year === new Date().getFullYear();
  const currentIdx = new Date().getMonth();

  const ytdRows = [];

  for (let m = 1; m <= 12; m++) {
    const idx = m - 1;
    if (currentYear && idx === currentIdx) continue;

    const rows = filteredMonthly.value.filter(r => Number(r.month_no) === m);
    if (!rows.length) continue;

    const { availability, oee: monthOee } = metricsOf(rows);
    utilization[idx] = availability;
    oee[idx] = monthOee;
    ytdRows.push(...rows);
  }

  if (ytdRows.length) {
    const { availability, oee: ytdOee } = metricsOf(ytdRows);
    utilization[12] = availability;
    oee[12] = ytdOee;
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
    layout: { padding: { top: 20 } },
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
    chart = new Chart(canvas.value, {
      type: 'bar',
      data,
      options,
      plugins: [valueLabelPlugin],
    });
  }
}

onMounted(render);
watch(() => [props.monthly, props.selectedType], render, { deep: true });
onBeforeUnmount(() => { chart?.destroy(); chart = null; });
</script>