<template>
  <v-card elevation="2" class="overflow-hidden w-100">
    <v-data-table-virtual :headers="tableHeaders"
                          :items="machines"
                          fixed-header
                          density="compact"
                          hover
                          style="height: 50vh;"
                          @click:row="(_, { item }) => $emit('select', item)">
      <!-- Numeric columns — 2 dp -->
      <template v-for="col in numericCols" #[`item.${col}`]="{ item }" :key="col">
        {{ Number(item[col]).toFixed(2) }}
      </template>

      <!-- Colour-coded KPI columns -->
      <template v-for="kpi in kpiCols" #[`item.${kpi}`]="{ item }" :key="kpi">
        <span :style="{ color: kpiColor(kpi, item[kpi]), fontWeight: '600' }">
          {{ item[kpi] }}%
        </span>
      </template>
    </v-data-table-virtual>

    <!-- Totals footer -->
    <v-divider />
    <v-table density="compact" class="totals-footer">
      <tbody>
        <tr style="background-color: #f5f5f5; font-weight: 700;">
          <td style="width: 18%">TOTAL</td>
          <td v-for="n in 4" :key="n" style="width: 8%; text-align: center" />
          <td style="width: 10%; text-align: center">{{ totals.run_time }}</td>
          <td style="width: 10%; text-align: center">{{ totals.down_time }}</td>
          <td style="width: 10%; text-align: center">{{ totals.material_used }}</td>
          <td style="width: 10%; text-align: center">{{ totals.reject_weight }}</td>
        </tr>
      </tbody>
    </v-table>
  </v-card>
</template>

<script setup>
import { computed } from 'vue';

// ── Props / emits ─────────────────────────────────────────────────────────────

const props = defineProps({
  machines: { type: Array, required: true },
});

defineEmits(['select']);

// ── Table definition ──────────────────────────────────────────────────────────

const tableHeaders = [
  { title: 'Machine',           key: 'machine_name',  width: '18%' },
  { title: 'OEE (%)',           key: 'oee',           width: '8%',  align: 'center' },
  { title: 'Performance (%)',   key: 'performance',   width: '8%',  align: 'center' },
  { title: 'Availability (%)',  key: 'availability',  width: '8%',  align: 'center' },
  { title: 'Quality (%)',       key: 'quality',       width: '8%',  align: 'center' },
  { title: 'Run Time (hrs)',    key: 'run_time',      width: '10%', align: 'center' },
  { title: 'Down Time (hrs)',   key: 'down_time',     width: '10%', align: 'center' },
  { title: 'Material Used (kg)',key: 'material_used', width: '10%', align: 'center' },
  { title: 'Reject (kg)',       key: 'reject_weight', width: '10%', align: 'center' },
];

const numericCols = ['run_time', 'down_time', 'material_used', 'reject_weight'];
const kpiCols     = ['oee', 'performance', 'availability', 'quality'];

const TARGETS = { oee: 65, performance: 95, availability: 70, quality: 97 };

function kpiColor(type, value) {
  return Number(value) >= TARGETS[type] ? '#4caf50' : '#f44336';
}

// ── Totals row ────────────────────────────────────────────────────────────────

const totals = computed(() => {
  const sum = (key) => props.machines.reduce((s, r) => s + (Number(r[key]) || 0), 0);
  return {
    run_time:      sum('run_time').toFixed(2),
    down_time:     sum('down_time').toFixed(2),
    material_used: sum('material_used').toFixed(2),
    reject_weight: sum('reject_weight').toFixed(2),
  };
});
</script>
