<template>
    <div class="d-flex flex-column h-100 pa-3" style="min-height: 0; overflow: hidden;">
        <div class="d-flex align-center flex-shrink-0 mb-2">
            <v-icon color="primary" class="mr-2">mdi-upload</v-icon>
            <div>
                <div class="text-subtitle-1 font-weight-medium">Import Production Report</div>
                <div class="text-caption text-medium-emphasis">Upload a CSV to replace saved report rows for the
                    matching production date, shift and machine.</div>
            </div>
            <v-spacer />
            <v-btn color="primary" size="small" prepend-icon="mdi-file-upload-outline" @click="triggerImport">
                Select CSV
            </v-btn>
            <input ref="csvFileInput" type="file" accept=".csv" style="display: none;" @change="onCsvFileSelected" />
        </div>

        <div class="flex-grow-1 overflow-y-auto pr-1" style="min-height: 0;">
            <v-row class="ma-0 mb-1">
                <v-col cols="12" lg="6" class="pa-2">
                    <v-card variant="outlined" class="pa-4 rounded-lg h-100">
                        <div class="d-flex align-center mb-2">
                            <v-icon size="small" color="primary" class="mr-2">mdi-information-outline</v-icon>
                            <span class="text-subtitle-2 font-weight-bold">CSV format &amp; required columns</span>
                        </div>

                        <p class="text-body-2 mb-3">
                            The first row must be a header row using the exact keys below. Order does not matter and
                            extra columns are ignored. Numbers may include thousands separators; blank cells and a
                            single dash are read as zero. Dates accept <code>d/M/yyyy</code>, <code>dd/MM/yyyy</code> or
                            <code>yyyy-MM-dd</code>. The <code>machine_name</code> must match a machine set up under
                            Machine Names.
                        </p>

                        <div class="text-caption font-weight-bold mb-1">Required in every row</div>
                        <v-table density="compact" class="border rounded">
                            <thead>
                                <tr>
                                    <th class="text-left">Column</th>
                                    <th class="text-left">Type</th>
                                    <th class="text-left">Notes</th>
                                </tr>
                            </thead>
                            <tbody>
                                <tr v-for="c in requiredColumns" :key="c.key">
                                    <td><code>{{ c.key }}</code></td>
                                    <td>{{ c.type }}</td>
                                    <td class="text-medium-emphasis">{{ c.note }}</td>
                                </tr>
                            </tbody>
                        </v-table>
                    </v-card>
                </v-col>

                <v-col cols="12" lg="6" class="pa-2">
                    <v-card variant="outlined" class="pa-4 rounded-lg h-100">
                        <div class="d-flex align-center mb-2">
                            <v-icon size="small" color="primary" class="mr-2">mdi-format-list-bulleted</v-icon>
                            <span class="text-subtitle-2 font-weight-bold">Optional data columns</span>
                        </div>

                        <div class="text-body-2 mb-2">
                            <v-chip v-for="k in optionalIntColumns" :key="k" size="x-small" label class="mr-1 mb-1"
                                color="blue">{{ k }}</v-chip>
                            <v-chip v-for="k in optionalFloatColumns" :key="k" size="x-small" label class="mr-1 mb-1"
                                color="teal">{{ k }}</v-chip>
                            <v-chip v-for="k in optionalTextColumns" :key="k" size="x-small" label class="mr-1 mb-1"
                                color="grey">{{ k }}</v-chip>
                        </div>

                        <div class="text-caption text-medium-emphasis mb-4">
                            Blue = whole number, teal = decimal, grey = text. Computed columns (shift output, material
                            used, availability, etc.) are derived on the server and must not be included.
                        </div>

                        <v-btn size="small" variant="text" color="primary" prepend-icon="mdi-download"
                            @click="downloadTemplate">
                            Download blank template
                        </v-btn>
                    </v-card>
                </v-col>
            </v-row>

            <div v-if="fileName" class="d-flex align-center mb-2 px-2">
                <v-icon size="small" class="mr-1">mdi-file-delimited-outline</v-icon>
                <span class="text-body-2 mr-2">{{ fileName }}</span>
                <v-chip size="x-small" label color="success" class="mr-1">{{ validCount }} valid</v-chip>
                <v-chip v-if="errorCount" size="x-small" label color="error">{{ errorCount }} with issues</v-chip>
            </div>

            <v-alert v-if="parseError" type="error" density="compact" variant="tonal" class="mb-2 mx-2">
                {{ parseError }}
            </v-alert>

            <div v-if="preview.length" class="border rounded overflow-x-auto mx-2" style="max-height: 40vh;">
                <table class="preview-table">
                    <thead>
                        <tr>
                            <th class="status-col">Row</th>
                            <th v-for="key in previewColumns" :key="key">{{ key }}</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr v-for="row in preview" :key="row._line" :class="{ 'error-row': row._errors.length }">
                            <td class="status-col">
                                <v-tooltip v-if="row._errors.length" location="right">
                                    <template #activator="{ props }">
                                        <v-icon v-bind="props" color="error" size="small">mdi-alert-circle</v-icon>
                                    </template>
                                    <div v-for="(e, idx) in row._errors" :key="idx">{{ e }}</div>
                                </v-tooltip>
                                <v-icon v-else color="success" size="small">mdi-check-circle</v-icon>
                                <span class="ml-1 text-caption">{{ row._line }}</span>
                            </td>
                            <td v-for="key in previewColumns" :key="key"
                                :class="{ 'cell-error': row._errorKeys.has(key) }">
                                {{ formatCell(row.data[key]) }}
                            </td>
                        </tr>
                    </tbody>
                </table>
            </div>
        </div>

        <div v-if="preview.length" class="d-flex align-center flex-shrink-0 mt-3">
            <v-spacer />
            <v-btn variant="text" class="mr-2" @click="resetImport">Clear</v-btn>
            <v-btn color="primary" :disabled="validCount === 0" :loading="importing" prepend-icon="mdi-database-import"
                @click="startImport">
                Import {{ validCount }} row(s)
            </v-btn>
        </div>

        <v-dialog v-model="errorDialog.visible" max-width="480" persistent>
            <v-card>
                <v-card-title class="text-warning">
                    <v-icon start>mdi-alert</v-icon>
                    {{ errorCount }} row(s) have issues
                </v-card-title>
                <v-card-text>
                    <p class="text-body-2 mb-2">
                        The file contains {{ errorCount }} row(s) with invalid or missing values. How would you like to
                        proceed?
                    </p>
                    <p class="text-body-2 mb-0">
                        <strong>Skip</strong> imports the {{ validCount }} valid row(s) and ignores the rest.
                        <strong>Stop</strong> cancels the import so you can correct the file.
                    </p>
                </v-card-text>
                <v-card-actions>
                    <v-spacer />
                    <v-btn variant="text" @click="errorDialog.visible = false">Stop</v-btn>
                    <v-btn color="primary" variant="elevated" :disabled="validCount === 0"
                        @click="confirmSkipAndImport">
                        Skip bad rows &amp; import
                    </v-btn>
                </v-card-actions>
            </v-card>
        </v-dialog>

        <v-dialog v-model="confirmDialog.visible" max-width="480" persistent>
            <v-card>
                <v-card-title class="text-error">
                    <v-icon start>mdi-database-alert</v-icon>
                    Replace existing data?
                </v-card-title>
                <v-card-text>
                    <p class="text-body-2 mb-2">
                        This import will delete and replace saved report and reject data for the following combinations
                        of
                        production date, shift and machine:
                    </p>
                    <div class="border rounded pa-2 mb-2" style="max-height: 180px; overflow-y: auto;">
                        <div v-for="g in confirmDialog.groups" :key="g.label" class="text-caption">
                            {{ g.label }} <span class="text-medium-emphasis">({{ g.count }} row{{ g.count === 1 ? '' :
                                's'
                            }})</span>
                        </div>
                    </div>
                    <p class="text-body-2 mb-0">This action cannot be undone.</p>
                </v-card-text>
                <v-card-actions>
                    <v-spacer />
                    <v-btn variant="text" @click="confirmDialog.visible = false">Cancel</v-btn>
                    <v-btn color="error" variant="elevated" :loading="importing" @click="sendImport">
                        Replace &amp; import
                    </v-btn>
                </v-card-actions>
            </v-card>
        </v-dialog>

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

