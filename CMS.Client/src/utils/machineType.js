export const MACHINE_TYPES = {
    ISBM: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16],
    EBM: [20, 21, 22, 23, 29],
    IM: [17, 18, 19, 24, 25, 28],
    '2 Stages': [26, 27],
};

export const TYPE_ORDER = ['ISBM', 'EBM', 'IM', '2 Stages'];

const ID_TO_TYPE = Object.entries(MACHINE_TYPES).reduce((map, [type, ids]) => {
    ids.forEach(id => { map[id] = type; });
    return map;
}, {});

export function typeOf(idMachine) {
    return ID_TO_TYPE[Number(idMachine)] ?? null;
}

export function machinesOfType(machines, type) {
    if (!type || type === 'All') return machines;
    return machines.filter(m => typeOf(m.id_machine) === type);
}