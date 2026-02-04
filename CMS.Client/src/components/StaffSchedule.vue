<template>
  <v-row dense style="flex-direction: column; overflow: hidden; height: 82vh;">
    <v-col cols="auto">
      <v-toolbar flat density="compact" class="px-2">
        <v-icon color="primary" size="small">mdi-account-multiple</v-icon>
        <v-toolbar-title class="text-subtitle-1 ml-2">
          Staff Schedule - {{ formattedDate }}
        </v-toolbar-title>
        <v-spacer></v-spacer>
        <div class="d-flex align-center ga-2">
          <div class="d-flex align-center ga-1">
            <v-label class="text-caption">Start Date:</v-label>
            <v-date-input v-if="editMode"
                          v-model="scheduleStart"
                          :max="scheduleEnd"
                          density="compact"
                          hide-details
                          variant="outlined"
                          style="width: 160px;"></v-date-input>
            <span v-else class="text-caption font-weight-medium">
              {{ formatDate(scheduleStart) || 'N/A' }}
            </span>
          </div>

          <div class="d-flex align-center ga-1">
            <v-label class="text-caption">End Date:</v-label>
            <v-date-input v-if="editMode"
                          v-model="scheduleEnd"
                          :min="scheduleStart"
                          density="compact"
                          hide-details
                          variant="outlined"
                          style="width: 160px;"></v-date-input>
            <span v-else class="text-caption font-weight-medium">
              {{ formatDate(scheduleEnd) || 'N/A' }}
            </span>
          </div>

          <div class="d-flex align-center ga-1">
            <v-btn v-if="!editMode" icon="mdi-pencil" @click="enterEditMode" variant="text" size="x-small"></v-btn>
            <template v-if="editMode">
              <v-btn icon="mdi-content-save" color="success" @click="saveChanges" variant="text" size="x-small"></v-btn>
              <v-btn icon="mdi-close" color="error" @click="cancelEdit" variant="text" size="x-small"></v-btn>
            </template>
          </div>
        </div>
      </v-toolbar>
    </v-col>

    <v-col style="flex: 1; overflow-y: auto; min-height: 0;">
      <v-row dense>
        <v-col cols="6">
          <div class="text-center text-subtitle-2 font-weight-bold mb-1">Unscheduled Staff</div>
          <v-data-table-virtual :headers="headers"
                                fixed-header
                                :items="groupUnscheduled"
                                density="compact"
                                style="height: 65vh;"
                                :loading="loading"
                                class=" elevation-1 fixed-table">
            <template v-slot:item.staff_role="{ item }">
              <select v-if="editMode"
                      v-model="item.staff_role"
                      class="text-caption pa-1 rounded"
                      style="width: 100%; border: 1px solid #e0e0e0; background-color: white; cursor: pointer;">
                <option v-for="role in staffRoles" :key="role" :value="role">{{ role }}</option>
              </select>
              <span v-else
                    class="text-caption px-1 py-1 rounded"
                    :style="{ backgroundColor: ROLE_COLORS[item.staff_role] || 'grey', color: 'white', display: 'inline-block' }">
                {{ item.staff_role || 'N/A' }}
              </span>
            </template>

            <template v-slot:item.status="{ item }">
              <select v-if="editMode"
                      v-model="item.status"
                      class="text-caption pa-1 rounded"
                      style="width: 100%; border: 1px solid #e0e0e0; background-color: white; cursor: pointer;">
                <option v-for="status in statusOptions" :key="status" :value="status">{{ status }}</option>
              </select>
              <span v-else
                    class="text-caption px-1 py-1 rounded"
                    :style="{ backgroundColor: STAFF_COLORS[item.status] || 'grey', color: 'white', display: 'inline-block' }">
                {{ item.status || 'INACTIVE' }}
              </span>
            </template>

            <template v-slot:item.machine_name="{ item }">
              <v-select v-if="editMode"
                        v-model="item.id_machine"
                        multiple
                        :items="machineOptions"
                        item-title="text"
                        item-value="value"
                        density="compact"
                        hide-details
                        variant="plain"
                        :disabled="item.staff_role !== 'PACKER'"
                        @update:model-value="changeStatus(item, $event)" />
              <span v-else class="text-caption">{{ getMachineNames(item.machine_name) }}</span>
            </template>

            <template v-slot:item.work_shift="{ item }">
              <select v-if="editMode"
                      v-model="item.work_shift"
                      class="text-caption pa-1 rounded"
                      style="width: 100%; border: 1px solid #e0e0e0; background-color: white; cursor: pointer;"
                      @change="changeShift(item, item.work_shift)">
                <option :value="null"></option>
                <option v-for="shift in shiftOptions" :key="shift.value" :value="shift.value">
                  {{ shift.text }}
                </option>
              </select>
              <span v-else class="text-caption">{{ shiftOptions.find(opt => opt.value === item.work_shift)?.text || '' }}</span>
            </template>
          </v-data-table-virtual>
        </v-col>

        <v-col cols="6">
          <div class="text-center text-subtitle-2 font-weight-bold mb-1">Morning Shift</div>
          <v-data-table-virtual :headers="headers"
                                fixed-header
                                :items="groupShift1"
                                density="compact"
                                style="height: 30vh;"
                                :loading="loading"
                                class=" elevation-1 fixed-table">
            <template v-slot:item.staff_role="{ item }">
              <select v-if="editMode"
                      v-model="item.staff_role"
                      class="text-caption pa-1 rounded"
                      style="width: 100%; border: 1px solid #e0e0e0; background-color: white; cursor: pointer;">
                <option v-for="role in staffRoles" :key="role" :value="role">{{ role }}</option>
              </select>
              <span v-else
                    class="text-caption px-1 py-1 rounded"
                    :style="{ backgroundColor: ROLE_COLORS[item.staff_role] || 'grey', color: 'white', display: 'inline-block' }">
                {{ item.staff_role || 'N/A' }}
              </span>
            </template>

            <template v-slot:item.status="{ item }">
              <select v-if="editMode"
                      v-model="item.status"
                      class="text-caption pa-1 rounded"
                      style="width: 100%; border: 1px solid #e0e0e0; background-color: white; cursor: pointer;">
                <option v-for="status in statusOptions" :key="status" :value="status">{{ status }}</option>
              </select>
              <span v-else
                    class="text-caption px-1 py-1 rounded"
                    :style="{ backgroundColor: STAFF_COLORS[item.status] || 'grey', color: 'white', display: 'inline-block' }">
                {{ item.status || 'INACTIVE' }}
              </span>
            </template>

            <template v-slot:item.machine_name="{ item }">
              <v-select v-if="editMode"
                        v-model="item.id_machine"
                        multiple
                        :items="machineOptions"
                        item-title="text"
                        item-value="value"
                        density="compact"
                        hide-details
                        variant="plain"
                        :disabled="item.staff_role !== 'PACKER'"
                        @update:model-value="changeStatus(item, $event)" />
              <span v-else class="text-caption">{{ getMachineNames(item.machine_name) }}</span>
            </template>

            <template v-slot:item.work_shift="{ item }">
              <select v-if="editMode"
                      v-model="item.work_shift"
                      class="text-caption pa-1 rounded"
                      style="width: 100%; border: 1px solid #e0e0e0; background-color: white; cursor: pointer;"
                      @change="changeShift(item, item.work_shift)">
                <option :value="null"></option>
                <option v-for="shift in shiftOptions" :key="shift.value" :value="shift.value">
                  {{ shift.text }}
                </option>
              </select>
              <span v-else class="text-caption">{{ shiftOptions.find(opt => opt.value === item.work_shift)?.text || '' }}</span>
            </template>
          </v-data-table-virtual>

          <div class="text-center text-subtitle-2 font-weight-bold mb-1">Night Shift</div>
          <v-data-table-virtual :headers="headers"
                                fixed-header
                                :items="groupShift2"
                                density="compact"
                                style="height: 30vh;"
                                :loading="loading"
                                class=" elevation-1 fixed-table">
            <template v-slot:item.staff_role="{ item }">
              <select v-if="editMode"
                      v-model="item.staff_role"
                      class="text-caption pa-1 rounded"
                      style="width: 100%; border: 1px solid #e0e0e0; background-color: white; cursor: pointer;">
                <option v-for="role in staffRoles" :key="role" :value="role">{{ role }}</option>
              </select>
              <span v-else
                    class="text-caption px-1 py-1 rounded"
                    :style="{ backgroundColor: ROLE_COLORS[item.staff_role] || 'grey', color: 'white', display: 'inline-block' }">
                {{ item.staff_role || 'N/A' }}
              </span>
            </template>

            <template v-slot:item.status="{ item }">
              <select v-if="editMode"
                      v-model="item.status"
                      class="text-caption pa-1 rounded"
                      style="width: 100%; border: 1px solid #e0e0e0; background-color: white; cursor: pointer;">
                <option v-for="status in statusOptions" :key="status" :value="status">{{ status }}</option>
              </select>
              <span v-else
                    class="text-caption px-1 py-1 rounded"
                    :style="{ backgroundColor: STAFF_COLORS[item.status] || 'grey', color: 'white', display: 'inline-block' }">
                {{ item.status || 'INACTIVE' }}
              </span>
            </template>

            <template v-slot:item.machine_name="{ item }">
              <v-select v-if="editMode"
                        v-model="item.id_machine"
                        multiple
                        :items="machineOptions"
                        item-title="text"
                        item-value="value"
                        density="compact"
                        hide-details
                        variant="plain"
                        :disabled="item.staff_role !== 'PACKER'"
                        @update:model-value="changeStatus(item, $event)" />
              <span v-else class="text-caption">{{ getMachineNames(item.machine_name) }}</span>
            </template>

            <template v-slot:item.work_shift="{ item }">
              <select v-if="editMode"
                      v-model="item.work_shift"
                      class="text-caption pa-1 rounded"
                      style="width: 100%; border: 1px solid #e0e0e0; background-color: white; cursor: pointer;"
                      @change="changeShift(item, item.work_shift)">
                <option :value="null"></option>
                <option v-for="shift in shiftOptions" :key="shift.value" :value="shift.value">
                  {{ shift.text }}
                </option>
              </select>
              <span v-else class="text-caption">{{ shiftOptions.find(opt => opt.value === item.work_shift)?.text || '' }}</span>
            </template>
          </v-data-table-virtual>
        </v-col>
      </v-row>
    </v-col>
  </v-row>
