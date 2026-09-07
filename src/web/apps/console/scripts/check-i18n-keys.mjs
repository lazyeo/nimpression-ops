#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');
const srcDir = path.join(projectRoot, 'src');
const appDir = path.join(srcDir, 'app');
const i18nDir = path.join(srcDir, 'assets', 'i18n');

/**
 * Known dynamic key prefix expansions.
 * Dynamic prefixes in templates are formed like `'PREFIX_' + expr | i18n`.
 * Rather than skipping them (which would miss "new status without i18n key" defects),
 * we expand each prefix against all expected domain enum values.
 */
export const DYNAMIC_PREFIX_MAPPINGS = {
  'DISPATCH.STATUS_': [
    'DISPATCH.STATUS_DRAFT',
    'DISPATCH.STATUS_ASSIGNED',
    'DISPATCH.STATUS_ACKNOWLEDGED',
    'DISPATCH.STATUS_IN_PROGRESS',
    'DISPATCH.STATUS_COMPLETED',
    'DISPATCH.STATUS_CANCELLED',
  ],
  'DISPATCH.PRIORITY_': [
    'DISPATCH.PRIORITY_LOW',
    'DISPATCH.PRIORITY_MEDIUM',
    'DISPATCH.PRIORITY_HIGH',
    'DISPATCH.PRIORITY_URGENT',
  ],
  'FINES.STATUS_': [
    'FINES.STATUS_SUBMITTED',
    'FINES.STATUS_UNDER_REVIEW',
    'FINES.STATUS_ACCEPTED',
    'FINES.STATUS_DISPUTED',
    'FINES.STATUS_WAIVED',
  ],
  'PAYROLL.STATUS_': [
    'PAYROLL.STATUS_OPEN',
    'PAYROLL.STATUS_CALCULATING',
    'PAYROLL.STATUS_FINALISED',
    'PAYROLL.STATUS_PAID',
    'PAYROLL.STATUS_VOIDED',
  ],
  'TIMESHEETS.STATUS_': [
    'TIMESHEETS.STATUS_ACTIVE',
    'TIMESHEETS.STATUS_COMPLETED',
    'TIMESHEETS.STATUS_AUTOCLOSED',
  ],
  'VEHICLES.STATUS_': [
    'VEHICLES.STATUS_ACTIVE',
    'VEHICLES.STATUS_MAINTENANCE',
    'VEHICLES.STATUS_INACTIVE',
    'VEHICLES.STATUS_DECOMMISSIONED',
  ],
  'DRIVERS.STATUS_': [
    'DRIVERS.STATUS_ACTIVE',
    'DRIVERS.STATUS_INACTIVE',
    'DRIVERS.STATUS_SUSPENDED',
    'DRIVERS.STATUS_ON_LEAVE',
    'DRIVERS.STATUS_TERMINATED',
  ],
  'DRIVER.SHIFT_STATUS_': [
    'DRIVER.SHIFT_STATUS_NOT_STARTED',
    'DRIVER.SHIFT_STATUS_ACTIVE',
    'DRIVER.SHIFT_STATUS_ON_BREAK',
    'DRIVER.SHIFT_STATUS_COMPLETED',
  ],
  'DRIVER.TRIP_STATUS_': [
    'DRIVER.TRIP_STATUS_PENDING',
    'DRIVER.TRIP_STATUS_ASSIGNED',
    'DRIVER.TRIP_STATUS_ACKNOWLEDGED',
    'DRIVER.TRIP_STATUS_IN_PROGRESS',
    'DRIVER.TRIP_STATUS_COMPLETED',
    'DRIVER.TRIP_STATUS_CANCELLED',
  ],
};

/**
 * Keys that intentionally represent unmapped/test keys in test files.
 */
export const TEST_IGNORE_KEYS = new Set(['NON.EXISTENT.KEY']);

/**
 * Recursively extracts all dot-separated keys from a JSON dictionary.
 */
