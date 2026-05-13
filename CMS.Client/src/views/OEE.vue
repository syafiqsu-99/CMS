<template>
  <v-container fluid class="pa-2 h-100 d-flex flex-column" style="overflow-y: auto; overflow-x: hidden;">

    <!-- Title -->
    <v-row no-gutters align="center" justify="center" class="mb-1">
      <v-col cols="12" class="text-center">
        <h2 class="font-weight-bold">OEE Dashboard</h2>
      </v-col>
    </v-row>

    <!-- Date Filters -->
    <v-row no-gutters align="center" justify="center" class="mb-2">
      <v-col cols="12" md="4" class="px-1">
        <v-date-input v-model="startDate"
                      label="Start Date"
                      :max="endDate"
                      variant="outlined"
                      density="compact"
                      hide-details
                      display-format="fullDate" />
      </v-col>
      <v-col cols="12" md="4" class="px-1">
        <v-date-input v-model="endDate"
                      label="End Date"
                      :min="startDate"
                      variant="outlined"
                      density="compact"
                      hide-details
                      display-format="fullDate" />
      </v-col>
      <v-col cols="12" md="4" class="px-1 d-flex align-center">
        <v-btn color="success"
               prepend-icon="mdi-microsoft-excel"
               variant="elevated"
               :loading="exporting"
               :disabled="machineData.length === 0"
               @click="exportOEEExcel"
               class="mt-1">
          Export Excel
        </v-btn>
      </v-col>
    </v-row>

    <!-- Summary doughnut cards -->
    <SummaryCards :metrics="summaryMetrics" class="mb-2" />

    <!-- Machine table -->
    <v-row class="mb-2">
      <v-col cols="12">
        <MachineList :machines="machineData" @select="openDetail" />
      </v-col>
    </v-row>

    <!-- Pareto charts -->
    <v-row>
      <v-col cols="12" md="4" class="px-1">
        <ParetoChart title="Total Reject"
                      :data="rejectData"
                      value-key="total_reject"
                      label-key="id_type"
                      full-label-key="type"
                      bar-label="Total Reject (kg)"
                      bar-color="#F44336"
                      y-axis-label="Weight (kg)"
                      x-axis-label="Product Type"
                      empty-icon="mdi-chart-bar" />
      </v-col>
      <v-col cols="12" md="4" class="px-1">
        <ParetoChart title="Total Output"
                      :data="outputData"
                      value-key="total_output"
                      label-key="id_type"
                      full-label-key="type"
                      bar-label="Total Output (kg)"
                      bar-color="#2196F3"
                      y-axis-label="Weight (kg)"
                      x-axis-label="Product Type"
                      empty-icon="mdi-chart-bar" />
      </v-col>
      <v-col cols="12" md="4" class="px-1">
        <ParetoChart title="Total Downtime"
                      :data="downtimeData"
                      value-key="hours"
                      label-key="id_type"
                      full-label-key="type"
                      bar-label="Downtime (Hours)"
                      bar-color="#FF7043"
                      y-axis-label="Time (hrs)"
                      x-axis-label="Downtime Category"
                      empty-icon="mdi-chart-timeline-variant" />
      </v-col>
    </v-row>

    <!-- Machine detail dialog -->
    <v-dialog v-model="detailDialog" max-width="1400px" scrollable>
      <MachineOEE :selected-machine="selectedMachine"
                  :detail-dialog="detailDialog"
                  :start-date="dateString(startDate)"
                  :end-date="dateString(endDate)"
                  @update:detail-dialog="detailDialog = $event" />
    </v-dialog>

  </v-container>
</template>

