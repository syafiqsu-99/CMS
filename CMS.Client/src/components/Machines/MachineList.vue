<template>
  <v-card variant="flat" style="height: 100%;; overflow-y: auto;" elevation="0" class="bg-grey-lighten-4">
    <v-list dense class="pa-2 bg-transparent">
      <v-list-item v-for="machine in machines"
                   :key="machine.id_machine"
                   @click="$emit('select', machine)"
                   :class="{ 'bg-white elevation-2': selected?.id_machine === machine.id_machine, 'bg-white': selected?.id_machine !== machine.id_machine }"
                   class="px-3 py-2 mb-2 cursor-pointer rounded-lg"
                   :style="{
                       borderLeft: selected?.id_machine === machine.id_machine ? '4px solid #1976D2' : 'none',
                       transition: 'all 0.2s ease'
                     }">
        <v-row dense align="center" no-gutters>
          <v-col cols="2" class="text-center">
            <div class="text-body-2 font-weight-bold text-truncate" :title="machine.machine_name">
              {{ machine.machine_name }}
            </div>
          </v-col>

          <v-col cols="10" class="text-center">
            <div v-if="chartData[machine.id_machine]" style="height: 40px;">
              <Bar :data="chartData[machine.id_machine].data"
                   :options="chartData[machine.id_machine].options" />
            </div>
            <div v-else class="text-caption text-grey">
              No data
            </div>
            <v-chip :color="machine.currentStatus?.color"
                    size="x-small"
                    class="mt-1 font-weight-bold">
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
    machines: { type: Array, required: true },
    chartData: { type: Object, required: true },
    selected: { type: Object, default: null },
  });

  defineEmits(['select']);
</script>