const csvFileInput = ref(null);
const fileName = ref('');
const importing = ref(false);
const parseError = ref('');
const preview = ref([]);
const knownMachineNames = ref(new Set());

const snackbar = ref({ show: false, message: '', color: 'success' });
const errorDialog = ref({ visible: false });
const confirmDialog = ref({ visible: false, groups: [] });

const requiredColumns = [
    { key: 'machine_name', type: 'text', note: 'Must match a machine under Machine Names' },
    { key: 'production_date', type: 'date', note: 'd/M/yyyy, dd/MM/yyyy or yyyy-MM-dd' },
    { key: 'shift', type: 'whole number', note: '1 = Morning, 2 = Night' },
    { key: 'id_type', type: 'whole number', note: 'SAP product code' },
    { key: 'mould', type: 'whole number', note: 'mould number' },
];

const optionalIntColumns = ['qty_perct', 'shot', 'qty_order', 'wip_opening', 'wip_closing', 'finish_good', 'qty_accum', 'reject_total_pcs'];
const optionalFloatColumns = ['gross_weight', 'part_weight', 'sap_ct', 'act_ct', 'production_running', 'reject_startup', 'reject_prod', 'reject_purging', 'reject_preform', 'change_full_set', 'change_half_set', 'change_parts', 'maintenance_dt', 'technician_dt', 'production_dt', 'buyoff_dt', 'planned_dt'];
const optionalTextColumns = ['packer', 'jo_no', 'remark'];

