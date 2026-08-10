<template>
    <div class="d-flex flex-column h-100 pa-2" style="min-height: 0;">
        <div class="text-subtitle-1 font-weight-bold mb-2 flex-shrink-0">
            Daily Report Auto-Save
        </div>

        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

        <div class="flex-grow-1 overflow-auto" style="min-height: 0;">
            <v-card variant="outlined" class="pa-4 rounded-lg mb-4" style="max-width: 720px;">
                <v-switch v-model="autoSaveEnabled"
                    :label="autoSaveEnabled ? 'Auto-save enabled' : 'Auto-save disabled'" color="primary" hide-details
                    class="mb-2" @update:model-value="onToggle" />

                <div class="text-caption text-medium-emphasis mb-4">
                    When enabled, a report file is written to the folder below after each shift
                    (06:00 and 18:00). A folder path is required to enable auto-save.
                </div>

                <v-text-field v-model="folderPath" label="Report Folder Path" placeholder="\\server\share\Reports"
                    variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-folder-outline" />

                <div class="text-caption text-medium-emphasis mt-2">
                    Files are saved as
                    <code>&lt;path&gt;\&lt;month&gt;. &lt;Month&gt;\dd.MM.yyyy.xlsx</code>.
                </div>

                <div class="d-flex align-center mt-4 ga-3">
                    <v-btn variant="outlined" @click="loadConfig">Reset</v-btn>
                    <v-btn color="primary" :loading="saving" @click="saveConfig">Save</v-btn>
                    <span v-if="statusMessage" :class="statusColor" class="text-body-2">
                        {{ statusMessage }}
                    </span>
                </div>
            </v-card>

            <v-card variant="outlined" class="pa-4 rounded-lg" style="max-width: 720px;">
                <div class="text-subtitle-2 font-weight-bold mb-1">Manual Export</div>
                <div class="text-caption text-medium-emphasis mb-4">
                    Generate the file for a chosen production date and shift now. If a folder path is
                    set you can choose to save there or download to your PC; otherwise it downloads to
                    your PC.
                </div>

                <div class="d-flex align-center flex-wrap ga-3">
                    <v-text-field v-model="runDate" label="Production Date" type="date" variant="outlined"
                        density="compact" hide-details style="max-width: 200px;" />

                    <v-select v-model="runShift" :items="shiftOptions" item-title="label" item-value="value"
                        label="Shift" variant="outlined" density="compact" hide-details style="max-width: 180px;" />

                    <v-btn color="success" :loading="running" prepend-icon="mdi-file-export-outline"
                        @click="onExportClick">
                        Export Now
                    </v-btn>
                </div>

                <div v-if="runMessage" :class="runColor" class="text-body-2 mt-3">
                    {{ runMessage }}
                </div>
            </v-card>
        </div>

        <v-dialog v-model="destDialog" max-width="440px">
            <v-card>
                <v-card-title class="text-subtitle-1 font-weight-bold py-3 px-4 d-flex align-center ga-2">
                    <v-icon color="success">mdi-file-export-outline</v-icon>
                    Export Destination
                </v-card-title>
                <v-divider />
                <v-card-text class="pt-4 text-body-2">
                    Where should the report for {{ runDate }} (shift {{ runShift }}) go?
                </v-card-text>
                <v-divider />
                <v-card-actions class="pa-3">
                    <v-spacer />
                    <v-btn variant="text" @click="destDialog = false">Cancel</v-btn>
                    <v-btn variant="outlined" color="primary" @click="exportTo('pc')">
                        Download to PC
                    </v-btn>
                    <v-btn color="success" variant="flat" @click="exportTo('folder')">
                        Save to Folder
                    </v-btn>
                </v-card-actions>
            </v-card>
        </v-dialog>
    </div>
</template>

<script setup>
import { ref, inject, onMounted } from 'vue';

const showSnackbar = inject('showSnackbar', null);

const folderPath = ref('');
const autoSaveEnabled = ref(false);
const loading = ref(false);
const saving = ref(false);
const statusMessage = ref('');
const statusColor = ref('text-success');

const runDate = ref(new Date().toLocaleDateString('en-CA'));
const runShift = ref(1);
const running = ref(false);
const runMessage = ref('');
const runColor = ref('text-success');
const destDialog = ref(false);

