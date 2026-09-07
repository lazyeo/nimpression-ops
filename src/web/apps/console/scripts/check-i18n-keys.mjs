#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseCSharpEnums } from './check-enum-contract.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');
const srcDir = path.join(projectRoot, 'src');
const appDir = path.join(srcDir, 'app');
const i18nDir = path.join(srcDir, 'assets', 'i18n');
const defaultServerEnumsDir = path.resolve(projectRoot, '../../../../src/server/Nimpression.Domain/Enums');

/**
 * Uniform PascalCase / camelCase / delimited string to SCREAMING_SNAKE_CASE conversion.
 */
export function toScreamingSnake(input) {
  if (!input) return '';
  return input
    .trim()
    .replace(/([A-Z]+)([A-Z][a-z])/g, '$1_$2')
    .replace(/([a-z\d])([A-Z])/g, '$1_$2')
    .replace(/[-\s]+/g, '_')
    .toUpperCase();
}

/**
 * Explicit registry mapping template dynamic prefixes to C# domain enums or union member lists.
 * Any dynamic prefix encountered in templates/code that is NOT in this registry will fail Guard 9,
 * guaranteeing zero silent skips when new dynamic prefixes are added.
 */
export const DYNAMIC_PREFIX_ENUM_MAP = {
  'FINES.STATUS_': 'FineStatus',
  'TIMESHEETS.STATUS_': 'ShiftStatus',
  'PAYROLL.STATUS_': 'PayPeriodStatus',
  'DISPATCH.STATUS_': 'JobTaskStatus',
  'DISPATCH.PRIORITY_': 'TaskPriority',
  'DRIVERS.STATUS_': 'DriverStatus',
  'VEHICLES.STATUS_': 'VehicleStatus',
  'DRIVER.SHIFT_STATUS_': ['NOT_STARTED', 'ACTIVE', 'ON_BREAK', 'COMPLETED'],
  'DRIVER.TRIP_STATUS_': ['PENDING', 'ASSIGNED', 'ACKNOWLEDGED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED'],
  'ROLES.': 'UserRole',
  'CHARTS.TASK_FUNNEL.STAGES.': 'JobTaskStatus',
};

/**
 * Computes prefix mappings for a given enum dictionary.
 */
export function getDynamicPrefixMappings(csEnums, prefixEnumMap = DYNAMIC_PREFIX_ENUM_MAP) {
  const mappings = {};
  for (const [prefix, target] of Object.entries(prefixEnumMap)) {
    if (Array.isArray(target)) {
      mappings[prefix] = target.map((m) => `${prefix}${toScreamingSnake(m)}`);
    } else if (typeof target === 'string' && csEnums && csEnums.has(target)) {
      mappings[prefix] = csEnums.get(target).members.map((m) => `${prefix}${toScreamingSnake(m)}`);
    }
  }
  return mappings;
}

export const DYNAMIC_PREFIX_MAPPINGS = getDynamicPrefixMappings(parseCSharpEnums(defaultServerEnumsDir));

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
 * Evaluates template conversion expression against an enum member value.
 */
export function evaluateTemplateExpression(expr, member) {
  if (!expr || !expr.trim()) return toScreamingSnake(member);
  let cleanExpr = expr.trim();
  cleanExpr = cleanExpr.replace(
    /\(\s*([^|]+)\s*\|\s*(?:screamingSnake|toScreamingSnake)\s*\)/g,
    'toScreamingSnake($1)',
  );
  cleanExpr = cleanExpr.replace(/([^|]+)\s*\|\s*(?:screamingSnake|toScreamingSnake)/g, 'toScreamingSnake($1)');
  cleanExpr = cleanExpr.replace(/\|\s*i18n.*$/, '').trim();

  try {
    const fn = new Function(
      'val',
      'toScreamingSnake',
      `
      const fine = { status: val };
      const shift = { status: val };
      const period = { status: val };
      const task = { status: val, priority: val };
      const driver = { status: val };
      const veh = { status: val };
      const user = { role: val };
      const currentShift = () => ({ status: val });
      const authService = { currentUser: () => ({ role: val }) };
      const getStatusKey = (s) => toScreamingSnake(s);
      return (${cleanExpr});
    `,
    );
    const result = fn(member, toScreamingSnake);
    return typeof result === 'string' ? result : toScreamingSnake(member);
  } catch {
    return toScreamingSnake(member);
  }
}

