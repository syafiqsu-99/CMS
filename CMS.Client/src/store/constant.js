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
  { text: 'Morning',  value: 1 },
  { text: 'Night',    value: 2 }
];

export const MACHINEOPTIONS = [
  { text: 'A5',   value: 1 },
  { text: 'A6',   value: 2 },
  { text: 'A7',   value: 3 },
  { text: 'A8',   value: 4 },
  { text: 'A9',   value: 5 },
  { text: 'A10',  value: 6 },
  { text: 'A12',  value: 7 },
  { text: 'A13',  value: 8 },
  { text: 'A14',  value: 9 },
  { text: 'A15',  value: 10 },
  { text: 'A16',  value: 11 },
  { text: 'A17',  value: 12 },
  { text: 'A18',  value: 13 },
  { text: 'A19',  value: 14 },
  { text: 'A21',  value: 15 },
  { text: 'A23',  value: 16 },
  { text: 'KM16', value: 17 },
  { text: 'KM17', value: 18 },
  { text: 'KM18', value: 19 },
  { text: 'KM25', value: 20 },
  { text: 'KM26', value: 21 },
  { text: 'Y1',   value: 22 },
  { text: 'SM5',  value: 23 },
  { text: 'H1',   value: 24 },
  { text: 'B3',   value: 25 },
  { text: 'TEST', value: 26 }
];

export const MACHINEPOSITIONS = {
  KM25:   { x: 200, y: 50, width: 50,  height: 50 },
  KM16:   { x: 300, y: 50, width: 50,  height: 50 },
  KM17:   { x: 400, y: 50, width: 50,  height: 50 },
  J_WELL: { x: 500, y: 50, width: 200, height: 50 },

  B3:     { x: 50,  y: 200, width: 50, height: 50 },
  H1:     { x: 150, y: 200, width: 50, height: 50 },
  SM5:    { x: 250, y: 200, width: 50, height: 50 },
  Y1:     { x: 350, y: 200, width: 50, height: 50 },
  KM26:   { x: 450, y: 200, width: 50, height: 50 },
  H2:     { x: 550, y: 200, width: 50, height: 50 },
  A23:    { x: 650, y: 200, width: 50, height: 50 },
  BRAVAN: { x: 950, y: 200, width: 50, height: 150 },

  A17:    { x: 50,  y: 350, width: 50, height: 50 },
  A19:    { x: 150, y: 350, width: 50, height: 50 },
  A18:    { x: 250, y: 350, width: 50, height: 50 },
  A21:    { x: 350, y: 350, width: 50, height: 50 },
  A16:    { x: 450, y: 350, width: 50, height: 50 },

  A5:     { x: 50,  y: 500, width: 50, height: 50 },
  A10:    { x: 150, y: 500, width: 50, height: 50 },
  A13:    { x: 250, y: 500, width: 50, height: 50 },
  A14:    { x: 350, y: 500, width: 50, height: 50 },
  A7:     { x: 450, y: 500, width: 50, height: 50 },
  A12:    { x: 550, y: 500, width: 50, height: 50 },
  A6:     { x: 650, y: 500, width: 50, height: 50 },
  A15:    { x: 750, y: 500, width: 50, height: 50 },
  A9:     { x: 850, y: 500, width: 50, height: 50 },
  A8:     { x: 950, y: 500, width: 50, height: 50 }
};

export const STAFF_COLORS = {
  'ACTIVE': 'green',
  'INACTIVE': 'red'
};

export const ROLE_COLORS = {
  'SUPERVISOR': 'indigo',
  'LINE LEADER': 'teal',
  'PRODUCTION COORDINATOR': 'cyan',
  'PACKER': 'khaki',
  'MATERIAL HANDLER': 'pink'
};
