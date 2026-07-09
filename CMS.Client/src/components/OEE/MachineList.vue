<template>
  <v-card elevation="2" class="overflow-hidden w-100">
    <v-data-table-virtual :headers="tableHeaders"
                          :items="machines"
                          fixed-header
                          density="compact"
                          hover
                          style="height: 50vh;"
                          @click:row="(_, { item }) => $emit('select', item)">
      <template v-for="kpi in kpiCols" #[`item.${kpi}`]="{ item }" :key="kpi">
        <span :style="{ color: kpiColor(kpi, item[kpi]), fontWeight: '600' }">
          {{ item[kpi] }}%
        </span>
      </template>
    </v-data-table-virtual>

    <v-divider />
    <v-table density="compact" class="totals-footer">
      <tbody>
        <tr style="background-color: #f5f5f5; font-weight: 700; cursor: pointer;"
            title="Click for detailed breakdown"
            @click="detailDialog = true">
          <td style="width: 32%">
            TOTAL
            <v-icon size="small" class="ml-1" color="grey-darken-1">mdi-magnify-plus-outline</v-icon>
          </td>
          <td v-for="kpi in kpiCols" :key="kpi" style="width: 17%; text-align: center">
            <span :style="{ color: kpiColor(kpi, plantTotals[kpi]) }">{{ plantTotals[kpi] }}%</span>
          </td>
        </tr>
      </tbody>
    </v-table>
  </v-card>

  <v-dialog v-model="detailDialog" max-width="1600">
    <v-card>
      <v-card-title class="bg-primary text-white d-flex justify-space-between align-center">
        <span>Detailed Breakdown</span>
        <v-btn icon variant="text" @click="detailDialog = false">
          <v-icon color="white">mdi-close</v-icon>
        </v-btn>
      </v-card-title>

      <v-card-text class="pa-0">
        <div class="detail-scroll">
          <table class="detail-table">
            <thead>
              <!-- Group header row -->
              <tr class="group-row">
                <th class="sticky-col group-blank" rowspan="2">Machine</th>
                <th v-for="c in coreCols" :key="c.key" rowspan="2" class="core-th">
                  <span :title="c.tip">{{ c.title }}<v-icon v-if="c.tip" size="x-small" class="ml-1">mdi-information-outline</v-icon></span>
                </th>
                <th :colspan="unplannedCols.length" class="grp-unplanned">
                  Unplanned Downtime Breakdown (hrs)
                </th>
              </tr>
              <!-- Sub header row -->
              <tr class="sub-row">
                <th v-for="c in unplannedCols" :key="c.key" class="grp-unplanned-sub">
                  <span :title="c.tip">{{ c.title }}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="m in machines" :key="m.id_machine">
                <td class="sticky-col name-cell">{{ m.machine_name }}</td>
                <td v-for="c in coreCols" :key="c.key" class="num">
                  {{ Number(m[c.key] || 0).toFixed(2) }}
                </td>
                <td v-for="c in unplannedCols" :key="c.key" class="num unplanned-cell">
                  {{ Number(m[c.key] || 0).toFixed(2) }}
                </td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="total-row">
                <td class="sticky-col name-cell">TOTAL</td>
                <td v-for="c in coreCols" :key="c.key" class="num">{{ totals[c.key] }}</td>
                <td v-for="c in unplannedCols" :key="c.key" class="num unplanned-cell">{{ totals[c.key] }}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { ref, computed } from 'vue';

  const props = defineProps({
    machines: { type: Array, required: true },
  });

  defineEmits(['select']);

  const detailDialog = ref(false);

  // ── Main table ────────────────────────────────────────────────────────────────

  const tableHeaders = [
    { title: 'Machine', key: 'machine_name', width: '32%' },
    { title: 'OEE (%)', key: 'oee', width: '17%', align: 'center' },
    { title: 'Performance (%)', key: 'performance', width: '17%', align: 'center' },
    { title: 'Availability (%)', key: 'availability', width: '17%', align: 'center' },
    { title: 'Quality (%)', key: 'quality', width: '17%', align: 'center' },
  ];

  const kpiCols = ['oee', 'performance', 'availability', 'quality'];
  const TARGETS = { oee: 65, performance: 95, availability: 70, quality: 97 };

  function kpiColor(type, value) {
    return Number(value) >= TARGETS[type] ? '#4caf50' : '#f44336';
  }

  // ── Detail dialog columns ─────────────────────────────────────────────────────

  const coreCols = [
    { key: 'run_time', title: 'Run Time (hrs)', tip: 'Total time in PRODUCTION RUNNING state' },
    { key: 'unplanned_dt', title: 'Unplanned D/T (hrs)', tip: 'Sum of all unplanned downtime buckets (right)' },
    { key: 'planned_dt', title: 'Planned D/T (hrs)', tip: 'NO SCHEDULE + SCHEDULED MAINTENANCE' },
    { key: 'operating_time', title: 'Operating (hrs)', tip: 'Operating Time = Run Time + Unplanned Downtime' },
    { key: 'material_used', title: 'Material Used (kg)', tip: 'Total material consumed' },
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

  const detailCols = [...coreCols, ...unplannedCols].map(c => c.key);

  // ── Totals ────────────────────────────────────────────────────────────────────

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
    Object.fromEntries(detailCols.map(key => [key, sum(key).toFixed(2)]))
  );
</script>

<style scoped>
  .detail-scroll {
    max-height: 62vh;
    overflow: auto;
  }

  .detail-table {
    border-collapse: separate;
    border-spacing: 0;
    width: 100%;
    font-size: 0.8rem;
    white-space: nowrap;
  }

    .detail-table th,
    .detail-table td {
      padding: 6px 10px;
      border-bottom: 1px solid #e0e0e0;
    }

    .detail-table thead th {
      position: sticky;
      top: 0;
      z-index: 3;
      background: #fff;
    }

    .detail-table .sub-row th {
      top: 34px;
      z-index: 3;
    }

  .num {
    text-align: center;
  }

  /* Frozen machine column */
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

  /* Core (non-grouped) headers */
  .core-th {
    background: #eceff1;
    vertical-align: middle;
    text-align: center;
  }

  .group-blank {
    background: #eceff1;
    vertical-align: middle;
  }

  /* Unplanned downtime group colouring */
  .grp-unplanned {
    background: #1a3a6b;
    color: #fff;
    text-align: center;
    font-weight: 600;
  }

  .grp-unplanned-sub {
    background: #d6e0f0;
    text-align: center;
    font-weight: 600;
  }

  .unplanned-cell {
    background: #f2f6fc;
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
