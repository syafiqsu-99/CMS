<template>
  <v-card elevation="2" class="overflow-hidden w-100">
    <div class="ml-scroll">
      <table class="ml-table">
        <thead>
          <tr class="group-row">
            <th class="sticky-col grp-blank" rowspan="2">Machine</th>
            <th :colspan="kpiCols.length" class="grp grp-kpi">OEE Metrics (%)</th>
            <th :colspan="coreCols.length" class="grp grp-core">Time &amp; Material</th>
            <th :colspan="unplannedCols.length" class="grp grp-unplanned">Unplanned Downtime Breakdown (hrs)</th>
          </tr>
          <tr class="sub-row">
            <th v-for="c in kpiCols" :key="c.key" class="sub sub-kpi">{{ c.title }}</th>
            <th v-for="c in coreCols" :key="c.key" class="sub sub-core">
              <span :title="c.tip">{{ c.title }}</span>
            </th>
            <th v-for="c in unplannedCols" :key="c.key" class="sub sub-unplanned">
              <span :title="c.tip">{{ c.title }}</span>
            </th>
          </tr>
        </thead>

        <tbody>
          <tr v-for="m in machines" :key="m.id_machine" class="body-row" @click="$emit('select', m)">
            <td class="sticky-col name-cell">{{ m.machine_name }}</td>
            <td v-for="c in kpiCols" :key="c.key" class="num kpi-cell">
              <span :style="{ color: kpiColor(c.key, m[c.key]), fontWeight: 600 }">{{ fmtInt(m[c.key]) }}%</span>
            </td>
            <td v-for="c in coreCols" :key="c.key" class="num core-cell">{{ fmt2(m[c.key]) }}</td>
            <td v-for="c in unplannedCols" :key="c.key" class="num unplanned-cell">{{ fmt2(m[c.key]) }}</td>
          </tr>
        </tbody>

        <tfoot>
          <tr class="total-row">
            <td class="sticky-col name-cell">TOTAL</td>
            <td v-for="c in kpiCols" :key="c.key" class="num kpi-cell">
              <span :style="{ color: kpiColor(c.key, plantTotals[c.key]) }">{{ plantTotals[c.key] }}%</span>
            </td>
            <td v-for="c in coreCols" :key="c.key" class="num core-cell">{{ totals[c.key] }}</td>
            <td v-for="c in unplannedCols" :key="c.key" class="num unplanned-cell">{{ totals[c.key] }}</td>
          </tr>
        </tfoot>
      </table>
    </div>
  </v-card>
</template>

<script setup>
import { computed } from 'vue';

const props = defineProps({
  machines: { type: Array, required: true },
});

defineEmits(['select']);

const TARGETS = { oee: 65, performance: 95, availability: 70, quality: 97 };

const kpiCols = [
  { key: 'oee', title: 'OEE' },
  { key: 'performance', title: 'Perf' },
  { key: 'availability', title: 'Avail' },
  { key: 'quality', title: 'Quality' },
];

const coreCols = [
  { key: 'run_time', title: 'Run Time', tip: 'Total time in PRODUCTION RUNNING state' },
  { key: 'unplanned_dt', title: 'Unplanned D/T', tip: 'Sum of all unplanned downtime buckets (right)' },
  { key: 'planned_dt', title: 'Planned D/T', tip: 'NO SCHEDULE + SCHEDULED MAINTENANCE' },
  { key: 'operating_time', title: 'Operating', tip: 'Operating Time = Run Time + Unplanned Downtime' },
  { key: 'available_time', title: 'Avail Hour', tip: 'Run + Down hours' },
  { key: 'material_used', title: 'Material (kg)', tip: 'Total material consumed' },
  { key: 'reject_weight', title: 'Reject (kg)', tip: 'Total reject weight' },
];

