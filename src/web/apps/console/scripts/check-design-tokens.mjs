#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');
const srcDir = path.join(projectRoot, 'src');

const tokenFiles = [
  path.join(srcDir, 'styles', 'tokens.scss'),
  path.join(srcDir, 'styles', 'theme.scss'),
];

let hasErrors = false;
const errors = [];

function recordError(msg) {
  hasErrors = true;
  errors.push(msg);
}

function getDeclaredTokens() {
  const declared = new Set();
  const tokenDefRegex = /--([a-zA-Z0-9_-]+)\s*:/g;

  for (const file of tokenFiles) {
    if (!fs.existsSync(file)) {
      recordError(`[Token Guard] Token definition file missing: ${path.relative(projectRoot, file)}`);
      continue;
    }
    const content = fs.readFileSync(file, 'utf8');
    let m;
    while ((m = tokenDefRegex.exec(content)) !== null) {
      declared.add('--' + m[1]);
    }
  }
  return declared;
}

function getAllFiles(dir, exts, results = []) {
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

function scanTokenReferences(declaredTokens) {
  const files = getAllFiles(srcDir, ['.scss', '.html', '.ts']);
  const varRefRegex = /var\(\s*(--[a-zA-Z0-9_-]+)(?:\s*,\s*([^)]+))?\)/g;
  let totalUsages = 0;
  const referencedSet = new Set();

  for (const file of files) {
    const content = fs.readFileSync(file, 'utf8');
    const lines = content.split('\n');

    lines.forEach((line, idx) => {
      let m;
      const lineRegex = new RegExp(varRefRegex.source, 'g');
      while ((m = lineRegex.exec(line)) !== null) {
        totalUsages++;
        const token = m[1];
        const fallback = m[2];
        referencedSet.add(token);

        if (!declaredTokens.has(token)) {
          const lineNum = idx + 1;
          const relPath = path.relative(projectRoot, file);
          const fallbackNote = fallback ? ` (has fallback: "${fallback.trim()}")` : '';
          recordError(
            `[Undefined Token] ${relPath}:${lineNum} -> "${token}" is not defined in tokens.scss or theme.scss${fallbackNote}`,
          );
        }
      }
    });
  }

  return { totalUsages, uniqueReferenced: referencedSet.size };
}

console.log('--- [design-tokens-guard] Running design token verification ---');
const declaredTokens = getDeclaredTokens();
console.log(`[design-tokens-guard] Parsed ${declaredTokens.size} declared tokens from tokens.scss & theme.scss`);

const { totalUsages, uniqueReferenced } = scanTokenReferences(declaredTokens);
console.log(`[design-tokens-guard] Scanned ${totalUsages} token usages (${uniqueReferenced} unique tokens)`);

if (hasErrors) {
  console.error(`\n[design-tokens-guard] FAILED: Found ${errors.length} undeclared token reference(s):`);
  errors.forEach((err, i) => console.error(`  ${i + 1}. ${err}`));
  console.error('\nBuild aborted: All var(--token) references must be declared in tokens.scss or theme.scss.\n');
  process.exit(1);
} else {
  console.log('[design-tokens-guard] PASSED: All CSS variable references are valid design tokens.\n');
  process.exit(0);
}
