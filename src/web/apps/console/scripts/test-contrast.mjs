#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  stripComments,
  parseThemeTokens,
  resolveVarReferences,
  parseColor,
  compositeColors,
  getContrastRatio,
  runBenchmarkSelfCheck,
  runContrastGuard,
} from './check-contrast.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');
const fixturesDir = path.join(__dirname, 'fixtures');
const preW22Fixture = path.join(fixturesDir, 'theme-pre-w22.scss');
const postW22Fixture = path.join(fixturesDir, 'theme-post-w22.scss');
const liveThemePath = path.join(projectRoot, 'src', 'styles', 'theme.scss');

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

console.log('--- [contrast-guard-tests] Running contrast guard automated test suite ---');

// 1. Benchmark checks (AC 4)
console.log('\n[Suite 1] WCAG Formula & Known Reference Benchmarks (AC 4)');
try {
  runBenchmarkSelfCheck();
  assert(true, 'runBenchmarkSelfCheck() executes successfully');
} catch (e) {
  assert(false, `runBenchmarkSelfCheck() threw: ${e.message}`);
}

const black = parseColor('#000');
const white = parseColor('#fff');
const gray1 = parseColor('#767676');
const gray2 = parseColor('#949494');
const blue = parseColor('#0d6efd');

assert(getContrastRatio(black, white).toFixed(2) === '21.00', '#000 on #fff is exactly 21.00:1');
assert(getContrastRatio(gray1, white).toFixed(2) === '4.54', '#767676 on #fff is exactly 4.54:1');
assert(getContrastRatio(gray2, white).toFixed(2) === '3.03', '#949494 on #fff is exactly 3.03:1');
assert(getContrastRatio(blue, white).toFixed(2) === '4.50', '#0d6efd on #fff is exactly 4.50:1');

// 2. Color Format Parsing (R1.4)
console.log('\n[Suite 2] Color Formats Support (Hex, RGB, RGBA, OKLCH, Named)');
const hex3 = parseColor('#fff');
assert(hex3.r === 255 && hex3.g === 255 && hex3.b === 255 && hex3.a === 1, 'Parses #fff');

const hex6 = parseColor('#0284c7');
assert(hex6.r === 2 && hex6.g === 132 && hex6.b === 199 && hex6.a === 1, 'Parses #0284c7');

const hex8 = parseColor('#0284c780');
assert(
  hex8.r === 2 && hex8.g === 132 && hex8.b === 199 && Math.abs(hex8.a - 0.5) < 0.01,
  'Parses #0284c780',
);

const rgbaModern = parseColor('rgb(15 23 42 / 50%)');
assert(
  rgbaModern.r === 15 && rgbaModern.g === 23 && rgbaModern.b === 42 && rgbaModern.a === 0.5,
  'Parses modern rgb(15 23 42 / 50%)',
);

const rgbaLegacy = parseColor('rgba(34, 197, 94, 0.18)');
assert(
  rgbaLegacy.r === 34 && rgbaLegacy.g === 197 && rgbaLegacy.b === 94 && rgbaLegacy.a === 0.18,
  'Parses legacy rgba(34, 197, 94, 0.18)',
);

const oklchWhite = parseColor('oklch(1 0 0)');
assert(
  oklchWhite.r === 255 && oklchWhite.g === 255 && oklchWhite.b === 255,
  'Parses oklch(1 0 0) as white',
);

const oklchAlpha = parseColor('oklch(0.6 0.15 240 / 75%)');
assert(oklchAlpha.a === 0.75, 'Parses oklch with alpha percentage');

// 3. SCSS Parsing with Nested Braces (R1.1, R1.5)
console.log('\n[Suite 3] SCSS Nested Braces Parsing & Dark Inheritance');
const mockScss = `
  /* Top Comment */
  :root,
  [data-theme='light'] {
    --bg-app: #f8fafc;
    --text-primary: #0f172a;
    --inherited-color: #123456;
  }

  @media (prefers-color-scheme: dark) {
    :root:not([data-theme='light']) {
      --bg-app: #090d16;
      --text-primary: #f8fafc;
    }
  }

  [data-theme='dark'] {
    --bg-app: #090d16;
    --text-primary: #f8fafc;
  }
`;

const parsedMock = parseThemeTokens(mockScss);
assert(parsedMock.light['--bg-app'] === '#f8fafc', 'Parses light tokens from root/light selector');
assert(
  parsedMock.dark['--bg-app'] === '#090d16',
  'Parses dark tokens from nested @media and dark selector',
);
assert(
  parsedMock.dark['--inherited-color'] === '#123456',
  'Dark mode inherits un-overridden tokens from light mode',
);

