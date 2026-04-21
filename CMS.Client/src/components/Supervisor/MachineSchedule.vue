<template>
  <v-row dense style="flex-direction: column; overflow: hidden; height: 82vh;">

    <!-- Toolbar -->
    <v-col cols="auto">
      <v-toolbar flat density="compact" class="px-2">
        <v-icon color="primary" size="small">mdi-monitor-dashboard</v-icon>
        <v-toolbar-title class="text-subtitle-1 ml-2">Machine Status</v-toolbar-title>
        <v-spacer />
        <v-chip size="small"
                :color="polling ? 'success' : 'error'"
                variant="tonal"
                prepend-icon="mdi-circle"
                class="mr-2">
          {{ polling ? 'Live' : 'Paused' }}
        </v-chip>
        <v-btn :icon="polling ? 'mdi-pause' : 'mdi-play'"
               size="small"
               variant="tonal"
               @click="togglePolling" />
      </v-toolbar>
    </v-col>

    <!-- Machine cards -->
    <v-col style="flex: 1; overflow-y: auto; min-height: 0;">
      <v-row dense class="pa-1">
        <v-col v-for="machine in machineData"
               :key="machine.id_machine"
               cols="6">
          <v-card elevation="2" :color="machine.color">
            <v-row no-gutters>

              <!-- Machine name -->
              <v-col cols="2" class="d-flex align-center justify-center">
                <div class="text-h5 font-weight-bold text-center">
                  {{ machine.machine_name }}
                </div>
              </v-col>

              <!-- Main info -->
              <v-col cols="8" class="pa-2">
                <div class="text-center">
                  <div class="scroll-container" :ref="el => setContainerRef(el, machine.id_machine)">
                    <div v-if="!shouldScroll[machine.id_machine]"
                         class="scrolling-text text-subtitle-1 font-weight-bold">
                      {{ machine.type }}
                    </div>
                    <div v-else class="marquee-wrapper">
                      <div class="marquee-content">
                        <span class="text-subtitle-1 font-weight-bold">{{ machine.type }}</span>
                        <span class="text-subtitle-1 font-weight-bold">{{ machine.type }}</span>
                      </div>
                    </div>
                  </div>
                </div>

                <v-divider class="my-1" />

                <v-row dense class="text-caption">
                  <v-col cols="6" class="text-right pr-2">
                    <div>Output:</div>
                    <div>Plan Output:</div>
                    <div>Efficiency:</div>
                  </v-col>
                  <v-col cols="6" class="text-left font-weight-bold">
                    <div>{{ formatNumber(machine.output) }} pcs</div>
                    <div>{{ formatNumber(machine.planned_output) }} pcs</div>
                    <div>{{ calculateEfficiency(machine) }}%</div>
                  </v-col>
                </v-row>

                <v-divider class="my-1" />

                <div class="text-center">
                  <v-chip :color="machine.color"
                          size="small"
                          variant="elevated"
                          class="text-caption font-weight-bold">
                    {{ machine.category || 'N/A' }}
                  </v-chip>
                </div>
              </v-col>

              <!-- Change Mould button -->
              <v-col cols="2">
                <v-btn style="height: 100%;"
                       block
                       elevation="2"
                       :color="machine.color"
                       variant="elevated"
                       density="compact"
                       @click="promptChangeMould(machine)">
                  <div class="text-center">
                    <div class="text-caption font-weight-bold" style="writing-mode: horizontal-tb;">
                      CHANGE<br>MOULD
                    </div>
                    <div class="mt-1 text-caption" style="writing-mode: horizontal-tb; font-size: 0.65rem;">
                      <div>SAP: {{ machine.id_type }}</div>
                      <div>Mould: {{ machine.mould }}</div>
                    </div>
                  </div>
                </v-btn>
              </v-col>

            </v-row>
          </v-card>
        </v-col>
      </v-row>
    </v-col>
  </v-row>

  <!-- Change Mould Dialog -->
  <v-dialog v-model="changeMouldDialog.visible" max-width="400">
    <v-card>
      <v-card-title class="text-subtitle-1 pa-3">Change Mould</v-card-title>
      <v-card-text class="pa-3">
        <v-text-field label="Machine Type"
                      v-model="currentType"
                      readonly
                      density="compact"
                      variant="outlined"
                      hide-details
                      class="mb-2" />
        <v-number-input label="SAP"
                        v-model="newSAP"
                        :min="0"
                        :max="999999"
                        density="compact"
                        variant="outlined"
                        hide-details
                        class="mb-2" />
        <v-number-input label="Mould"
                        v-model="newMould"
                        :min="0"
                        :max="10"
                        density="compact"
                        variant="outlined"
                        hide-details />
      </v-card-text>
      <v-card-actions class="pa-3">
        <v-spacer />
        <v-btn size="small" @click="changeMouldDialog.visible = false">Cancel</v-btn>
        <v-btn color="primary"
               size="small"
               :disabled="!isValid"
               :loading="saving"
               @click="confirmChangeMould">
          Confirm
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue';
  import { useMachineStore } from '@/store/machineStore';

  // ── Props ─────────────────────────────────────────────────────────────────────

  const props = defineProps({
    SAPData: { type: Array, required: true, default: () => [] },
  });

  const emit = defineEmits(['refresh-data']);

  // ── Store ─────────────────────────────────────────────────────────────────────

  const store = useMachineStore();

  // Reactive alias — v-for uses id_machine as key for efficient re-renders
  const machineData = computed(() => store.machineData);

  // ── Polling (mounted only) ────────────────────────────────────────────────────

  const POLL_INTERVAL = 10_000; // 10 s
  let pollTimer = null;
  const polling = ref(false);

  function startPolling() {
    if (pollTimer) return;
    polling.value = true;
    pollTimer = setInterval(() => store.loadMachineMaster(), POLL_INTERVAL);
  }

  function stopPolling() {
    polling.value = false;
    if (pollTimer) { clearInterval(pollTimer); pollTimer = null; }
  }

  function togglePolling() {
    polling.value ? stopPolling() : startPolling();
  }

  onMounted(() => { store.loadMachineMaster(); startPolling(); });
  onUnmounted(stopPolling);

  // ── Text overflow detection ───────────────────────────────────────────────────

  const containerRefs = ref({});
  const shouldScroll  = ref({});

  function setContainerRef(el, id) {
    if (!el) return;
    containerRefs.value[id] = el;
    nextTick(() => measureElement(id));
  }

  function measureElement(id) {
    const container = containerRefs.value[id];
    if (!container) return;
    const machine = machineData.value.find(m => m.id_machine === id);
    if (!machine) return;

    const tmp = document.createElement('span');
    tmp.style.cssText = 'display:inline-block;white-space:nowrap;visibility:hidden;position:absolute;';
    tmp.className = 'text-subtitle-1 font-weight-bold';
    tmp.textContent = machine.type || '';
    container.appendChild(tmp);
    shouldScroll.value[id] = tmp.offsetWidth > container.clientWidth - 20;
    container.removeChild(tmp);
  }

  watch(machineData, async () => {
    await nextTick();
    machineData.value.forEach(m => measureElement(m.id_machine));
  }, { deep: false });

  // ── Change Mould ──────────────────────────────────────────────────────────────

  const changeMouldDialog = ref({ visible: false, item: null });
  const newSAP     = ref(null);
  const newMould   = ref(null);
  const saving = ref(false);

  const currentType = computed(() => {
    if (newSAP.value == null || newMould.value == null) return 'N/A';
  
    const found = props.SAPData.find(
      i => Number(i.id_type) === Number(newSAP.value) && Number(i.mould) === Number(newMould.value)
    );
    return found?.type ?? 'N/A';
  });

  const isValid = computed(() => {
    return currentType.value !== 'N/A';
  });

  function promptChangeMould(machine) {
    changeMouldDialog.value.item = machine;
    newSAP.value   = machine.id_type;
    newMould.value = machine.mould;
    changeMouldDialog.value.visible = true;
  }

  async function confirmChangeMould() {
    const item = changeMouldDialog.value.item;
    saving.value = true;
    try {
      const res = await fetch('/api/supervisor/mould-change', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          id_machine:   Number(item.id_machine),
          machine_name: String(item.machine_name),
          id_type:      Number(newSAP.value),
          mould:        Number(newMould.value),
        }),
      });
      if (!res.ok) throw new Error('Failed to change mould');
      changeMouldDialog.value.visible = false;
      await store.loadMachineMaster();
      emit('refresh-data');
    } catch (err) {
      console.error('Error changing mould:', err);
    } finally {
      saving.value = false;
    }
  }

  // ── Helpers ───────────────────────────────────────────────────────────────────

  const formatNumber = (v) => (Number(v) || 0).toFixed(0);

  function calculateEfficiency(machine) {
    if (!machine.planned_output) return '0.00';
    return (((machine.output || 0) / machine.planned_output) * 100).toFixed(2);
  }
</script>

<style scoped>
  .scroll-container {
    position: relative;
    overflow: hidden;
    width: 100%;
  }

  .scrolling-text {
    display: inline-block;
    white-space: nowrap;
  }

  .marquee-wrapper {
    overflow: hidden;
    position: relative;
    width: 100%;
  }

  .marquee-content {
    display: flex;
    animation: marquee 10s linear infinite;
    width: fit-content;
  }

    .marquee-content span {
      white-space: nowrap;
      padding-right: 3em;
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
