<template>
  <div style="display: flex; flex-direction: column; overflow: hidden;">
    <v-toolbar flat density="compact" class="px-2">
      <v-icon color="primary" size="small">mdi-database</v-icon>
      <v-toolbar-title class="text-subtitle-1 ml-2">Daily Production Report</v-toolbar-title>
      <v-spacer></v-spacer>
      <div class="d-flex ga-2">
        <v-date-input v-model="production_date"
                      label="Production Date"
                      :max="maxDate"
                      variant="outlined"
                      density="compact"
                      hide-details
                      display-format="fullDate"
                      style="width: 200px;" />
        <v-select label="Shift"
                  v-model="shift"
                  :items="[{ title: 'Morning', value: 1 }, { title: 'Night', value: 2 }]"
                  density="compact"
                  variant="outlined"
                  hide-details
                  style="width: 200px;" />
      </div>
    </v-toolbar>

    <div style="flex: 1; overflow-y: auto;">
      <v-data-table-virtual :headers="productHeaders"
                            style="height: 67vh;"
                            fixed-header
                            :items="dailyReport"
                            :loading="loading"
                            loading-text="Loading"
                            density="compact"
                            class="elevation-1 fixed-table">
        <template v-slot:headers="{ columns }">
          <tr>
            <th v-for="column in columns"
                :key="column.key"
                class="text-center font-weight-bold text-caption py-1"
                :style="{ backgroundColor: column.backgroundColor, width: column.width }">
              {{ column.title }}
            </th>
          </tr>
        </template>
        <template v-slot:item="{ item, columns }">
          <tr>
            <td v-for="column in columns"
                :key="column.key"
                class="text-center text-caption py-0"
                :style="{ backgroundColor: getColumnColor(column.key), color: 'black' }">
              <v-text-field v-if="isEditable(column.key)"
                            v-model="item[column.key]"
                            hide-details
                            variant="plain"
                            density="compact"
                            :style="{ minWidth: column.minWidth }"
                            @update:modelValue="onFieldChange(item, column.key)"
                            @blur="onFieldBlur(item, column.key)" />
              <span v-else>{{ item[column.key] }}</span>
            </td>
          </tr>
        </template>
      </v-data-table-virtual>

      <div class="pa-1">
        <v-btn color="success" @click="saveReport" size="small" class="mr-2">
          <v-icon size="small">mdi-content-save</v-icon>
          Save
        </v-btn>
        <v-btn color="warning" @click="exportProducts" size="small" class="mr-2">
          <v-icon size="small">mdi-download</v-icon>
          Export
        </v-btn>
        <v-btn color="info" @click="triggerImport" size="small" :loading="importing">
          <v-icon size="small">mdi-upload</v-icon>
          Import
        </v-btn>
        <input ref="csvFileInput"
               type="file"
               accept=".csv"
               style="display: none;"
               @change="onCsvFileSelected" />
      </div>
    </div>
    <v-snackbar v-model="snackbar.show"
                :color="snackbar.color"
                :timeout="3000"
                location="bottom right">
      {{ snackbar.message }}
      <template v-slot:actions>
        <v-btn variant="text" @click="snackbar.show = false">Close</v-btn>
      </template>
    </v-snackbar>
  </div>
</template>

