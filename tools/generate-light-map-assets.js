'use strict';

// Reproducible presentation-only derivative. Canonical SVGs remain untouched.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const root = path.resolve(__dirname, '..');
const resources = path.join(root, 'Resources');
const output = path.join(resources, 'map-themes', 'light');
const maps = ['plano_norte_limpio.svg', 'plano_nivel3_limpio.svg', 'plano_sur_limpio.svg', 'plano_id.svg', 'plano_qc_limpio.svg'];
const replacements = new Map([
  ['#ffffff', '#fafafa'], ['#eeeeee', '#f7f7f5'], ['#dcdcdc', '#e5e7eb'], ['#dbdbdb', '#e5e7eb'], ['#d1d1d1', '#e5e7eb'],
  ['#bababa', '#374151'], ['#989898', '#1f2937'], ['#808080', '#111827'], ['#767676', '#374151'], ['#636466', '#1f2937'], ['#545454', '#111827'], ['#505050', '#111827'], ['#000000', '#05070b']
]);
fs.mkdirSync(output, { recursive: true });
for (const file of maps) {
  const canonicalPath = path.join(resources, file);
  const canonical = fs.readFileSync(canonicalPath, 'utf8');
  let derived = canonical;
  for (const [from, to] of replacements) derived = derived.replace(new RegExp(from, 'gi'), to);
  derived = derived.replace(/<svg\b([^>]*)>/i, '<svg$1><rect id="presentation-light-canvas" width="100%" height="100%" fill="#fafafa"/>');
  fs.writeFileSync(path.join(output, file), derived, 'utf8');
  console.log(`${file}: canonical ${crypto.createHash('sha256').update(canonical).digest('hex')} → light derived`);
}
