#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');

const i18nDir = path.join(projectRoot, 'src/assets/i18n');
const appDir = path.join(projectRoot, 'src/app');

let hasErrors = false;
const errors = [];

function recordError(msg) {
  hasErrors = true;
  errors.push(msg);
}

// 1. Check Dictionary Key Symmetry
function getAllKeys(obj, prefix = '') {
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

function checkI18nKeySymmetry() {
  const enPath = path.join(i18nDir, 'en-NZ.json');
  const zhPath = path.join(i18nDir, 'zh-CN.json');

  if (!fs.existsSync(enPath)) {
    recordError(`Missing English dictionary at: ${enPath}`);
    return;
  }
  if (!fs.existsSync(zhPath)) {
    recordError(`Missing Chinese dictionary at: ${zhPath}`);
    return;
  }

  let enJson, zhJson;
  try {
    enJson = JSON.parse(fs.readFileSync(enPath, 'utf8'));
  } catch (err) {
    recordError(`Failed to parse ${enPath}: ${err.message}`);
    return;
  }
  try {
    zhJson = JSON.parse(fs.readFileSync(zhPath, 'utf8'));
  } catch (err) {
    recordError(`Failed to parse ${zhPath}: ${err.message}`);
    return;
  }

  const enKeys = new Set(getAllKeys(enJson));
  const zhKeys = new Set(getAllKeys(zhJson));

  for (const key of enKeys) {
    if (!zhKeys.has(key)) {
      recordError(`[Key Asymmetry] Key '${key}' exists in en-NZ.json but is missing in zh-CN.json`);
    }
  }

  for (const key of zhKeys) {
    if (!enKeys.has(key)) {
      recordError(`[Key Asymmetry] Key '${key}' exists in zh-CN.json but is missing in en-NZ.json`);
    }
  }
}

// 2. Scan Files for Hardcoded Chinese
const CHINESE_CHAR_REGEX = /[\u4e00-\u9fa5]/;

function getAllFiles(dir, exts, results = []) {
  if (!fs.existsSync(dir)) return results;
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      getAllFiles(fullPath, exts, results);
    } else if (entry.isFile() && exts.some(ext => entry.name.endsWith(ext))) {
      results.push(fullPath);
    }
  }
  return results;
}

function stripCommentsFromCode(code) {
  // Strip multi-line comments /* ... */
  let stripped = code.replace(/\/\*[\s\S]*?\*\//g, (match) => {
    return ' '.repeat(match.length);
  });
  // Strip single-line comments // ...
  stripped = stripped.replace(/\/\/.*$/gm, (match) => {
    return ' '.repeat(match.length);
  });
  return stripped;
}

function stripHtmlComments(html) {
  return html.replace(/<!--[\s\S]*?-->/g, (match) => {
    return ' '.repeat(match.length);
  });
}

function scanHtmlFiles() {
  const htmlFiles = getAllFiles(appDir, ['.html']);
  for (const file of htmlFiles) {
    const content = fs.readFileSync(file, 'utf8');
    const cleaned = stripHtmlComments(content);
    const lines = cleaned.split('\n');
    lines.forEach((line, idx) => {
      if (CHINESE_CHAR_REGEX.test(line)) {
        const lineNum = idx + 1;
        const relPath = path.relative(projectRoot, file);
        recordError(`[Hardcoded Chinese in Template] ${relPath}:${lineNum} -> "${line.trim()}"`);
      }
    });
  }
}

function scanTsFiles() {
  const tsFiles = getAllFiles(appDir, ['.ts']).filter(f => !f.endsWith('.spec.ts') && !f.endsWith('.d.ts'));
  for (const file of tsFiles) {
    const content = fs.readFileSync(file, 'utf8');
    const cleaned = stripCommentsFromCode(content);
    const lines = cleaned.split('\n');

    lines.forEach((line, idx) => {
      if (CHINESE_CHAR_REGEX.test(line)) {
        const lineNum = idx + 1;
        const relPath = path.relative(projectRoot, file);
        recordError(`[Hardcoded Chinese in Code] ${relPath}:${lineNum} -> "${line.trim()}"`);
      }
    });
  }
}

// Execute checks
console.log('--- [i18n-scanner] Running F13.2 bilingual check ---');
checkI18nKeySymmetry();
scanHtmlFiles();
scanTsFiles();

if (hasErrors) {
  console.error('\n[i18n-scanner] FAILED: Found ' + errors.length + ' i18n violation(s):');
  errors.forEach((err, i) => console.error(`  ${i + 1}. ${err}`));
  console.error('\nBuild aborted due to i18n violations.\n');
  process.exit(1);
} else {
  console.log('[i18n-scanner] PASSED: All translation keys are symmetrical and no hardcoded Chinese characters found.\n');
  process.exit(0);
}
