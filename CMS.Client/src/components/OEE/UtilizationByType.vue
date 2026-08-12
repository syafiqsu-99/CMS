<template>
    <v-card elevation="1" class="pa-3">
        <div class="d-flex align-center justify-space-between mb-2">
            <h4 class="text-subtitle-1 font-weight-bold">
                Machine Utilization — by Individual<span v-if="selectedType !== 'All'"> · {{ selectedType }}</span>
            </h4>
            <span class="text-caption text-grey">{{ curLabel }} vs {{ prevLabel }}</span>
        </div>
        <div style="height: 320px;">
            <canvas v-if="rows.length" ref="canvas" />
            <div v-else class="d-flex flex-column align-center justify-center w-100 h-100">
                <v-icon size="40" color="grey-lighten-1">mdi-chart-bar</v-icon>
                <span class="text-grey text-body-2 mt-2">No utilization data</span>
            </div>
        </div>
    </v-card>
</template>

<script setup>
import { ref, watch, computed, onMounted, onBeforeUnmount, nextTick } from 'vue';
import Chart from 'chart.js/auto';
import { machinesOfType } from '@/utils/machineType';

const props = defineProps({
    data: { type: Array, default: () => [] },
    month: { type: Number, required: true },
    year: { type: Number, required: true },
    selectedType: { type: String, default: 'All' },
});

const canvas = ref(null);
let chart = null;

const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const curLabel = computed(() => `${monthNames[props.month - 1]} '${String(props.year).slice(2)}`);
const prevLabel = computed(() => {
    const pm = props.month === 1 ? 12 : props.month - 1;
    const py = props.month === 1 ? props.year - 1 : props.year;
    return `${monthNames[pm - 1]} '${String(py).slice(2)}`;
});

const rows = computed(() => machinesOfType(props.data, props.selectedType));

const valueLabelPlugin = {
    id: 'utilValueLabel',
    afterDatasetsDraw(c) {
        const { ctx } = c;
        const idx = c.data.datasets.findIndex(d => d._isCurrent);
        if (idx < 0) return;
        const meta = c.getDatasetMeta(idx);
        ctx.save();
        ctx.font = 'bold 9px sans-serif';
        ctx.fillStyle = '#fff';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'bottom';
        meta.data.forEach((el, i) => {
            const raw = c.data.datasets[idx].data[i];
            if (raw != null) ctx.fillText(`${Number(raw).toFixed(0)}%`, el.x, el.base - 4);
        });
        ctx.restore();
    },
};

function render() {
    if (!canvas.value || !rows.value.length) return;
    const labels = rows.value.map(r => r.machine_name);

    const data = {
        labels,
        datasets: [
            { type: 'bar', label: prevLabel.value, data: rows.value.map(r => r.previous), backgroundColor: '#8d99ae', order: 3 },
            { type: 'bar', label: curLabel.value, data: rows.value.map(r => r.current), backgroundColor: '#4a7a2a', order: 2, _isCurrent: true },
            {
                type: 'line', label: `Avg '${String(props.year - 1).slice(2)}`, data: rows.value.map(r => r.priorYearAvg),
                borderColor: '#e91e63', backgroundColor: '#e91e63', tension: 0.2, pointRadius: 3, order: 1
            },
        ],
    };

    const options = {
        responsive: true,
        maintainAspectRatio: false,
        scales: { y: { min: 0, max: 100, ticks: { callback: v => v + '%' } } },
        plugins: {
            legend: { position: 'bottom' },
            tooltip: { callbacks: { label: c => `${c.dataset.label}: ${Number(c.parsed.y).toFixed(0)}%` } },
        },
    };

    if (chart) { chart.data = data; chart.update(); }
    else chart = new Chart(canvas.value, { type: 'bar', data, options, plugins: [valueLabelPlugin] });
}

watch(() => [props.data, props.selectedType, props.month, props.year], async () => {
    await nextTick();
    render();
}, { deep: true });

onMounted(render);
onBeforeUnmount(() => { chart?.destroy(); chart = null; });
</script>