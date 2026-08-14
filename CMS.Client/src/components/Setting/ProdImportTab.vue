<template>
    <div class="d-flex flex-column pa-3" style="height: 100%; overflow: hidden;">
        <div class="d-flex align-center flex-shrink-0 mb-2">
            <v-icon color="primary" class="mr-2">mdi-upload</v-icon>
            <div>
                <div class="text-subtitle-1 font-weight-medium">Import Production Report</div>
                <div class="text-caption text-medium-emphasis">Upload a saved report file to review, then write its rows
                    back to the database.</div>
            </div>
            <v-spacer />
            <v-btn color="primary" size="small" prepend-icon="mdi-file-upload-outline" :loading="parsing"
                @click="triggerImport">
                Select Report File
            </v-btn>
            <input ref="fileInput" type="file"
                accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" style="display: none;"
                @change="onFileSelected" />
        </div>

        <v-alert type="warning" variant="tonal" density="compact" border="start" class="flex-shrink-0 mb-3">
            Importing replaces every saved row that matches a production date, shift and machine present in the file.
            Existing values for those combinations are deleted before the new rows are inserted. Combinations not
            present
            in the file are left untouched.
        </v-alert>

        <div class="flex-grow-1 overflow-y-auto pr-1" style="min-height: 0;">
            <v-card v-if="!preview" variant="outlined" class="pa-4 rounded-lg mb-3">
                <div class="d-flex align-center mb-2">
                    <v-icon size="small" color="primary" class="mr-2">mdi-information-outline</v-icon>
                    <span class="text-subtitle-2 font-weight-bold">How this works</span>
                </div>

                <p class="text-body-2 mb-3">
                    Upload the same <code>.xlsx</code> report the system saves to the report folder (or downloads to
                    your
                    PC). To make corrections, open a saved report, edit the values, save it, then upload it here. You'll
                    see exactly what will be written and can confirm before anything changes.
                </p>

                <div class="text-caption font-weight-bold mb-1">What gets read</div>
                <ul class="text-body-2 mb-3" style="padding-left: 18px;">
                    <li>The <strong>Daily Report</strong> or <strong>Monthly Report</strong> sheet, using the header row
                        of column names.</li>
                    <li>Rows are matched on <code>machine_name</code>, <code>production_date</code> and
                        <code>shift</code>. The machine name must match a machine set up under Machine Names.
                    </li>
                    <li>Blank rows (including the yellow shift separator in daily files) are skipped.</li>
                    <li>Computed columns (shift output, material used, availability, percentages, etc.) are ignored and
                        recalculated by the database, so edits to them have no effect.</li>
                </ul>
            </v-card>

            <v-alert v-if="parseError" type="error" density="compact" variant="tonal" class="mb-2">
                {{ parseError }}
            </v-alert>

            <template v-if="preview">
                <div class="d-flex align-center mb-2">
                    <v-icon size="small" class="mr-1">mdi-file-excel-outline</v-icon>
                    <span class="text-body-2 mr-2">{{ fileName }}</span>
                    <v-chip size="x-small" label color="primary" class="mr-1">{{ preview.totalRows }} row(s)</v-chip>
                    <v-chip size="x-small" label color="deep-purple">{{ preview.groups.length }} group(s)</v-chip>
                </div>

                <div class="text-caption font-weight-bold mb-1">Data to be replaced</div>
                <v-table density="compact" class="border rounded mb-3" style="max-width: 640px;">
                    <thead>
                        <tr>
                            <th class="text-left">Production Date</th>
                            <th class="text-left">Shift</th>
                            <th class="text-left">Machine</th>
                            <th class="text-right">Rows</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr v-for="(g, i) in preview.groups" :key="i">
                            <td>{{ g.production_date }}</td>
                            <td>{{ g.shift }}</td>
                            <td>{{ g.machine_name }}</td>
                            <td class="text-right">{{ g.count }}</td>
                        </tr>
                    </tbody>
                </v-table>

                <div class="text-caption font-weight-bold mb-1">Rows to import</div>
                <div class="border rounded overflow-x-auto" style="max-height: 34vh;">
                    <table class="preview-table">
                        <thead>
                            <tr>
                                <th v-for="key in previewColumns" :key="key">{{ key }}</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="(row, i) in preview.rows" :key="i">
                                <td v-for="key in previewColumns" :key="key">{{ formatCell(row[key]) }}</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </template>
        </div>

        <div v-if="preview" class="d-flex align-center flex-shrink-0 mt-3">
            <v-spacer />
            <v-btn variant="text" class="mr-2" :disabled="importing" @click="resetImport">Clear</v-btn>
            <v-btn color="error" variant="elevated" prepend-icon="mdi-database-import" :loading="importing"
                @click="sendImport">
                Confirm &amp; import {{ preview.totalRows }} row(s)
            </v-btn>
        </div>

        <v-snackbar v-model="snackbar.show" :color="snackbar.color" :timeout="4000" location="bottom right">
            {{ snackbar.message }}
            <template #actions>
                <v-btn variant="text" @click="snackbar.show = false">Close</v-btn>
            </template>
        </v-snackbar>
    </div>