<script setup>
  import { ref, computed, watch, onMounted } from 'vue';
  import SummaryCards from '@/components/OEE/SummaryCards.vue';
  import MachineList from '@/components/OEE/MachineList.vue';
  import ParetoChart from '@/components/OEE/ParetoChart.vue';
  import MachineOEE from '@/components/OEE/MachineOEE.vue';

  // ── State ─────────────────────────────────────────────────────────────────────

  const startDate = ref(new Date());
  const endDate = ref(new Date());

  const machineData = ref([]);
  const rejectData = ref([]);
  const outputData = ref([]);
  const downtimeData = ref([]);
  const detailDialog = ref(false);
  const selectedMachine = ref(null);
  const exporting = ref(false);

  // ── Summary metrics derived from machine data ─────────────────────────────────

  const summaryMetrics = computed(() => {
    const valid = machineData.value.filter(m => m.id_machine >= 1 && m.id_machine <= 40 && Number(m.oee) > 0);
    if (!valid.length) return defaultMetrics();

    const avg = (key) => valid.reduce((s, m) => s + (Number(m[key]) || 0), 0) / valid.length;
    return [
      { title: 'Overall OEE', value: Number(avg('oee').toFixed(0)), type: 'oee' },
      { title: 'Performance', value: Number(avg('performance').toFixed(0)), type: 'performance' },
      { title: 'Availability', value: Number(avg('availability').toFixed(0)), type: 'availability' },
      { title: 'Quality', value: Number(avg('quality').toFixed(0)), type: 'quality' },
    ];
  });

  function defaultMetrics() {
    return [
      { title: 'Overall OEE', value: 0, type: 'oee' },
      { title: 'Performance', value: 0, type: 'performance' },
      { title: 'Availability', value: 0, type: 'availability' },
      { title: 'Quality', value: 0, type: 'quality' },
    ];
  }

  // ── Date helpers ──────────────────────────────────────────────────────────────

  function dateString(d) {
    return d instanceof Date ? d.toLocaleDateString('en-CA') : d;
  }

  // ── Data fetch ────────────────────────────────────────────────────────────────

  async function fetchAll() {
    const start = dateString(startDate.value);
    const end = dateString(endDate.value);

    const [oeeRes, rejectRes, outputRes, downtimeRes] = await Promise.all([
      fetch(`/api/oee?start_date=${start}&end_date=${end}`),
      fetch(`/api/oee/reject?start_date=${start}&end_date=${end}`),
      fetch(`/api/oee/output?start_date=${start}&end_date=${end}`),
      fetch(`/api/oee/downtime?start_date=${start}&end_date=${end}`),
    ]);

    if (!oeeRes.ok) {
      const text = await oeeRes.text();
      console.error('[OEE] /api/oee failed:', oeeRes.status, text);
      return;
    }
    if (!rejectRes.ok) { console.error('[OEE] /api/oee/reject failed:', rejectRes.status); return; }
    if (!outputRes.ok) { console.error('[OEE] /api/oee/output failed:', outputRes.status); return; }
    if (!downtimeRes.ok) { console.error('[OEE] /api/oee/downtime failed:', downtimeRes.status); return; }

    machineData.value = (await oeeRes.json()).map(normaliseRow);
    rejectData.value = await rejectRes.json();
    outputData.value = await outputRes.json();
    downtimeData.value = await downtimeRes.json();
  }

  function normaliseRow(item) {
    return {
      ...item,
      oee: Number(item.oee || 0).toFixed(2),
      performance: Number(item.performance || 0).toFixed(2),
      availability: Number(item.availability || 0).toFixed(2),
      quality: Number(item.quality || 0).toFixed(2),
      run_time: Number(item.run_time || 0),
      down_time: Number(item.down_time || 0),
      material_used: Number(item.material_used || 0),
      reject_weight: Number(item.reject_weight || 0),
    };
  }

  // ── Detail dialog ─────────────────────────────────────────────────────────────

  function openDetail(machine) {
    selectedMachine.value = machine;
    detailDialog.value = true;
  }

  // ── Lifecycle / watchers ──────────────────────────────────────────────────────

  watch([startDate, endDate], () => {
    fetchAll();
    detailDialog.value = false;
  });

  const exportOEEExcel = async () => {
    exporting.value = true;
    try {
      const params = new URLSearchParams({
        start_date: dateString(startDate.value),
        end_date: dateString(endDate.value),
      });

      const response = await fetch(`/api/oee/ExportOEE?${params}`, { method: 'GET' });

      if (!response.ok) {
        const err = await response.json().catch(() => ({}));
        throw new Error(err.message || `Export failed (HTTP ${response.status})`);
      }

      const disposition = response.headers.get('Content-Disposition') ?? '';
      const nameMatch   = disposition.match(/filename[^;=\n]*=["']?([^"';\n]+)/i);
      const fileName    = nameMatch
        ? nameMatch[1].trim()
        : `OEE_Report_${dateString(startDate.value)}_to_${dateString(endDate.value)}.xlsx`;

      const blob = await response.blob();
      const url  = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href     = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      setTimeout(() => { document.body.removeChild(link); URL.revokeObjectURL(url); }, 200);
    } catch (err) {
      console.error('[OEE Export]', err);
      alert(`Export failed: ${err.message}`);
    } finally {
      exporting.value = false;
    }
  };

  onMounted(fetchAll);
</script>
