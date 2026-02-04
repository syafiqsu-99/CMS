<template>
  <v-row dense style="flex-direction: column; overflow: hidden; height: 82vh;">
    <v-col cols="auto">
      <v-toolbar flat density="compact" class="px-2">
        <v-icon color="primary" size="small">mdi-monitor-dashboard</v-icon>
        <v-toolbar-title class="text-subtitle-1 ml-2">
          Machine Status
        </v-toolbar-title>
      </v-toolbar>
    </v-col>

    <v-col style="flex: 1; overflow-y: auto; min-height: 0;">
      <v-row dense class="pa-1">
        <v-col v-for="(machine, index) in machineData" :key="machine.id || index" cols="6">
          <v-card elevation="2" :color="machine.color">
            <v-row no-gutters>
              <v-col cols="2" class="d-flex align-center justify-center">
                <div class="text-h5 font-weight-bold text-center">
                  {{ machine.machine_name }}
                </div>
              </v-col>

              <v-col cols="8" class="pa-2">
                <div class="text-center">
                  <div class="scroll-container" :ref="el => setContainerRef(el, index)">
                    <div v-if="!shouldScroll[index]" class="scrolling-text text-subtitle-1 font-weight-bold">
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

                <v-divider class="my-1"></v-divider>

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

                <v-divider class="my-1"></v-divider>

                <div class="text-center">
                  <v-chip :color="getStatusColor(machine)" size="small" variant="elevated" class="text-caption font-weight-bold">
                    {{ machine.category || 'N/A' }}
                  </v-chip>
                </div>
              </v-col>

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
        <v-btn color="primary" size="small" :disabled="!isValid" @click="confirmChangeMould">Confirm</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  import { ref, onMounted, onUnmounted, computed, watch, nextTick } from 'vue';

  const changeMouldDialog = ref({ visible: false, item: null });
  const emit = defineEmits(['refresh-data']);
  const newSAP = ref('');
  const newMould = ref('');
  const currentType = ref('');
  const isValid = ref(false);

  const containerRefs = ref([]);
  const shouldScroll = ref([]);

  const { machineData, SAPData } = defineProps({
    machineData: { type: Array, required: true, default: () => [] },
    SAPData: { type: Array, required: true, default: () => [] },
  });

  watch(() => machineData, () => {
    nextTick(() => measureAll());
  }, { immediate: true, deep: true });

  watch(
    () => changeMouldDialog.value.item,
    (newItem) => {
      if (newItem) {
        newSAP.value = newItem.id_type ?? null;
        newMould.value = newItem.mould ?? null;
        currentType.value = newItem.type ?? "N/A";
      }
    },
    { immediate: true }
  );

  watch([newSAP, newMould], ([sapValue, mouldValue]) => {
    const foundItem = SAPData.find(
      (item) => item.id_type === sapValue && item.mould === mouldValue
    );

    if (foundItem) {
      currentType.value = foundItem.type;
      isValid.value = true;
    } else {
      currentType.value = "N/A";
      isValid.value = false;
    }
  });

  const formatNumber = (v) => (Number(v) || 0).toFixed(0);

  const formatPercentage = (v) => (Number(v) || 0).toFixed(2);

  const getStatusColor = (m) => {
    if (m.data && m.data[0] && m.data[0].color) return m.data[0].color;
    if (m.status_start === true) return "success";
    if (m.status_start === false) return "error";
    return "info";
  };

  function calculateEfficiency(machineData) {
    if (!machineData || !machineData.planned_output || machineData.planned_output === 0) return '0.00';
    const efficiency = ((machineData.output || 0) / machineData.planned_output) * 100;
    return efficiency.toFixed(2);
  }

  function promptChangeMould(machine) {
    changeMouldDialog.value.item = machine;
    changeMouldDialog.value.visible = true;
  }

  async function confirmChangeMould() {
    const item = changeMouldDialog.value.item;

    const payload = {
      id_machine: Number(item.id_machine),
      machine_name: String(item.machine_name),
      id_type: Number(newSAP.value),
      mould: Number(newMould.value)
    };

    try {
      const response = await fetch("/api/MachineLog/MachineMaster", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });

      if (!response.ok) throw new Error("Failed to change mould");

      changeMouldDialog.value.visible = false;

      await nextTick();
      emit('refresh-data');
    } catch (err) {
      console.error("Error changing mould:", err);
      changeMouldDialog.value.visible = false;
    }
  }

  function setContainerRef(el, idx) {
    if (el) {
      containerRefs.value[idx] = el;
      nextTick(() => measureElement(idx));
    }
  }

  function measureElement(idx) {
    const container = containerRefs.value[idx];
    if (!container) return;

    const tempSpan = document.createElement('span');
    tempSpan.style.cssText = 'display: inline-block; white-space: nowrap; visibility: hidden; position: absolute;';
    tempSpan.className = 'text-subtitle-1 font-weight-bold';
    tempSpan.textContent = machineData[idx]?.type || '';
    
    container.appendChild(tempSpan);
    
    const textWidth = tempSpan.offsetWidth;
    const containerWidth = container.clientWidth;
    
    container.removeChild(tempSpan);
    shouldScroll.value[idx] = textWidth > containerWidth - 20;
  }

  async function measureAll() {
    await nextTick();
    await nextTick();
    
    const count = machineData.length;
    shouldScroll.value = Array(count).fill(false);

    for (let i = 0; i < count; i++) {
      measureElement(i);
    }
  }

  onMounted(() => {
    setTimeout(() => measureAll(), 300);
  });
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