</template>

<script setup>
import { ref, computed } from 'vue';

const fileInput = ref(null);
const fileName = ref('');
const selectedFile = ref(null);
const parsing = ref(false);
const importing = ref(false);
const parseError = ref('');
const preview = ref(null);

const snackbar = ref({ show: false, message: '', color: 'success' });

// Column order for the preview table: identity keys first, then whatever else the
// server returned. Server already dropped computed columns.
const KEY_ORDER = ['machine_name', 'production_date', 'shift', 'id_type', 'mould'];

const previewColumns = computed(() => {
    if (!preview.value?.rows?.length) return [];
    const seen = new Set();
    const cols = [];
    for (const key of KEY_ORDER) {
        if (preview.value.rows.some(r => key in r) && !seen.has(key)) { seen.add(key); cols.push(key); }
    }
    for (const row of preview.value.rows) {
        for (const key of Object.keys(row)) {
            if (!seen.has(key)) { seen.add(key); cols.push(key); }
        }
    }
    return cols;
});

function triggerImport() {
    if (fileInput.value) {
        fileInput.value.value = '';
        fileInput.value.click();
    }
}

async function onFileSelected(event) {
    const file = event.target.files?.[0];
    if (!file) return;

    resetImport();
    selectedFile.value = file;
    fileName.value = file.name;

    parsing.value = true;
    try {
        const form = new FormData();
        form.append('file', file);

        const res = await fetch('/api/supervisor/import-report-xlsx/preview', {
            method: 'POST',
            body: form,
        });

        const data = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(data.message || `Server responded with ${res.status}`);

        preview.value = data;
    } catch (err) {
        parseError.value = `Could not read the report: ${err.message}`;
        selectedFile.value = null;
    } finally {
        parsing.value = false;
    }
}

async function sendImport() {
    if (!selectedFile.value) return;

    importing.value = true;
    try {
        const form = new FormData();
        form.append('file', selectedFile.value);

        const res = await fetch('/api/supervisor/import-report-xlsx', {
            method: 'POST',
            body: form,
        });

        const data = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(data.message || `Server responded with ${res.status}`);

        const count = Array.isArray(data) ? data.length : (data?.updated ?? preview.value?.totalRows ?? '');
        showSnackbar(`Import complete${count !== '' ? ` \u2014 ${count} row(s) written.` : '.'}`, 'success');
        resetImport();
    } catch (err) {
        showSnackbar(`Import failed: ${err.message}`, 'error');
    } finally {
        importing.value = false;
    }
}

function resetImport() {
    fileName.value = '';
    selectedFile.value = null;
    parseError.value = '';
    preview.value = null;
}

function formatCell(v) {
    if (v === null || v === undefined || v === '') return '';
    return v;
}

function showSnackbar(message, color = 'success') {
    snackbar.value = { show: true, message, color };
}
</script>

<style scoped>
.preview-table {
    border-collapse: collapse;
    width: 100%;
    font-size: 0.75rem;
    white-space: nowrap;
}

.preview-table th,
.preview-table td {
    border: 1px solid #e0e0e0;
    padding: 2px 8px;
    text-align: left;
}

.preview-table thead th {
    position: sticky;
    top: 0;
    background: #8EA9DB;
    color: #000;
    z-index: 2;
    font-weight: 600;
}
</style>