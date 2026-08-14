<template>
    <div class="d-flex flex-column h-100 pa-3" style="min-height: 0;">
        <div class="d-flex align-center flex-shrink-0 mb-2">
            <v-icon color="primary" class="mr-2">mdi-tag-text-outline</v-icon>
            <div>
                <div class="text-subtitle-1 font-weight-medium">Machine Names</div>
                <div class="text-caption text-medium-emphasis">Set the current display name for each machine id. Used
                    across live views and CSV import.</div>
            </div>
            <v-spacer />
            <v-btn variant="text" class="mr-2" :disabled="loading || saving" @click="load">Reset</v-btn>
            <v-btn color="primary" prepend-icon="mdi-content-save" :loading="saving" :disabled="loading || !dirty"
                @click="save">
                Save
            </v-btn>
        </div>

        <v-alert v-if="message" :type="messageType" density="compact" variant="tonal"
            class="flex-shrink-0 mb-2 text-caption py-1">
            {{ message }}
        </v-alert>

        <div class="flex-grow-1 d-flex flex-column overflow-hidden border rounded" style="min-height: 0;">
            <v-data-table-virtual :headers="headers" :items="rows" :loading="loading" height="100%" density="compact"
                fixed-header item-value="id_machine" class="fill-table"
                no-data-text="No machines found in machine_master.">
                <template #item.id_machine="{ item }">
                    <span class="text-medium-emphasis">{{ item.id_machine }}</span>
                </template>

                <template #item.machine_name="{ item }">
                    <v-text-field v-model="item.machine_name" density="compact" variant="outlined" hide-details
                        :error="isDuplicate(item)" @update:model-value="onEdit" style="max-width: 420px;"
                        class="my-1" />
                </template>
            </v-data-table-virtual>
        </div>
    </div>
</template>

<script setup>
import { ref, computed } from 'vue';

const headers = [
    { title: 'Machine ID', key: 'id_machine', width: '140px', sortable: false },
    { title: 'Machine Name', key: 'machine_name', sortable: false },
];

const rows = ref([]);
const original = ref({});
const loading = ref(false);
const saving = ref(false);
const message = ref('');
const messageType = ref('success');

const dirty = computed(() => rows.value.some(r => (r.machine_name ?? '').trim() !== (original.value[r.id_machine] ?? '')));

const nameCounts = computed(() => {
    const counts = {};
    for (const r of rows.value) {
        const key = (r.machine_name ?? '').trim().toLowerCase();
        if (!key) continue;
        counts[key] = (counts[key] ?? 0) + 1;
    }
    return counts;
});

function isDuplicate(row) {
    const key = (row.machine_name ?? '').trim().toLowerCase();
    return key !== '' && nameCounts.value[key] > 1;
}

function onEdit() {
    message.value = '';
}

async function load() {
    loading.value = true;
    message.value = '';
    try {
        const res = await fetch('/api/setting/machines');
        if (!res.ok) throw new Error(res.statusText);
        const data = await res.json();
        rows.value = data.map(d => ({ id_machine: d.id_machine, machine_name: d.machine_name ?? '' }));
        original.value = Object.fromEntries(rows.value.map(r => [r.id_machine, r.machine_name]));
    } catch (err) {
        message.value = `Failed to load machine names: ${err.message}`;
        messageType.value = 'error';
    } finally {
        loading.value = false;
    }
}

async function save() {
    const blank = rows.value.find(r => !(r.machine_name ?? '').trim());
    if (blank) {
        message.value = `Machine ${blank.id_machine} has no name. Names cannot be empty.`;
        messageType.value = 'error';
        return;
    }
    if (rows.value.some(isDuplicate)) {
        message.value = 'Machine names must be unique. Resolve the highlighted duplicates first.';
        messageType.value = 'error';
        return;
    }

    saving.value = true;
    message.value = '';
    try {
        const payload = Object.fromEntries(rows.value.map(r => [String(r.id_machine), r.machine_name.trim()]));
        const res = await fetch('/api/setting/machines', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload),
        });
        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            throw new Error(err.error ?? res.statusText);
        }
        original.value = Object.fromEntries(rows.value.map(r => [r.id_machine, r.machine_name.trim()]));
        message.value = 'Machine names saved.';
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