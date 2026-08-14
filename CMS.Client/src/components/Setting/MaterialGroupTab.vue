<template>
    <div class="d-flex flex-column h-100 pa-3" style="min-height: 0;">
        <div class="d-flex align-center flex-shrink-0 mb-2">
            <v-icon color="primary" class="mr-2">mdi-shape-outline</v-icon>
            <div>
                <div class="text-subtitle-1 font-weight-medium">Material Groups</div>
                <div class="text-caption text-medium-emphasis">Map each SAP material to PP, PE or PET. Drives the
                    Production Wastages chart in the OEE tab.</div>
            </div>
            <v-spacer />
            <v-text-field v-model="search" density="compact" variant="outlined" hide-details clearable
                prepend-inner-icon="mdi-magnify" placeholder="Search material" style="max-width: 260px;" class="mr-2" />
            <v-btn variant="text" class="mr-2" :disabled="loading || saving" @click="load">Reset</v-btn>
            <v-btn color="primary" prepend-icon="mdi-content-save" :loading="saving" :disabled="loading" @click="save">
                Save
            </v-btn>
        </div>

        <v-alert v-if="message" :type="messageType" density="compact" variant="tonal"
            class="flex-shrink-0 mb-2 text-caption py-1">
            {{ message }}
        </v-alert>

        <div class="flex-grow-1 d-flex flex-column overflow-hidden border rounded" style="min-height: 0;">
            <v-data-table-virtual :headers="headers" :items="items" :loading="loading" height="100%" density="compact"
                fixed-header item-value="material" class="fill-table"
                :no-data-text="available.length ? 'No materials match the search.' : 'No materials found.'">
                <template #item.material="{ item }">
                    <span class="font-weight-medium">{{ item.material }}</span>
                </template>

                <template #item.group="{ item }">
                    <v-select :items="groupNames" :model-value="groupOf(item.material)" density="compact"
                        variant="outlined" hide-details clearable placeholder="Unassigned"
                        @update:model-value="assign(item.material, $event)" style="max-width: 220px;" class="my-1" />
                </template>
            </v-data-table-virtual>
        </div>
    </div>
</template>

<script setup>
import { ref, computed } from 'vue';

const groupNames = ['PET', 'PE', 'PP'];
const headers = [
    { title: 'Material', key: 'material', sortable: false },
    { title: 'Group', key: 'group', width: '260px', sortable: false },
];

const available = ref([]);
const mapping = ref({ PET: [], PE: [], PP: [] });
const search = ref('');
const loading = ref(false);
const saving = ref(false);
const message = ref('');
const messageType = ref('success');

const filtered = computed(() => {
    const q = (search.value ?? '').trim().toLowerCase();
    if (!q) return available.value;
    return available.value.filter(m => m.toLowerCase().includes(q));
});

const items = computed(() => filtered.value.map(m => ({ material: m })));

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
    message.value = '';
}

async function load() {
    loading.value = true;
    message.value = '';
    try {
        const res = await fetch('/api/oee/material-groups');
        if (!res.ok) throw new Error(res.statusText);
        const data = await res.json();
        available.value = data.available ?? [];
        mapping.value = { PET: [], PE: [], PP: [], ...(data.mapping ?? {}) };
    } catch (err) {
        message.value = `Failed to load materials: ${err.message}`;
        messageType.value = 'error';
    } finally {
        loading.value = false;
    }
}

async function save() {
    saving.value = true;
    message.value = '';
    try {
        const res = await fetch('/api/oee/material-groups', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(mapping.value),
        });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        message.value = 'Material groups saved.';
        messageType.value = 'success';
    } catch (err) {
        message.value = `Failed to save: ${err.message}`;
        messageType.value = 'error';
    } finally {
        saving.value = false;
    }
}

load();
</script>

<style scoped>
.fill-table {
    flex: 1 1 0;
    min-height: 0;
}

.fill-table :deep(.v-table__wrapper) {
    height: 100%;
    overflow-y: auto;
}
</style>