<template>
  <v-row no-gutters class="h-100">
    <v-col cols="9" class="h-100">
      <v-container fluid class="h-100">
        <v-row class="h-100">
          <v-col cols="12" class="h-100 d-flex flex-column">
            <svg width="100%"
                 height="100%"
                 viewBox="0 0 1050 650">
              <defs>
                <pattern id="grid" width="50" height="50" patternUnits="userSpaceOnUse">
                  <path d="M 50 0 L 0 0 0 50" fill="none" stroke="#f5f5f5" stroke-width="1" />
                </pattern>
              </defs>
              <rect width="100%" height="100%" fill="url(#grid)" />

              <g v-for="(position, machineKey) in machinePositions" :key="machineKey">
                <rect :x="position.x"
                      :y="position.y"
                      :width="position.width || 50"
                      :height="position.height || 50"
                      :fill="machineStatus[machineKey] || 'grey'"
                      stroke="#333"
                      stroke-width="2"
                      rx="4" />
                <text :x="position.x + (position.width || 50) / 2"
                      :y="position.y + (position.height || 50) / 2"
                      text-anchor="middle"
                      dominant-baseline="middle"
                      font-size="12"
                      font-weight="bold"
                      fill="black">
                  {{ machineKey }}
                </text>
                <g v-for="(staffGroup, machineGroup) in groupedStaff" :key="machineGroup">
                  <g v-for="machine in machineGroup.split('/')" :key="machine + '-line'">
                    <line v-if="machinePositions[machine.trim()]"
                          :x1="machinePositions[machine.trim()].x + (machinePositions[machine.trim()].width || 50) / 2"
                          :y1="machinePositions[machine.trim()].y + (machinePositions[machine.trim()].height || 50) / 2"
                          :x2="getStaffCoordinates(machineGroup).x"
                          :y2="getStaffCoordinates(machineGroup).y + 40"
                          stroke="#555"
                          stroke-width="5" />
                  </g>
                  <rect :x="getStaffCoordinates(machineGroup).x - 50 - (staffGroup.length - 1) * 30"
                        :y="getStaffCoordinates(machineGroup).y - 5"
                        :width="100 + (staffGroup.length - 1) * 60"
                        :height="90"
                        fill="rgba(255, 255, 255, 0.5)"
                        stroke="#666"
                        stroke-width="2"
                        stroke-dasharray="5,5"
                        rx="8" />
                  <g v-for="(staff, staffIndex) in staffGroup" :key="staff.staff_id">
                    <image :x="getStaffCoordinates(machineGroup).x - 25 + (staffIndex * 60) - (staffGroup.length - 1) * 30"
                           :y="getStaffCoordinates(machineGroup).y"
                           :href="getStaffPhoto(staff.staff_id)"
                           width="50"
                           height="60"
                           style="object-fit: cover; border: 1px solid #ccc;" />
                    <text :x="getStaffCoordinates(machineGroup).x + (staffIndex * 60) - (staffGroup.length - 1) * 30"
                          :y="getStaffCoordinates(machineGroup).y + 75"
                          text-anchor="middle"
                          font-size="10"
                          fill="#333"
                          font-weight="500">
                      {{ staff.staff_name }}
                    </text>
                  </g>
                </g>
              </g>
            </svg>
          </v-col>
        </v-row>
      </v-container>
    </v-col>

    <v-col cols="3" class="h-100">
      <v-container fluid class="h-100 d-flex flex-column">
        <div class="d-flex flex-column" style="flex-basis: 70%; max-height: 70%; overflow: hidden;">
          <v-row no-gutters class="justify-center flex-grow-0">
            <v-col cols="6">
              <v-row no-gutters>
                <v-col cols="12" class="text-center">
                  <h4 class="font-weight-bold" style="margin-bottom: 4px;">Supervisor</h4>
                </v-col>

                <v-col v-for="staff in supervisor"
                       :key="staff.staff_id"
                       class="text-center"
                       style="padding: 0 4px;">
                  <div class="d-flex flex-column align-center">
                    <v-img :src="getStaffPhoto(staff.staff_id)"
                           width="45"
                           height="65"
                           class="ma-1"
                           style="object-fit: cover; border: 1px solid #ccc;" />
                    <div class="mt-1 text-caption"
                         style="max-width: 90px; word-wrap: break-word; line-height: 1.1;">
                      {{ staff.staff_name }}
                    </div>
                  </div>
                </v-col>
              </v-row>
            </v-col>

            <v-col cols="6">
              <v-row no-gutters>
                <v-col cols="12" class="text-center">
                  <h4 class="font-weight-bold" style="margin-bottom: 4px;">Line Leader</h4>
                </v-col>

                <v-col v-for="staff in lineleader"
                       :key="staff.staff_id"
                       class="text-center"
                       style="padding: 0 4px;">
                  <div class="d-flex flex-column align-center">
                    <v-img :src="getStaffPhoto(staff.staff_id)"
                           width="45"
                           height="65"
                           class="ma-1"
                           style="object-fit: cover; border: 1px solid #ccc;" />
                    <div class="mt-1 text-caption"
                         style="max-width: 90px; word-wrap: break-word; line-height: 1.1;">
                      {{ staff.staff_name }}
                    </div>
                  </div>
                </v-col>
              </v-row>
            </v-col>
          </v-row>

          <v-row no-gutters class="justify-center" style="flex-grow: 1; overflow: hidden;">
            <v-col cols="12" class="text-center">
              <h4 class="font-weight-bold" style="margin-bottom: 4px;">Material Handler</h4>
            </v-col>

            <v-col v-for="staff in mathandler"
                   :key="staff.staff_id"
                   cols="auto"
                   class="text-center"
                   style="padding: 0 4px;">
              <div class="d-flex flex-column align-center">
                <v-img :src="getStaffPhoto(staff.staff_id)"
                       width="45"
                       height="65"
                       class="ma-1"
                       style="object-fit: cover; border: 1px solid #ccc;" />
                <div class="mt-1 text-caption"
                     style="max-width: 90px; word-wrap: break-word; line-height: 1.1;">
                  {{ staff.staff_name }}
                </div>
              </div>
            </v-col>
          </v-row>
        </div>

        <div class="d-flex flex-column mt-1">
          <v-card variant="outlined" class="pa-2 flex-grow-1" style="overflow: hidden;">
            <v-card-title class="text-center font-weight-bold pa-1"
                          style="font-size: 14px;">
              Attendance
            </v-card-title>

            <v-card-text class="pa-1" style="font-size: 12px; line-height: 1.2;">
              <v-row dense no-gutters style="font-size: 11px; line-height: 1.05;">
                <v-col cols="6" class="py-0 my-0">Planned</v-col>
                <v-col cols="6" class="text-right py-0 my-0">{{ planWork }}</v-col>

                <v-col cols="6" class="py-0 my-0">Actual</v-col>
                <v-col cols="6" class="text-right py-0 my-0">{{ actWork }}</v-col>

                <v-col cols="12" class="py-0 my-1">
                  <v-divider class="ma-0"></v-divider>
                </v-col>

                <v-col cols="6" class="py-0 my-0">Annual Leave</v-col>
                <v-col cols="6" class="text-right py-0 my-0">{{ annualLeave }}</v-col>

                <v-col cols="6" class="py-0 my-0">Medical Leave</v-col>
                <v-col cols="6" class="text-right py-0 my-0">{{ medicalLeave }}</v-col>

                <v-col cols="6" class="py-0 my-0">Others</v-col>
                <v-col cols="6" class="text-right py-0 my-0">{{ othersLeave }}</v-col>
              </v-row>
            </v-card-text>
          </v-card>
        </div>
      </v-container>
    </v-col>
  </v-row>
