#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  containsColorLiteral,
  isLineExempted,
  scanHardcodedColors,
  runHardcodedColorsGuard,
} from './check-hardcoded-colors.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');

let totalTests = 0;
let passedTests = 0;
let failedTests = 0;

function assert(condition, message) {
  totalTests++;
  if (condition) {
    passedTests++;
    console.log(`  [PASS] ${message}`);
  } else {
    failedTests++;
    console.error(`  [FAIL] ${message}`);
  }
}

console.log('--- [hardcoded-colors-guard-tests] Running Guard 10 automated test suite ---');

// 1. Color Format Detection
console.log('\n[Suite 1] Color Format Detection Regex');
assert(containsColorLiteral('color: #fff;'), 'Detects 3-digit hex #fff');
assert(containsColorLiteral('background: #0284c7;'), 'Detects 6-digit hex #0284c7');
assert(containsColorLiteral('border: 1px solid #0284c780;'), 'Detects 8-digit hex #0284c780');
assert(containsColorLiteral('background: rgba(15, 23, 42, 0.6);'), 'Detects rgba(...)');
assert(containsColorLiteral('color: rgb(15, 23, 42);'), 'Detects rgb(...)');
assert(containsColorLiteral('color: hsl(200, 50%, 50%);'), 'Detects hsl(...)');
assert(containsColorLiteral('color: hsla(200, 50%, 50%, 0.5);'), 'Detects hsla(...)');
assert(!containsColorLiteral('color: var(--color-primary);'), 'Ignores var(--color-primary)');
assert(!containsColorLiteral('font-size: 1rem; margin: 0;'), 'Ignores non-color CSS rules');

// 2. Exemption Mechanism
console.log('\n[Suite 2] Item-by-Item Exemption Parsing');
const linesSameLine = ['background: #0284c7; // allow-hardcoded-color: ECharts canvas requirement'];
assert(isLineExempted(linesSameLine, 0), 'Exempts line with inline allow-hardcoded-color');

const linesPreceding = [
  '// allow-hardcoded-color: Third-party canvas rendering',
  'background: #0284c7;',
];
assert(isLineExempted(linesPreceding, 1), 'Exempts line with preceding allow-hardcoded-color comment');

const linesHtml = ['<!-- allow-hardcoded-color: Static email badge -->', '<span style="color: #dc2626">Due</span>'];
assert(isLineExempted(linesHtml, 1), 'Exempts HTML with preceding comment');

const linesNoExempt = ['background: #0284c7;'];
assert(!isLineExempted(linesNoExempt, 0), 'Does not exempt line without marker');

// 3. Dual-Direction Verification (Clean copy mutation in /tmp)
console.log('\n[Suite 3] Dual-Direction Verification in Clean Environment (AC 4)');
const tmpDir = path.join('/tmp', `color-guard-test-${Date.now()}`);
fs.mkdirSync(tmpDir, { recursive: true });

// A. Clean file with design tokens -> PASS
fs.writeFileSync(
  path.join(tmpDir, 'clean.component.scss'),
  `.card { background: var(--bg-surface); color: var(--text-primary); }`,
  'utf8',
);
fs.writeFileSync(
  path.join(tmpDir, 'clean.component.html'),
  `<div class="card"><span class="title">Hello</span></div>`,
  'utf8',
);
const cleanResult = runHardcodedColorsGuard(tmpDir);
assert(cleanResult.success, 'Clean fixture with tokens passes with 0 violations');

// B. Inject unexempted hardcoded color -> FAIL
fs.writeFileSync(
  path.join(tmpDir, 'injected.component.scss'),
  `.danger { color: #dc2626; }`,
  'utf8',
);
const injectedResult = runHardcodedColorsGuard(tmpDir);
assert(!injectedResult.success, 'Injected unexempted hardcoded color fails guard');
assert(injectedResult.violations.length === 1, 'Injected fixture detects exactly 1 violation');

// C. Add explicit item exemption -> PASS
fs.writeFileSync(
  path.join(tmpDir, 'injected.component.scss'),
  `.danger { color: #dc2626; } // allow-hardcoded-color: Spec requirement test`,
  'utf8',
);
const exemptResult = runHardcodedColorsGuard(tmpDir);
assert(exemptResult.success, 'Explicitly exempted hardcoded color passes guard with exit code 0');

// Clean up temporary test files
fs.rmSync(tmpDir, { recursive: true, force: true });

// 4. Workspace Clean Status Verification (W39 Cleanup)
console.log('\n[Suite 4] Full Workspace Hardcoded Color Audit (W39 Cleanup)');
const wsViolations = scanHardcodedColors();
const wsFiles = new Set(wsViolations.map((v) => v.file));
const scssCount = Array.from(wsFiles).filter((f) => f.endsWith('.scss')).length;
const htmlCount = Array.from(wsFiles).filter((f) => f.endsWith('.html')).length;
assert(scssCount === 0, `Workspace has 0 SCSS files with hardcoded colors (found ${scssCount})`);
assert(htmlCount === 0, `Workspace has 0 HTML files with hardcoded colors (found ${htmlCount})`);
assert(wsViolations.length === 0, `Workspace has 0 hardcoded color locations across all files (found ${wsViolations.length})`);

const guardResult = runHardcodedColorsGuard();
assert(guardResult.success, 'runHardcodedColorsGuard() returns success on clean workspace');

console.log(
  `\n--- Test Summary: ${passedTests}/${totalTests} tests passed (${failedTests} failures) ---\n`,
);

if (failedTests > 0) {
  process.exit(1);
} else {
  process.exit(0);
}