<script setup>
  import { ref, onMounted, computed, watch } from 'vue';
  import { pinia } from '@/store';

  const { SAPData } = defineProps({
    SAPData: {
      type: Array,
      required: true,
      default: () => []
    }
  });

  const snackbar = ref({
    show: false,
    message: '',
    color: 'success'
  });

  const store = pinia();
  const dailyReport = ref([]);
  const loading = ref(false);
  const importing = ref(false);
  const csvFileInput = ref(null);

  const productHeaders = [
    { title: 'M/C No', key: 'machine_name', width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'Shift', key: 'shift', width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'Packer Name', key: 'packer', width: '200px', backgroundColor: '#8EA9DB' },
    { title: 'Material', key: 'material', width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'SAP', key: 'id_type', width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'Mould', key: 'mould', width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'Type', key: 'type', width: '400px', backgroundColor: '#8EA9DB' },
    { title: 'JO No. (Prod. Order No.)', key: 'jo_no', width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'Cav', key: 'qty_perct', width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'Gross Weight (gm)', key: 'gross_weight', width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'Net Weight (gm)', key: 'part_weight', width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'Shot', key: 'shot_accum', width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'Qty Order (pcs)', key: 'qty_order', width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'WIP Opening (pcs)', key: 'wip_opening', width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'WIP Closing (pcs)', key: 'wip_closing', width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'Shift Output', key: 'shift_output', width: '150px', backgroundColor: '#B1A0C7' },
    { title: 'Finish Good (Inward - pcs) To Warehouse', key: 'finish_good', width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'Inward to Warehouse (kg)', key: 'inward', width: '150px', backgroundColor: '#B1A0C7' },
    { title: 'Accumulate Qty Build (pcs)', key: 'qty_accum', width: '160px', backgroundColor: '#8EA9DB' },
    { title: 'Balance Qty (pcs)', key: 'qty_balance', width: '130px', backgroundColor: '#B1A0C7' },
    { title: 'Material Used (kg)', key: 'material_used', width: '130px', backgroundColor: '#B1A0C7' },
    { title: 'Runner (kg)', key: 'runner', width: '100px', backgroundColor: '#B1A0C7' },
    { title: 'Start Up (kg)', key: 'reject_startup', width: '110px', backgroundColor: '#8EA9DB' },
    { title: 'Start Up (%)', key: 'reject_startup_per', width: '110px', backgroundColor: '#B1A0C7' },
    { title: 'Prod. Reject (kg)', key: 'reject_prod', width: '120px', backgroundColor: '#8EA9DB' },
    { title: 'Prod. Reject (%)', key: 'reject_prod_per', width: '120px', backgroundColor: '#B1A0C7' },
    { title: 'Actual CT (s)', key: 'act_ct', width: '110px', backgroundColor: '#8EA9DB' },
    { title: 'Run Hours', key: 'production_running', width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'SAP Target CT (s)', key: 'sap_ct', width: '130px', backgroundColor: '#8EA9DB' },
    { title: 'Mould Set Up Time (hrs) Full Set', key: 'change_full_set', width: '180px', backgroundColor: '#FFFF66' },
    { title: 'Mould Set Up Time (hrs) Half Set', key: 'change_half_set', width: '180px', backgroundColor: '#FFFF66' },
    { title: 'Mould Set Up Time (hrs) Blow Mould', key: 'change_parts', width: '180px', backgroundColor: '#FFFF66' },
    { title: 'M/C DT (hrs) Maintenance', key: 'maintenance_dt', width: '160px', backgroundColor: '#FFFF66' },
    { title: 'M/C DT (hrs) Technician', key: 'technician_dt', width: '160px', backgroundColor: '#FFFF66' },
    { title: 'Idle DT Prod/ QC/ Other', key: 'production_dt', width: '160px', backgroundColor: '#FFFF66' },
    { title: 'Remark', key: 'remark', width: '400px', backgroundColor: '#FFFF66' },
    { title: 'Part Scrap', key: 'part_scrap', width: '100px', backgroundColor: '#FFFF66' },
    { title: 'Purging', key: 'reject_purging', width: '100px', backgroundColor: '#FFFF66' },
    { title: 'Preform', key: 'reject_preform', width: '100px', backgroundColor: '#FFFF66' },
    { title: 'Prod Reject (pcs)', key: 'reject_prod_pcs', width: '130px', backgroundColor: '#B1A0C7' }
  ];

  const CALC_TRIGGER_KEYS = new Set([
    'shot_accum', 'qty_perct', 'part_weight', 'gross_weight',
    'finish_good', 'qty_order', 'qty_accum',
    'reject_startup', 'reject_prod', 'reject_purging', 'reject_preform'
  ]);

  const FLOAT_KEYS = new Set([
    'gross_weight', 'part_weight', 'act_ct', 'sap_ct',
    'reject_startup', 'reject_prod', 'reject_purging', 'reject_preform',
    'part_scrap', 'production_running'
  ]);

  const NON_EDITABLE_KEYS = new Set([
    'shift_output', 'inward', 'qty_balance', 'material_used', 'runner',
    'reject_startup_per', 'reject_prod_per', 'production_running',
    'change_full_set', 'change_half_set', 'change_parts',
    'maintenance_dt', 'technician_dt', 'production_dt', 'reject_prod_pcs'
  ]);

  const YELLOW_KEYS = new Set([
    'change_full_set', 'change_half_set', 'change_parts',
    'maintenance_dt', 'technician_dt', 'production_dt',
    'remark', 'part_scrap', 'reject_purging', 'reject_preform'
  ]);

  const INT_FIELDS = new Set([
    'id_machine', 'id_type', 'mould', 'qty_perct', 'shot_accum', 'qty_order', 'wip_opening',
    'wip_closing', 'finish_good', 'qty_accum', 'qty_balance', 'shift_output',
    'reject_total_pcs'
  ]);

  const FLOAT_FIELDS = new Set([
    'gross_weight', 'part_weight', 'inward', 'material_used', 'runner',
    'reject_startup', 'reject_startup_per', 'reject_prod', 'reject_prod_per',
    'act_ct', 'production_running', 'sap_ct', 'change_full_set', 'change_half_set',
    'change_parts', 'maintenance_dt', 'technician_dt', 'production_dt',
    'unallocated', 'part_scrap', 'reject_purging',
    'reject_preform', 'reject_prod_pcs'
  ]);

  function sanitizeRow(row) {
    const clean = { ...row };
    for (const key of INT_FIELDS) if (key in clean) clean[key] = parseInt(clean[key]) || 0;
    for (const key of FLOAT_FIELDS) if (key in clean) clean[key] = parseFloat(clean[key]) || 0;

    clean._orig_id_type = parseInt(clean._orig_id_type) || clean.id_type;
    clean._orig_mould = parseInt(clean._orig_mould) ?? clean.mould;
    return clean;
  }

  const production_date = ref(null);
  const shift = ref(null);

  const f = (v, d = 2) => parseFloat((parseFloat(v) || 0).toFixed(d));
  const n = (v) => parseFloat(v) || 0;
  const i = (v) => parseInt(v) || 0;

  function getDate() {
    return new Date().toISOString().split('T')[0];
  }

  function getShift() {
    const h = new Date().getHours();
    return (h >= 6 && h < 18) ? 1 : 2;
  }

  function formatDate(date) {
    if (date instanceof Date) return date.toLocaleDateString('en-CA');
    if (typeof date === 'string') {
      if (/^\d{4}-\d{2}-\d{2}$/.test(date)) return date;
      return new Date(date).toLocaleDateString('en-CA');
    }
    return null;
  }

  function recalcRow(item) {
    const shot = i(item.shot_accum);
    const cav = i(item.qty_perct);
    const partWt = n(item.part_weight);
    const grossWt = n(item.gross_weight);
    const finishGood = i(item.finish_good);
    const startup = n(item.reject_startup);
    const rejProd = n(item.reject_prod);
    const purging = n(item.reject_purging);
    const preform = n(item.reject_preform);

    const shiftOutput = shot * cav;
    const materialUsed = f((partWt * shiftOutput) / 1000);

    item.shift_output = shiftOutput;
    item.qty_balance = i(item.qty_order) - i(item.qty_accum);
    item.inward = f((partWt * finishGood) / 1000);
    item.material_used = materialUsed;
    item.runner = f(((grossWt - partWt) * shiftOutput) / 1000);
    item.reject_startup_per = f(materialUsed ? (startup / materialUsed) * 100 : 0);
    item.reject_prod_per = f(materialUsed ? (rejProd / materialUsed) * 100 : 0);
    item.reject_prod_pcs = f(partWt ? (startup + rejProd + purging + preform) / partWt : 0);
  }

  const maxDate = computed(() => getDate());
  const isEditable = (key) => !NON_EDITABLE_KEYS.has(key);
  const getColumnColor = (key) => YELLOW_KEYS.has(key) ? '#FFFF99' : 'transparent';

  onMounted(async () => {
    production_date.value = getDate();
    shift.value = getShift();
    await loadReportData();
  });

  watch([production_date, shift], ([date, s]) => {
    if (date && s) loadReportData();
  });

  function onFieldChange(item, key) {
    if (key === 'id_type' || key === 'mould') {
      const match = SAPData.find(x =>
        String(x.id_type) === String(item.id_type) &&
        String(x.mould) === String(item.mould)
      );
      if (match) {
        const target = dailyReport.value.find(r => r === item);
        if (target) {
          target.material = match.material ?? target.material;
          target.type = match.type ?? target.type;
          target.qty_perct = match.qty_perct ?? target.qty_perct;
          target.gross_weight = match.gross_weight ?? target.gross_weight;
          target.part_weight = match.part_weight ?? target.part_weight;
          target.sap_ct = match.sap_ct ?? target.sap_ct;
          recalcRow(target);
        }
      }
      return;
    }

    if (CALC_TRIGGER_KEYS.has(key)) {
      recalcRow(item);
    }
  }

  function onFieldBlur(item, key) {
    if (FLOAT_KEYS.has(key)) {
      item[key] = f(item[key]);
    }
  }

  async function loadReportData() {
    if (!production_date.value || !shift.value) return;

    loading.value = true;

    try {
      const selectedDate = formatDate(production_date.value);
      const isCurrentShift = selectedDate === formatDate(getDate()) && shift.value === getShift();
      const loader = isCurrentShift ? store.loadDailyReport : store.loadPrevReport;
      const result = await loader(selectedDate, shift.value);

      dailyReport.value = result.map(row => ({
        ...row,
        _orig_id_type: row.id_type,
        _orig_mould: row.mould
      }));
    } catch (err) {
      console.error('Error loading report data:', err);
      dailyReport.value = [];
    } finally {
      loading.value = false;
    }
  }

  async function saveReport() {
    if (!production_date.value || !shift.value) return;
    loading.value = true;
    try {
      const selectedDate = formatDate(production_date.value);
      const isCurrentShift = selectedDate === formatDate(getDate()) && shift.value === getShift();
      const endpoint = isCurrentShift ? '/api/MachineLog/DailyReport' : '/api/MachineLog/PrevReport';

      const response = await fetch(endpoint, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          production_date: selectedDate,
          shift: shift.value,
          reportList: dailyReport.value.map(sanitizeRow)
        })
      });

      if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
      const updated = await response.json();
      dailyReport.value = updated.map(row => ({
        ...row,
        _orig_id_type: row.id_type,
        _orig_mould: row.mould
      }))
    } catch (err) {
      console.error('Save error:', err);
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function exportProducts() {
    loading.value = true;
    try {
      loading.value = true;
    
      const selectedDate = formatDate(production_date.value);
      const response = await fetch(
        `/api/MachineLog/ExportReport?production_date=${selectedDate}&shift=${shift.value}`,
        { method: 'GET' }
      );
    
      if (!response.ok) throw new Error('Export failed');
    
      const url = window.URL.createObjectURL(await response.blob());
      const link = Object.assign(document.createElement('a'), {
        href: url,
        download: `DailyReport_${selectedDate}_Shift${shift.value}.xlsx`
      });
      document.body.appendChild(link);
      link.click();
      setTimeout(() => { document.body.removeChild(link); window.URL.revokeObjectURL(url); }, 100);
    } catch (err) {
      console.error('Export error:', err);
    } finally {
      loading.value = false;
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
    if (lines.length < 2) return [];
    
    const headers = lines[0].split(',').map(h => h.trim());
    
    return lines.slice(1).map(line => {
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

      const row = {};
      headers.forEach((h, idx) => {
        row[h] = values[idx] ?? '';
      });
      return row;
    }).filter(row => Object.values(row).some(v => v !== ''));
  }

  const CSV_INT_FIELDS = new Set([
    'id_machine', 'id_type', 'mould', 'shift',
    'qty_perct',
    'shot',
    'qty_order', 'wip_opening', 'wip_closing',
    'finish_good', 'qty_accum', 'reject_total_pcs'
  ])

  const CSV_FLOAT_FIELDS = new Set([
    'gross_weight', 'part_weight',
    'reject_startup', 'reject_prod',
    'act_ct', 'production_running', 'sap_ct',
    'change_full_set', 'change_half_set', 'change_parts',
    'maintenance_dt', 'technician_dt', 'production_dt', 'unallocated',
    'reject_purging', 'reject_preform'
  ])

  function sanitiseCsvRow(row) {
    const clean = {}
    for (const [rawKey, rawVal] of Object.entries(row)) {
      const key = rawKey

      const strVal = (rawVal == null || String(rawVal).trim() === '-' || String(rawVal).trim() === '')
        ? ''
        : String(rawVal).trim()

      const numStr = strVal.replace(/^(-?\d{1,3})(,\d{3})+(\.\d+)?$/, m => m.replace(/,/g, ''))

      if (CSV_INT_FIELDS.has(key)) {
        clean[key] = parseInt(numStr) || 0
      } else if (CSV_FLOAT_FIELDS.has(key)) {
        clean[key] = parseFloat(numStr) || 0
      } else {
        clean[key] = strVal
      }
    }
    return clean
  }

  async function onCsvFileSelected(event) {
    const file = event.target.files?.[0];
    if (!file) return;

    importing.value = true;
    try {
      const text = await file.text();
      const rawRows = parseCsv(text);

      if (!rawRows.length) {
        showSnackbar('CSV file is empty or could not be parsed.', 'error');
        return;
  }

      const rows = rawRows.map(sanitiseCsvRow)

      const response = await fetch('/api/MachineLog/ImportReport', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reportList: rows })
      });

      if (!response.ok) {
        const err = await response.json().catch(() => ({}));
        throw new Error(err.message || `HTTP error! status: ${response.status}`);
    }

      const updated = await response.json();

      dailyReport.value = updated.map(row => ({
        ...row,
        _orig_id_type: row.id_type,
        _orig_mould: row.mould
      }));

      showSnackbar(`Import successful — ${updated.length} row(s) updated.`, 'success');
    } catch (err) {
      console.error('Import error:', err);
      showSnackbar(`Import failed: ${err.message}`, 'error');
    } finally {
      importing.value = false;
      }
      const parsedDate = new Date(date);
      return parsedDate.toLocaleDateString('en-CA');
    }

  function showSnackbar(message, color = 'success') {
    snackbar.value = { show: true, message, color };
  }
</script>

<style scoped>
  .fixed-table :deep(table) {
    table-layout: fixed !important;
  }

  .fixed-table table th:first-child,
  .fixed-table table td:first-child {
    position: sticky;
    left: 0;
    z-index: 1;
    background: #8EA9DB !important;
  }
</style>
