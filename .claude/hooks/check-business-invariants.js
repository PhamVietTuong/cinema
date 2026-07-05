#!/usr/bin/env node
/* ============================================================================
 * Cinema — PreToolUse anti-regression business hook (Node, no deps)
 * ----------------------------------------------------------------------------
 * Fires before Edit/Write/MultiEdit/NotebookEdit. Reads the tool payload on
 * stdin, then:
 *   1. detects which cinema module the touched file belongs to (path/entity match)
 *   2. matches the diff text against diff_rules regexes (P0/P1/P2)
 *   3. matches the diff text against business-flows.json trigger_keywords
 *   4. P0 violation -> permissionDecision "ask" (Claude must confirm before writing)
 *      otherwise (module/flow matched, no P0) -> additionalContext soft warning
 *      nothing matched -> silent allow (exit 0, no output)
 *
 * Cross-platform, dependency-free. Wired via .claude/settings.json PreToolUse.
 * ==========================================================================*/
'use strict';
const fs = require('fs');
const path = require('path');

function readStdin() {
  try { return fs.readFileSync(0, 'utf8'); } catch { return ''; }
}
function loadJson(p) {
  try { return JSON.parse(fs.readFileSync(p, 'utf8')); } catch { return null; }
}

const HERE = __dirname;
const mapping = loadJson(path.join(HERE, 'module-paths-mapping.json'));
const flows = loadJson(path.join(HERE, 'business-flows.json'));

const raw = readStdin();
if (!raw) process.exit(0);

let payload;
try { payload = JSON.parse(raw); } catch { process.exit(0); }

const tool = payload.tool_name || '';
if (!['Edit', 'Write', 'MultiEdit', 'NotebookEdit'].includes(tool)) process.exit(0);

const ti = payload.tool_input || {};
const filePath = ti.file_path || '';
if (!filePath) process.exit(0);

// Build the diff text we scan, per tool shape.
let diff = '';
if (tool === 'Edit') diff = `${ti.old_string || ''}\n---NEW---\n${ti.new_string || ''}`;
else if (tool === 'Write') diff = ti.content || '';
else if (tool === 'MultiEdit') diff = (ti.edits || []).map(e => `${e.old_string || ''}\n---NEW---\n${e.new_string || ''}`).join('\n');
else if (tool === 'NotebookEdit') diff = ti.new_source || '';

const normPath = filePath.replace(/\\/g, '/').toLowerCase();
const fileName = path.basename(filePath).toLowerCase();
const diffLower = diff.toLowerCase();

// --- 1. module match ---
const matchedModules = [];
if (mapping && mapping.modules) {
  for (const [name, mod] of Object.entries(mapping.modules)) {
    let hit = (mod.paths || []).some(p => normPath.includes(String(p).toLowerCase()));
    if (!hit) {
      hit = (mod.entities || []).some(e => fileName.includes(String(e).toLowerCase().replace(/\.cs$/, '')));
    }
    if (hit) matchedModules.push({ name, ...mod });
  }
}

// --- 2. diff_rules ---
const violations = [];
let hasP0 = false;
if (mapping && mapping.diff_rules) {
  for (const rule of mapping.diff_rules) {
    let re;
    try { re = new RegExp(rule.pattern); } catch { continue; }
    if (re.test(diff)) {
      violations.push(rule);
      if (rule.severity === 'P0') hasP0 = true;
    }
  }
}

// --- 3. business flows (keyword in diff) ---
const matchedFlows = [];
if (flows && flows.flows) {
  for (const f of flows.flows) {
    const hitKw = (f.trigger_keywords || []).find(k => diffLower.includes(String(k).toLowerCase()));
    if (hitKw) matchedFlows.push({ flow: f, hit: hitKw });
  }
}

if (matchedModules.length === 0 && violations.length === 0 && matchedFlows.length === 0) {
  process.exit(0); // silent allow
}

// --- build message ---
const L = [];
L.push('============================================================');
L.push('  CINEMA ANTI-REGRESSION — business invariants');
L.push('============================================================');
L.push(`File : ${filePath}`);
L.push(`Tool : ${tool}`);
L.push('');

for (const mod of matchedModules) {
  L.push(`>> MODULE: ${mod.label}`);
  if (mod.flow_id) L.push(`   Flow test: /test-flow ${mod.flow_id}`);
  L.push('   Top invariants to re-read before this edit:');
  for (const inv of (mod.top_p0 || [])) L.push(`   - ${inv}`);
  if (mod.coupled_to && mod.coupled_to.length) L.push(`   ! Coupled modules: ${mod.coupled_to.join(', ')}`);
  L.push('');
}

if (violations.length) {
  L.push('>> RISKY PATTERNS DETECTED IN THE DIFF:');
  L.push('');
  for (const v of violations) {
    const tag = v.severity === 'P0' ? '[!! P0 !!]' : v.severity === 'P1' ? '[ P1 ]' : '[ P2 ]';
    L.push(`   ${tag} ${v.id} — ${v.label}`);
    L.push(`      Violates: ${v.violates}`);
    L.push(`      → ${v.message}`);
    L.push('');
  }
}

if (matchedFlows.length) {
  L.push('>> BUSINESS FLOWS POTENTIALLY IMPACTED:');
  L.push('');
  for (const { flow, hit } of matchedFlows) {
    L.push(`   [FLOW] ${flow.id} — ${flow.name}`);
    L.push(`      Detected via keyword: '${hit}'`);
    for (const s of (flow.regression_symptoms || []).slice(0, 3)) L.push(`        * ${s}`);
    if (flow.flow_id) L.push(`      Run: /test-flow ${flow.flow_id}`);
    L.push('');
  }
}

L.push('------------------------------------------------------------');
L.push('Re-read the invariants above; run the relevant /test-flow before pushing.');
L.push('============================================================');
const msg = L.join('\n');

let out;
if (hasP0) {
  out = {
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      permissionDecision: 'ask',
      permissionDecisionReason: `P0 invariant risk on a documented cinema module. Confirm you've re-read the invariants + have a plan before writing.\n\n${msg}`,
    },
  };
} else {
  out = {
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      additionalContext: msg,
    },
  };
}
process.stdout.write(JSON.stringify(out));
process.exit(0);
