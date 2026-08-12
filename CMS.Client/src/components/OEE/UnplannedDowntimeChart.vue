<template>
    <v-card elevation="1" class="pa-3">
        <h4 class="text-subtitle-1 font-weight-bold mb-2">Unplanned Downtime</h4>
        <div style="height: 320px;">
            <canvas v-if="hasData" ref="canvas" />
            <div v-else class="d-flex flex-column align-center justify-center w-100 h-100">
                <v-icon size="40" color="grey-lighten-1">mdi-chart-bar-stacked</v-icon>
                <span class="text-grey text-body-2 mt-2">No downtime data</span>
            </div>
        </div>
    </v-card>
</template>

<script setup>
import { ref, watch, computed, onMounted, onBeforeUnmount, nextTick } from 'vue';
import Chart from 'chart.js/auto';

const props = defineProps({
    payload: { type: Object, default: null },
});

const canvas = ref(null);
let chart = null;

const hasData = computed(() =>
    !!props.payload && Array.isArray(props.payload.months) && props.payload.months.length > 0
);

const SERIES = [
    { key: 'process', label: 'Process', color: '#c2185b' },
    { key: 'moldChange', label: 'Mold Change', color: '#2e7d32' },
    { key: 'breakdown', label: 'Breakdown Maintenance', color: '#f9a825' },
    { key: 'idle', label: 'Machine Idle', color: '#d32f2f' },
];

function render() {
    if (!canvas.value || !hasData.value) return;
    const p = props.payload;
    const points = [...p.months, p.currentAvg, p.priorAvg];
    const labels = points.map(m => m.label);

    const datasets = SERIES.map(s => ({
        label: s.label,
        data: points.map(m => m[s.key]),
        backgroundColor: s.color,
        stack: 'dt',
    }));

    const data = { labels, datasets };
    const options = {
        responsive: true,
        maintainAspectRatio: false,
        scales: {
            x: { stacked: true },
            y: { stacked: true, ticks: { callback: v => v + '%' } },
        },
        plugins: {
            legend: { position: 'bottom' },
            tooltip: { callbacks: { label: c => `${c.dataset.label}: ${Number(c.parsed.y).toFixed(1)}%` } },
        },
    };

    if (chart) { chart.data = data; chart.update(); }
    else chart = new Chart(canvas.value, { type: 'bar', data, options });
}

watch(() => props.payload, async () => { await nextTick(); render(); }, { deep: true });
onMounted(render);
onBeforeUnmount(() => { chart?.destroy(); chart = null; });
</script>