<template>
  <div style="display: flex; flex-direction: column; overflow: hidden;">
    <v-toolbar flat density="compact" class="px-2">
      <v-icon color="primary" size="small">mdi-database</v-icon>
      <v-toolbar-title class="text-subtitle-1 ml-2">
        Daily Production Report
      </v-toolbar-title>
      <v-spacer></v-spacer>
      <div class="d-flex ga-2">
        <v-date-input clearable
                      v-model="production_date"
                      label="Production Date"
                      density="compact"
                      :max="maxDate"
                      variant="outlined"
                      hide-details
                      style="width: 180px;" />
        <v-select label="Shift"
                  v-model="shift"
                  :items="[{ title: 'Morning', value: 1 }, { title: 'Night', value: 2 }]"
                  density="compact"
                  variant="outlined"
                  hide-details
                  style="width: 130px;" />
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
                :style="{ backgroundColor: column.backgroundColor, width: column.width}">
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
                            :style="{ minWidth: column.minWidth }" />
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
        <v-btn color="warning" @click="exportProducts" size="small">
          <v-icon size="small">mdi-download</v-icon>
          Export
        </v-btn>
      </div>
    </div>
  </div>
</template>

<script setup>
  import { ref, onMounted, onUnmounted, computed, watch, nextTick } from 'vue';
  import { pinia } from '@/store'
  import * as XLSX from 'xlsx';

  const store = pinia();
  const dailyReport = ref([]);
  const loading = ref(false);
  const productHeaders = [
    { title: 'M/C No'                                 , key: 'machine_name'       , width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'Shift'                                  , key: 'shift'              , width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'Packer Name'                            , key: 'packer'             , width: '200px', backgroundColor: '#8EA9DB' },
    { title: 'Material'                               , key: 'material'           , width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'SAP'                                    , key: 'id_type'            , width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'Mould'                                  , key: 'mould'              , width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'Type'                                   , key: 'type'               , width: '400px', backgroundColor: '#8EA9DB' },
    { title: 'JO No. (Prod. Order No.)'               , key: 'jo_no'              , width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'Cav'                                    , key: 'qty_perct'          , width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'Gross Weight (gm)'                      , key: 'gross_weight'       , width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'Net Weight (gm)'                        , key: 'part_weight'        , width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'Shot'                                   , key: 'shot_accum'         , width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'Qty Order (pcs)'                        , key: 'qty_order'          , width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'WIP Opening (pcs)'                      , key: 'wip_opening'        , width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'WIP Closing (pcs)'                      , key: 'wip_closing'        , width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'Shift Output'                           , key: 'shift_output'       , width: '150px', backgroundColor: '#B1A0C7' },
    { title: 'Finish Good (Inward - pcs) To Warehouse', key: 'finish_good'        , width: '150px', backgroundColor: '#8EA9DB' },
    { title: 'Inward to Warehouse (kg)'               , key: 'inward'             , width: '150px', backgroundColor: '#B1A0C7' },
    { title: 'Accumulate Qty Build (pcs)'             , key: 'qty_accum'          , width: '160px', backgroundColor: '#8EA9DB' },
    { title: 'Balance Qty (pcs)'                      , key: 'qty_balance'        , width: '130px', backgroundColor: '#B1A0C7' },
    { title: 'Material Used (kg)'                     , key: 'material_used'      , width: '130px', backgroundColor: '#B1A0C7' },
    { title: 'Runner (kg)'                            , key: 'runner'             , width: '100px', backgroundColor: '#B1A0C7' },
    { title: 'Start Up (kg)'                          , key: 'reject_startup'     , width: '110px', backgroundColor: '#8EA9DB' },
    { title: 'Start Up (%)'                           , key: 'reject_startup_per' , width: '110px', backgroundColor: '#B1A0C7' },
    { title: 'Prod. Reject (kg)'                      , key: 'reject_prod'        , width: '120px', backgroundColor: '#8EA9DB' },
    { title: 'Prod. Reject (%)'                       , key: 'reject_prod_per'    , width: '120px', backgroundColor: '#B1A0C7' },
    { title: 'Actual CT (s)'                          , key: 'act_ct'             , width: '110px', backgroundColor: '#8EA9DB' },
    { title: 'Run Hours'                              , key: 'production_running' , width: '100px', backgroundColor: '#8EA9DB' },
    { title: 'SAP Target CT (s)'                      , key: 'sap_ct'             , width: '130px', backgroundColor: '#8EA9DB' },
    { title: 'Mould Set Up Time (hrs) Full Set'       , key: 'change_full_set'    , width: '180px', backgroundColor: '#FFFF66' },
    { title: 'Mould Set Up Time (hrs) Half Set'       , key: 'change_half_set'    , width: '180px', backgroundColor: '#FFFF66' },
    { title: 'Mould Set Up Time (hrs) Blow Mould'     , key: 'change_parts'       , width: '180px', backgroundColor: '#FFFF66' },
    { title: 'M/C DT (hrs) Maintenance'               , key: 'maintenance_dt'     , width: '160px', backgroundColor: '#FFFF66' },
    { title: 'M/C DT (hrs) Technician'                , key: 'technician_dt'      , width: '160px', backgroundColor: '#FFFF66' },
    { title: 'Idle DT Prod/ QC/ Other'                , key: 'production_dt'      , width: '160px', backgroundColor: '#FFFF66' },
    { title: 'Remark'                                 , key: 'remark'             , width: '400px', backgroundColor: '#FFFF66' },
    { title: 'Part Scrap'                             , key: 'part_scrap'         , width: '100px', backgroundColor: '#FFFF66' },
    { title: 'Purging'                                , key: 'reject_purging'     , width: '100px', backgroundColor: '#FFFF66' },
    { title: 'Preform'                                , key: 'reject_preform'     , width: '100px', backgroundColor: '#FFFF66' },
    { title: 'Prod Reject (pcs)'                      , key: 'reject_prod_pcs'    , width: '130px', backgroundColor: '#B1A0C7' }
  ];
  const production_date = ref(null);
  const shift = ref(null);

  onMounted(async () => {
    production_date.value = getDate();
    shift.value = getShift();
    await loadReportData();
  });

  watch([production_date, shift], () => {
    if (production_date.value && shift.value) {
      loadReportData();
    }
  });

  watch(dailyReport, (rows) => {
    rows.forEach(item => {
      item.shift_output = item.shot_accum * item.qty_perct;
      item.qty_balance = item.qty_order - item.qty_accum;
      item.inward = +((item.part_weight * item.finish_good) / 1000).toFixed(2);
      item.material_used = +((item.part_weight * item.shift_output) / 1000).toFixed(2);
      item.runner = +(((item.gross_weight - item.part_weight) * item.shift_output) / 1000).toFixed(2);
      item.sap_ct = +(item.sap_ct || 0).toFixed(2);
      item.act_ct = +(item.act_ct || 0).toFixed(2);
      item.reject_startup = +(item.reject_startup || 0).toFixed(2);
      item.reject_startup_per = +((item.reject_startup / item.material_used) * 100 || 0).toFixed(2);
      item.reject_prod = +(item.reject_prod || 0).toFixed(2);
      item.reject_prod_per = +((item.reject_prod / item.material_used) * 100 || 0).toFixed(2);
      item.reject_purging = +(item.reject_purging || 0).toFixed(2);
      item.reject_preform = +(item.reject_preform || 0).toFixed(2);
      item.reject_prod_pcs = +((item.reject_startup + item.reject_prod + item.reject_purging + item.reject_preform) / item.part_weight || 0).toFixed(2);
    });
  }, { deep: true });

  const maxDate = computed(() => {
    return getDate();
  });

  async function loadReportData() {
    if (!production_date.value || !shift.value) return;

    loading.value = true;

    try {
      const selectedDate = formatDate(production_date.value);
      const todayDate = formatDate(getDate());
      const currentShift = getShift();

      if (selectedDate === todayDate && shift.value === currentShift) {
        const result = await store.loadDailyReport(selectedDate, shift.value);
        dailyReport.value = result;
      }
      else {
        const result = await store.loadPrevReport(selectedDate, shift.value);
        dailyReport.value = result;
      }
    } catch (error) {
      console.error('Error loading report data:', error);
      dailyReport.value = [];
    } finally {
      loading.value = false;
    }
  }

  const isEditable = (key) => {
    const nonEditable = ['shift_output', 'inward', 'qty_balance', 'material_used', 'runner', 'reject_startup_per', 'reject_prod_per', 'production_running', 'sap_ct', 'change_full_set', 'change_half_set', 'change_parts', 'maintenance_dt', 'technician_dt', 'production_dt', 'reject_prod_pcs']
    return !nonEditable.includes(key)
  }

  const getColumnColor = (key) => {
    const map = {
      change_full_set: '#FFFF99',
      change_half_set: '#FFFF99',
      change_parts: '#FFFF99',
      maintenance_dt: '#FFFF99',
      technician_dt: '#FFFF99',
      production_dt: '#FFFF99',
      remark: '#FFFF99',
      part_scrap: '#FFFF99',
      reject_purging: '#FFFF99',
      reject_preform: '#FFFF99',
    }
    return map[key] || 'transparent'
  }

  async function saveReport() {
    if (!production_date.value || !shift.value) return;

    try {
      loading.value = true

      const selectedDate = formatDate(production_date.value);
      const todayDate = formatDate(getDate());
      const currentShift = getShift();

      const payload = {
        production_date: selectedDate,
        shift: shift.value,
        reportList: dailyReport.value
      }

      let response;

      if (selectedDate === todayDate && shift.value === currentShift) {
        response = await fetch('/api/MachineLog/DailyReport', {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(payload)
        });
      } else {
        response = await fetch('/api/MachineLog/PrevReport', {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(payload)
        });
      }

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const updatedData = await response.json();
      dailyReport.value = updatedData;

    } catch (err) {
      console.error('Save error:', err)
      throw err;
    } finally {
      loading.value = false
    }
  }

  async function exportProducts() {
    try {
      loading.value = true;
    
      const selectedDate = formatDate(production_date.value);
      const response = await fetch(
        `/api/MachineLog/ExportReport?productionDate=${selectedDate}&shift=${shift.value}`,
        { method: 'GET' }
      );
    
      if (!response.ok) throw new Error('Export failed');
    
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `DailyReport_${selectedDate}_Shift${shift.value}.xlsx`;
      document.body.appendChild(link);
      link.click();
    
      setTimeout(() => {
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
      }, 100);
    
    } catch (error) {
      console.error('Export error:', error);
    } finally {
      loading.value = false;
    }
  }

  function getShift() {
    const now = new Date();
    const currentHour = now.getHours();
    return (currentHour >= 6 && currentHour < 18) ? 1 : 2;
  }

  function getDate() {
    const today = new Date();
    return today.toISOString().split('T')[0];
  }

  function formatDate(date) {
    if (date instanceof Date) {
      return date.toLocaleDateString('en-CA');
    }
    if (typeof date === 'string') {
      if (/^\d{4}-\d{2}-\d{2}$/.test(date)) {
        return date;
      }
      const parsedDate = new Date(date);
      return parsedDate.toLocaleDateString('en-CA');
    }
    return null;
  }
</script>
<style scoped>
  .fixed-table :deep(table) {
    table-layout: fixed !important;
  }
</style>
