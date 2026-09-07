#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  getAllKeys,
  collectReferencedKeys,
  runI18nKeysGuard,
  DYNAMIC_PREFIX_MAPPINGS,
  DYNAMIC_PREFIX_ENUM_MAP,
  toScreamingSnake,
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
  DYNAMIC_PREFIX_MAPPINGS['PAYROLL.STATUS_'].length === 4,
  'PAYROLL.STATUS_ expands to 4 status variants',
);
assert(
  DYNAMIC_PREFIX_MAPPINGS['TIMESHEETS.STATUS_'].length === 4,
  'TIMESHEETS.STATUS_ expands to 4 status variants from ShiftStatus',
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
  `<span>{{ 'DISPATCH.STATUS_' + toScreamingSnake(task.status) | i18n }}</span>`,
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

// 4. Defect Catching on Pre-Fix Transformations (FINES.STATUS_UNDERREVIEW & TIMESHEETS.STATUS_AUTOCLOSED)
console.log('\n[Suite 4] Pre-Fix Bug Reproduction (FINES.STATUS_UNDERREVIEW & TIMESHEETS.STATUS_AUTOCLOSED)');
const tmpBugDir = path.join('/tmp', `i18n-guard-bug-test-${Date.now()}`);
const mockBugI18n = path.join(tmpBugDir, 'i18n');
const mockBugApp = path.join(tmpBugDir, 'app');
fs.mkdirSync(mockBugI18n, { recursive: true });
fs.mkdirSync(mockBugApp, { recursive: true });

// Dictionaries with canonical keys
const validEn = {
  FINES: {
    STATUS_SUBMITTED: 'Submitted',
    STATUS_UNDER_REVIEW: 'Under Review',
    STATUS_ACCEPTED: 'Accepted',
    STATUS_DISPUTED: 'Disputed',
    STATUS_WAIVED: 'Waived',
  },
  TIMESHEETS: {
    STATUS_ACTIVE: 'On Duty',
    STATUS_COMPLETED: 'Completed',
    STATUS_AUTO_CLOSED: 'Auto Closed',
    STATUS_CANCELLED: 'Cancelled',
  },
};
fs.writeFileSync(path.join(mockBugI18n, 'en-NZ.json'), JSON.stringify(validEn, null, 2), 'utf8');
fs.writeFileSync(path.join(mockBugI18n, 'zh-CN.json'), JSON.stringify(validEn, null, 2), 'utf8');

// Pre-fix fines component using replace(' ', '_').toUpperCase()
fs.writeFileSync(
  path.join(mockBugApp, 'fines.component.html'),
  `<span>{{ 'FINES.STATUS_' + fine.status.replace(' ', '_').toUpperCase() | i18n }}</span>`,
  'utf8',
);
// Pre-fix timesheets component using shift.status.toUpperCase()
fs.writeFileSync(
  path.join(mockBugApp, 'timesheets.component.html'),
  `<span>{{ 'TIMESHEETS.STATUS_' + shift.status.toUpperCase() | i18n }}</span>`,
  'utf8',
);

const bugGuardResult = runI18nKeysGuard({
  i18nDir: mockBugI18n,
  appDir: mockBugApp,
});

assert(!bugGuardResult.success, 'Guard fails on pre-fix malformed dynamic concatenation');
assert(
  bugGuardResult.errors.some((e) => e.key === 'FINES.STATUS_UNDERREVIEW'),
  'Accurately catches FINES.STATUS_UNDERREVIEW caused by replace(" ", "_")',
);
assert(
  bugGuardResult.errors.some((e) => e.key === 'TIMESHEETS.STATUS_AUTOCLOSED'),
  'Accurately catches latent TIMESHEETS.STATUS_AUTOCLOSED caused by shift.status.toUpperCase()',
);

// 5. Negative Verification: Adding Domain Enum Member Without Translation Key
console.log('\n[Suite 5] Negative Verification (Unmapped Domain Enum Member)');
const mockCustomEnums = new Map([
  [
    'FineStatus',
    {
      members: ['Submitted', 'UnderReview', 'Accepted', 'Disputed', 'Waived', 'Appealed'],
    },
  ],
]);
const negGuardResult = runI18nKeysGuard({
  i18nDir: mockBugI18n,
  appDir: mockBugApp,
  csEnums: mockCustomEnums,
});
assert(!negGuardResult.success, 'Guard fails when new domain enum member is added without translation');
assert(
  negGuardResult.errors.some((e) => e.key === 'FINES.STATUS_APPEALED'),
  'Accurately catches missing translation for new enum member FINES.STATUS_APPEALED',
);

// 6. Unregistered Dynamic Prefix Rejection
console.log('\n[Suite 6] Unregistered Dynamic Prefix Fail-Fast Rejection');
const tmpUnregApp = path.join(tmpTestDir, 'unreg_app');
fs.mkdirSync(tmpUnregApp, { recursive: true });
fs.writeFileSync(
  path.join(tmpUnregApp, 'custom.component.html'),
  `<span>{{ 'UNKNOWN_MODULE.STATUS_' + toScreamingSnake(item.status) | i18n }}</span>`,
  'utf8',
);

const unregResult = runI18nKeysGuard({
  i18nDir: mockBugI18n,
  appDir: tmpUnregApp,
});
assert(!unregResult.success, 'Guard fails on unregistered dynamic prefix');
assert(
  unregResult.errors.some((e) => e.key === 'UNKNOWN_MODULE.STATUS_'),
  'Guarantees fail-fast error on unregistered dynamic prefix UNKNOWN_MODULE.STATUS_',
);

// 7. Test Key Whitelist Filtering
console.log('\n[Suite 7] Test-Only Key Filtering');
fs.writeFileSync(
  path.join(mockAppDir, 'test.spec.ts'),
  `expect(service.translate('NON.EXISTENT.KEY')).toBe('NON.EXISTENT.KEY');`,
  'utf8',
);
const keysCollected = collectReferencedKeys(mockAppDir);
assert(!keysCollected.has('NON.EXISTENT.KEY'), 'NON.EXISTENT.KEY is filtered from violation list');

// Cleanup temporary test fixture directories
fs.rmSync(tmpTestDir, { recursive: true, force: true });
fs.rmSync(tmpBugDir, { recursive: true, force: true });

console.log(
  `\n--- Test Summary: ${passedTests}/${totalTests} tests passed (${failedTests} failures) ---\n`,
);

if (failedTests > 0) {
  process.exit(1);
} else {
  process.exit(0);
}