</template>

<script setup>
  import { ref, onMounted, onUnmounted, computed } from 'vue'
  import { MACHINEPOSITIONS } from '@/store/constant.js';

  defineProps({
    machineStatus: {
      type: Object,
      required: true
    }
  })

  const supervisor = ref([]);
  const lineleader = ref([]);
  const mathandler = ref([]);
  const groupedStaff = ref({})
  const machineArrays = ref({})
  const planWork = ref(0);
  const actWork = ref(0);
  const annualLeave = ref(0);
  const medicalLeave = ref(0);
  const othersLeave = ref(0);
  const machinePositions = MACHINEPOSITIONS;
  const shift = ref(null);

  onMounted(async () => {
    shift.value = getShift();
    const result = await fetchAttendance();
    groupedStaff.value = result.groupedStaff;
    machineArrays.value = result.machineArrays;
    supervisor.value = result.supervisor;
    lineleader.value = result.lineleader;
  })

  function getShift() {
    const now = new Date();
    const currentHour = now.getHours();
    return (currentHour >= 6 && currentHour < 18) ? 1 : 2;
  }

  async function fetchAttendance() {
    try {
      const response = await fetch('/api/MachineLog/Attendance');
      const data = await response.json();
        
      supervisor.value = data.filter(staff => staff.staff_role === 'SUPERVISOR' && staff.status === 'ACTIVE');
      lineleader.value = data.filter(staff => staff.staff_role === 'LINE LEADER' && staff.status === 'ACTIVE');
      mathandler.value = data.filter(staff => staff.staff_role === 'MATERIAL HANDLER' && staff.status === 'ACTIVE');

      planWork.value = data.filter(item => item.shift == shift.value).length;
      actWork.value = data.filter(item => item.shift == shift.value && item.status === 'ACTIVE').length;

      annualLeave.value = data.filter(item => item.status === 'ANNUAL LEAVE' && item.shift == shift.value).length;
      medicalLeave.value = data.filter(item => item.status === 'MEDICAL LEAVE' && item.shift == shift.value).length;
      othersLeave.value = data.filter(item => item.status === 'OTHERS' && item.shift == shift.value).length;

      const groupedStaff = {};

      data.forEach(item => {
        const machineGroup = item.machine_name || '';

        if (!groupedStaff[machineGroup] && machineGroup !== '') {
          groupedStaff[machineGroup] = [];
        }
        if (machineGroup !== '') {
          groupedStaff[machineGroup].push({
            ...item,
            staff_name: item.staff_name?.split(' ')[0] || '',
          });
        }
      });

      const machineNameArrays = {};
      Object.keys(groupedStaff).forEach(machineGroup => {
        machineNameArrays[machineGroup] = machineGroup.split('/');
      });

      return {
        groupedStaff: groupedStaff || {},
        machineArrays: machineNameArrays || {},
        supervisor: supervisor.value || [],
        lineleader: lineleader.value || [],
        mathandler: mathandler.value || [],
        planWork: planWork.value || 0,
        actWork: actWork.value || 0,
        annualLeave: annualLeave.value || 0,
        medicalLeave: medicalLeave.value || 0,
        othersLeave: othersLeave.value || 0,
      };
    } catch (error) {
      console.error("Error fetching staff attendance data:", error);
    }
  }

  function getStaffPhoto(staff_id) {
    const photoUrl = `/api/MachineLog/StaffPhoto/${staff_id}?t=${Date.now()}`;
    return photoUrl;
  }

  function getStaffCoordinates(machineGroup) {
    const machines = machineGroup.split('/');

    if (machines.length === 1) {
      const position = machinePositions[machines[0]];
      if (position) {
        return {
          x: position.x + (position.width || 50) / 2,
          y: position.y + (position.height || 50) + 10
        };
      }
    } else {
      let minX = Infinity;
      let maxX = -Infinity;
      let minY = Infinity;
      let validMachines = 0;

      machines.forEach(machine => {
        const position = machinePositions[machine.trim()];
        if (position) {
          const machineStartX = position.x;
          const machineEndX = position.x + (position.width || 50);
          const machineStartY = position.y + (position.height || 50);

          minX = Math.min(minX, machineStartX);
          maxX = Math.max(maxX, machineEndX);
          minY = Math.min(minY, machineStartY);
          validMachines++;
        }
      });

      if (validMachines > 0) {
        const centerX = (minX + maxX) / 2;
        const centerY = minY + 10;

        return { x: centerX, y: centerY };
      }
    }
    return { x: 100, y: 100 };
  }
</script>
