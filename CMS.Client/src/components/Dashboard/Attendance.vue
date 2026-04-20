<template>
  <v-row no-gutters class="h-100">
    <!-- ── LEFT: SVG floor map ─────────────────────────────────────────── -->
    <v-col cols="9" class="h-100 d-flex flex-row" style="gap:6px; padding:6px;">

      <!-- 1st Floor card — narrow -->
      <v-card elevation="3" style="width:220px; min-width:220px; flex-shrink:0;
                                   display:flex; flex-direction:column; overflow:hidden;">
        <v-card-title class="pa-2 pb-1 d-flex align-center ga-1"
                      style="background:#E8EAF6; border-bottom:2px solid #5C6BC0;">
          <v-icon size="14" color="indigo-darken-2">mdi-stairs-up</v-icon>
          <span style="font-size:11px; font-weight:700; color:#283593;">1st Floor</span>
        </v-card-title>

        <div style="flex:1; overflow:hidden;">
          <svg width="100%" height="100%" viewBox="0 0 220 650" preserveAspectRatio="xMidYMid meet">
            <defs>
              <pattern id="grid1f" width="50" height="50" patternUnits="userSpaceOnUse">
                <path d="M 50 0 L 0 0 0 50" fill="none" stroke="#f0f0f0" stroke-width="1" />
              </pattern>
              <filter id="cardShadow1f" x="-10%" y="-10%" width="120%" height="130%">
                <feDropShadow dx="0" dy="2" stdDeviation="3" flood-color="rgba(0,0,0,0.15)" />
              </filter>
            </defs>
            <rect width="100%" height="100%" fill="url(#grid1f)" />

            <!-- Elbow lines for 1st floor groups -->
            <g v-for="(staffGroup, machineGroup) in firstFloorGroupedStaff"
               :key="'1f-lines-' + machineGroup">
              <path v-for="machine in machineGroup.split('/')"
                    :key="machine + '-1f-elbow'"
                    :d="buildElbow(machine.trim(), machineGroup, staffGroup, '1f')"
                    :stroke="getGroupColor(machineGroup, firstFloorGroupKeys)"
                    stroke-width="2"
                    stroke-dasharray="5,3"
                    fill="none"
                    opacity="0.8" />
            </g>

            <!-- 1st floor machines -->
            <g v-for="(position, machineKey) in firstFloorMachines" :key="'1f-' + machineKey">
              <rect :x="position.x" :y="position.y"
                    :width="position.width || 50" :height="position.height || 50"
                    :fill="machineStatus[machineKey] || '#9E9E9E'"
                    stroke="#000" stroke-width="2" rx="6" />
              <text :x="position.x + (position.width || 50) / 2"
                    :y="position.y + (position.height || 50) / 2"
                    text-anchor="middle" dominant-baseline="middle"
                    font-size="12" font-weight="bold" fill="black">
                {{ machineKey }}
              </text>
            </g>

            <!-- Staff cards for 1st floor -->
            <g v-for="(staffGroup, machineGroup) in firstFloorGroupedStaff"
               :key="'1f-cards-' + machineGroup">
              <rect :x="resolvedXForFloor(machineGroup, staffGroup, '1f')"
                    :y="getStaffYForGroup(machineGroup, staffGroup, '1f') - 6"
                    :width="getGroupCardWidth(staffGroup)"
                    :height="CARD_HEIGHT"
                    fill="rgba(255,255,255,0.95)"
                    :stroke="getGroupColor(machineGroup, firstFloorGroupKeys)"
                    stroke-width="1.5" rx="10"
                    filter="url(#cardShadow1f)" />

              <g v-for="(staff, staffIndex) in staffGroup" :key="'1f-' + staff.staff_id">
                <clipPath :id="`clip-1f-${staff.staff_id}`">
                  <circle :cx="resolvedAvatarCxForFloor(machineGroup, staffGroup, staffIndex, '1f')"
                          :cy="getStaffYForGroup(machineGroup, staffGroup, '1f') + 22"
                          r="18" />
                </clipPath>
                <circle :cx="resolvedAvatarCxForFloor(machineGroup, staffGroup, staffIndex, '1f')"
                        :cy="getStaffYForGroup(machineGroup, staffGroup, '1f') + 22"
                        r="20"
                        :fill="staff.photoError ? getRoleColor(staff.staff_role) : '#E3F2FD'"
                        stroke="white" stroke-width="2" />
                <image v-if="!staff.photoError"
                       :x="resolvedAvatarCxForFloor(machineGroup, staffGroup, staffIndex, '1f') - 18"
                       :y="getStaffYForGroup(machineGroup, staffGroup, '1f') + 4"
                       width="36" height="36"
                       :href="staff.photo_url"
                       :clip-path="`url(#clip-1f-${staff.staff_id})`"
                       preserveAspectRatio="xMidYMid slice"
                       @error="staff.photoError = true" />
                <text v-else
                      :x="resolvedAvatarCxForFloor(machineGroup, staffGroup, staffIndex, '1f')"
                      :y="getStaffYForGroup(machineGroup, staffGroup, '1f') + 26"
                      text-anchor="middle" dominant-baseline="middle"
                      font-size="12" font-weight="bold" fill="white">
                  {{ getInitials(staff.staff_name) }}
                </text>
                <text :x="resolvedAvatarCxForFloor(machineGroup, staffGroup, staffIndex, '1f')"
                      :y="getStaffYForGroup(machineGroup, staffGroup, '1f') + 55"
                      text-anchor="middle" font-size="10" fill="#424242" font-weight="600">
                  {{ staff.staff_name }}
                </text>
              </g>
            </g>
          </svg>
        </div>
      </v-card>

      <!-- Ground Floor card — fills remaining width -->
      <v-card elevation="3" style="flex:1; min-width:0; display:flex;
                                   flex-direction:column; overflow:hidden;">
        <v-card-title class="pa-2 pb-1 d-flex align-center ga-1"
                      style="background:#E8F5E9; border-bottom:2px solid #43A047;">
          <v-icon size="14" color="green-darken-2">mdi-domain</v-icon>
          <span style="font-size:11px; font-weight:700; color:#1B5E20;">Ground Floor</span>
        </v-card-title>

        <div style="flex:1; overflow:hidden;">
          <svg width="100%" height="100%" viewBox="0 0 1050 650">
            <defs>
              <pattern id="grid" width="50" height="50" patternUnits="userSpaceOnUse">
                <path d="M 50 0 L 0 0 0 50" fill="none" stroke="#f0f0f0" stroke-width="1" />
              </pattern>
              <filter id="cardShadow" x="-10%" y="-10%" width="120%" height="130%">
                <feDropShadow dx="0" dy="2" stdDeviation="3" flood-color="rgba(0,0,0,0.15)" />
              </filter>
            </defs>
            <rect width="100%" height="100%" fill="url(#grid)" />

            <!-- Elbow lines — ground floor -->
            <g v-for="(staffGroup, machineGroup) in groundFloorGroupedStaff"
               :key="'gf-lines-' + machineGroup">
              <path v-for="machine in machineGroup.split('/')"
                    :key="machine + '-gf-elbow'"
                    :d="buildElbow(machine.trim(), machineGroup, staffGroup, 'gf')"
                    :stroke="getGroupColor(machineGroup, groundFloorGroupKeys)"
                    stroke-width="2"
                    stroke-dasharray="5,3"
                    fill="none"
                    opacity="0.8" />
            </g>

            <!-- Ground floor machines -->
            <g v-for="(position, machineKey) in groundFloorMachines" :key="'gf-' + machineKey">
              <rect :x="position.x" :y="position.y"
                    :width="position.width || 50" :height="position.height || 50"
                    :fill="machineStatus[machineKey] || '#9E9E9E'"
                    stroke="#000" stroke-width="2" rx="6" />
              <text :x="position.x + (position.width || 50) / 2"
                    :y="position.y + (position.height || 50) / 2"
                    text-anchor="middle" dominant-baseline="middle"
                    font-size="18" font-weight="bold" fill="black">
                {{ machineKey }}
              </text>
            </g>

            <!-- Staff cards — ground floor -->
            <g v-for="(staffGroup, machineGroup) in groundFloorGroupedStaff"
               :key="'gf-cards-' + machineGroup">
              <rect :x="resolvedXForFloor(machineGroup, staffGroup, 'gf')"
                    :y="getStaffYForGroup(machineGroup, staffGroup, 'gf') - 6"
                    :width="getGroupCardWidth(staffGroup)"
                    :height="CARD_HEIGHT"
                    fill="rgba(255,255,255,0.95)"
                    :stroke="getGroupColor(machineGroup, groundFloorGroupKeys)"
                    stroke-width="1.5" rx="10"
                    filter="url(#cardShadow)" />

              <g v-for="(staff, staffIndex) in staffGroup" :key="'gf-' + staff.staff_id">
                <clipPath :id="`clip-gf-${staff.staff_id}`">
                  <circle :cx="resolvedAvatarCxForFloor(machineGroup, staffGroup, staffIndex, 'gf')"
                          :cy="getStaffYForGroup(machineGroup, staffGroup, 'gf') + 22"
                          r="18" />
                </clipPath>
                <circle :cx="resolvedAvatarCxForFloor(machineGroup, staffGroup, staffIndex, 'gf')"
                        :cy="getStaffYForGroup(machineGroup, staffGroup, 'gf') + 22"
                        r="20"
                        :fill="staff.photoError ? getRoleColor(staff.staff_role) : '#E3F2FD'"
                        stroke="white" stroke-width="2" />
                <image v-if="!staff.photoError"
                       :x="resolvedAvatarCxForFloor(machineGroup, staffGroup, staffIndex, 'gf') - 18"
                       :y="getStaffYForGroup(machineGroup, staffGroup, 'gf') + 4"
                       width="36" height="36"
                       :href="staff.photo_url"
                       :clip-path="`url(#clip-gf-${staff.staff_id})`"
                       preserveAspectRatio="xMidYMid slice"
                       @error="staff.photoError = true" />
                <text v-else
                      :x="resolvedAvatarCxForFloor(machineGroup, staffGroup, staffIndex, 'gf')"
                      :y="getStaffYForGroup(machineGroup, staffGroup, 'gf') + 26"
                      text-anchor="middle" dominant-baseline="middle"
                      font-size="12" font-weight="bold" fill="white">
                  {{ getInitials(staff.staff_name) }}
                </text>
                <text :x="resolvedAvatarCxForFloor(machineGroup, staffGroup, staffIndex, 'gf')"
                      :y="getStaffYForGroup(machineGroup, staffGroup, 'gf') + 55"
                      text-anchor="middle" font-size="10" fill="#424242" font-weight="600">
                  {{ staff.staff_name }}
                </text>
              </g>
            </g>
          </svg>
        </div>
      </v-card>

    </v-col>

    <!-- ── RIGHT panel ─────────────────────────────────────────────────── -->
    <v-col cols="3" class="h-100" style="background:#FAFAFA; border-left:1px solid #E0E0E0;">
      <div class="h-100 d-flex flex-column pa-2" style="gap:6px;">

        <!-- Supervisor + Line Leader side by side -->
        <v-row dense class="flex-grow-0 ma-0" style="gap:6px; flex-wrap:nowrap;">
          <v-col class="pa-0">
            <v-card elevation="1" style="height:100%;">
              <v-card-title class="pa-2 pb-1 d-flex align-center ga-1">
                <v-icon size="14" color="deep-purple">mdi-shield-account</v-icon>
                <span style="font-size:11px; font-weight:700;">Supervisor</span>
                <v-chip size="x-small" color="deep-purple" variant="tonal" class="ml-auto">{{ supervisor.length }}</v-chip>
              </v-card-title>
              <v-divider />
              <v-card-text class="pa-2">
                <div v-if="supervisor.length === 0"
                     class="d-flex flex-column align-center justify-center text-center py-2" style="opacity:0.45;">
                  <v-icon size="22" color="grey">mdi-account-off</v-icon>
                  <div style="font-size:10px; margin-top:2px;">None</div>
                </div>
                <div v-else class="d-flex flex-wrap justify-center" style="gap:6px;">
                  <div v-for="staff in supervisor" :key="staff.staff_id"
                       class="d-flex flex-column align-center" style="gap:3px;">
                    <v-avatar size="44" :color="staff.photoError ? getRoleColor(staff.staff_role) : undefined"
                              style="border:2px solid #fff; box-shadow:0 1px 4px rgba(0,0,0,0.18);">
                      <v-img v-if="!staff.photoError" :src="staff.photo_url" cover @error="staff.photoError = true" />
                      <v-icon v-else size="24" color="white">mdi-account</v-icon>
                    </v-avatar>
                    <div style="font-size:9px; font-weight:600; text-align:center;
                                max-width:56px; word-break:break-word; line-height:1.2; color:#424242;">
                      {{ staff.staff_name }}
                    </div>
                  </div>
                </div>
              </v-card-text>
            </v-card>
          </v-col>

          <v-col class="pa-0">
            <v-card elevation="1" style="height:100%;">
              <v-card-title class="pa-2 pb-1 d-flex align-center ga-1">
                <v-icon size="14" color="blue-darken-2">mdi-account-star</v-icon>
                <span style="font-size:11px; font-weight:700;">Line Leader</span>
                <v-chip size="x-small" color="blue-darken-2" variant="tonal" class="ml-auto">{{ lineleader.length }}</v-chip>
              </v-card-title>
              <v-divider />
              <v-card-text class="pa-2">
                <div v-if="lineleader.length === 0"
                     class="d-flex flex-column align-center justify-center text-center py-2" style="opacity:0.45;">
                  <v-icon size="22" color="grey">mdi-account-off</v-icon>
                  <div style="font-size:10px; margin-top:2px;">None</div>
                </div>
                <div v-else class="d-flex flex-wrap justify-center" style="gap:6px;">
                  <div v-for="staff in lineleader" :key="staff.staff_id"
                       class="d-flex flex-column align-center" style="gap:3px;">
                    <v-avatar size="44" :color="staff.photoError ? getRoleColor(staff.staff_role) : undefined"
                              style="border:2px solid #fff; box-shadow:0 1px 4px rgba(0,0,0,0.18);">
                      <v-img v-if="!staff.photoError" :src="staff.photo_url" cover @error="staff.photoError = true" />
                      <v-icon v-else size="24" color="white">mdi-account</v-icon>
                    </v-avatar>
                    <div style="font-size:9px; font-weight:600; text-align:center;
                                max-width:56px; word-break:break-word; line-height:1.2; color:#424242;">
                      {{ staff.staff_name }}
                    </div>
                  </div>
                </div>
              </v-card-text>
            </v-card>
          </v-col>
        </v-row>

        <!-- Material Handler -->
        <v-card elevation="1" class="flex-grow-1" style="min-height:0; display:flex; flex-direction:column;">
          <v-card-title class="pa-2 pb-1 d-flex align-center ga-1 flex-grow-0">
            <v-icon size="14" color="teal">mdi-hand-truck</v-icon>
            <span style="font-size:11px; font-weight:700;">Material Handler</span>
            <v-chip size="x-small" color="teal" variant="tonal" class="ml-auto">{{ mathandler.length }}</v-chip>
          </v-card-title>
          <v-divider />
          <v-card-text class="pa-2 flex-grow-1" style="overflow-y:auto; min-height:0;">
            <div v-if="mathandler.length === 0"
                 class="d-flex flex-column align-center justify-center text-center h-100"
                 style="opacity:0.45; min-height:60px;">
              <v-icon size="22" color="grey">mdi-account-off</v-icon>
              <div style="font-size:10px; margin-top:2px;">None assigned</div>
            </div>
            <div v-else class="d-flex flex-wrap justify-center" style="gap:8px;">
              <div v-for="staff in mathandler" :key="staff.staff_id"
                   class="d-flex flex-column align-center" style="gap:3px;">
                <v-avatar size="44" :color="staff.photoError ? getRoleColor(staff.staff_role) : undefined"
                          style="border:2px solid #fff; box-shadow:0 1px 4px rgba(0,0,0,0.18);">
                  <v-img v-if="!staff.photoError" :src="staff.photo_url" cover @error="staff.photoError = true" />
                  <v-icon v-else size="24" color="white">mdi-account</v-icon>
                </v-avatar>
                <div style="font-size:9px; font-weight:600; text-align:center;
                            max-width:56px; word-break:break-word; line-height:1.2; color:#424242;">
                  {{ staff.staff_name }}
                </div>
              </div>
            </div>
          </v-card-text>
        </v-card>

        <!-- Attendance -->
        <v-card elevation="1" class="flex-grow-0">
          <v-card-title class="pa-2 pb-1 d-flex align-center ga-1">
            <v-icon size="14" color="primary">mdi-clipboard-list</v-icon>
            <span style="font-size:11px; font-weight:700;">Attendance</span>
          </v-card-title>
          <v-divider />
          <v-card-text class="pa-2">
            <div class="d-flex align-center ga-1 mb-2">
              <span style="font-size:11px; color:#616161; width:50px;">Present</span>
              <v-progress-linear :model-value="planWork > 0 ? (actWork / planWork) * 100 : 0"
                                 color="success" bg-color="grey-lighten-3" height="10" rounded class="flex-grow-1" />
              <span style="font-size:11px; font-weight:700; min-width:32px; text-align:right;">
                {{ actWork }}/{{ planWork }}
              </span>
            </div>
            <v-divider class="mb-2" />
            <div style="display:flex; flex-direction:column; gap:4px;">
              <div class="d-flex align-center justify-space-between">
                <div class="d-flex align-center ga-1">
                  <v-icon size="12" color="orange">mdi-umbrella-beach</v-icon>
                  <span style="font-size:11px;">Annual Leave</span>
                </div>
                <v-chip size="x-small" :color="annualLeave > 0 ? 'orange' : 'grey-lighten-2'" variant="tonal">{{ annualLeave }}</v-chip>
              </div>
              <div class="d-flex align-center justify-space-between">
                <div class="d-flex align-center ga-1">
                  <v-icon size="12" color="red">mdi-hospital-box</v-icon>
                  <span style="font-size:11px;">Medical Leave</span>
                </div>
                <v-chip size="x-small" :color="medicalLeave > 0 ? 'red' : 'grey-lighten-2'" variant="tonal">{{ medicalLeave }}</v-chip>
              </div>
              <div class="d-flex align-center justify-space-between">
                <div class="d-flex align-center ga-1">
                  <v-icon size="12" color="grey">mdi-dots-horizontal-circle</v-icon>
                  <span style="font-size:11px;">Others</span>
                </div>
                <v-chip size="x-small" :color="othersLeave > 0 ? 'grey-darken-1' : 'grey-lighten-2'" variant="tonal">{{ othersLeave }}</v-chip>
              </div>
            </div>
          </v-card-text>
        </v-card>

      </div>
    </v-col>
  </v-row>
