<template>
  <v-container fluid class="pa-0 h-100 d-flex flex-column" style="overflow: hidden;">
    <v-row no-gutters class="flex-grow-1" style="height: 100%; overflow: hidden;">

      <!-- Machine slideshow (top 70 %) -->
      <v-col cols="12" style="height: 70vh">
        <v-window v-model="machine_layout" show-arrows="hover" continuous class="h-100">


          <v-window-item :value="1" class="h-100">
            <Attendance :machine-status="machineColor" />
          </v-window-item>
        </v-window>
      </v-col>

      <v-col cols="12" style="height: 30vh; max-height: 30vh;">
        <v-window v-model="running_slideshow"
                  show-arrows="hover"
                  continuous
                  class="h-100 overflow-hidden">
          <v-window-item v-for="(group, index) in paginatedMachines" :key="index" class="h-100">
            <v-row class="fill-height ma-0">
              <v-col v-for="machine in group" :key="machine.id_machine" cols="2" class="pa-1 d-flex flex-column">
                <v-card rounded="xl"
                        variant="elevated"
                        elevation="8"
                        :color="machine.data[0]?.color"
                        class="flex-fill d-flex flex-column overflow-hidden"
                        style="max-height: 100%;">
                  <v-card-title class="text-center pa-1 text-h6 font-weight-bold flex-shrink-0" style="min-height: 40px;">
                    {{ machine.machine_name }}
                  </v-card-title>
                  <v-card-text class="pa-1 flex-fill d-flex flex-column overflow-hidden" style="flex: 1 1 0; min-height: 0;">
                    <div class="marquee-wrapper flex-grow-1 overflow-hidden">
                      <div class="marquee-content">
                        <span class="text-caption font-weight-bold">{{ machine.type }}</span>
                        <span class="text-caption font-weight-bold">{{ machine.type }}</span>
                      </div>
                    </div>

                    <v-divider class="my-1 flex-shrink-0"></v-divider>

                    <v-row dense class="text-caption flex-shrink-0 ma-0" style="height: 55px;">
                      <v-col cols="6" class="text-right pa-1">
                        <div>Output:</div>
                        <div>Plan:</div>
                        <div>Eff:</div>
                      </v-col>
                      <v-col cols="6" class="text-left pa-1">
                        <div>{{ machine.output }}</div>
                        <div>{{ machine.plan_output.toFixed(0) }}</div>
                        <div>{{ machine.eff.toFixed(2) }}%</div>
                      </v-col>
                    </v-row>

                    <v-divider class="my-1 flex-shrink-0"></v-divider>

                    <div class="d-flex justify-center align-center flex-shrink-0">
                      <v-chip :color="machine.data[0]?.color"
                              size="small"
                              variant="elevated"
                              class="text-caption px-2"
                              style="height: 24px;">
                        {{ machine.data[0]?.category || 'No Data' }}
                      </v-chip>
                    </div>
                  </v-card-text>
                </v-card>
              </v-col>
            </v-row>
          </v-window-item>
        </v-window>
      </v-col>

    </v-row>
  </v-container>
</template>