/**
 * Collects all i18n keys referenced in templates and source files.
 */
export function collectReferencedKeys(targetAppDir, options = {}) {
  const serverEnumsDir = options.serverEnumsDir || defaultServerEnumsDir;
  const csEnums = options.csEnums || parseCSharpEnums(serverEnumsDir);
  const prefixEnumMap = options.prefixEnumMap || DYNAMIC_PREFIX_ENUM_MAP;
  const ignoreKeys = options.ignoreKeys || TEST_IGNORE_KEYS;
  const htmlFiles = getAllFiles(targetAppDir, ['.html']);
  const tsFiles = getAllFiles(targetAppDir, ['.ts']);

  const collectedKeys = new Map(); // key -> Set of location descriptions
  const unregisteredPrefixes = [];

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

      // Dynamic prefix in templates: 'FOO.STATUS_' + expr | i18n or 'ROLES.' + expr
      const dynPipeRegex =
        /['"]([A-Z][A-Z0-9_]*[_.](?:[A-Z0-9_]*[_.])*)['"]\s*\+\s*(.+?)(?=\s*\|\s*i18n\b|\s*\}\})/g;
      while ((m = dynPipeRegex.exec(line)) !== null) {
        const prefix = m[1];
        const expr = m[2];

        if (prefixEnumMap[prefix]) {
          const enumTarget = prefixEnumMap[prefix];
          let members = [];
          if (Array.isArray(enumTarget)) {
            members = enumTarget;
          } else if (typeof enumTarget === 'string') {
            const enumDef = csEnums.get(enumTarget);
            if (enumDef) {
              members = enumDef.members;
            } else {
              unregisteredPrefixes.push({
                prefix,
                location: loc,
                message: `Domain enum '${enumTarget}' mapped from prefix '${prefix}' not found in C# enums at ${serverEnumsDir}`,
              });
            }
          }

          for (const member of members) {
            // Canonical key derived from C# domain enum
            const canonicalKey = `${prefix}${toScreamingSnake(member)}`;
            addKey(canonicalKey, `${loc} (canonical key for enum member '${member}')`);

            // Evaluated key from actual template expression
            const evaluatedKey = `${prefix}${evaluateTemplateExpression(expr, member)}`;
            if (evaluatedKey !== canonicalKey) {
              addKey(evaluatedKey, `${loc} (evaluated via expression on '${member}')`);
            }
          }
        } else {
          unregisteredPrefixes.push({
            prefix,
            location: loc,
            message: `[Unregistered Dynamic Prefix] Dynamic prefix '${prefix}' referenced at ${loc} is not registered in DYNAMIC_PREFIX_ENUM_MAP.`,
          });
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
            'CHARTS',
          ].includes(namespace)
        ) {
          addKey(k, loc);
        }
      }
    });
  }

  if (options.unregisteredPrefixes) {
    options.unregisteredPrefixes.push(...unregisteredPrefixes);
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

  const unregisteredPrefixes = [];
  const referencedKeys = collectReferencedKeys(customAppDir, {
    ...options,
    unregisteredPrefixes,
  });

  console.log(
    `[i18n-keys-guard] Collected ${referencedKeys.size} distinct i18n key references across templates and code`,
  );

  const missingErrors = [];

  for (const unreg of unregisteredPrefixes) {
    missingErrors.push({
      key: unreg.prefix,
      lang: 'contract',
      location: unreg.location,
      message: unreg.message,
    });
  }

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
