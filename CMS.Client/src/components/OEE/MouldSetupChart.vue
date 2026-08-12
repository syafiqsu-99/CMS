<template>
    <v-card elevation="1" class="pa-3">
        <div class="d-flex align-center justify-space-between mb-2">
            <h4 class="text-subtitle-1 font-weight-bold">ISBM / EBM Mould Setup Hours</h4>
            <v-btn size="small" variant="text" color="primary" prepend-icon="mdi-target" @click="$emit('edit-targets')">
                Targets
            </v-btn>
        </div>
        <div v-if="hasData" class="d-flex flex-wrap">
            <div v-for="set in sets" :key="set.key" class="chart-cell">
                <div class="text-caption text-center font-weight-bold mb-1">{{ set.title }} · Target {{ set.target }}
                </div>
                <div style="height: 240px;"><canvas :ref="el => setCanvas(set.key, el)" /></div>
            </div>
        </div>
        <div v-else class="d-flex flex-column align-center justify-center" style="height: 200px;">
            <v-icon size="40" color="grey-lighten-1">mdi-chart-line</v-icon>
            <span class="text-grey text-body-2 mt-2">No mould setup data</span>
        </div>
    </v-card>
</template>

<script setup>
import { ref, watch, computed, onMounted, onBeforeUnmount, nextTick } from 'vue';
import Chart from 'chart.js/auto';

const props = defineProps({
    payload: { type: Object, default: null },
});
defineEmits(['edit-targets']);

const canvases = {};
const charts = {};

const hasData = computed(() =>
    !!props.payload && Array.isArray(props.payload.months) && props.payload.months.length > 0
);

const targets = computed(() => props.payload?.targets ?? { blow: 1.5, half: 3.0, full: 5.5 });

const sets = computed(() => [
    { key: 'blow', title: 'Blow Mould', target: targets.value.blow },
    { key: 'half', title: 'Half Set', target: targets.value.half },
    { key: 'full', title: 'Full Set', target: targets.value.full },
]);

function setCanvas(key, el) {
    if (el) canvases[key] = el;
}

function pointsFor(key) {
    const p = props.payload;
    const arr = [...p.months, p.currentAvg, p.priorAvg];
    return {
        labels: arr.map(m => m.label),
        freq: arr.map(m => m[key].frequency),
        hours: arr.map(m => (m[key].frequency > 0 ? m[key].avgHours : null)),
    };
}

function renderOne(set) {
    const el = canvases[set.key];
    if (!el) return;
    const { labels, freq, hours } = pointsFor(set.key);

    const data = {
        labels,
        datasets: [
            { type: 'bar', label: 'Frequency', data: freq, backgroundColor: '#f9a825', yAxisID: 'yFreq', order: 3 },
            {
                type: 'line', label: 'Setup Hour', data: hours, borderColor: '#1a3a6b', backgroundColor: '#1a3a6b',
                tension: 0.2, pointRadius: 3, spanGaps: false, yAxisID: 'yHour', order: 1
            },
            {
                type: 'line', label: 'Target', data: labels.map(() => set.target), borderColor: '#e91e63',
                borderWidth: 2, pointRadius: 0, yAxisID: 'yHour', order: 2
            },
        ],
    };

    const options = {
        responsive: true,
        maintainAspectRatio: false,
        scales: {
            yHour: { position: 'left', beginAtZero: true, title: { display: true, text: 'Avg Setup Hr' } },
            yFreq: { position: 'right', beginAtZero: true, grid: { drawOnChartArea: false }, title: { display: true, text: 'Frequency' } },
        },
        plugins: { legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 10 } } } },
    };

    if (charts[set.key]) { charts[set.key].data = data; charts[set.key].update(); }
    else charts[set.key] = new Chart(el, { type: 'bar', data, options });
}

function renderAll() {
    if (!hasData.value) return;
    sets.value.forEach(renderOne);
}

watch(() => props.payload, async () => {
    await nextTick();
    renderAll();
}, { deep: true });

onMounted(async () => { await nextTick(); renderAll(); });
onBeforeUnmount(() => {
    Object.values(charts).forEach(c => c?.destroy());
});
</script>

<style scoped>
.chart-cell {
    flex: 1 1 320px;
    min-width: 300px;
    padding: 4px 8px;
}
</style>