export function getAllKeys(obj, prefix = '') {
  let keys = [];
  for (const [k, v] of Object.entries(obj)) {
    const fullKey = prefix ? `${prefix}.${k}` : k;
    if (v && typeof v === 'object' && !Array.isArray(v)) {
      keys = keys.concat(getAllKeys(v, fullKey));
    } else {
      keys.push(fullKey);
    }
  }
  return keys;
}

/**
 * Recursively gets all files matching the given extensions.
 */
export function getAllFiles(dir, exts, results = []) {
  if (!fs.existsSync(dir)) return results;
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name !== 'node_modules' && entry.name !== '.angular' && entry.name !== 'dist') {
        getAllFiles(fullPath, exts, results);
      }
    } else if (entry.isFile() && exts.some((ext) => entry.name.endsWith(ext))) {
      results.push(fullPath);
    }
  }
  return results;
}

/**
 * Collects all i18n keys referenced in templates and source files.
 */
export function collectReferencedKeys(targetAppDir, options = {}) {
  const dynamicMappings = options.dynamicMappings || DYNAMIC_PREFIX_MAPPINGS;
  const ignoreKeys = options.ignoreKeys || TEST_IGNORE_KEYS;
  const htmlFiles = getAllFiles(targetAppDir, ['.html']);
  const tsFiles = getAllFiles(targetAppDir, ['.ts']);

  const collectedKeys = new Map(); // key -> Set of location descriptions

  function addKey(key, loc) {
    if (ignoreKeys.has(key)) return;
    if (!collectedKeys.has(key)) {
      collectedKeys.set(key, new Set());
    }
    collectedKeys.get(key).add(loc);
  }

  // 1. Scan HTML templates
  for (const file of htmlFiles) {
    const content = fs.readFileSync(file, 'utf8');
    const rel = path.relative(projectRoot, file);
    const lines = content.split('\n');

    lines.forEach((line, idx) => {
      const lineNum = idx + 1;
      const loc = `${rel}:${lineNum}`;

      // Static keys with | i18n pipe: 'FOO.BAR' | i18n
      const staticPipeRegex = /['"]([A-Z][A-Z0-9_]*(?:\.[A-Z0-9_]+)+)['"]\s*\|\s*i18n\b/g;
      let m;
      while ((m = staticPipeRegex.exec(line)) !== null) {
        addKey(m[1], loc);
      }

      // Dynamic prefix with | i18n pipe: 'FOO.PREFIX_' + ... | i18n
      const dynPipeRegex = /['"]([A-Z][A-Z0-9_]*(?:\.[A-Z0-9_]*_))['"]\s*\+/g;
      while ((m = dynPipeRegex.exec(line)) !== null) {
        const prefix = m[1];
        if (dynamicMappings[prefix]) {
          for (const expKey of dynamicMappings[prefix]) {
            addKey(expKey, `${loc} (via dynamic prefix '${prefix}')`);
          }
        } else {
          // Unknown dynamic prefix without expansion mapping -> report prefix directly
          addKey(prefix, loc);
        }
      }

      // Ternary in template: (cond ? 'A.B' : 'C.D') | i18n
      const ternaryRegex =
        /\?\s*['"]([A-Z][A-Z0-9_]*(?:\.[A-Z0-9_]+)+)['"]\s*:\s*['"]([A-Z][A-Z0-9_]*(?:\.[A-Z0-9_]+)+)['"]/g;
      while ((m = ternaryRegex.exec(line)) !== null) {
        addKey(m[1], loc);
        addKey(m[2], loc);
      }
    });
  }

  // 2. Scan TS files
  for (const file of tsFiles) {
    const content = fs.readFileSync(file, 'utf8');
    const rel = path.relative(projectRoot, file);
    const lines = content.split('\n');

    lines.forEach((line, idx) => {
      const lineNum = idx + 1;
      const loc = `${rel}:${lineNum}`;

      // translate('FOO.BAR', ...)
      const translateRegex = /translate\(\s*['"]([A-Z][A-Z0-9_]*(?:\.[A-Z0-9_]+)+)['"]/g;
      let m;
      while ((m = translateRegex.exec(line)) !== null) {
        addKey(m[1], loc);
      }

      // Explicit error / message / notification string assignments matching namespace
      const codeKeyRegex = /['"]([A-Z][A-Z0-9_]+(?:\.[A-Z0-9_]+)+)['"]/g;
      while ((m = codeKeyRegex.exec(line)) !== null) {
        const k = m[1];
        const namespace = k.split('.')[0];
        if (
          [
            'COMMON',
            'AUTH',
            'NOTIFICATIONS',
            'AUDIT',
            'TIMESHEETS',
            'PAYROLL',
            'VEHICLES',
            'DRIVERS',
            'DISPATCH',
            'FINES',
            'NEWS',
            'AREAS',
            'OFFLINE',
            'DRIVER',
            'NAV',
            'ROLES',
          ].includes(namespace)
        ) {
          addKey(k, loc);
        }
      }
    });
  }

  return collectedKeys;
}

/**
 * Runs Guard 9: verifies that every referenced i18n key exists in both en-NZ.json and zh-CN.json.
 */
export function runI18nKeysGuard(options = {}) {
  const customI18nDir = options.i18nDir || i18nDir;
  const customAppDir = options.appDir || appDir;
  const enPath = path.join(customI18nDir, 'en-NZ.json');
  const zhPath = path.join(customI18nDir, 'zh-CN.json');

  console.log('--- [i18n-keys-guard] Running template & code i18n key existence verification ---');

  if (!fs.existsSync(enPath)) {
    console.error(`[i18n-keys-guard] ERROR: en-NZ.json not found at: ${enPath}`);
    return { success: false, errors: [`Missing English dictionary at: ${enPath}`] };
  }
  if (!fs.existsSync(zhPath)) {
    console.error(`[i18n-keys-guard] ERROR: zh-CN.json not found at: ${zhPath}`);
    return { success: false, errors: [`Missing Chinese dictionary at: ${zhPath}`] };
  }

  const enJson = JSON.parse(fs.readFileSync(enPath, 'utf8'));
  const zhJson = JSON.parse(fs.readFileSync(zhPath, 'utf8'));

  const enKeys = new Set(getAllKeys(enJson));
  const zhKeys = new Set(getAllKeys(zhJson));

  const referencedKeys = collectReferencedKeys(customAppDir, options);
  console.log(
    `[i18n-keys-guard] Collected ${referencedKeys.size} distinct i18n key references across templates and code`,
  );

  const missingErrors = [];

  for (const [key, locs] of referencedKeys.entries()) {
    const locArr = Array.from(locs);
    const primaryLoc = locArr[0];

    if (!enKeys.has(key)) {
      missingErrors.push({
        key,
        lang: 'en-NZ.json',
        location: primaryLoc,
        message: `[Missing i18n Key] '${key}' referenced at ${primaryLoc} is missing in en-NZ.json`,
      });
    }

    if (!zhKeys.has(key)) {
      missingErrors.push({
        key,
        lang: 'zh-CN.json',
        location: primaryLoc,
        message: `[Missing i18n Key] '${key}' referenced at ${primaryLoc} is missing in zh-CN.json`,
      });
    }
  }

  if (missingErrors.length > 0) {
    console.error(`\n[i18n-keys-guard] FAILED: Found ${missingErrors.length} missing translation key(s):`);
    missingErrors.forEach((err, i) => console.error(`  ${i + 1}. ${err.message}`));
    console.error('\nBuild aborted: All keys referenced in templates and code must exist in i18n dictionaries.\n');
    return { success: false, errors: missingErrors, totalReferenced: referencedKeys.size };
  } else {
    console.log(
      `[i18n-keys-guard] PASSED: All ${referencedKeys.size} referenced translation keys exist in both en-NZ and zh-CN dictionaries.\n`,
    );
    return { success: true, errors: [], totalReferenced: referencedKeys.size };
  }
}

// Direct CLI execution
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const result = runI18nKeysGuard();
  if (!result.success) {
    process.exit(1);
  } else {
    process.exit(0);
  }
}
