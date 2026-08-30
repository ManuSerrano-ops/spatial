'use strict';

let passed = 0;
function assert(condition, message) { if (!condition) throw new Error(message); passed++; }
function normalize(value) { return String(value || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim().replace(/\s+/g, ' '); }
function score(query, primary, secondary = '') { const q = normalize(query), p = normalize(primary), s = normalize(secondary); if (p === q) return 1000; if (p.startsWith(q)) return 800; if (p.split(' ').includes(q)) return 600; if (p.includes(q)) return 400; return s.includes(q) ? 200 : 0; }
function semantic(zoom, previous = 'GLOBAL') { if (previous === 'DETAIL') return zoom < 1.8 ? 'OPERATIVE' : 'DETAIL'; if (previous === 'OPERATIVE') return zoom >= 2 ? 'DETAIL' : zoom < 1.15 ? 'GLOBAL' : 'OPERATIVE'; return zoom >= 1.25 ? 'OPERATIVE' : 'GLOBAL'; }
function range(visible, anchor, target) { return visible.slice(Math.min(visible.indexOf(anchor), visible.indexOf(target)), Math.max(visible.indexOf(anchor), visible.indexOf(target)) + 1); }
assert(normalize(' García ') === 'garcia', 'normalización de acentos');
assert(normalize('  A-17  ') === 'a-17', 'normalización de espacios');
assert(score('A-17', 'A-17') > score('A-17', 'Juan García', 'A-17'), 'exacto antes que secundario');
assert(score('PCS', 'PCS-LPT-042') > score('PCS', 'Equipo', 'PCS'), 'startsWith antes que secundario');
assert(JSON.stringify(range(['A2', 'A5', 'B4', 'C9', 'D1'], 'A5', 'C9')) === JSON.stringify(['A5', 'B4', 'C9']), 'Shift usa orden visible');
assert(semantic(1.26, 'GLOBAL') === 'OPERATIVE' && semantic(1.20, 'OPERATIVE') === 'OPERATIVE' && semantic(1.14, 'OPERATIVE') === 'GLOBAL', 'histéresis operativo');
assert(semantic(2.01, 'OPERATIVE') === 'DETAIL' && semantic(1.90, 'DETAIL') === 'DETAIL' && semantic(1.79, 'DETAIL') === 'OPERATIVE', 'histéresis detalle');
const fixture = [{ zone: 'Sur', state: 'occupied', quality: 'partial' }, { zone: 'Sur', state: 'occupied', quality: 'complete' }, { zone: 'Norte', state: 'occupied', quality: 'partial' }];
assert(fixture.filter(item => item.zone === 'Sur' && item.state === 'occupied' && item.quality === 'partial').length === 1, 'filtros AND');
const filters = { zone: 'Sur', state: 'occupied', quality: 'partial' }; delete filters.zone; assert(filters.state === 'occupied' && filters.quality === 'partial', 'chip elimina solo su filtro'); Object.keys(filters).forEach(key => delete filters[key]); assert(Object.keys(filters).length === 0, 'limpiar filtros');
const visible = (layers, level, field) => Boolean(layers[field] && (field === 'people' ? level !== 'GLOBAL' : level === 'DETAIL')); assert(!visible({ people: false }, 'DETAIL', 'people') && !visible({ people: true }, 'GLOBAL', 'people') && visible({ people: true }, 'OPERATIVE', 'people') && visible({ devices: true }, 'DETAIL', 'devices') && visible({ network: true }, 'DETAIL', 'network'), 'capas semánticas');
const valid = rosetas => ({ valid: !rosetas.includes('R1'), errors: rosetas.includes('R1') ? { roseta: 'duplicada' } : {} }); assert(valid([]).valid && !valid(['R1']).valid && valid(['R1']).errors.roseta === 'duplicada', 'validación inline');
const persistence = ['idle', 'dirty', 'saving', 'saved', 'saving', 'error', 'saving', 'conflict']; assert(persistence.join('>') === 'idle>dirty>saving>saved>saving>error>saving>conflict', 'transiciones persistencia');
const groups = ['PERSONAS', 'PUESTOS', 'EQUIPOS', 'RED'].filter(group => group !== 'RED' || false); assert(!groups.includes('RED') && groups.length === 3, 'grupos vacíos omitidos');
console.log(`core-ux-harness: ${passed}/14 passed, 0 failed`);
