export const STAFFROLES = [
  'SUPERVISOR',
  'LINE LEADER',
  'PACKER',
  'MATERIAL HANDLER',
  'PRODUCTION COORDINATOR'
];

export const STATUSOPTIONS = [
  'ACTIVE',
  'INACTIVE',
  'ANNUAL LEAVE',
  'MEDICAL LEAVE',
  'OTHERS'
];

export const UTILITIES = [
  'BARREL',
  'HYDRAULIC MOTOR',
  'DEHUMIDIFIER',
  'CHILLER',
  'MATERIAL',
  'DRY CYCLE'
];

export const SHIFTOPTIONS = [
  { text: 'Morning', value: 1 },
  { text: 'Night', value: 2 }
];

export const MACHINEOPTIONS = [
  { text: 'A5', value: 1 },
  { text: 'A6', value: 2 },
  { text: 'A7', value: 3 },
  { text: 'A8', value: 4 },
  { text: 'A9', value: 5 },
  { text: 'A10', value: 6 },
  { text: 'A12', value: 7 },
  { text: 'A13', value: 8 },
  { text: 'A14', value: 9 },
  { text: 'A15', value: 10 },
  { text: 'A16', value: 11 },
  { text: 'A17', value: 12 },
  { text: 'A18', value: 13 },
  { text: 'A19', value: 14 },
  { text: 'A21', value: 15 },
  { text: 'A23', value: 16 },
  { text: 'B3', value: 17 },
  { text: 'BRAVAN1', value: 18 },
  { text: 'BRAVAN2', value: 19 },
  { text: 'H1', value: 20 },
  { text: 'H2', value: 21 },
  { text: 'JWELL', value: 22 },
  { text: 'KM16', value: 23 },
  { text: 'KM17', value: 24 },
  { text: 'KM25', value: 25 },
  { text: 'KM26', value: 26 },
  { text: 'LABELLING', value: 27 },
  { text: 'SM5', value: 28 },
  { text: 'SU1', value: 29 },
  { text: 'Y1', value: 30 },
  { text: 'TEST', value: 0 },
];

export const MACHINEPOSITIONS = {
  // ── Ground Floor (floor: 0) ───────────────────────────────────────────
  KM25: { x: 200, y: 50, width: 50, height: 50, floor: 0 },
  KM16: { x: 300, y: 50, width: 50, height: 50, floor: 0 },
  KM17: { x: 400, y: 50, width: 50, height: 50, floor: 0 },
  JWELL: { x: 500, y: 50, width: 300, height: 50, floor: 0 },

  B3: { x: 50, y: 200, width: 50, height: 50, floor: 0 },
  H1: { x: 150, y: 200, width: 50, height: 50, floor: 0 },
  SM5: { x: 250, y: 200, width: 50, height: 50, floor: 0 },
  Y1: { x: 350, y: 200, width: 50, height: 50, floor: 0 },
  KM26: { x: 450, y: 200, width: 50, height: 50, floor: 0 },
  H2: { x: 550, y: 200, width: 50, height: 50, floor: 0 },
  A23: { x: 650, y: 200, width: 50, height: 50, floor: 0 },
  LABELLING: { x: 800, y: 200, width: 100, height: 200, floor: 0 },
  BRAVAN1: { x: 950, y: 200, width: 100, height: 200, floor: 0 },

  A17: { x: 50, y: 350, width: 50, height: 50, floor: 0 },
  A19: { x: 150, y: 350, width: 50, height: 50, floor: 0 },
  A18: { x: 250, y: 350, width: 50, height: 50, floor: 0 },
  A21: { x: 350, y: 350, width: 50, height: 50, floor: 0 },
  A16: { x: 450, y: 350, width: 50, height: 50, floor: 0 },

  A5: { x: 50, y: 500, width: 50, height: 50, floor: 0 },
  A10: { x: 150, y: 500, width: 50, height: 50, floor: 0 },
  A13: { x: 250, y: 500, width: 50, height: 50, floor: 0 },
  A14: { x: 350, y: 500, width: 50, height: 50, floor: 0 },
  A7: { x: 450, y: 500, width: 50, height: 50, floor: 0 },
  A12: { x: 550, y: 500, width: 50, height: 50, floor: 0 },
  A6: { x: 650, y: 500, width: 50, height: 50, floor: 0 },
  A15: { x: 750, y: 500, width: 50, height: 50, floor: 0 },
  A9: { x: 850, y: 500, width: 50, height: 50, floor: 0 },
  A8: { x: 950, y: 500, width: 50, height: 50, floor: 0 },

  // ── 1st Floor (floor: 1) ─────────────────────────────────────────────
  BRAVAN2: { x: 50, y: 200, width: 150, height: 50, floor: 1 },
  SU1: { x: 100, y: 400, width: 50, height: 50, floor: 1 },
};

export const ROLE_COLORS = {
  'SUPERVISOR':              'indigo',
  'LINE LEADER':             'teal',
  'PRODUCTION COORDINATOR':  'cyan',
  'PACKER':                  'khaki',
  'MATERIAL HANDLER':        'pink',
};
 
export const STAFF_COLORS = {
  ACTIVE:   'green',
  INACTIVE: 'red',
};

export function getCategoryColor(category) {
  const map = {
    'PRODUCTION RUNNING':    '#00ff00',
    'PRODUCT BUYOFF':        '#808080',
    'NO OPERATOR':           '#ffff00',
    'NO SCHEDULE':           '#ffff00',
    'MATERIAL DRYING':       '#ffff00',
    'OTHERS PROD':           '#ffff00',
    'QUALITY ISSUE':         '#ff0000',
    'SAMPLE RUNNING':        '#ff0000',
    'MOULD CHANGE':          '#ff0000',
    'OTHERS TECH':           '#ff0000',
    'SCHEDULED MAINTENANCE': '#ffa500',
    'MACHINE BREAKDOWN':     '#ffa500',
    'OTHERS MAIN':           '#ffa500',
  };
  return map[category] ?? '#808080';
}

export function getTodayString() {
  return new Date().toLocaleDateString('en-CA');
}
 
export function getCurrentShift() {
  const h = new Date().getHours();
  return h >= 6 && h < 18 ? 1 : 2;
}
 
export function formatDate(date) {
  if (!date) return null;
  if (date instanceof Date) return date.toLocaleDateString('en-CA');
  if (/^\d{4}-\d{2}-\d{2}$/.test(date)) return date;
  return new Date(date).toLocaleDateString('en-CA');
}
