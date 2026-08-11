<template>
    <v-row no-gutters class="mb-1">
        <v-col v-for="card in typeCards" :key="card.type" cols="6" md="3" class="px-1">
            <v-card class="pa-2 d-flex flex-column align-center justify-center text-center"
                :elevation="isActive(card.type) ? 6 : 2" style="cursor: pointer;" :style="{
                    borderLeft: `4px solid ${card.color}`,
                    outline: isActive(card.type) ? '2px solid #1976d2' : 'none',
                }" @click="$emit('select', card.type)">
                <div class="text-caption font-weight-bold text-grey-darken-1">
                    {{ card.type }}
                    <span class="text-grey">· {{ card.count }}</span>
                </div>
                <div class="text-h5 font-weight-bold" :style="{ color: card.color }">
                    {{ card.oee }}%
                </div>
            </v-card>
        </v-col>
    </v-row>
</template>

<script setup>
import { computed } from 'vue';
import { TYPE_ORDER, machinesOfType } from '@/utils/machineType';

const props = defineProps({
    machines: { type: Array, required: true },
    selectedType: { type: String, default: 'All' },
});

defineEmits(['select']);

const OEE_TARGET = 65;

function sum(rows, key) {
    return rows.reduce((s, r) => s + (Number(r[key]) || 0), 0);
}

function oeeOf(rows) {
    const run = sum(rows, 'run_time');
    const operating = sum(rows, 'operating_time');
    const sap = sum(rows, 'total_sap_time');
    const act = sum(rows, 'total_actual_time');
    const mat = sum(rows, 'material_used');
    const rej = sum(rows, 'reject_weight');

    const availability = operating > 0 ? (run / operating) * 100 : 0;
    const performance = act > 0 ? (sap / act) * 100 : 0;
    const quality = mat > 0 ? Math.max(0, ((mat - rej) / mat) * 100) : 0;
    return (availability * performance * quality) / 10000;
}

const typeCards = computed(() =>
    TYPE_ORDER.map(type => {
        const rows = machinesOfType(props.machines, type);
        const oee = oeeOf(rows);
        return {
            type,
            count: rows.length,
            oee: oee.toFixed(0),
            color: oee >= OEE_TARGET ? '#4caf50' : '#f44336',
        };
    })
);

function isActive(type) {
    return props.selectedType === type;
}
</script>