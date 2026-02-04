import { defineStore } from 'pinia'

export const pinia = defineStore('machine', {
  state: () => ({
    totalMachines: 0,
    runningMachines: 0,
    stopMachines: 0,
    activeStaff: 0,
    machineData: [],
    SAPData: [],
    ProdData: []
  }),

  getters: {
    getTotalMachines: (state) => state.totalMachines,
    getRunningMachines: (state) => state.runningMachines,
    getStopMachines: (state) => state.stopMachines,
    getActiveStaff: (state) => state.activeStaff,
    getMachineData: (state) => state.machineData,
    getSAPData: (state) => state.SAPData,
    getProdData: (state) => state.ProdData,
    getMachinesByStatus: (state) => (isRunning) => {
      return state.machineData.filter(m =>
        isRunning ? (m.status_start && !m.status_off) : !(m.status_start && !m.status_off)
      )
    }
  },

  actions: {
    async loadAttendance() {
      try {
        const staffRes = await fetch('/api/MachineLog/Attendance')
        const staffData = await staffRes.json()
        this.activeStaff = staffData.filter(s => s.machine_name && s.status === 'ACTIVE').length
        return { activeStaff: this.activeStaff, staffData }
      } catch (err) {
        console.error('Error loading attendance:', err.message)
        return []
      }
    },

    async loadMachineMaster() {
      try {
        const machineRes = await fetch('/api/MachineLog/MachineMaster')
        const machines = await machineRes.json()
        this.machineData = machines

        const validMachines = machines.filter(m => m.machine_name !== 'TEST')
        this.totalMachines = validMachines.length
        this.runningMachines = validMachines.filter(m => m.status_start).length
        this.stopMachines = validMachines.filter(m => !m.status_start).length

        return {
          machineData: this.machineData,
          totalMachines: this.totalMachines,
          runningMachines: this.runningMachines,
          stopMachines: this.stopMachines
        }
      } catch (err) {
        console.error('Error loading machine master:', err.message)
        return []
      }
    },

    async loadSAP() {
      try {
        const SAPRes = await fetch('/api/MachineLog/SAP')
        this.SAPData = await SAPRes.json()
        return this.SAPData
      } catch (err) {
        console.error('Error loading SAP:', err.message)
        return []
      }
    },

    async loadDailyReport(production_date, shift) {
      try {
        const ProdRes = await fetch(`/api/MachineLog/DailyReport?production_date=${production_date}&shift=${shift}`)
        this.ProdData = await ProdRes.json()
        return this.ProdData
      } catch (err) {
        console.error('Error loading Daily Report:', err.message)
        return []
      }
    },

    async loadPrevReport(production_date, shift) {
      try {
        const ProdRes = await fetch(`/api/MachineLog/PrevReport?production_date=${production_date}&shift=${shift}`)
        this.ProdData = await ProdRes.json()
        return this.ProdData
      } catch (err) {
        console.error('Error loading Previous Report:', err.message)
        return []
      }
    }
  }
})