// 4. Var references & cycle detection (R1.3)
console.log('\n[Suite 4] var() Resolution & Cycle Detection');
const varTokens = {
  '--base-brand': '#006eb6',
  '--color-primary': 'var(--base-brand)',
  '--btn-bg': 'var(--color-primary)',
};
assert(
  resolveVarReferences('var(--btn-bg)', varTokens) === '#006eb6',
  'Resolves nested var() references',
);
assert(
  resolveVarReferences('var(--non-existent, #ffffff)', varTokens) === '#ffffff',
  'Resolves var() with fallback value',
);

let caughtCycle = false;
try {
  const cycleTokens = { '--a': 'var(--b)', '--b': 'var(--a)' };
  resolveVarReferences('var(--a)', cycleTokens);
} catch (e) {
  caughtCycle = true;
}
assert(caughtCycle, 'Detects and throws on cyclic var() references');

// 5. Alpha Compositing (R1.2)
console.log('\n[Suite 5] Alpha Compositing');
const semiWhite = { r: 255, g: 255, b: 255, a: 0.5 };
const blackBg = { r: 0, g: 0, b: 0, a: 1 };
const comp = compositeColors(semiWhite, blackBg);
assert(
  comp.r === 128 && comp.g === 128 && comp.b === 128 && comp.a === 1,
  'Composites 50% white over black to 128 gray',
);

// 6. Pre-W22 Regression Fixture (R1: Catches historical 4.10:1 accident)
console.log('\n[Suite 6] Pre-W22 Historical Incident Regression (Frozen Fixture)');
const preW22Result = runContrastGuard(preW22Fixture);
assert(!preW22Result.success, 'Pre-W22 fixture fails contrast guard as expected');
assert(
  preW22Result.violations.length === 1,
  `Pre-W22 fixture has exactly 1 violation (found ${preW22Result.violations.length})`,
);
if (preW22Result.violations.length === 1) {
  const v = preW22Result.violations[0];
  assert(
    v.fgToken === '--text-inverse' && v.bgToken === '--color-primary',
    'Violation is --text-inverse on --color-primary',
  );
  assert(
    v.formattedRatio === '4.10:1',
    `Violation contrast ratio is 4.10:1 (got ${v.formattedRatio})`,
  );
  assert(v.theme === 'light', 'Violation occurs in light theme');
}

// 7. Post-W22 Fixed Theme Fixture (R2: Fully compliant theme passes with exit 0)
console.log('\n[Suite 7] Post-W22 Fixed Theme Fixture (Frozen Fixture)');
const postW22Result = runContrastGuard(postW22Fixture);
assert(postW22Result.success, 'Post-W22 fixture passes contrast guard with exit code 0');
assert(postW22Result.violations.length === 0, 'Post-W22 fixture has 0 violations');

// 8. Reverse Mutation Verification (AC 3: Mutating badge text in clean copy)
console.log('\n[Suite 8] Reverse Mutation Verification (AC 3)');
const postW22Content = fs.readFileSync(postW22Fixture, 'utf8');
const mutatedContent = postW22Content.replace(
  '--badge-success-text: #166534;',
  '--badge-success-text: #bbf7d0;',
);
const tmpMutatedPath = path.join('/tmp', `theme-mutated-${Date.now()}.scss`);
fs.writeFileSync(tmpMutatedPath, mutatedContent, 'utf8');

const mutatedResult = runContrastGuard(tmpMutatedPath);
fs.unlinkSync(tmpMutatedPath);
assert(!mutatedResult.success, 'Mutated theme fails contrast guard');
assert(
  mutatedResult.violations.length === 1,
  `Mutated theme catches exactly 1 violation (found ${mutatedResult.violations.length})`,
);
if (mutatedResult.violations.length === 1) {
  const v = mutatedResult.violations[0];
  assert(
    v.fgToken === '--badge-success-text' && v.bgToken === '--badge-success-bg',
    'Mutated violation is on --badge-success-text vs --badge-success-bg',
  );
}

// 9. Live Theme Sanity Check
console.log('\n[Suite 9] Live Workspace theme.scss Sanity Check');
if (fs.existsSync(liveThemePath)) {
  const liveResult = runContrastGuard(liveThemePath);
  assert(liveResult.success, 'Live workspace theme.scss passes contrast guard with 0 violations');
}

console.log(
  `\n--- Test Summary: ${passedTests}/${totalTests} tests passed (${failedTests} failures) ---\n`,
);

if (failedTests > 0) {
  process.exit(1);
} else {
  process.exit(0);
}
