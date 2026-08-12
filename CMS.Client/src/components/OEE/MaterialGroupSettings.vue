<template>
    <v-dialog :model-value="modelValue" max-width="640" @update:model-value="$emit('update:modelValue', $event)">
        <v-card>
            <v-card-title class="bg-primary text-white">Material Group Mapping</v-card-title>
            <v-card-text class="pt-4">
                <p class="text-body-2 text-grey-darken-1 mb-3">
                    Assign each material (from the SAP list) to a wastage group. Unassigned materials are excluded from
                    the wastage chart.
                </p>
                <div v-for="mat in available" :key="mat" class="d-flex align-center mb-2">
                    <span class="text-body-2 font-weight-medium" style="width: 45%;">{{ mat }}</span>
                    <v-select :items="groupNames" :model-value="groupOf(mat)" density="compact" hide-details
                        variant="outlined" clearable placeholder="Unassigned" @update:model-value="assign(mat, $event)"
                        style="width: 55%;" />
                </div>
                <div v-if="!available.length" class="text-grey text-center py-4">No materials found in the SAP table.
                </div>
            </v-card-text>
            <v-card-actions>
                <v-spacer />
                <v-btn variant="text" @click="$emit('update:modelValue', false)">Cancel</v-btn>
                <v-btn color="primary" variant="elevated" :loading="saving" @click="save">Save</v-btn>
            </v-card-actions>
        </v-card>
    </v-dialog>
</template>

<script setup>
import { ref, watch } from 'vue';

const props = defineProps({
    modelValue: { type: Boolean, default: false },
});
const emit = defineEmits(['update:modelValue', 'saved']);

const groupNames = ['PET', 'PE', 'PP'];
const available = ref([]);
const mapping = ref({ PET: [], PE: [], PP: [] });
const saving = ref(false);

async function load() {
    const res = await fetch('/api/oee/material-groups');
    if (!res.ok) return;
    const data = await res.json();
    available.value = data.available ?? [];
    mapping.value = { PET: [], PE: [], PP: [], ...(data.mapping ?? {}) };
}

function groupOf(mat) {
    return groupNames.find(g => (mapping.value[g] ?? []).includes(mat)) ?? null;
}

function assign(mat, group) {
    groupNames.forEach(g => {
        mapping.value[g] = (mapping.value[g] ?? []).filter(m => m !== mat);
    });
    if (group) {
        if (!mapping.value[group]) mapping.value[group] = [];
        mapping.value[group].push(mat);
    }
}

async function save() {
    saving.value = true;
    try {
        const res = await fetch('/api/oee/material-groups', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(mapping.value),
        });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        emit('saved');
        emit('update:modelValue', false);
    } catch (err) {
        console.error('[MaterialGroups] save:', err);
    } finally {
        saving.value = false;
    }
}

watch(() => props.modelValue, (open) => { if (open) load(); });
</script>