<script setup>
  import { ref, computed, onMounted, onUnmounted } from 'vue';
  import {
    Chart as ChartJS, CategoryScale, LinearScale,
    BarElement, TimeScale, Tooltip, Legend,
  } from 'chart.js';
  import 'chartjs-adapter-luxon';
  import { DateTime } from 'luxon';
  import { useMachineStore } from '@/store/machineStore';
  import Attendance from '@/components/Dashboard/Attendance.vue';

  ChartJS.register(CategoryScale, LinearScale, BarElement, TimeScale, Tooltip, Legend);

  const store = useMachineStore();

  // ── UI state ──────────────────────────────────────────────────────────────────

  const machine_layout = ref(1);
  const running_slideshow = ref(0);
  const machines = ref([]);
  const chartData = ref({});
  const machineColor = ref({});

  // ── Computed ──────────────────────────────────────────────────────────────────

  const paginatedMachines = computed(() => {
    const size = 6;
    return Array.from(
      { length: Math.ceil(machines.value.length / size) },
      (_, i) => machines.value.slice(i * size, (i + 1) * size),
    );
  });

  // ── Timeline fetch ────────────────────────────────────────────────────────────

  async function fetchTimelineData() {
    try {
      const res = await fetch('/api/dashboard/timeline');
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();

      machines.value = groupByMachine(data);
      machineColor.value = buildColorMap(data);
      buildChartData();
    } catch (err) {
      console.error('[Dashboard] fetchTimelineData:', err.message);
    }
  }

  function groupByMachine(data) {
    const grouped = {};
    for (const item of data) {
      if (!grouped[item.id_machine]) {
        grouped[item.id_machine] = {
          id_machine: item.id_machine,
          machine_name: item.machine_name ?? 'UNDEFINED',
          type: item.type ?? 'UNDEFINED',
          output: item.output ?? 0,
          plan_output: item.plan_output ?? 0,
          eff: item.efficiency ?? 0.0,
          data: [],
        };
      }
      grouped[item.id_machine].data.push({
        category: item.category ?? 'UNDEFINED',
        start: item.start ?? null,
        finish: item.finish ?? null,
        color: item.color ?? 'rgba(0,0,0,0.1)',
      });
    }
    return Object.values(grouped);
  }

  function buildColorMap(data) {
    const statusMap = {};
    for (const item of data) {
      const name = item.machine_name;
      if (!statusMap[name] || new Date(item.start) > new Date(statusMap[name].start)) {
        statusMap[name] = { start: item.start, color: item.color };
      }
    }
    return Object.fromEntries(Object.entries(statusMap).map(([k, v]) => [k, v.color]));
  }

  function buildChartData() {
    const now = DateTime.now();
    const start = now.startOf('day');
    const end = now.endOf('day');

    const built = {};
    for (const machine of machines.value) {
      const datasets = machine.data
        .filter(d => d.start && d.finish)
        .map(d => ({
          x: [d.start, d.finish],
          backgroundColor: d.color,
        }));

      built[machine.id_machine] = {
        data: { datasets: datasets.length ? [{ data: datasets, backgroundColor: datasets.map(d => d.x ? d.backgroundColor : 'transparent'), barThickness: 14 }] : [] },
        options: {
          animation: false,
          indexAxis: 'y',
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { display: false }, tooltip: { enabled: false } },
          scales: {
            x: {
              type: 'time',
              min: start.toISO(),
              max: end.toISO(),
              time: { unit: 'hour', displayFormats: { hour: 'ha' } },
              ticks: { stepSize: 4, color: '#000', font: { size: 8 } },
              grid: { display: false },
            },
            y: { display: false },
          },
        },
      };
    }
    chartData.value = built;
  }

  // ── Polling ───────────────────────────────────────────────────────────────────
  const POLL_INTERVAL = 10_000;

  let timelineTimer = null;
  let statusTimer = null;
  let slideshowTimer = null;

  function startPolling() {
    if (!timelineTimer) {
      timelineTimer = setInterval(fetchTimelineData, POLL_INTERVAL);
    }
    if (!statusTimer) {
      statusTimer = setInterval(() => store.loadMachineMaster(), POLL_INTERVAL);
    }
    if (!slideshowTimer) {
      slideshowTimer = setInterval(() => {
        running_slideshow.value =
          (running_slideshow.value + 1) % Math.max(paginatedMachines.value.length, 1);
      }, POLL_INTERVAL);
    }
  }

  function stopPolling() {
    if (timelineTimer) { clearInterval(timelineTimer); timelineTimer = null; }
    if (statusTimer) { clearInterval(statusTimer); statusTimer = null; }
    if (slideshowTimer) { clearInterval(slideshowTimer); slideshowTimer = null; }
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────────

  onMounted(async () => {
    await Promise.all([
      store.loadMachineMaster(),
      fetchTimelineData(),
    ]);
    startPolling();
  });

  onUnmounted(() => {
    stopPolling();
  });
</script>

<style scoped>
  .marquee-wrapper {
    overflow: hidden;
    position: relative;
    width: 100%;
  }

  .marquee-content {
    display: flex;
    animation: marquee 10s linear infinite;
    width: fit-content;
    will-change: transform;
  }

    .marquee-content span {
      white-space: nowrap;
      padding-right: 3em;
      flex-shrink: 0;
    }

  @keyframes marquee {
    0% {
      transform: translateX(0);
    }

    100% {
      transform: translateX(-50%);
    }
  }
</style>
