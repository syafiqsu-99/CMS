// CMS.Client/src/store/machineStore.js
import { defineStore } from 'pinia';
import { formatDate, getTodayString, getCurrentShift } from '@/utils/constant.js';

export const useMachineStore = defineStore('machine', {
  state: () => ({
    machineData: [],
    SAPData: [],
    ProdData: [],
    activeStaff: 0,
    totalMachines: 0,
    runningMachines: 0,
    stopMachines: 0,
    _fetchingMaster: false,
    _fetchingSAP: false,
  }),

  getters: {
    machineById: (state) => (id) => state.machineData.find(m => m.id_machine === id),
  },

  actions: {
    async loadMachineMaster() {
      if (this._fetchingMaster) return this.machineData;
      this._fetchingMaster = true;
      try {
        const res = await fetch('/api/machines');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const machines = await res.json();
        this.machineData = machines;
        const valid = machines.filter(m => m.machine_name !== 'TEST');
        this.totalMachines = valid.length;
        this.runningMachines = valid.filter(m => m.status_start).length;
        this.stopMachines = valid.filter(m => !m.status_start).length;
        return machines;
      } catch (err) {
        console.error('[store] loadMachineMaster:', err.message);
        return this.machineData;
      } finally {
        this._fetchingMaster = false;
      }
    },

    async loadSAP() {
      try {
        const res = await fetch('/api/setting/sap');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        this.SAPData = await res.json();
        return this.SAPData;
      } catch (err) {
        console.error('[store] loadSAP:', err.message);
        return this.SAPData;
      }
    },

    async loadAttendance() {
      try {
        const res = await fetch('/api/supervisor/attendance');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = await res.json();
        this.activeStaff = data.filter(s => s.machine_name && s.status === 'ACTIVE').length;
        return data;
      } catch (err) {
        console.error('[store] loadAttendance:', err.message);
        return [];
      }
    },

    async loadDailyReport(date, shift) {
      try {
        const d = formatDate(date);
        const res = await fetch(`/api/supervisor/daily-report?production_date=${d}&shift=${shift}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        this.ProdData = await res.json();
        return this.ProdData;
      } catch (err) {
        console.error('[store] loadDailyReport:', err.message);
        return [];
      }
    },

    async loadPrevReport(date, shift) {
      try {
        const d = formatDate(date);
        const res = await fetch(`/api/supervisor/prev-report?production_date=${d}&shift=${shift}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        this.ProdData = await res.json();
        return this.ProdData;
      } catch (err) {
        console.error('[store] loadPrevReport:', err.message);
        return [];
      }
    },

    async loadInitialData() {
      await Promise.all([
        this.loadMachineMaster(),
      ]);
    },
  },
});
