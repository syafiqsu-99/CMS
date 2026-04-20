<template>
  <v-container fluid class="pa-0" style="height: 81vh; overflow: hidden; display: flex; flex-direction: column;">

    <!-- Header row -->
    <v-row dense align="center" class="mb-2 flex-grow-0 px-1 pt-1">
      <v-col cols="auto">
        <div class="d-flex align-center ga-2">
          <v-icon color="primary">mdi-calendar-clock</v-icon>
          <span class="text-subtitle-1 font-weight-bold">Shift Calendar</span>
          <v-chip size="small" variant="tonal" color="primary">{{ calendarTitle }}</v-chip>
        </div>
      </v-col>
      <v-spacer />
      <v-col cols="auto" class="d-flex align-center ga-2">
        <v-btn icon="mdi-chevron-left" size="small" variant="tonal" @click="prevMonth" />
        <v-btn size="small" variant="outlined" @click="goToToday">Today</v-btn>
        <v-btn icon="mdi-chevron-right" size="small" variant="tonal" @click="nextMonth" />
        <v-btn color="primary" prepend-icon="mdi-auto-fix" size="small" :loading="filling" @click="autoFillMonth">
          Auto-fill Month
        </v-btn>
        <v-btn variant="tonal" prepend-icon="mdi-refresh" size="small" :loading="loading" @click="loadCalendar">
          Refresh
        </v-btn>
      </v-col>
    </v-row>

    <!-- Legend -->
    <v-row dense class="flex-grow-0 px-1 mb-1">
      <v-col cols="auto" v-for="dt in dayTypes" :key="dt.value">
        <div class="d-flex align-center ga-1">
          <div :style="{ width: '12px', height: '12px', borderRadius: '3px', backgroundColor: dt.color, border: '1px solid ' + dt.border }" />
          <span class="text-caption text-medium-emphasis">{{ dt.label }}</span>
        </div>
      </v-col>
      <v-spacer />
      <v-col cols="auto" class="d-flex align-center ga-1">
        <v-icon size="14" color="blue-darken-2">mdi-circle</v-icon>
        <span class="text-caption text-medium-emphasis">Today</span>
        <v-icon size="14" color="grey" class="ml-2">mdi-clock-outline</v-icon>
        <span class="text-caption text-medium-emphasis">Custom hours set</span>
      </v-col>
    </v-row>

    <!-- Calendar grid -->
    <v-row dense class="flex-grow-1 px-1 pb-1" style="min-height: 0; overflow: hidden;">
      <v-col cols="12" style="height: 100%; display: flex; flex-direction: column; overflow: hidden;">
        <v-card elevation="2" style="flex: 1; display: flex; flex-direction: column; overflow: hidden;">

          <!-- Day headers -->
          <div class="d-flex px-2 pt-2 pb-1 flex-grow-0">
            <div v-for="d in ['Mon','Tue','Wed','Thu','Fri','Sat','Sun']" :key="d"
                 class="text-center text-caption font-weight-bold text-medium-emphasis"
                 style="flex: 1;">
              {{ d }}
            </div>
          </div>
          <v-divider />

          <!-- Weeks -->
          <div class="pa-2 flex-grow-1" style="overflow-y: auto; min-height: 0;">
            <div v-for="(week, wi) in calendarWeeks" :key="wi" class="d-flex ga-1 mb-1">
              <div v-for="(day, di) in week" :key="di" style="flex: 1; min-width: 0;">

                <!-- Empty spacer cell -->
                <div v-if="!day" style="min-height: 90px;" />

                <!-- Real day cell -->
                <div v-else
                     class="calendar-cell pa-1"
                     :class="{ 'today-cell': isToday(day.date) }"
                     :style="getCellBg(day)"
                     @click="openDialog(day)">

                  <!-- Date number -->
                  <div class="d-flex align-center justify-space-between mb-1">
                    <span class="text-caption font-weight-bold"
                          :style="{ color: isToday(day.date) ? '#1565C0' : '#333' }">
                      {{ day.date.getDate() }}
                    </span>
                    <v-icon v-if="isToday(day.date)" size="8" color="primary">mdi-circle</v-icon>
                  </div>

                  <!-- Shift badges -->
                  <div class="d-flex flex-column ga-1">
                    <div v-for="shift in [1, 2]" :key="shift"
                         class="shift-badge"
                         :style="getShiftBadgeStyle(day, shift)"
                         :title="getShiftTooltip(day, shift)">
                      <div class="d-flex align-center justify-space-between px-1">
                        <span style="font-size: 9px; line-height: 1.3;">
                          <strong>S{{ shift }}:</strong> {{ getShiftLabel(day, shift) }}
                        </span>
                        <v-icon v-if="hasCustomHours(day, shift)" size="9" color="grey-darken-1">mdi-clock-outline</v-icon>
                      </div>
                      <div v-if="getShiftHours(day, shift)" class="px-1" style="font-size: 8px; opacity: 0.75; line-height: 1.2;">
                        {{ getShiftHours(day, shift) }}
                      </div>
                    </div>
                  </div>
                </div>

              </div>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Edit Dialog -->
    <v-dialog v-model="dialog" max-width="460" persistent>
      <v-card>
        <v-card-title class="d-flex align-center ga-2 py-3 px-4">
          <v-icon color="primary">mdi-calendar-edit</v-icon>
          <span class="text-subtitle-1 font-weight-bold">{{ formatDateDisplay(editingDay?.date) }}</span>
        </v-card-title>
        <v-divider />
        <v-card-text class="pt-4 pb-2">
          <div v-for="shift in [1, 2]" :key="shift" class="mb-4">

            <!-- Shift header -->
            <div class="d-flex align-center ga-2 mb-2">
              <v-icon size="16" :color="shift === 1 ? 'orange-darken-2' : 'indigo-darken-2'">
                {{ shift === 1 ? 'mdi-weather-sunny' : 'mdi-weather-night' }}
              </v-icon>
              <span class="text-body-2 font-weight-bold">
                Shift {{ shift }} — {{ shift === 1 ? 'Morning (06:00–18:00)' : 'Night (18:00–06:00)' }}
              </span>
            </div>

            <v-row dense>
              <v-col cols="6">
                <v-select v-model="editingShifts[shift].day_type"
                          :items="dayTypes"
                          item-title="label"
                          item-value="value"
                          label="Day Type"
                          variant="outlined"
                          density="compact"
                          hide-details
                          @update:model-value="onDayTypeChange(shift)" />
              </v-col>
              <v-col cols="6">
                <v-text-field v-model.number="editingShifts[shift].planned_hours"
                              label="Planned Hours"
                              type="number"
                              min="0.5"
                              max="12"
                              step="0.5"
                              variant="outlined"
                              density="compact"
                              suffix="hrs"
                              :disabled="editingShifts[shift].day_type !== 'OVERTIME'"
                              :hint="editingShifts[shift].day_type === 'NORMAL'
                           ? 'Fixed 12h for working day'
                           : editingShifts[shift].day_type === 'OFFDAY'
                             ? 'No planned hours on off days'
                             : 'Enter OT hours (0.5 – 12)'"
                              persistent-hint />
              </v-col>

              <!-- Overtime window: auto-derived, read-only display -->
              <v-col v-if="editingShifts[shift].day_type === 'OVERTIME'" cols="12" class="mt-2">
                <div class="d-flex align-center ga-3 pa-2 rounded"
                     style="background: #FFF8E1; border: 1px solid #FFD54F;">
                  <div class="d-flex flex-column align-center">
                    <span class="text-caption text-medium-emphasis">OT Start</span>
                    <span class="text-body-2 font-weight-bold">{{ SHIFT_START[shift] }}</span>
                  </div>
                  <v-icon color="orange-darken-2">mdi-arrow-right</v-icon>
                  <div class="d-flex flex-column align-center">
                    <span class="text-caption text-medium-emphasis">OT End</span>
                    <span class="text-body-2 font-weight-bold">
                      {{ calcOtFinish(shift, editingShifts[shift].planned_hours) }}
                    </span>
                  </div>
                  <v-spacer />
                  <div v-if="editingShifts[shift].planned_hours < 12" class="d-flex flex-column align-center">
                    <span class="text-caption text-medium-emphasis">Off-day gap</span>
                    <span class="text-body-2 font-weight-bold text-grey">
                      {{ calcOtFinish(shift, editingShifts[shift].planned_hours) }}
                      → {{ SHIFT_END[shift] }}
                    </span>
                  </div>
                </div>
                <div class="text-caption text-medium-emphasis mt-1 px-1">
                  <v-icon size="12" color="grey">mdi-information-outline</v-icon>
                  <span v-if="editingShifts[shift].planned_hours < 12">
                    The remaining {{ 12 - editingShifts[shift].planned_hours }}h
                    ({{ calcOtFinish(shift, editingShifts[shift].planned_hours) }}–{{ SHIFT_END[shift] }})
                    will be recorded as Off Day to complete the full shift.
                  </span>
                  <span v-else>Full 12h overtime — no off-day gap.</span>
                </div>
              </v-col>

            </v-row>
            <v-divider v-if="shift === 1" class="mt-3" />
          </div>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <v-btn variant="text" color="error" size="small" @click="resetToDefault">
            <v-icon start size="14">mdi-restore</v-icon>Reset to Default
          </v-btn>
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancel</v-btn>
          <v-btn color="primary" variant="flat" :loading="saving" @click="saveDay">Save</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar.show" :color="snackbar.color" location="bottom right" timeout="3000">
      {{ snackbar.message }}
    </v-snackbar>

  </v-container>