</template>

<script setup>
  import { ref, computed, onMounted, nextTick, watchEffect } from 'vue';
  import { STAFFROLES, STATUSOPTIONS, SHIFTOPTIONS, MACHINEOPTIONS, ROLE_COLORS, STAFF_COLORS } from '@/store/constant.js';

  const loading = ref(false);
  const editMode = ref(false);
  const editStaffMode = ref([]);
  const staffAssignments = ref([]);
  const expandedStaffAssignments = ref([]);
  const scheduleStart = ref(null);
  const scheduleEnd = ref(null);
  const staffRoles = STAFFROLES;
  const statusOptions = STATUSOPTIONS;
  const shiftOptions = SHIFTOPTIONS;
  const machineOptions = MACHINEOPTIONS;

  const groupUnscheduled = computed(() => {
    return currentItems.value.filter(item => !item.work_shift)
  })

  const groupShift1 = computed(() => {
    return currentItems.value.filter(item => item.work_shift === 1)
  })

  const groupShift2 = computed(() => {
    return currentItems.value.filter(item => item.work_shift === 2)
  })

  const currentItems = computed(() => (editMode.value ? editStaffMode.value : staffAssignments.value));

  const headers = [
    { title: 'Name', key: 'staff_name', width: '35%' },
    { title: 'Role', key: 'staff_role', width: '20%' },
    { title: 'Status', key: 'status', width: '15%' },
    { title: 'Machine', key: 'machine_name', width: '15%' },
    { title: 'Shift', key: 'work_shift', width: '15%' }
  ];

  const formattedDate = computed(() => new Date().toLocaleDateString());

  onMounted(async () => {
    await loadStaffData();
  });

  function toLocalDateString(date) {
    const d = new Date(date);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  watchEffect(() => {
    if (staffAssignments.value.length > 0) {
      scheduleStart.value = toLocalDateString(staffAssignments.value[0].start_date)
      scheduleEnd.value = toLocalDateString(staffAssignments.value[0].end_date)
    }
  })

  function formatDate(date) {
    if (!date) return 'N/A';
    const d = new Date(date);
    return isNaN(d.getTime()) ? date : d.toISOString().split('T')[0];
  }

  async function loadStaffData() {
    loading.value = true;
    try {
      const response = await fetch('/api/MachineLog/StaffSchedule');
      const data = await response.json();

      staffAssignments.value = data.map(item => {
        const machineNames = item.machine_name ? item.machine_name.split('/').map(x => x.trim()) : [];
        const idList = machineNames
          .map(name => {
            const found = machineOptions.find(opt => opt.text === name);
            return found ? found.value : null;
          }).filter(Boolean);

        return {
          ...item,
          machine_name: machineNames,
          id_machine: idList
        };
      });

      expandedStaffAssignments.value = data.flatMap(item => {
        const machines = item.machine_name ? item.machine_name.split('/') : [];
        return machines.map(machine => ({
          ...item,
          machine_name: machine
        }));
      });

      scheduleStart.value = staffAssignments.value[0].start_date;
      scheduleEnd.value = staffAssignments.value[0].end_date;
    } catch (err) {
      console.error('Error loading staff:', err);
    } finally {
      loading.value = false;
    }
  }

  function enterEditMode() {
    editMode.value = true;
    editStaffMode.value = JSON.parse(JSON.stringify(staffAssignments.value));
  }

  async function saveChanges() {
    try {
      loading.value = true;

      const payload = editStaffMode.value.map(staff => ({
        ...staff,
        start_date: toLocalDateString(scheduleStart.value),
        end_date: toLocalDateString(scheduleEnd.value),
        status: staff.status || 'INACTIVE',
        machine_name: staff.status ? staff.machine_name.join('/') : null,
        id_machine: staff.status ? staff.id_machine.join('/') : null,
        work_shift: staff.status ? staff.work_shift : null
      }));

      const response = await fetch('/api/MachineLog/StaffSchedule', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!response.ok) throw new Error('Failed to update staff data');
      const result = await response.json();

      staffAssignments.value = JSON.parse(JSON.stringify(editStaffMode.value)).map(s => ({
        ...s,
        start_date: toLocalDateString(scheduleStart.value),
        end_date: toLocalDateString(scheduleEnd.value)
      }));

      editMode.value = false;
      editStaffMode.value = [];

    } catch (error) {
      console.error('Error updating staff:', error);
    } finally {
      loading.value = false;
    }
  }

  function cancelEdit() {
    editMode.value = false;
    editStaffMode.value = [];
  }

  function getMachineNames(machineNames) {
    if (!machineNames) return 'N/A';

    if (typeof machineNames === 'string') {
      return machineNames;
    }
    if (Array.isArray(machineNames)) {
      return machineNames.join(' / ');
    }

    return 'N/A';
  }

  function changeStatus(item, selectedMachines) {
    if (selectedMachines && selectedMachines.length > 0) {
      item.id_machine = [...selectedMachines];
      item.machine_name = selectedMachines
        .map(id => {
          const found = machineOptions.find(opt => opt.value === id);
          return found ? found.text : null;
        })
        .filter(Boolean);
    }
    else {
      item.id_machine = [];
      item.machine_name = [];
      item.status = 'INACTIVE';
    }
  }

  function changeShift(item, selectedShift) {
    if (selectedShift && !item.status) {
      item.status = 'ACTIVE';
    }
    else {
      item.status = 'INACTIVE';
    }
  }
</script>

<style scoped>
  .fixed-table :deep(table) {
    table-layout: fixed !important;
  }

  .fixed-table :deep(td) {
    padding: 1px !important;
    height: 40px;
  }

  .fixed-table :deep(td:has(span)) {
    padding: 0 12px !important;
  }
</style>
