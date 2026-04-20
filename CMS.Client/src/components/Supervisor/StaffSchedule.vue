<template>
  <v-container fluid class="pa-0" style="height: 81vh; overflow: hidden; display: flex; flex-direction: column;">
    <v-row dense align="center" class="mb-2 flex-grow-0 px-1 pt-1">
      <v-col cols="auto">
        <div class="d-flex align-center ga-2">
          <v-icon color="primary">mdi-account-multiple</v-icon>
          <span class="text-subtitle-1 font-weight-bold">Staff Schedule</span>
          <v-chip size="small" variant="tonal" color="primary">{{ formattedDate }}</v-chip>
        </div>
      </v-col>
      <v-spacer />
      <v-col cols="auto" class="d-flex align-center ga-2">
        <div class="d-flex align-center ga-1">
          <v-date-input v-model="scheduleStart"
                        label="Start Date"
                        :max="scheduleEnd"
                        variant="outlined"
                        density="compact"
                        hide-details
                        display-format="fullDate"
                        style="width: 200px;" />
        </div>
        <div class="d-flex align-center ga-1">
          <v-date-input v-model="scheduleEnd"
                        label="End Date"
                        :min="scheduleStart"
                        variant="outlined"
                        density="compact"
                        hide-details
                        display-format="fullDate"
                        style="width: 200px;" />
        </div>
        <v-btn color="primary" prepend-icon="mdi-content-save" size="small" :loading="saving" @click="saveChanges">Save</v-btn>
        <v-btn variant="tonal" prepend-icon="mdi-refresh" size="small" :loading="loading" @click="loadStaffData">Refresh</v-btn>
      </v-col>
    </v-row>

    <v-row dense class="flex-grow-1 px-1 pb-1" style="min-height: 0; overflow: hidden;">

      <!-- ─── Staff Pool ──────────────────────────────────────────────────── -->
      <v-col cols="3" style="height: 100%; display: flex; flex-direction: column;">
        <v-card elevation="2" style="flex: 1; display: flex; flex-direction: column; overflow: hidden;">
          <v-card-title class="text-subtitle-2 pa-2 pb-1 d-flex align-center ga-2 flex-grow-0">
            <v-icon size="18" color="primary">mdi-account-group</v-icon>
            Staff Pool
            <v-chip size="x-small" color="primary" variant="tonal">{{ poolList.length }}</v-chip>
            <v-spacer />
            <v-btn icon size="x-small" color="primary" variant="tonal" @click="openAddDialog">
              <v-icon size="16">mdi-plus</v-icon>
            </v-btn>
          </v-card-title>
          <v-divider />
          <div class="pa-2 flex-grow-0">
            <v-text-field v-model="searchQuery" prepend-inner-icon="mdi-magnify" placeholder="Search staff..."
                          density="compact" hide-details variant="outlined" clearable />
          </div>
          <div class="pa-2 pt-0 flex-grow-1" style="overflow-y: auto; min-height: 0;">
            <draggable v-model="poolList"
                       :group="{ name: 'staff', pull: true, put: true }"
                       item-key="staff_id"
                       class="staff-pool"
                       :animation="150"
                       ghost-class="drag-ghost"
                       @add="onAddToPool">
              <template #item="{ element: staff }">
                <template v-if="!searchQuery || staff.staff_name?.toLowerCase().includes(searchQuery.toLowerCase())">
                  <v-card class="staff-card" elevation="1">
                    <v-card-text class="pa-1 d-flex flex-column align-center text-center" style="position: relative;">
                      <div style="position: absolute; top: 2px; right: 2px; display: flex; gap: 2px; z-index: 1;">
                        <v-btn icon size="x-small" variant="plain" @click.stop="openEditDialog(staff)">
                          <v-icon size="12" color="primary">mdi-pencil</v-icon>
                        </v-btn>
                        <v-btn icon size="x-small" variant="plain" @click.stop="confirmDelete(staff)">
                          <v-icon size="12" color="error">mdi-delete</v-icon>
                        </v-btn>
                      </div>
                      <v-avatar size="44" :color="getAvatarColor(staff.staff_role)" class="mb-1">
                        <v-img v-if="!staff.photoError"
                               :src="staff.photo_url"
                               :alt="staff.staff_name"
                               cover
                               @error="staff.photoError = true" />
                        <v-icon v-else size="28" color="white">mdi-account</v-icon>
                      </v-avatar>
                      <div class="text-caption font-weight-medium" style="line-height: 1.2; width: 72px; word-break: break-word;">
                        {{ staff.staff_name }}
                      </div>
                      <v-chip size="x-small" :color="ROLE_COLORS[staff.staff_role] || 'grey'" class="mt-1" style="font-size: 9px;">
                        {{ staff.staff_role || 'N/A' }}
                      </v-chip>
                    </v-card-text>
                  </v-card>
                </template>
              </template>
              <template #footer>
                <div v-if="poolList.length === 0" class="text-center text-caption text-medium-emphasis pa-4 w-100">
                  <v-icon size="32" color="grey-lighten-1">mdi-account-off</v-icon>
                  <div>No unscheduled staff</div>
                </div>
              </template>
            </draggable>
          </div>
        </v-card>
      </v-col>

      <!-- ─── Shifts + Leave ─────────────────────────────────────────────── -->
      <v-col cols="9" style="height: 100%; display: flex; flex-direction: column;">
        <v-row dense style="flex: 1; min-height: 0; overflow: hidden; height: 100%;">

          <!-- ── Morning & Night Shift ── -->
          <v-col cols="8" style="height: 100%; display: flex; flex-direction: column; gap: 8px;">

            <!-- Morning Shift -->
            <v-card elevation="2" style="flex: 1; display: flex; flex-direction: column; overflow: hidden; min-height: 0;">
              <v-card-title class="pa-2 d-flex align-center ga-2 bg-orange-lighten-5 flex-grow-0">
                <v-icon color="orange-darken-2" size="18">mdi-weather-sunny</v-icon>
                <span class="text-subtitle-2">Morning Shift</span>
                <v-chip size="x-small" color="orange" variant="tonal">{{ morningList.length }}</v-chip>
              </v-card-title>
              <v-divider />
              <div class="flex-grow-1" style="overflow-y: auto; min-height: 0; position: relative;">
                <div v-if="morningList.length === 0" class="drop-hint-overlay">
                  <draggable v-model="morningList"
                             :group="{ name: 'staff', pull: true, put: true }"
                             item-key="staff_id"
                             :animation="150"
                             ghost-class="drag-ghost"
                             class="shift-drop-empty"
                             @add="(e) => onAddToShift(e, 1)">
                    <template #item="{}">
                      <span></span>
                    </template>
                    <template #footer>
                      <div class="text-center text-caption text-medium-emphasis pa-4">
                        <v-icon size="24" color="orange-lighten-3">mdi-drag</v-icon>
                        <div>Drag staff here</div>
                      </div>
                    </template>
                  </draggable>
                </div>
                <div v-else class="pa-1">
                  <v-table density="compact" class="shift-table">
                    <thead>
                      <tr>
                        <th class="text-caption pa-1" style="width:36px;"></th>
                        <th class="text-caption pa-1">Name</th>
                        <th class="text-caption pa-1">Role</th>
                        <th class="text-caption pa-1">Machine</th>
                        <th class="text-caption pa-1" style="width:32px;"></th>
                      </tr>
                    </thead>
                    <draggable v-model="morningList"
                               :group="{ name: 'staff', pull: true, put: true }"
                               item-key="staff_id"
                               tag="tbody"
                               :animation="150"
                               ghost-class="drag-ghost-row"
                               @add="(e) => onAddToShift(e, 1)">
                      <template #item="{ element: staff }">
                        <tr class="shift-row">
                          <td class="pa-1">
                            <v-avatar size="28" :color="getAvatarColor(staff.staff_role)">
                              <v-img v-if="!staff.photoError" :src="staff.photo_url" cover @error="staff.photoError = true" />
                              <v-icon v-else size="18" color="white">mdi-account</v-icon>
                            </v-avatar>
                          </td>
                          <td class="text-caption pa-1">{{ staff.staff_name }}</td>
                          <td class="pa-1">
                            <v-chip size="x-small" :color="ROLE_COLORS[staff.staff_role] || 'grey'" style="font-size:9px;">
                              {{ staff.staff_role || 'N/A' }}
                            </v-chip>
                          </td>
                          <td class="pa-1">
                            <v-select v-if="staff.staff_role === 'PACKER'"
                                      v-model="staff.id_machine"
                                      :items="machineOptions"
                                      item-title="text"
                                      item-value="value"
                                      density="compact"
                                      hide-details
                                      variant="outlined"
                                      multiple
                                      placeholder="Machine"
                                      style="min-width:90px; font-size:9px;"
                                      @update:model-value="updateMachineNames(staff)" />
                            <span v-else class="text-caption text-medium-emphasis">—</span>
                          </td>
                          <td class="pa-1 text-center">
                            <v-btn icon size="x-small" variant="plain" color="error" @click.stop="removeToPool(staff, 'morning')">
                              <v-icon size="14">mdi-close</v-icon>
                            </v-btn>
                          </td>
                        </tr>
                      </template>
                    </draggable>
                  </v-table>
                </div>
              </div>
            </v-card>

            <!-- Night Shift -->
            <v-card elevation="2" style="flex: 1; display: flex; flex-direction: column; overflow: hidden; min-height: 0;">
              <v-card-title class="pa-2 d-flex align-center ga-2 bg-indigo-lighten-5 flex-grow-0">
                <v-icon color="indigo-darken-2" size="18">mdi-weather-night</v-icon>
                <span class="text-subtitle-2">Night Shift</span>
                <v-chip size="x-small" color="indigo" variant="tonal">{{ nightList.length }}</v-chip>
              </v-card-title>
              <v-divider />
              <div class="flex-grow-1" style="overflow-y: auto; min-height: 0; position: relative;">
                <div v-if="nightList.length === 0" class="drop-hint-overlay">
                  <draggable v-model="nightList"
                             :group="{ name: 'staff', pull: true, put: true }"
                             item-key="staff_id"
                             :animation="150"
                             ghost-class="drag-ghost"
                             class="shift-drop-empty"
                             @add="(e) => onAddToShift(e, 2)">
                    <template #item="{}">
                      <span></span>
                    </template>
                    <template #footer>
                      <div class="text-center text-caption text-medium-emphasis pa-4">
                        <v-icon size="24" color="indigo-lighten-3">mdi-drag</v-icon>
                        <div>Drag staff here</div>
                      </div>
                    </template>
                  </draggable>
                </div>
                <div v-else class="pa-1">
                  <v-table density="compact" class="shift-table">
                    <thead>
                      <tr>
                        <th class="text-caption pa-1" style="width:36px;"></th>
                        <th class="text-caption pa-1">Name</th>
                        <th class="text-caption pa-1">Role</th>
                        <th class="text-caption pa-1">Machine</th>
                        <th class="text-caption pa-1" style="width:32px;"></th>
                      </tr>
                    </thead>
                    <draggable v-model="nightList"
                               :group="{ name: 'staff', pull: true, put: true }"
                               item-key="staff_id"
                               tag="tbody"
                               :animation="150"
                               ghost-class="drag-ghost-row"
                               @add="(e) => onAddToShift(e, 2)">
                      <template #item="{ element: staff }">
                        <tr class="shift-row">
                          <td class="pa-1">
                            <v-avatar size="28" :color="getAvatarColor(staff.staff_role)">
                              <v-img v-if="!staff.photoError" :src="staff.photo_url" cover @error="staff.photoError = true" />
                              <v-icon v-else size="18" color="white">mdi-account</v-icon>
                            </v-avatar>
                          </td>
                          <td class="text-caption pa-1">{{ staff.staff_name }}</td>
                          <td class="pa-1">
                            <v-chip size="x-small" :color="ROLE_COLORS[staff.staff_role] || 'grey'" style="font-size:9px;">
                              {{ staff.staff_role || 'N/A' }}
                            </v-chip>
                          </td>
                          <td class="pa-1">
                            <v-select v-if="staff.staff_role === 'PACKER'"
                                      v-model="staff.id_machine"
                                      :items="machineOptions"
                                      item-title="text"
                                      item-value="value"
                                      density="compact"
                                      hide-details
                                      variant="outlined"
                                      multiple
                                      placeholder="Machine"
                                      style="min-width:90px; font-size:9px;"
                                      @update:model-value="updateMachineNames(staff)" />
                            <span v-else class="text-caption text-medium-emphasis">—</span>
                          </td>
                          <td class="pa-1 text-center">
                            <v-btn icon size="x-small" variant="plain" color="error" @click.stop="removeToPool(staff, 'night')">
                              <v-icon size="14">mdi-close</v-icon>
                            </v-btn>
                          </td>
                        </tr>
                      </template>
                    </draggable>
                  </v-table>
                </div>
              </div>
            </v-card>

          </v-col>

          <!-- ── Morning Leave ── -->
          <v-col cols="4" style="height: 100%; display: flex; flex-direction: column;">
            <v-card elevation="2" style="flex: 1; display: flex; flex-direction: column; overflow: hidden;">
              <v-card-title class="pa-2 d-flex align-center ga-2 bg-orange-lighten-5 flex-grow-0">
                <v-icon color="orange-darken-2" size="18">mdi-weather-sunny</v-icon>
                <span class="text-subtitle-2">Morning Leave / Absent</span>
                <v-chip size="x-small" color="orange" variant="tonal">{{ totalMorningLeaveCount }}</v-chip>
              </v-card-title>
              <v-divider />
              <v-tabs v-model="morningLeaveTab" density="compact" color="red" class="flex-grow-0">
                <v-tab value="Annual Leave" class="text-caption">
                  <v-icon start size="13">mdi-umbrella-beach</v-icon>Annual
                </v-tab>
                <v-tab value="Medical Leave" class="text-caption">
                  <v-icon start size="13">mdi-hospital-box</v-icon>Medical
                </v-tab>
                <v-tab value="Other Leave" class="text-caption">
                  <v-icon start size="13">mdi-dots-horizontal</v-icon>Others
                </v-tab>
              </v-tabs>
              <v-divider />
              <div class="flex-grow-1 pa-2" style="overflow-y: auto; min-height: 0;">

                <draggable v-show="morningLeaveTab === 'Annual Leave'"
                           v-model="morningLeaveAnnual"
                           :group="{ name: 'staff', pull: true, put: true }"
                           item-key="staff_id"
                           :animation="150"
                           ghost-class="drag-ghost"
                           class="leave-drop-zone"
                           @add="(e) => onAddToLeave(e, 1, 'ANNUAL LEAVE')">
                  <template #item="{ element: staff }">
                    <v-card class="staff-card-leave mb-1" elevation="1">
                      <v-card-text class="pa-1 d-flex align-center ga-2">
                        <v-avatar size="32" color="red-lighten-3">
                          <v-img v-if="!staff.photoError" :src="staff.photo_url" cover @error="staff.photoError = true" />
                          <v-icon v-else size="20" color="white">mdi-account</v-icon>
                        </v-avatar>
                        <div class="flex-grow-1 text-caption font-weight-medium">{{ staff.staff_name }}</div>
                        <v-btn icon size="x-small" variant="plain" color="error" @click.stop="removeToPool(staff, 'morningLeaveAnnual')">
                          <v-icon size="12">mdi-close</v-icon>
                        </v-btn>
                      </v-card-text>
                    </v-card>
                  </template>
                  <template #footer>
                    <div v-if="morningLeaveAnnual.length === 0" class="text-center text-caption text-medium-emphasis pa-4">
                      <v-icon size="28" color="red-lighten-3">mdi-drag</v-icon>
                      <div>Drag staff here for Annual Leave</div>
                    </div>
                  </template>
                </draggable>

                <draggable v-show="morningLeaveTab === 'Medical Leave'"
                           v-model="morningLeaveMedical"
                           :group="{ name: 'staff', pull: true, put: true }"
                           item-key="staff_id"
                           :animation="150"
                           ghost-class="drag-ghost"
                           class="leave-drop-zone"
                           @add="(e) => onAddToLeave(e, 1, 'MEDICAL LEAVE')">
                  <template #item="{ element: staff }">
                    <v-card class="staff-card-leave mb-1" elevation="1">
                      <v-card-text class="pa-1 d-flex align-center ga-2">
                        <v-avatar size="32" color="red-lighten-3">
                          <v-img v-if="!staff.photoError" :src="staff.photo_url" cover @error="staff.photoError = true" />
                          <v-icon v-else size="20" color="white">mdi-account</v-icon>
                        </v-avatar>
                        <div class="flex-grow-1 text-caption font-weight-medium">{{ staff.staff_name }}</div>
                        <v-btn icon size="x-small" variant="plain" color="error" @click.stop="removeToPool(staff, 'morningLeaveMedical')">
                          <v-icon size="12">mdi-close</v-icon>
                        </v-btn>
                      </v-card-text>
                    </v-card>
                  </template>
                  <template #footer>
                    <div v-if="morningLeaveMedical.length === 0" class="text-center text-caption text-medium-emphasis pa-4">
                      <v-icon size="28" color="red-lighten-3">mdi-drag</v-icon>
                      <div>Drag staff here for Medical Leave</div>
                    </div>
                  </template>
                </draggable>

                <draggable v-show="morningLeaveTab === 'Other Leave'"
                           v-model="morningLeaveOther"
                           :group="{ name: 'staff', pull: true, put: true }"
                           item-key="staff_id"
                           :animation="150"
                           ghost-class="drag-ghost"
                           class="leave-drop-zone"
                           @add="(e) => onAddToLeave(e, 1, 'OTHER LEAVE')">
                  <template #item="{ element: staff }">
                    <v-card class="staff-card-leave mb-1" elevation="1">
                      <v-card-text class="pa-1 d-flex align-center ga-2">
                        <v-avatar size="32" color="red-lighten-3">
                          <v-img v-if="!staff.photoError" :src="staff.photo_url" cover @error="staff.photoError = true" />
                          <v-icon v-else size="20" color="white">mdi-account</v-icon>
                        </v-avatar>
                        <div class="flex-grow-1 text-caption font-weight-medium">{{ staff.staff_name }}</div>
                        <v-btn icon size="x-small" variant="plain" color="error" @click.stop="removeToPool(staff, 'morningLeaveOther')">
                          <v-icon size="12">mdi-close</v-icon>
                        </v-btn>
                      </v-card-text>
                    </v-card>
                  </template>
                  <template #footer>
                    <div v-if="morningLeaveOther.length === 0" class="text-center text-caption text-medium-emphasis pa-4">
                      <v-icon size="28" color="red-lighten-3">mdi-drag</v-icon>
                      <div>Drag staff here for Other Leave</div>
                    </div>
                  </template>
                </draggable>

              </div>
            </v-card>
            <v-card elevation="2" style="flex: 1; display: flex; flex-direction: column; overflow: hidden;">
              <v-card-title class="pa-2 d-flex align-center ga-2 bg-indigo-lighten-5 flex-grow-0">
                <v-icon color="indigo-darken-2" size="18">mdi-weather-night</v-icon>
                <span class="text-subtitle-2">Night Leave / Absent</span>
                <v-chip size="x-small" color="indigo" variant="tonal">{{ totalNightLeaveCount }}</v-chip>
              </v-card-title>
              <v-divider />
              <v-tabs v-model="nightLeaveTab" density="compact" color="red" class="flex-grow-0">
                <v-tab value="Annual Leave" class="text-caption">
                  <v-icon start size="13">mdi-umbrella-beach</v-icon>Annual
                </v-tab>
                <v-tab value="Medical Leave" class="text-caption">
                  <v-icon start size="13">mdi-hospital-box</v-icon>Medical
                </v-tab>
                <v-tab value="Other Leave" class="text-caption">
                  <v-icon start size="13">mdi-dots-horizontal</v-icon>Others
                </v-tab>
              </v-tabs>
              <v-divider />
              <div class="flex-grow-1 pa-2" style="overflow-y: auto; min-height: 0;">

                <draggable v-show="nightLeaveTab === 'Annual Leave'"
                           v-model="nightLeaveAnnual"
                           :group="{ name: 'staff', pull: true, put: true }"
                           item-key="staff_id"
                           :animation="150"
                           ghost-class="drag-ghost"
                           class="leave-drop-zone"
                           @add="(e) => onAddToLeave(e, 2, 'ANNUAL LEAVE')">
                  <template #item="{ element: staff }">
                    <v-card class="staff-card-leave mb-1" elevation="1">
                      <v-card-text class="pa-1 d-flex align-center ga-2">
                        <v-avatar size="32" color="red-lighten-3">
                          <v-img v-if="!staff.photoError" :src="staff.photo_url" cover @error="staff.photoError = true" />
                          <v-icon v-else size="20" color="white">mdi-account</v-icon>
                        </v-avatar>
                        <div class="flex-grow-1 text-caption font-weight-medium">{{ staff.staff_name }}</div>
                        <v-btn icon size="x-small" variant="plain" color="error" @click.stop="removeToPool(staff, 'nightLeaveAnnual')">
                          <v-icon size="12">mdi-close</v-icon>
                        </v-btn>
                      </v-card-text>
                    </v-card>
                  </template>
                  <template #footer>
                    <div v-if="nightLeaveAnnual.length === 0" class="text-center text-caption text-medium-emphasis pa-4">
                      <v-icon size="28" color="red-lighten-3">mdi-drag</v-icon>
                      <div>Drag staff here for Annual Leave</div>
                    </div>
                  </template>
                </draggable>

                <draggable v-show="nightLeaveTab === 'Medical Leave'"
                           v-model="nightLeaveMedical"
                           :group="{ name: 'staff', pull: true, put: true }"
                           item-key="staff_id"
                           :animation="150"
                           ghost-class="drag-ghost"
                           class="leave-drop-zone"
                           @add="(e) => onAddToLeave(e, 2, 'MEDICAL LEAVE')">
                  <template #item="{ element: staff }">
                    <v-card class="staff-card-leave mb-1" elevation="1">
                      <v-card-text class="pa-1 d-flex align-center ga-2">
                        <v-avatar size="32" color="red-lighten-3">
                          <v-img v-if="!staff.photoError" :src="staff.photo_url" cover @error="staff.photoError = true" />
                          <v-icon v-else size="20" color="white">mdi-account</v-icon>
                        </v-avatar>
                        <div class="flex-grow-1 text-caption font-weight-medium">{{ staff.staff_name }}</div>
                        <v-btn icon size="x-small" variant="plain" color="error" @click.stop="removeToPool(staff, 'nightLeaveMedical')">
                          <v-icon size="12">mdi-close</v-icon>
                        </v-btn>
                      </v-card-text>
                    </v-card>
                  </template>
                  <template #footer>
                    <div v-if="nightLeaveMedical.length === 0" class="text-center text-caption text-medium-emphasis pa-4">
                      <v-icon size="28" color="red-lighten-3">mdi-drag</v-icon>
                      <div>Drag staff here for Medical Leave</div>
                    </div>
                  </template>
                </draggable>

                <draggable v-show="nightLeaveTab === 'Other Leave'"
                           v-model="nightLeaveOther"
                           :group="{ name: 'staff', pull: true, put: true }"
                           item-key="staff_id"
                           :animation="150"
                           ghost-class="drag-ghost"
                           class="leave-drop-zone"
                           @add="(e) => onAddToLeave(e, 2, 'OTHER LEAVE')">
                  <template #item="{ element: staff }">
                    <v-card class="staff-card-leave mb-1" elevation="1">
                      <v-card-text class="pa-1 d-flex align-center ga-2">
                        <v-avatar size="32" color="red-lighten-3">
                          <v-img v-if="!staff.photoError" :src="staff.photo_url" cover @error="staff.photoError = true" />
                          <v-icon v-else size="20" color="white">mdi-account</v-icon>
                        </v-avatar>
                        <div class="flex-grow-1 text-caption font-weight-medium">{{ staff.staff_name }}</div>
                        <v-btn icon size="x-small" variant="plain" color="error" @click.stop="removeToPool(staff, 'nightLeaveOther')">
                          <v-icon size="12">mdi-close</v-icon>
                        </v-btn>
                      </v-card-text>
                    </v-card>
                  </template>
                  <template #footer>
                    <div v-if="nightLeaveOther.length === 0" class="text-center text-caption text-medium-emphasis pa-4">
                      <v-icon size="28" color="red-lighten-3">mdi-drag</v-icon>
                      <div>Drag staff here for Other Leave</div>
                    </div>
                  </template>
                </draggable>

              </div>
            </v-card>
          </v-col>

          <!-- ── Night Leave ── -->
          <!--<v-col cols="4" style="height: 50%; display: flex; flex-direction: column;">
          </v-col>-->

        </v-row>
      </v-col>
    </v-row>

    <!-- ─── Add/Edit Staff Dialog ────────────────────────────────────────── -->
    <v-dialog v-model="staffDialog.show" max-width="420" persistent>
      <v-card>
        <v-card-title class="text-subtitle-1 pa-4 pb-2">
          {{ staffDialog.isEdit ? 'Edit Staff' : 'Add New Staff' }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row dense>
            <v-col cols="12" class="d-flex flex-column align-center mb-3">
              <v-avatar size="80" :color="getAvatarColor(staffDialog.form.staff_role)" class="mb-2" style="cursor:pointer;" @click="triggerPhotoUpload">
                <v-img v-if="staffDialog.photoPreview" :src="staffDialog.photoPreview" cover />
                <span v-else class="text-h6 font-weight-bold" style="color:white;">
                  {{ getInitials(staffDialog.form.staff_name) || '?' }}
                </span>
              </v-avatar>
              <v-btn size="x-small" variant="tonal" prepend-icon="mdi-camera" @click="triggerPhotoUpload">
                {{ staffDialog.isEdit ? 'Change Photo' : 'Upload Photo' }}
              </v-btn>
              <input ref="photoInputRef" type="file" accept="image/*" style="display:none" @change="onPhotoSelected" />
            </v-col>
            <v-col cols="12">
              <v-text-field v-model="staffDialog.form.staff_name" label="Staff Name" density="compact"
                            variant="outlined" hide-details class="mb-3" />
            </v-col>
            <v-col cols="12">
              <v-select v-model="staffDialog.form.staff_role" :items="staffRoles" label="Role"
                        density="compact" variant="outlined" hide-details class="mb-3" />
            </v-col>
            <v-col cols="12">
              <v-number-input :model-value="staffDialog.form.staff_id ?? undefined"
                              @update:model-value="staffDialog.form.staff_id = $event ?? null"
                              label="Staff ID"
                              density="compact"
                              variant="outlined"
                              hide-details
                              :min="1"
                              :disabled="staffDialog.isEdit"
                              controlVariant="hidden" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="closeStaffDialog">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="staffDialog.saving" @click="saveStaff">
            {{ staffDialog.isEdit ? 'Update' : 'Add Staff' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ─── Delete Confirm Dialog ────────────────────────────────────────── -->
    <v-dialog v-model="deleteDialog.show" max-width="360">
      <v-card>
        <v-card-title class="text-subtitle-1 pa-4">Confirm Delete</v-card-title>
        <v-card-text class="pa-4 pt-0">
          Remove <strong>{{ deleteDialog.staff?.staff_name }}</strong> from the staff list? This cannot be undone.
        </v-card-text>
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog.show = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="deleteDialog.deleting" @click="deleteStaff">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar.show" :color="snackbar.color" location="bottom right" timeout="3000">
      {{ snackbar.message }}
    </v-snackbar>
  </v-container>
</template>

<script setup>
  import { ref, computed, onMounted, watchEffect } from 'vue';
  import draggable from 'vuedraggable';
  import { STAFFROLES, STATUSOPTIONS, SHIFTOPTIONS, MACHINEOPTIONS, ROLE_COLORS, STAFF_COLORS } from '@/utils/constant.js';

  const loading = ref(false);
  const saving = ref(false);
  const searchQuery = ref('');
  const morningLeaveTab = ref('Annual Leave');
  const nightLeaveTab = ref('Annual Leave');
  const scheduleStart = ref(null);
  const scheduleEnd = ref(null);
  const machineOptions = MACHINEOPTIONS;
  const staffRoles = STAFFROLES;
  const photoInputRef = ref(null);

  const snackbar = ref({ show: false, message: '', color: 'success' });
  const staffDialog = ref({
    show: false, isEdit: false, saving: false,
    photoFile: null, photoPreview: null,
    form: { staff_name: '', staff_role: '', staff_id: '' }
  });
  const deleteDialog = ref({ show: false, staff: null, deleting: false });

  // ─── Lists ─────────────────────────────────────────────────────────────────────
  const poolList = ref([]);
  const morningList = ref([]);
  const nightList = ref([]);
  const morningLeaveAnnual = ref([]);
  const morningLeaveMedical = ref([]);
  const morningLeaveOther = ref([]);
  const nightLeaveAnnual = ref([]);
  const nightLeaveMedical = ref([]);
  const nightLeaveOther = ref([]);

  // Key → ref map used by removeToPool so we always mutate the correct reactive array
  const listRefMap = {
    morning: morningList,
    night: nightList,
    morningLeaveAnnual,
    morningLeaveMedical,
    morningLeaveOther,
    nightLeaveAnnual,
    nightLeaveMedical,
    nightLeaveOther,
  };

  // ─── Computed ──────────────────────────────────────────────────────────────────
  const formattedDate = computed(() => new Date().toLocaleDateString());
  const totalMorningLeaveCount = computed(
    () => morningLeaveAnnual.value.length + morningLeaveMedical.value.length + morningLeaveOther.value.length
  );
  const totalNightLeaveCount = computed(
    () => nightLeaveAnnual.value.length + nightLeaveMedical.value.length + nightLeaveOther.value.length
  );

  // ─── Lifecycle ─────────────────────────────────────────────────────────────────
  onMounted(async () => { await loadStaffData(); });

  watchEffect(() => {
    const allLists = [morningList, nightList, morningLeaveAnnual, morningLeaveMedical, morningLeaveOther, nightLeaveAnnual, nightLeaveMedical, nightLeaveOther];
    for (const list of allLists) {
      if (list.value.length > 0 && list.value[0].start_date) {
        scheduleStart.value = toLocalDateString(list.value[0].start_date);
        scheduleEnd.value = toLocalDateString(list.value[0].end_date);
        break;
      }
    }
  });

  // ─── Helpers ───────────────────────────────────────────────────────────────────
  function toLocalDateString(date) {
    if (!date) return null;
    const d = new Date(date);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  function getInitials(name) {
    if (!name) return '?';
    return name.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
  }

  function getAvatarColor(role) { return ROLE_COLORS[role] || 'blue-grey'; }

  function showSnackbar(message, color = 'success') {
    snackbar.value = { show: true, message, color };
  }

  function mapItem(item) {
    const machineNames = item.machine_name
      ? (Array.isArray(item.machine_name) ? item.machine_name : item.machine_name.split('/').map(x => x.trim()))
      : [];
    const idList = machineNames.map(name => {
      const found = machineOptions.find(opt => opt.text === name);
      return found ? found.value : null;
    }).filter(Boolean);
    return {
      ...item,
      machine_name: machineNames,
      id_machine: idList,
      photo_url: `/api/MachineLog/StaffPhoto/${item.staff_id}`,
      photoError: false
    };
  }

  // ─── Data Loading ──────────────────────────────────────────────────────────────
  async function loadStaffData() {
    loading.value = true;
    try {
      const response = await fetch('/api/MachineLog/StaffSchedule');
      const data = await response.json();

      poolList.value = [];
      morningList.value = [];
      nightList.value = [];
      morningLeaveAnnual.value = [];
      morningLeaveMedical.value = [];
      morningLeaveOther.value = [];
      nightLeaveAnnual.value = [];
      nightLeaveMedical.value = [];
      nightLeaveOther.value = [];

      if (data.length > 0) {
        scheduleStart.value = toLocalDateString(data[0].start_date);
        scheduleEnd.value = toLocalDateString(data[0].end_date);
      }

      data.forEach(item => {
        const mapped = mapItem(item);
        const status = (mapped.status ?? '').toUpperCase();
        const shift = mapped.work_shift;

        if (status === 'ANNUAL LEAVE') {
          (shift === 2 ? nightLeaveAnnual : morningLeaveAnnual).value.push(mapped);
        } else if (status === 'MEDICAL LEAVE') {
          (shift === 2 ? nightLeaveMedical : morningLeaveMedical).value.push(mapped);
        } else if (status === 'OTHER LEAVE') {
          (shift === 2 ? nightLeaveOther : morningLeaveOther).value.push(mapped);
        } else if (shift === 1) {
          morningList.value.push(mapped);
        } else if (shift === 2) {
          nightList.value.push(mapped);
        } else {
          poolList.value.push(mapped);
        }
      });
    } catch (err) {
      console.error('Error loading staff:', err);
      showSnackbar('Failed to load staff data', 'error');
    } finally {
      loading.value = false;
    }
  }

  // ─── Drag handlers ─────────────────────────────────────────────────────────────
  function onAddToShift(evt, shift) {
    const list = (shift === 1 ? morningList : nightList).value;
    const staff = list[evt.newIndex];
    if (staff) {
      staff.work_shift = shift;
      staff.status = 'ACTIVE';
    }
  }

  function onAddToLeave(evt, shift, statusLabel) {
    const key = `${shift === 1 ? 'morning' : 'night'}Leave${{ 'ANNUAL LEAVE': 'Annual', 'MEDICAL LEAVE': 'Medical', 'OTHER LEAVE': 'Other' }[statusLabel]}`;
    const list = listRefMap[key]?.value;
    if (!list) return;
    const staff = list[evt.newIndex];
    if (staff) {
      staff.work_shift = shift;
      staff.status = statusLabel;
      staff.id_machine = [];
      staff.machine_name = [];
    }
  }

  function onAddToPool(evt) {
    const staff = poolList.value[evt.newIndex];
    if (staff) {
      staff.work_shift = null;
      staff.status = 'INACTIVE';
      staff.id_machine = [];
      staff.machine_name = [];
    }
  }

  // ─── Remove to pool (fixes original bug by using listRefMap instead of raw array) ──
  function removeToPool(staff, listKey) {
    const sourceRef = listRefMap[listKey];
    if (!sourceRef) return;
    const idx = sourceRef.value.findIndex(s => s.staff_id === staff.staff_id);
    if (idx !== -1) {
      const [removed] = sourceRef.value.splice(idx, 1);
      removed.work_shift = null;
      removed.status = 'INACTIVE';
      removed.id_machine = [];
      removed.machine_name = [];
      poolList.value.push(removed);
    }
  }

  function updateMachineNames(staff) {
    const sorted = [...(staff.id_machine || [])].sort((a, b) => {
      const nameA = machineOptions.find(o => o.value === a)?.text ?? '';
      const nameB = machineOptions.find(o => o.value === b)?.text ?? '';
      return nameA.localeCompare(nameB);
    });
    staff.id_machine = sorted;
    staff.machine_name = sorted.map(id => machineOptions.find(o => o.value === id)?.text).filter(Boolean);
  }

  // ─── Save Schedule ─────────────────────────────────────────────────────────────
  async function saveChanges() {
    saving.value = true;
    try {
      const allStaff = [
        ...poolList.value.map(s => ({ ...s, work_shift: null, status: 'INACTIVE' })),
        ...morningList.value.map(s => ({ ...s, work_shift: 1, status: 'ACTIVE' })),
        ...nightList.value.map(s => ({ ...s, work_shift: 2, status: 'ACTIVE' })),
        ...morningLeaveAnnual.value.map(s => ({ ...s, work_shift: 1, status: 'ANNUAL LEAVE' })),
        ...morningLeaveMedical.value.map(s => ({ ...s, work_shift: 1, status: 'MEDICAL LEAVE' })),
        ...morningLeaveOther.value.map(s => ({ ...s, work_shift: 1, status: 'OTHER LEAVE' })),
        ...nightLeaveAnnual.value.map(s => ({ ...s, work_shift: 2, status: 'ANNUAL LEAVE' })),
        ...nightLeaveMedical.value.map(s => ({ ...s, work_shift: 2, status: 'MEDICAL LEAVE' })),
        ...nightLeaveOther.value.map(s => ({ ...s, work_shift: 2, status: 'OTHER LEAVE' })),
      ];

      const payload = allStaff.map(staff => {
        const sortedMachines = Array.isArray(staff.machine_name)
          ? [...staff.machine_name].sort()
          : (staff.machine_name ? staff.machine_name.split('/').map(m => m.trim()).sort() : []);
        const sortedIds = Array.isArray(staff.id_machine)
          ? [...staff.id_machine].sort((a, b) => {
            const nameA = machineOptions.find(o => o.value === a)?.text ?? '';
            const nameB = machineOptions.find(o => o.value === b)?.text ?? '';
            return nameA.localeCompare(nameB);
          })
          : [];
        return {
          ...staff,
          start_date: toLocalDateString(scheduleStart.value),
          end_date: toLocalDateString(scheduleEnd.value),
          machine_name: sortedMachines.length ? sortedMachines.join('/') : null,
          id_machine: sortedIds.length ? sortedIds.join('/') : null,
        };
      });

      const response = await fetch('/api/MachineLog/StaffSchedule', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      if (!response.ok) throw new Error('Failed to save');
      showSnackbar('Schedule saved successfully!', 'success');
    } catch {
      showSnackbar('Failed to save schedule', 'error');
    } finally {
      saving.value = false;
    }
  }

  // ─── Staff CRUD ────────────────────────────────────────────────────────────────
  function openAddDialog() {
    staffDialog.value = {
      show: true, isEdit: false, saving: false,
      photoFile: null, photoPreview: null,
      form: { staff_name: '', staff_role: staffRoles[0] || '', staff_id: null }
    };
  }

  function openEditDialog(staff) {
    staffDialog.value = {
      show: true, isEdit: true, saving: false,
      photoFile: null,
      photoPreview: staff.photoError ? null : staff.photo_url,
      form: { staff_name: staff.staff_name, staff_role: staff.staff_role, staff_id: staff.staff_id }
    };
  }

  function closeStaffDialog() { staffDialog.value.show = false; }
  function triggerPhotoUpload() { photoInputRef.value?.click(); }

  function onPhotoSelected(event) {
    const file = event.target.files[0];
    if (!file) return;
    staffDialog.value.photoFile = file;
    const reader = new FileReader();
    reader.onload = e => { staffDialog.value.photoPreview = e.target.result; };
    reader.readAsDataURL(file);
    event.target.value = '';
  }

  async function saveStaff() {
    staffDialog.value.saving = true;
    try {
      const form = staffDialog.value.form;
      const isEdit = staffDialog.value.isEdit;
      const staffPayload = { staff_name: form.staff_name, staff_role: form.staff_role, staff_id: form.staff_id };

      const response = await fetch('/api/MachineLog/Staff', {
        method: isEdit ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(staffPayload)
      });
      if (!response.ok) throw new Error('Failed to save staff');
      const result = await response.json();
      const savedId = isEdit ? form.staff_id : result.staff_id;

      if (staffDialog.value.photoFile && savedId) {
        const formData = new FormData();
        formData.append('file', staffDialog.value.photoFile);
        formData.append('staff_id', savedId);
        await fetch('/api/MachineLog/StaffPhoto', { method: 'POST', body: formData });
      }

      showSnackbar(isEdit ? 'Staff updated!' : 'Staff added!', 'success');
      closeStaffDialog();
      await loadStaffData();
    } catch {
      showSnackbar('Failed to save staff', 'error');
    } finally {
      staffDialog.value.saving = false;
    }
  }

  function confirmDelete(staff) { deleteDialog.value = { show: true, staff, deleting: false }; }

  async function deleteStaff() {
    deleteDialog.value.deleting = true;
    try {
      const response = await fetch('/api/MachineLog/Staff', {
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ staff_id: deleteDialog.value.staff.staff_id })
      });
      if (!response.ok) throw new Error('Failed to delete');
      showSnackbar('Staff deleted', 'success');
      deleteDialog.value.show = false;
      await loadStaffData();
    } catch {
      showSnackbar('Failed to delete staff', 'error');
    } finally {
      deleteDialog.value.deleting = false;
    }
  }
</script>

<style scoped>
  .staff-pool {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    padding: 2px;
    border-radius: 8px;
    min-height: 60px;
  }

  .staff-card {
    cursor: grab;
    transition: transform 0.15s, box-shadow 0.15s;
    border-radius: 8px !important;
    width: 78px;
    user-select: none;
  }

    .staff-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(0,0,0,0.15) !important;
    }

    .staff-card:active { cursor: grabbing; }

  .staff-card-leave {
    cursor: grab;
    border-radius: 8px !important;
    user-select: none;
  }

    .staff-card-leave:hover { background: rgba(0,0,0,0.02); }

  .leave-drop-zone {
    min-height: 80px;
    border-radius: 8px;
  }

  .shift-drop-empty {
    min-height: 80px;
    width: 100%;
    display: flex;
    flex-direction: column;
  }

  .drop-hint-overlay { height: 100%; }

  .shift-table { width: 100%; }

  .shift-row {
    cursor: grab;
    transition: background 0.1s;
    user-select: none;
  }

    .shift-row:hover { background: rgba(0,0,0,0.03); }
    .shift-row:active { cursor: grabbing; }

  .drag-ghost {
    opacity: 0.4;
    background: #c8ebfb;
    border-radius: 8px;
  }

  .drag-ghost-row {
    opacity: 0.4;
    background: #c8ebfb;
  }
</style>