</template>

<script setup>
  import { ref, computed, onMounted } from 'vue';
  import { MACHINEPOSITIONS, ROLE_COLORS } from '@/utils/constant.js';

  defineProps({
    machineStatus: { type: Object, required: true }
  });

  // ── Layout constants ───────────────────────────────────────────────────────────
  const AVATAR_SIZE = 40;
  const AVATAR_PADDING = 8;
  const CARD_PAD_H = 10;
  const CARD_HEIGHT = 72;
  const CARD_MARGIN = 8;
  const ELBOW_DROP = 8;

  // One distinct colour per staff group
  const GROUP_COLORS = [
    '#E53935', // red
    '#8E24AA', // purple
    '#1E88E5', // blue
    '#00897B', // teal
    '#F4511E', // deep-orange
    '#43A047', // green
    '#FFB300', // amber
    '#6D4C41', // brown
    '#00ACC1', // cyan
    '#5E35B1', // deep-purple
  ];

  // ── State ─────────────────────────────────────────────────────────────────────
  const supervisor = ref([]);
  const lineleader = ref([]);
  const mathandler = ref([]);
  const groupedStaff = ref({});
  const planWork = ref(0);
  const actWork = ref(0);
  const annualLeave = ref(0);
  const medicalLeave = ref(0);
  const othersLeave = ref(0);
  const shift = ref(null);

  // ── Floor-split machine maps ───────────────────────────────────────────────────
  const groundFloorMachines = computed(() =>
    Object.fromEntries(Object.entries(MACHINEPOSITIONS).filter(([, v]) => v.floor === 0))
  );
  const firstFloorMachines = computed(() =>
    Object.fromEntries(Object.entries(MACHINEPOSITIONS).filter(([, v]) => v.floor === 1))
  );

  function getMachineGroupFloor(machineGroup) {
    const machines = machineGroup.split('/').map(m => m.trim());
    return machines.some(m => MACHINEPOSITIONS[m]?.floor === 1) ? 1 : 0;
  }

  const groundFloorGroupedStaff = computed(() =>
    Object.fromEntries(
      Object.entries(groupedStaff.value).filter(([key]) => getMachineGroupFloor(key) === 0)
    )
  );
  const firstFloorGroupedStaff = computed(() =>
    Object.fromEntries(
      Object.entries(groupedStaff.value).filter(([key]) => getMachineGroupFloor(key) === 1)
    )
  );

  const groundFloorGroupKeys = computed(() => Object.keys(groundFloorGroupedStaff.value));
  const firstFloorGroupKeys = computed(() => Object.keys(firstFloorGroupedStaff.value));

  // ── Helpers ───────────────────────────────────────────────────────────────────

  function getShift() {
    const h = new Date().getHours();
    return (h >= 6 && h < 18) ? 1 : 2;
  }

  function getStaffPhotoUrl(staff_id) {
    return `/api/dashboard/StaffPhoto/${staff_id}`;
  }

  function getRoleColor(role) {
    return (ROLE_COLORS && ROLE_COLORS[role]) || '#78909C';
  }

  function getInitials(name) {
    if (!name) return '?';
    return name.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
  }

  function getGroupColor(machineGroup, floorGroupKeys) {
    const keys = Array.isArray(floorGroupKeys)
      ? floorGroupKeys
      : (floorGroupKeys?.value ?? []);
    const idx = keys.indexOf(machineGroup);
    return GROUP_COLORS[Math.max(0, idx) % GROUP_COLORS.length];
  }

  // ── Data fetch ────────────────────────────────────────────────────────────────

  onMounted(async () => {
    shift.value = getShift();
    await fetchAttendance();
  });

  async function fetchAttendance() {
    try {
      const res = await fetch('/api/dashboard/Attendance');
      const data = await res.json();

      const enrich = item => ({
        ...item,
        photo_url: getStaffPhotoUrl(item.staff_id),
        photoError: false
      });

      supervisor.value = data.filter(s => s.staff_role === 'SUPERVISOR' && s.status === 'ACTIVE').map(enrich);
      lineleader.value = data.filter(s => s.staff_role === 'LINE LEADER' && s.status === 'ACTIVE').map(enrich);
      mathandler.value = data.filter(s => s.staff_role === 'MATERIAL HANDLER' && s.status === 'ACTIVE').map(enrich);

      planWork.value = data.filter(i => i.shift == shift.value).length;
      actWork.value = data.filter(i => i.shift == shift.value && i.status === 'ACTIVE').length;
      annualLeave.value = data.filter(i => i.status === 'ANNUAL LEAVE').length;
      medicalLeave.value = data.filter(i => i.status === 'MEDICAL LEAVE').length;
      othersLeave.value = data.filter(i => i.status === 'OTHER LEAVE').length;

      const grouped = {};
      data.forEach(item => {
        const key = item.machine_name || '';
        if (!key) return;
        if (!grouped[key]) grouped[key] = [];
        grouped[key].push({
          ...enrich(item),
          staff_name: item.staff_name?.split(' ')[0] || ''
        });
      });
      groupedStaff.value = grouped;
    } catch (err) {
      console.error('Error fetching attendance:', err);
    }
  }

  // ── SVG geometry ──────────────────────────────────────────────────────────────

  function getGroupCardWidth(staffGroup) {
    return CARD_PAD_H * 2
      + staffGroup.length * AVATAR_SIZE
      + Math.max(0, staffGroup.length - 1) * AVATAR_PADDING;
  }

  function getStaffYForGroup(machineGroup) {
    const machines = machineGroup.split('/').map(m => m.trim());
    let topMostY = Infinity;
    let topMostBottom = 100;
    machines.forEach(m => {
      const pos = MACHINEPOSITIONS[m];
      if (!pos) return;
      if (pos.y < topMostY) {
        topMostY = pos.y;
        topMostBottom = pos.y + (pos.height || 50);
      }
    });
    return topMostBottom + 16;
  }

  function naturalAnchorX(machineGroup) {
    const machines = machineGroup.split('/').map(m => m.trim());
    if (machines.length === 1) {
      const pos = MACHINEPOSITIONS[machines[0]];
      return pos ? pos.x + (pos.width || 50) / 2 : 100;
    }
    let minX = Infinity, maxX = -Infinity, valid = 0;
    machines.forEach(m => {
      const pos = MACHINEPOSITIONS[m];
      if (!pos) return;
      minX = Math.min(minX, pos.x);
      maxX = Math.max(maxX, pos.x + (pos.width || 50));
      valid++;
    });
    return valid > 0 ? (minX + maxX) / 2 : 100;
  }

  function naturalCardX(machineGroup, staffGroup) {
    return naturalAnchorX(machineGroup) - getGroupCardWidth(staffGroup) / 2;
  }

  const resolvedCardXMapGF = computed(() => resolveCollisions(groundFloorGroupedStaff.value));
  const resolvedCardXMap1F = computed(() => resolveCollisions(firstFloorGroupedStaff.value));

  function resolveCollisions(floorGroupedStaff) {
    const keys = Object.keys(floorGroupedStaff);
    if (!keys.length) return {};

    const cards = keys.map(key => {
      const group = floorGroupedStaff[key];
      return {
        key,
        x: naturalCardX(key, group),
        w: getGroupCardWidth(group),
        cardY: getStaffYForGroup(key) - 6,
      };
    });

    const MAX_ITER = 60;
    for (let iter = 0; iter < MAX_ITER; iter++) {
      let moved = false;
      for (let i = 0; i < cards.length; i++) {
        for (let j = i + 1; j < cards.length; j++) {
          const a = cards[i], b = cards[j];
          if (Math.abs(a.cardY - b.cardY) >= CARD_HEIGHT) continue;
          const overlap = Math.min(a.x + a.w, b.x + b.w) - Math.max(a.x, b.x);
          if (overlap <= 0) continue;
          const push = (overlap + CARD_MARGIN) / 2;
          if (a.x + a.w / 2 <= b.x + b.w / 2) { a.x -= push; b.x += push; }
          else { a.x += push; b.x -= push; }
          moved = true;
        }
      }
      if (!moved) break;
    }

    const map = {};
    cards.forEach(c => { map[c.key] = c.x; });
    return map;
  }

  function resolvedXForFloor(machineGroup, staffGroup, floorTag) {
    const map = floorTag === '1f' ? resolvedCardXMap1F.value : resolvedCardXMapGF.value;
    return map[machineGroup] ?? naturalCardX(machineGroup, staffGroup);
  }

  function resolvedCardCenterXForFloor(machineGroup, staffGroup, floorTag) {
    return resolvedXForFloor(machineGroup, staffGroup, floorTag) + getGroupCardWidth(staffGroup) / 2;
  }

  function resolvedAvatarCxForFloor(machineGroup, staffGroup, staffIndex, floorTag) {
    const cardX = resolvedXForFloor(machineGroup, staffGroup, floorTag);
    return cardX + CARD_PAD_H + AVATAR_SIZE / 2 + staffIndex * (AVATAR_SIZE + AVATAR_PADDING);
  }

  function buildElbow(machineName, machineGroup, staffGroup, floorTag) {
    const pos = MACHINEPOSITIONS[machineName];
    if (!pos || !staffGroup) return '';

    const dx = resolvedCardCenterXForFloor(machineGroup, staffGroup, floorTag);
    const cardTopY = getStaffYForGroup(machineGroup) - 6;
    const cardBotY = cardTopY + CARD_HEIGHT;

    const cardIsAbove = cardBotY <= pos.y;

    const mx = pos.x + (pos.width || 50) / 2;
    const my = cardIsAbove ? pos.y : pos.y + (pos.height || 50);
    const dy = cardIsAbove ? cardBotY : cardTopY;
    const elbowY = cardIsAbove ? my - ELBOW_DROP : my + ELBOW_DROP;

    if (Math.abs(mx - dx) < 2) return `M ${mx} ${my} L ${mx} ${dy}`;
    return `M ${mx} ${my} L ${mx} ${elbowY} L ${dx} ${elbowY} L ${dx} ${dy}`;
  }
</script>
