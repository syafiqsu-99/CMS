<template>
    <v-card elevation="1" class="pa-3">
        <div class="d-flex align-center justify-space-between mb-2">
            <h4 class="text-subtitle-1 font-weight-bold">Production Wastages</h4>
            <div class="d-flex align-center ga-2">
                <v-text-field v-model.number="target" type="number" density="compact" hide-details variant="outlined"
                    label="Target %" style="width: 120px;" @update:model-value="renderAll" />
                <v-btn size="small" variant="text" color="primary" prepend-icon="mdi-cog" @click="$emit('edit-groups')">
                    Material Groups
                </v-btn>
            </div>
        </div>

        <div v-if="groups.length">
            <v-tabs v-model="tab" density="compact" color="primary">
                <v-tab v-for="g in groups" :key="g.group" :value="g.group">{{ g.group }}</v-tab>
            </v-tabs>
            <v-window v-model="tab">
                <v-window-item v-for="g in groups" :key="g.group" :value="g.group">
                    <div class="d-flex align-center justify-end text-caption text-grey mt-1">
                        YTD: {{ Number(g.ytd.materialUsed).toLocaleString() }} kg · {{ g.ytd.wastagePct }}%
                    </div>
                    <div style="height: 300px;"><canvas :ref="el => setCanvas(g.group, el)" /></div>
                </v-window-item>
            </v-window>
        </div>
        <div v-else class="d-flex flex-column align-center justify-center" style="height: 200px;">
            <v-icon size="40" color="grey-lighten-1">mdi-chart-bar</v-icon>
            <span class="text-grey text-body-2 mt-2">No material groups configured</span>
        </div>
    </v-card>
</template>

<script setup>
import { ref, watch, computed, onMounted, onBeforeUnmount, nextTick } from 'vue';
import Chart from 'chart.js/auto';

const props = defineProps({
    data: { type: Array, default: () => [] },
});
defineEmits(['edit-groups']);

const tab = ref(null);
const target = ref(3.0);
const canvases = {};
const charts = {};

const groups = computed(() => props.data ?? []);

function setCanvas(key, el) {
    if (el) canvases[key] = el;
}

function renderOne(g) {
    const el = canvases[g.group];
    if (!el) return;
    const labels = g.months.map(m => m.label);

    const data = {
        labels,
        datasets: [
            {
                type: 'bar', label: 'Material Used (kg)', data: g.months.map(m => m.materialUsed),
                backgroundColor: 'rgba(144, 202, 249, 0.7)', yAxisID: 'yKg', order: 3
            },
            {
                type: 'line', label: 'Wastage (%)', data: g.months.map(m => m.wastagePct),
                borderColor: '#0d1b4c', backgroundColor: '#0d1b4c', tension: 0.2, pointRadius: 3, yAxisID: 'yPct', order: 1
            },
            {
                type: 'line', label: 'Target', data: labels.map(() => target.value),
                borderColor: '#e91e63', borderWidth: 2, pointRadius: 0, yAxisID: 'yPct', order: 2
            },
        ],
    };

    const options = {
        responsive: true,
        maintainAspectRatio: false,
        scales: {
            yPct: { position: 'left', beginAtZero: true, ticks: { callback: v => v + '%' }, title: { display: true, text: 'Wastage %' } },
            yKg: { position: 'right', beginAtZero: true, grid: { drawOnChartArea: false }, title: { display: true, text: 'Material (kg)' } },
        },
        plugins: {
            legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 10 } } },
            tooltip: {
                callbacks: {
                    label: c => c.dataset.yAxisID === 'yPct'
                        ? `${c.dataset.label}: ${Number(c.parsed.y).toFixed(1)}%`
                        : `${c.dataset.label}: ${Number(c.parsed.y).toLocaleString()} kg`,
                },
            },
        },
    };

    if (charts[g.group]) { charts[g.group].data = data; charts[g.group].update(); }
    else charts[g.group] = new Chart(el, { type: 'bar', data, options });
}

function renderAll() {
    groups.value.forEach(renderOne);
}

watch(groups, async (g) => {
    if (g.length && !tab.value) tab.value = g[0].group;
    await nextTick();
    renderAll();
}, { deep: true });

watch(tab, async () => { await nextTick(); renderAll(); });

onMounted(async () => {
    if (groups.value.length) tab.value = groups.value[0].group;
    await nextTick();
    renderAll();
});
onBeforeUnmount(() => { Object.values(charts).forEach(c => c?.destroy()); });
</script>