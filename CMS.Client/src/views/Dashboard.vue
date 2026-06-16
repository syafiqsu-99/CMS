<template>
  <v-container fluid class="pa-0 h-100 d-flex flex-column" style="overflow: hidden;">
    <v-row no-gutters class="flex-grow-1" style="height: 100%; overflow: hidden;">

      <!-- Floor map (top 70%) -->
      <v-col cols="12" style="height: 70vh">
        <v-window v-model="machine_layout" show-arrows="hover" continuous class="h-100">
          <v-window-item :value="1" class="h-100">
            <Attendance :machine-status="machineColor" />
          </v-window-item>
        </v-window>
      </v-col>

      <!-- Machine card slideshow (bottom 30%) -->
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
                        :color="machine.color"
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
                        <div>{{ machine.planned_output }}</div>
                        <div>{{ machine.efficiency }}%</div>
                      </v-col>
                    </v-row>

                    <v-divider class="my-1 flex-shrink-0"></v-divider>

                    <div class="d-flex justify-center align-center flex-shrink-0">
                      <v-chip :color="machine.color"
                              size="small"
                              variant="elevated"
                              class="text-caption px-2"
                              style="height: 24px;">
                        {{ machine.category || 'No Data' }}
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
  import { useMachineStore } from '@/store/machineStore';
  import Attendance from '@/components/Dashboard/Attendance.vue';

  const store = useMachineStore();

  // ── UI state ──────────────────────────────────────────────────────────────────

  const machine_layout = ref(1);
  const running_slideshow = ref(0);
  const machineColor = computed(() =>
    Object.fromEntries(
      store.machineData
        .filter(m => m.machine_name !== 'TEST')
        .map(m => [m.machine_name, m.color])
    )
  );

  const machineList = computed(() =>
    store.machineData
      .filter(m => m.machine_name !== 'TEST')
      .map(m => ({
        id_machine: m.id_machine,
        machine_name: m.machine_name,
        type: m.type || 'N/A',
        output: m.output ?? 0,
        planned_output: m.planned_output ?? 0,
        efficiency: m.planned_output > 0
          ? ((m.output / m.planned_output) * 100).toFixed(2)
          : '0.00',
        category: m.category || 'N/A',
        color: m.color || '#808080',
      }))
  );

  const paginatedMachines = computed(() => {
    const size = 6;
    const list = machineList.value;
    return Array.from(
      { length: Math.ceil(list.length / size) },
      (_, i) => list.slice(i * size, (i + 1) * size),
    );
  });

  // ── Polling ───────────────────────────────────────────────────────────────────

  const POLL_INTERVAL = 10_000;

  let statusTimer = null;
  let slideshowTimer = null;

  function startPolling() {
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
    if (statusTimer) { clearInterval(statusTimer); statusTimer = null; }
    if (slideshowTimer) { clearInterval(slideshowTimer); slideshowTimer = null; }
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────────

  onMounted(async () => {
    await store.loadMachineMaster();
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
