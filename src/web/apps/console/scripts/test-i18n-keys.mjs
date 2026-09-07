#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  getAllKeys,
  collectReferencedKeys,
  runI18nKeysGuard,
  DYNAMIC_PREFIX_MAPPINGS,
} from './check-i18n-keys.mjs';

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

console.log('--- [i18n-keys-guard-tests] Running Guard 9 automated test suite ---');

// 1. Dictionary key flattening
console.log('\n[Suite 1] Dictionary Key Hierarchy Flattening');
const sampleDict = {
  AUTH: { LOGIN: 'Sign in', SUBTITLE: 'Sub' },
  COMMON: { UNITS: { H: 'hrs', M: 'mins' } },
};
const flattened = getAllKeys(sampleDict);
assert(flattened.includes('AUTH.LOGIN'), 'Flattens 2-level keys');
assert(flattened.includes('COMMON.UNITS.H'), 'Flattens 3-level keys');
assert(flattened.length === 4, 'Flattens exact key count (4)');

// 2. Dynamic Prefix Coverage & Enum Expansion
console.log('\n[Suite 2] Dynamic Prefix Expansion Coverage');
assert(
  DYNAMIC_PREFIX_MAPPINGS['DISPATCH.STATUS_'].length === 6,
  'DISPATCH.STATUS_ expands to 6 status variants',
);
assert(
  DYNAMIC_PREFIX_MAPPINGS['FINES.STATUS_'].length === 5,
  'FINES.STATUS_ expands to 5 status variants',
);
assert(
  DYNAMIC_PREFIX_MAPPINGS['PAYROLL.STATUS_'].length === 5,
  'PAYROLL.STATUS_ expands to 5 status variants',
);
assert(
  DYNAMIC_PREFIX_MAPPINGS['TIMESHEETS.STATUS_'].length === 3,
  'TIMESHEETS.STATUS_ expands to 3 status variants',
);
assert(
  DYNAMIC_PREFIX_MAPPINGS['VEHICLES.STATUS_'].length === 4,
  'VEHICLES.STATUS_ expands to 4 status variants',
);
assert(
  DYNAMIC_PREFIX_MAPPINGS['DRIVERS.STATUS_'].length === 5,
  'DRIVERS.STATUS_ expands to 5 status variants',
);
assert(
  DYNAMIC_PREFIX_MAPPINGS['DRIVER.SHIFT_STATUS_'].length === 4,
  'DRIVER.SHIFT_STATUS_ expands to 4 status variants',
);
assert(
  DYNAMIC_PREFIX_MAPPINGS['DRIVER.TRIP_STATUS_'].length === 6,
  'DRIVER.TRIP_STATUS_ expands to 6 status variants',
);

// 3. Simulated Missing Status Enum Injection (Defect Verification)
console.log('\n[Suite 3] Missing Status Enum Defect Catching');
const tmpTestDir = path.join('/tmp', `i18n-guard-test-${Date.now()}`);
fs.mkdirSync(tmpTestDir, { recursive: true });

const mockEn = {
  DISPATCH: {
    STATUS_DRAFT: 'Draft',
    STATUS_ASSIGNED: 'Assigned',
    STATUS_ACKNOWLEDGED: 'Acknowledged',
    STATUS_IN_PROGRESS: 'In Progress',
    STATUS_COMPLETED: 'Completed',
    // Deliberately missing STATUS_CANCELLED
  },
};
const mockZh = {
  DISPATCH: {
    STATUS_DRAFT: '草稿',
    STATUS_ASSIGNED: '已分配',
    STATUS_ACKNOWLEDGED: '已接单',
    STATUS_IN_PROGRESS: '进行中',
    STATUS_COMPLETED: '已完成',
    STATUS_CANCELLED: '已取消',
  },
};

const mockI18nDir = path.join(tmpTestDir, 'i18n');
const mockAppDir = path.join(tmpTestDir, 'app');
fs.mkdirSync(mockI18nDir, { recursive: true });
fs.mkdirSync(mockAppDir, { recursive: true });

fs.writeFileSync(path.join(mockI18nDir, 'en-NZ.json'), JSON.stringify(mockEn, null, 2), 'utf8');
fs.writeFileSync(path.join(mockI18nDir, 'zh-CN.json'), JSON.stringify(mockZh, null, 2), 'utf8');

// HTML using dynamic prefix
fs.writeFileSync(
  path.join(mockAppDir, 'test.component.html'),
  `<span>{{ 'DISPATCH.STATUS_' + getStatusKey(task.status) | i18n }}</span>`,
  'utf8',
);

const guardResult = runI18nKeysGuard({
  i18nDir: mockI18nDir,
  appDir: mockAppDir,
});

assert(!guardResult.success, 'Guard fails when an enum status key is missing in dictionary');
assert(
  guardResult.errors.some((e) => e.key === 'DISPATCH.STATUS_CANCELLED' && e.lang === 'en-NZ.json'),
  'Accurately catches missing dynamic status key DISPATCH.STATUS_CANCELLED in en-NZ.json',
);

// 4. Test Key Whitelist Filtering
console.log('\n[Suite 4] Test-Only Key Filtering');
fs.writeFileSync(
  path.join(mockAppDir, 'test.spec.ts'),
  `expect(service.translate('NON.EXISTENT.KEY')).toBe('NON.EXISTENT.KEY');`,
  'utf8',
);
const keysCollected = collectReferencedKeys(mockAppDir);
assert(!keysCollected.has('NON.EXISTENT.KEY'), 'NON.EXISTENT.KEY is filtered from violation list');

// Cleanup temporary test fixture directory
fs.rmSync(tmpTestDir, { recursive: true, force: true });

console.log(
  `\n--- Test Summary: ${passedTests}/${totalTests} tests passed (${failedTests} failures) ---\n`,
);

if (failedTests > 0) {
  process.exit(1);
} else {
  process.exit(0);
}
