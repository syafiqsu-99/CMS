<template>
  <v-card variant="flat" elevation="1" style="height: calc(100vh - 100px); overflow-y: auto;">
    <v-card-title class="text-subtitle-2 font-weight-bold pa-3 pb-1">
      Machines
    </v-card-title>

    <v-list density="compact" class="py-0">
      <v-list-item v-for="machine in machines"
                   :key="machine.id_machine"
                   :active="selectedId === machine.id_machine"
                   active-color="primary"
                   rounded="lg"
                   class="mb-1 mx-1"
                   :style="{
          border: selectedId === machine.id_machine
            ? '2px solid #1976D2'
            : '2px solid transparent',
          transition: 'all 0.2s ease',
        }"
                   @click="$emit('select', machine)">
        <v-row dense align="center" no-gutters>
          <!-- Machine name -->
          <v-col cols="2" class="text-center">
            <div class="text-body-2 font-weight-bold text-truncate"
                 :title="machine.machine_name">
              {{ machine.machine_name }}
            </div>
          </v-col>

          <!-- Gantt mini-bar -->
          <v-col cols="10" class="text-center">
            <div v-if="chartData[machine.id_machine]" style="height: 40px;">
              <Bar :data="chartData[machine.id_machine].data"
                   :options="chartData[machine.id_machine].options" />
            </div>
            <div v-else class="text-caption text-grey">No data</div>

            <v-chip :color="machine.currentStatus?.color"
                    size="x-small"
                    class="mt-1">
              {{ machine.currentStatus?.category || 'N/A' }}
            </v-chip>
          </v-col>
        </v-row>
      </v-list-item>
    </v-list>
  </v-card>
</template>

<script setup>
import { Bar } from 'vue-chartjs';

defineProps({
  machines:   { type: Array,  required: true },
  chartData:  { type: Object, required: true },
  selectedId: { type: Number, default: null  },
});

defineEmits(['select']);
</script>