</template>

<script setup>
  import { ref, computed, watch, onMounted } from 'vue';

  // ── Day type config ────────────────────────────────────────────────────────────
  const dayTypes = [
    { value: 'NORMAL', label: 'Working Day', color: '#E3F2FD', border: '#90CAF9', badgeColor: '#1976D2' },
    { value: 'OVERTIME', label: 'Overtime', color: '#FFF8E1', border: '#FFD54F', badgeColor: '#F57F17' },
    { value: 'OFFDAY', label: 'Off Day', color: '#F5F5F5', border: '#E0E0E0', badgeColor: '#757575' },
  ];
  const dayTypeMap = Object.fromEntries(dayTypes.map(d => [d.value, d]));

  // Shift default start and end times
  const SHIFT_START = { 1: '06:00', 2: '18:00' };
  const SHIFT_END = { 1: '18:00', 2: '06:00' }; // nominal end of each shift
  const SHIFT_DEFAULT_HOURS = 12;

  // ── State ──────────────────────────────────────────────────────────────────────
  const today = new Date();
  const calendarYear = ref(today.getFullYear());
  const calendarMonth = ref(today.getMonth()); // 0-indexed
  const calendarData = ref([]);               // [{ production_date, shift, day_type, planned_hours, start, finish }]
  const loading = ref(false);
  const saving = ref(false);
  const filling = ref(false);
  const dialog = ref(false);
  const editingDay = ref(null);
  const editingShifts = ref({ 1: defaultShift(), 2: defaultShift() });
  const snackbar = ref({ show: false, message: '', color: 'success' });

  function defaultShift() {
    return { day_type: 'NORMAL', planned_hours: SHIFT_DEFAULT_HOURS };
  }

  // ── Computed ───────────────────────────────────────────────────────────────────
  const calendarTitle = computed(() => {
    const d = new Date(calendarYear.value, calendarMonth.value, 1);
    return d.toLocaleDateString('en-MY', { month: 'long', year: 'numeric' });
  });

  const calendarWeeks = computed(() => {
    const year = calendarYear.value;
    const month = calendarMonth.value;
    const first = new Date(year, month, 1);
    const last = new Date(year, month + 1, 0);
    const startDow = (first.getDay() + 6) % 7; // Mon=0

    const weeks = [];
    let week = Array(startDow).fill(null);

    for (let d = 1; d <= last.getDate(); d++) {
      const date = new Date(year, month, d);
      week.push({ date, entries: getEntriesForDate(date) });
      if (week.length === 7) { weeks.push(week); week = []; }
    }
    if (week.length) {
      while (week.length < 7) week.push(null);
      weeks.push(week);
    }
    return weeks;
  });

  // ── Helpers ────────────────────────────────────────────────────────────────────
  function formatDateKey(date) {
    return date.toLocaleDateString('en-CA'); // yyyy-MM-dd
  }

  function formatDateDisplay(date) {
    if (!date) return '';
    return date.toLocaleDateString('en-MY', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
  }

  function isToday(date) {
    return date.toDateString() === today.toDateString();
  }

  function getEntriesForDate(date) {
    const key = formatDateKey(date);
    return calendarData.value.filter(e => e.production_date === key);
  }

  function getEntry(day, shift) {
    // When a shift has both an OVERTIME row and an OFFDAY gap row,
    // return the OVERTIME row for display — the gap is implicit.
    const rows = day.entries.filter(e => e.shift === shift);
    return rows.find(e => e.day_type === 'OVERTIME')
      ?? rows.find(e => e.day_type === 'NORMAL')
      ?? rows[0]
      ?? null;
  }

  function getShiftLabel(day, shift) {
    const e = getEntry(day, shift);
    if (!e) return 'Working';  // default for all days including weekends
    return dayTypeMap[e.day_type]?.label.split(' ')[0] ?? e.day_type;
  }

  function getShiftHours(day, shift) {
    const e = getEntry(day, shift);
    if (!e || e.day_type === 'OFFDAY') return null;
    if (e.day_type === 'OVERTIME') {
      return `${SHIFT_START[shift]} → ${calcOtFinish(shift, e.planned_hours)}`;
    }
    if (e.planned_hours && e.planned_hours !== SHIFT_DEFAULT_HOURS) {
      return `${e.planned_hours}h`;
    }
    return null;
  }

  function hasCustomHours(day, shift) {
    const e = getEntry(day, shift);
    if (!e) return false;
    return e.day_type === 'OVERTIME' || (e.planned_hours && e.planned_hours !== SHIFT_DEFAULT_HOURS);
  }

  function getShiftTooltip(day, shift) {
    const e = getEntry(day, shift);
    if (!e) return `Shift ${shift}: Working Day`;
    const base = `Shift ${shift}: ${dayTypeMap[e.day_type]?.label}`;
    if (e.day_type === 'OVERTIME')
      return `${base} | ${SHIFT_START[shift]} → ${calcOtFinish(shift, e.planned_hours)}`;
    return `${base} | ${e.planned_hours}h`;
  }

  function getShiftBadgeStyle(day, shift) {
    const e = getEntry(day, shift);
    const dt = e ? dayTypeMap[e.day_type] : dayTypeMap['NORMAL']; // default NORMAL for all days
    return {
      backgroundColor: dt?.color ?? '#E3F2FD',
      border: `1px solid ${dt?.border ?? '#90CAF9'}`,
      borderRadius: '4px',
      padding: '2px 0',
      cursor: 'pointer',
    };
  }

  function getCellBg(day) {
    return {
      border: isToday(day.date) ? '2px solid #1565C0' : '1px solid #e0e0e0',
      borderRadius: '6px',
      backgroundColor: '#FFFFFF',
      cursor: 'pointer',
      minHeight: '90px',
    };
  }

  function getShiftStart(shift) {
    return SHIFT_START[shift];
  }

  // Derive OT end time from shift start + planned hours
  // Returns "HH:mm" string. Night shift can roll past midnight.
  function calcOtFinish(shift, hours) {
    const [startH, startM] = SHIFT_START[shift].split(':').map(Number);
    const totalMinutes = startH * 60 + startM + Math.round((hours ?? 0) * 60);
    const hh = Math.floor(totalMinutes / 60) % 24;
    const mm = totalMinutes % 60;
    return `${String(hh).padStart(2, '0')}:${String(mm).padStart(2, '0')}`;
  }

  function onDayTypeChange(shift) {
    const s = editingShifts.value[shift];
    if (s.day_type === 'NORMAL') {
      s.planned_hours = SHIFT_DEFAULT_HOURS; // always 12, not editable
    } else if (s.day_type === 'OVERTIME') {
      s.planned_hours = 10; // reasonable default OT — supervisor adjusts up to 12
    } else { // OFFDAY
      s.planned_hours = 0; // always 0, not editable
    }
  }

  function showSnackbar(message, color = 'success') {
    snackbar.value = { show: true, message, color };
  }

  // ── Navigation ─────────────────────────────────────────────────────────────────
  function prevMonth() {
    if (calendarMonth.value === 0) { calendarMonth.value = 11; calendarYear.value--; }
    else calendarMonth.value--;
  }
  function nextMonth() {
    if (calendarMonth.value === 11) { calendarMonth.value = 0; calendarYear.value++; }
    else calendarMonth.value++;
  }
  function goToToday() {
    calendarYear.value = today.getFullYear();
    calendarMonth.value = today.getMonth();
  }

  // ── API ────────────────────────────────────────────────────────────────────────
  async function loadCalendar() {
    loading.value = true;
    try {
      const y = calendarYear.value;
      const m = calendarMonth.value + 1;
      const res = await fetch(`/api/supervisor/shift-calendar?year=${y}&month=${m}`);
      const text = await res.text();
      calendarData.value = text ? JSON.parse(text) : [];
    } catch (err) {
      console.error('Failed to load calendar:', err);
      calendarData.value = [];
      showSnackbar('Failed to load calendar', 'error');
    } finally {
      loading.value = false;
    }
  }

  async function autoFillMonth() {
    filling.value = true;
    try {
      const y = calendarYear.value;
      const m = calendarMonth.value;
      const last = new Date(y, m + 1, 0).getDate();
      const payload = [];

      for (let d = 1; d <= last; d++) {
        const date = new Date(y, m, d);
        const key = formatDateKey(date);
        const existing1 = calendarData.value.find(e => e.production_date === key && e.shift === 1);
        const existing2 = calendarData.value.find(e => e.production_date === key && e.shift === 2);

        // All days default to NORMAL working day — supervisor sets offday/overtime manually
        if (!existing1) payload.push({
          production_date: key,          // string "yyyy-MM-dd" → backend parses to DateOnly
          shift: 1,                      // int
          day_type: 'NORMAL',            // string
          planned_hours: 12.0,           // float/single
          start_time: `${key} 06:00`,   // string datetime
          finish_time: null,             // string or null
        });
        if (!existing2) payload.push({
          production_date: key,
          shift: 2,
          day_type: 'NORMAL',
          planned_hours: 12.0,
          start_time: `${key} 18:00`,
          finish_time: null,
        });
      }

      if (payload.length === 0) {
        showSnackbar('All days already configured', 'info');
        return;
      }

      await fetch('/api/supervisor/shift-calendar', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });

      await loadCalendar();
      showSnackbar(`Auto-filled ${payload.length / 2} days`, 'success');
    } catch (err) {
      console.error('Auto-fill failed:', err);
      showSnackbar('Auto-fill failed', 'error');
    } finally {
      filling.value = false;
    }
  }

  // ── Dialog ─────────────────────────────────────────────────────────────────────
  function openDialog(day) {
    editingDay.value = day;
    [1, 2].forEach(shift => {
      const e = getEntry(day, shift);
      editingShifts.value[shift] = e
        ? { day_type: e.day_type, planned_hours: e.planned_hours ?? SHIFT_DEFAULT_HOURS }
        : { day_type: 'NORMAL', planned_hours: SHIFT_DEFAULT_HOURS };
    });
    dialog.value = true;
  }

  function resetToDefault() {
    [1, 2].forEach(shift => {
      editingShifts.value[shift] = { day_type: 'NORMAL', planned_hours: SHIFT_DEFAULT_HOURS };
    });
  }

  async function saveDay() {
    saving.value = true;
    try {
      const production_date = formatDateKey(editingDay.value.date);
      const nextDay = formatDateKey(new Date(new Date(production_date).getTime() + 86400000));
      const payload = [];

      for (const shift of [1, 2]) {
        const s = editingShifts.value[shift];

        // Enforce fixed hours per type; clamp OT to valid range 0.5–12
        const plannedHours = s.day_type === 'NORMAL' ? 12.0
          : s.day_type === 'OFFDAY' ? 0.0
            : Math.min(12.0, Math.max(0.5, parseFloat(s.planned_hours) || 10.0));

        const shiftStartStr = shift === 1 ? `${production_date} 06:00` : `${production_date} 18:00`;
        const shiftEndStr = shift === 1 ? `${production_date} 18:00` : `${nextDay} 06:00`;

        if (s.day_type === 'OVERTIME') {
          // Derive OT finish from shift start + planned hours
          const otFinishTime = calcOtFinish(shift, plannedHours);     // "HH:mm"
          const [hh] = otFinishTime.split(':').map(Number);
          const otFinishDate = (shift === 2 && hh < 12) ? nextDay : production_date;
          const otFinishStr = `${otFinishDate} ${otFinishTime}`;

          // Row 1: OVERTIME — planned_hours = 0 so it doesn't inflate the denominator.
          // The machine's run_time will still accumulate, allowing availability > 100%.
          payload.push({
            production_date,
            shift: parseInt(shift),
            day_type: 'OVERTIME',
            planned_hours: 0.0,
            start_time: shiftStartStr,
            finish_time: otFinishStr,
          });

          // Row 2: OFFDAY gap covering OT finish → shift end (only when OT < 12h)
          if (plannedHours < 12) {
            payload.push({
              production_date,
              shift: parseInt(shift),
              day_type: 'OFFDAY',
              planned_hours: 0.0,
              start_time: otFinishStr,
              finish_time: shiftEndStr,
            });
          }

        } else {
          // NORMAL or full OFFDAY — single row covering the full shift window
          payload.push({
            production_date,
            shift: parseInt(shift),
            day_type: s.day_type,
            planned_hours: plannedHours,
            start_time: shiftStartStr,
            finish_time: shiftEndStr,
          });
        }
      }

      await fetch('/api/supervisor/shift-calendar', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });

      dialog.value = false;
      await loadCalendar();
      showSnackbar('Saved successfully', 'success');
    } catch (err) {
      console.error('Save failed:', err);
      showSnackbar('Save failed', 'error');
    } finally {
      saving.value = false;
    }
  }

  // ── Watchers ───────────────────────────────────────────────────────────────────
  watch([calendarYear, calendarMonth], loadCalendar);
  onMounted(loadCalendar);
</script>

<style scoped>
  .calendar-cell {
    transition: box-shadow 0.15s, transform 0.1s;
    user-select: none;
  }

    .calendar-cell:hover {
      box-shadow: 0 3px 10px rgba(0, 0, 0, 0.12);
      transform: translateY(-1px);
    }

  .today-cell {
    box-shadow: 0 0 0 2px #1565C0 inset;
  }

  .shift-badge {
    transition: filter 0.1s;
  }

    .shift-badge:hover {
      filter: brightness(0.96);
    }
</style>
