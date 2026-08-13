import { defineStore } from 'pinia';
import { formatDate } from '@/utils/constant.js';

export const useMachineStore = defineStore('machine', {
  // #region State
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
  // #endregion

  // #region Getters
  getters: {
    machineById: (state) => (id) =>
      state.machineData.find(m => m.id_machine === id),
  },
  // #endregion

  actions: {
    // #region MachineMaster
    async loadMachineMaster() {
      if (this._fetchingMaster) return this.machineData;
      this._fetchingMaster = true;
      try {
        const res = await fetch('/api/machines');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const machines = await res.json();

        this.machineData = machines;
        this.totalMachines = machines.length;
        this.runningMachines = machines.filter(m => m.status_start).length;
        this.stopMachines = machines.filter(m => !m.status_start).length;
        return machines;
      } catch (err) {
        console.error('[store] loadMachineMaster:', err.message);
        return this.machineData;
      } finally {
        this._fetchingMaster = false;
      }
    },
    // #endregion

    // #region SAP
    async loadSAP() {
      if (this._fetchingSAP) return this.SAPData;
      this._fetchingSAP = true;
      try {
        const res = await fetch('/api/supervisor/sap');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        this.SAPData = await res.json();
        return this.SAPData;
      } catch (err) {
        console.error('[store] loadSAP:', err.message);
        return this.SAPData;
      } finally {
        this._fetchingSAP = false;
      }
    },
    // #endregion

    // #region Attendance
    async loadAttendance() {
      try {
        const res = await fetch('/api/dashboard/attendance');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = await res.json();
        this.activeStaff = data.filter(s => s.machine_name && s.status === 'ACTIVE').length;
        return data;
      } catch (err) {
        console.error('[store] loadAttendance:', err.message);
        return [];
      }
    },
    // #endregion

    // #region Reports
    async loadDailyReport(date, shift) {
      try {
        const production_date = formatDate(date);
        const res = await fetch(`/api/supervisor/daily-report?production_date=${production_date}&shift=${shift}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        this.ProdData = await res.json();
        return this.ProdData;
      } catch (err) {
        console.error('[store] loadDailyReport:', err.message);
        return [];
      }
    },
    // #endregion

    // #region Maintenance
    async checkMaintenance() {
      try {
        const res = await fetch('/api/base/Maintenance', { cache: 'no-store' });
        if (!res.ok) return { active: false, shutdownAt: null };
        const ct = res.headers.get('content-type') || '';
        if (!ct.includes('application/json')) return { active: false, shutdownAt: null };
        return await res.json();
      } catch {
        return { active: false, shutdownAt: null };
      }
    },
    // #endregion

    // #region Bootstrap
    async loadInitialData() {
      await this.loadMachineMaster();
    },
    // #endregion
  },
});