const unplannedCols = [
  { key: 'change_full_set', title: 'Full Set', tip: 'MOULD CHANGE — full set' },
  { key: 'change_half_set', title: 'Half Set', tip: 'MOULD CHANGE — half set' },
  { key: 'change_parts', title: 'Parts', tip: 'MOULD CHANGE — parts' },
  { key: 'maintenance_dt', title: 'Maintenance', tip: 'MACHINE BREAKDOWN + OTHERS MAIN' },
  { key: 'technician_dt', title: 'Technician', tip: 'QUALITY ISSUE + SAMPLE RUNNING + OTHERS TECH' },
  { key: 'production_dt', title: 'Production', tip: 'NO OPERATOR + MATERIAL DRYING + OTHERS PROD' },
  { key: 'buyoff_dt', title: 'Buyoff', tip: 'PRODUCT BUYOFF' },
];

const numericCols = [...coreCols, ...unplannedCols].map(c => c.key);

const fmtInt = (v) => Number(v || 0).toFixed(0);
const fmt2 = (v) => Number(v || 0).toFixed(2);

function kpiColor(type, value) {
  return Number(value) >= TARGETS[type] ? '#4caf50' : '#f44336';
}

const sum = (key) => props.machines.reduce((s, r) => s + (Number(r[key]) || 0), 0);

const plantTotals = computed(() => {
  const run = sum('run_time');
  const operating = sum('operating_time');
  const sap = sum('total_sap_time');
  const act = sum('total_actual_time');
  const mat = sum('material_used');
  const rej = sum('reject_weight');

  const availability = operating > 0 ? (run / operating) * 100 : 0;
  const performance = act > 0 ? (sap / act) * 100 : 0;
  const quality = mat > 0 ? Math.max(0, ((mat - rej) / mat) * 100) : 0;
  const oee = (availability * performance * quality) / 10000;

  return {
    oee: oee.toFixed(0),
    performance: performance.toFixed(0),
    availability: availability.toFixed(0),
    quality: quality.toFixed(0),
  };
});

const totals = computed(() =>
  Object.fromEntries(numericCols.map(key => [key, sum(key).toFixed(2)]))
);
</script>

<style scoped>
.ml-scroll {
  max-height: 55vh;
  overflow: auto;
}

.ml-table {
  border-collapse: separate;
  border-spacing: 0;
  width: 100%;
  font-size: 0.8rem;
  white-space: nowrap;
}

.ml-table th,
.ml-table td {
  padding: 6px 10px;
  border-bottom: 1px solid #e0e0e0;
}

.ml-table thead th {
  position: sticky;
  top: 0;
  z-index: 3;
}

.ml-table .sub-row th {
  top: 34px;
  z-index: 3;
}

.num {
  text-align: center;
}

.sticky-col {
  position: sticky;
  left: 0;
  z-index: 4;
  background: #fff;
  text-align: left;
  box-shadow: 2px 0 3px -1px rgba(0, 0, 0, 0.15);
}

thead .sticky-col {
  z-index: 6;
}

.name-cell {
  font-weight: 600;
}

.grp {
  text-align: center;
  font-weight: 700;
  color: #fff;
}

.grp-blank {
  background: #eceff1;
  vertical-align: middle;
}

.grp-kpi {
  background: #37474f;
}

.grp-core {
  background: #1a3a6b;
}

.grp-unplanned {
  background: #6a1b9a;
}

.sub {
  text-align: center;
  font-weight: 600;
}

.sub-kpi {
  background: #cfd8dc;
}

.sub-core {
  background: #d6e0f0;
}

.sub-unplanned {
  background: #e6d6f0;
}

.kpi-cell {
  background: #fafafa;
}

.core-cell {
  background: #f4f7fc;
}

.unplanned-cell {
  background: #f9f4fc;
}

.body-row {
  cursor: pointer;
}

.body-row:hover td {
  background: #eef4ff;
}

.total-row td {
  background: #f5f5f5;
  font-weight: 700;
  position: sticky;
  bottom: 0;
  z-index: 2;
}

.total-row .sticky-col {
  z-index: 5;
  background: #f5f5f5;
}
</style>