const REQUIRED_KEYS = requiredColumns.map(c => c.key);
const INT_KEYS = new Set(['shift', 'id_type', 'mould', ...optionalIntColumns]);
const FLOAT_KEYS = new Set(optionalFloatColumns);
const DATE_KEYS = new Set(['production_date']);
const TEXT_REQUIRED_KEYS = new Set(['machine_name']);
const TEMPLATE_KEYS = ['machine_name', 'production_date', 'shift', 'id_type', 'mould', ...optionalIntColumns, ...optionalFloatColumns, ...optionalTextColumns];

const validCount = computed(() => preview.value.filter(r => r._errors.length === 0).length);
const errorCount = computed(() => preview.value.filter(r => r._errors.length > 0).length);

const previewColumns = computed(() => {
    const seen = new Set();
    const cols = [];
    for (const key of [...REQUIRED_KEYS, ...optionalIntColumns, ...optionalFloatColumns, ...optionalTextColumns]) {
        if (preview.value.some(r => key in r.data) && !seen.has(key)) { seen.add(key); cols.push(key); }
    }
    for (const row of preview.value) {
        for (const key of Object.keys(row.data)) {
            if (!seen.has(key)) { seen.add(key); cols.push(key); }
        }
    }
    return cols;
});

async function loadMachineNames() {
    try {
        const res = await fetch('/api/setting/machines');
        if (!res.ok) return;
        const data = await res.json();
        knownMachineNames.value = new Set(data.map(d => String(d.machine_name ?? '').trim().toLowerCase()));
    } catch {
        knownMachineNames.value = new Set();
    }
}

function triggerImport() {
    if (csvFileInput.value) {
        csvFileInput.value.value = '';
        csvFileInput.value.click();
    }
}

function parseCsv(text) {
    const lines = text.replace(/\r\n/g, '\n').replace(/\r/g, '\n').trim().split('\n');
    if (lines.length < 2) return { headers: [], rows: [] };

    const splitLine = (line) => {
        const values = [];
        let current = '';
        let inQuotes = false;
        for (let i = 0; i < line.length; i++) {
            const ch = line[i];
            if (ch === '"') {
                inQuotes = !inQuotes;
            } else if (ch === ',' && !inQuotes) {
                values.push(current.trim());
                current = '';
            } else {
                current += ch;
            }
        }
        values.push(current.trim());
        return values;
    };

    const headers = splitLine(lines[0]).map(h => h.trim());
    const rows = [];
    for (let i = 1; i < lines.length; i++) {
        const values = splitLine(lines[i]);
        if (!values.some(v => v !== '')) continue;
        const row = {};
        headers.forEach((h, idx) => { row[h] = values[idx] ?? ''; });
        rows.push({ line: i + 1, raw: row });
    }
    return { headers, rows };
}

const DATE_RE = /^(\d{1,2}\/\d{1,2}\/\d{4}|\d{4}-\d{2}-\d{2})$/;

function normaliseNumber(str) {
    return str.replace(/^(-?\d{1,3})(,\d{3})+(\.\d+)?$/, m => m.replace(/,/g, ''));
}