const shiftOptions = [
    { label: 'Morning (Shift 1)', value: 1 },
    { label: 'Night (Shift 2)', value: 2 },
];

async function loadConfig() {
    loading.value = true;
    statusMessage.value = '';
    try {
        const res = await fetch('/api/setting/report-config');
        if (!res.ok) throw new Error(res.statusText);

        const data = await res.json();
        folderPath.value = data.folderPath ?? '';
        autoSaveEnabled.value = !!data.autoSaveEnabled;
    } catch (err) {
        statusMessage.value = `✗ Failed to load config: ${err.message}`;
        statusColor.value = 'text-error';
    } finally {
        loading.value = false;
    }
}

function onToggle(val) {
    if (val && !folderPath.value.trim()) {
        autoSaveEnabled.value = false;
        statusMessage.value = '✗ Set a folder path before enabling auto-save.';
        statusColor.value = 'text-error';
    }
}

async function saveConfig() {
    if (autoSaveEnabled.value && !folderPath.value.trim()) {
        statusMessage.value = '✗ A folder path is required when auto-save is enabled.';
        statusColor.value = 'text-error';
        return;
    }

    saving.value = true;
    statusMessage.value = '';
    try {
        const res = await fetch('/api/setting/report-config', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                folderPath: folderPath.value.trim(),
                autoSaveEnabled: autoSaveEnabled.value,
            }),
        });
        if (!res.ok) {
            const err = await res.json().catch(() => ({ error: res.statusText }));
            throw new Error(err.error ?? 'Unknown error');
        }
        statusMessage.value = '✓ Report config saved.';
        statusColor.value = 'text-success';
        if (showSnackbar) showSnackbar('Report config saved.', 'success');
    } catch (err) {
        statusMessage.value = `✗ Failed: ${err.message}`;
        statusColor.value = 'text-error';
        if (showSnackbar) showSnackbar(`Failed to save: ${err.message}`, 'error');
    } finally {
        saving.value = false;
    }
}

function onExportClick() {
    if (!runDate.value) {
        runMessage.value = '✗ Select a production date.';
        runColor.value = 'text-error';
        return;
    }

    // If a folder path is configured, let the user choose; otherwise download to PC.
    if (folderPath.value.trim()) {
        destDialog.value = true;
    } else {
        exportTo('pc');
    }
}

async function exportTo(destination) {
    destDialog.value = false;
    running.value = true;
    runMessage.value = '';

    const payload = {
        production_date: runDate.value,
        shift: runShift.value,
        destination,
    };

    try {
        const res = await fetch('/api/setting/report/run', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload),
        });

        if (destination === 'pc') {
            if (!res.ok) {
                const err = await res.json().catch(() => ({ error: res.statusText }));
                throw new Error(err.error ?? 'Unknown error');
            }
            await triggerBlobDownload(res);
            runMessage.value = '✓ Downloaded to your PC.';
            runColor.value = 'text-success';
            if (showSnackbar) showSnackbar('Report downloaded.', 'success');
        } else {
            const data = await res.json().catch(() => ({}));
            if (!res.ok) throw new Error(data.error ?? res.statusText);
            runMessage.value = `✓ Saved: ${data.filePath ?? 'file written.'}`;
            runColor.value = 'text-success';
            if (showSnackbar) showSnackbar('Report saved to folder.', 'success');
        }
    } catch (err) {
        runMessage.value = `✗ Failed: ${err.message}`;
        runColor.value = 'text-error';
        if (showSnackbar) showSnackbar(`Export failed: ${err.message}`, 'error');
    } finally {
        running.value = false;
    }
}

async function triggerBlobDownload(res) {
    const blob = await res.blob();
    const fileName = parseFileName(res.headers.get('content-disposition'))
        ?? `${runDate.value}_shift${runShift.value}.xlsx`;

    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    window.URL.revokeObjectURL(url);
}

function parseFileName(contentDisposition) {
    if (!contentDisposition) return null;
    const match = /filename\*?=(?:UTF-8'')?["']?([^"';]+)/i.exec(contentDisposition);
    return match ? decodeURIComponent(match[1]) : null;
}

onMounted(loadConfig);
</script>