function validateAndBuildRow(entry) {
    const errors = [];
    const errorKeys = new Set();
    const data = {};

    for (const [rawKey, rawVal] of Object.entries(entry.raw)) {
        const key = rawKey.trim();
        if (!key) continue;

        const trimmed = String(rawVal ?? '').trim();
        const blank = trimmed === '' || trimmed === '-';

        if (DATE_KEYS.has(key)) {
            if (blank || !DATE_RE.test(trimmed)) {
                errors.push(`${key}: invalid or missing date`);
                errorKeys.add(key);
            }
            data[key] = trimmed;
            continue;
        }

        if (TEXT_REQUIRED_KEYS.has(key)) {
            if (blank) {
                errors.push(`${key}: required`);
                errorKeys.add(key);
            } else if (!knownMachineNames.value.has(trimmed.toLowerCase())) {
                errors.push(`${key}: '${trimmed}' is not a known machine`);
                errorKeys.add(key);
            }
            data[key] = trimmed;
            continue;
        }

        if (INT_KEYS.has(key)) {
            if (blank) {
                if (REQUIRED_KEYS.includes(key)) { errors.push(`${key}: required`); errorKeys.add(key); }
                data[key] = 0;
            } else {
                const num = Number(normaliseNumber(trimmed));
                if (!Number.isFinite(num) || !Number.isInteger(num)) {
                    errors.push(`${key}: not a whole number`);
                    errorKeys.add(key);
                    data[key] = trimmed;
                } else {
                    data[key] = num;
                }
            }
            continue;
        }

        if (FLOAT_KEYS.has(key)) {
            if (blank) {
                data[key] = 0;
            } else {
                const num = Number(normaliseNumber(trimmed));
                if (!Number.isFinite(num)) {
                    errors.push(`${key}: not a number`);
                    errorKeys.add(key);
                    data[key] = trimmed;
                } else {
                    data[key] = num;
                }
            }
            continue;
        }

        data[key] = blank ? '' : trimmed;
    }

    for (const key of REQUIRED_KEYS) {
        if (!(key in data)) {
            errors.push(`${key}: column missing`);
            errorKeys.add(key);
        }
    }

    if ('shift' in data && !errorKeys.has('shift') && data.shift !== 1 && data.shift !== 2) {
        errors.push('shift: must be 1 or 2');
        errorKeys.add('shift');
    }

    return { _line: entry.line, data, _errors: errors, _errorKeys: errorKeys };
}

async function onCsvFileSelected(event) {
    const file = event.target.files?.[0];
    if (!file) return;

    resetImport();
    fileName.value = file.name;

    await loadMachineNames();

    try {
        const text = await file.text();
        const { headers, rows } = parseCsv(text);

        if (!rows.length) {
            parseError.value = 'The file is empty or could not be parsed.';
            return;
        }

        const missingRequired = REQUIRED_KEYS.filter(k => !headers.includes(k));
        if (missingRequired.length) {
            parseError.value = `Missing required column(s): ${missingRequired.join(', ')}. See the format guide above.`;
            return;
        }

        preview.value = rows.map(validateAndBuildRow);
    } catch (err) {
        parseError.value = `Could not read the file: ${err.message}`;
    }
}

function buildGroups(rows) {
    const map = new Map();
    for (const r of rows) {
        const key = `${r.data.production_date}|${r.data.shift}|${r.data.machine_name}`;
        const label = `${r.data.production_date} · Shift ${r.data.shift} · ${r.data.machine_name}`;
        if (!map.has(key)) map.set(key, { label, count: 0 });
        map.get(key).count += 1;
    }
    return [...map.values()].sort((a, b) => a.label.localeCompare(b.label));
}

function startImport() {
    if (errorCount.value > 0) {
        errorDialog.value.visible = true;
        return;
    }
    openConfirm();
}

function confirmSkipAndImport() {
    errorDialog.value.visible = false;
    openConfirm();
}

function openConfirm() {
    const validRows = preview.value.filter(r => r._errors.length === 0);
    confirmDialog.value = { visible: true, groups: buildGroups(validRows) };
}

async function sendImport() {
    importing.value = true;
    try {
        const payload = preview.value.filter(r => r._errors.length === 0).map(r => r.data);

        const response = await fetch('/api/supervisor/import-report', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload),
        });

        if (!response.ok) {
            const err = await response.json().catch(() => ({}));
            throw new Error(err.message || `Server responded with ${response.status}`);
        }

        const updated = await response.json();
        const count = Array.isArray(updated) ? updated.length : payload.length;
        showSnackbar(`Import complete — ${count} row(s) written.`, 'success');
        confirmDialog.value.visible = false;
        resetImport();
    } catch (err) {
        showSnackbar(`Import failed: ${err.message}`, 'error');
    } finally {
        importing.value = false;
    }
}

function downloadTemplate() {
    const csv = TEMPLATE_KEYS.join(',') + '\n';
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = Object.assign(document.createElement('a'), { href: url, download: 'production_report_template.csv' });
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
}

function resetImport() {
    fileName.value = '';
    parseError.value = '';
    preview.value = [];
    errorDialog.value.visible = false;
    confirmDialog.value = { visible: false, groups: [] };
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

.preview-table .status-col {
    position: sticky;
    left: 0;
    background: #f5f5f5;
    z-index: 1;
    text-align: center;
}

.preview-table thead .status-col {
    z-index: 3;
    background: #8EA9DB;
}

.error-row td {
    background: #ffebee;
}

.cell-error {
    background: #ef9a9a !important;
    font-weight: 600;
}
</style>