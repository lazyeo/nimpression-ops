#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');
const repoRoot = path.resolve(projectRoot, '../../../../');

const IGNORE_DIRS = new Set([
  'node_modules',
  '.git',
  'bin',
  'obj',
  'dist',
  '.angular',
  'artifacts',
  '.data',
  '.turbo',
  '.alma',
  '_design',
  '.vscode',
]);

const IGNORE_FILES = new Set([
  'pnpm-lock.yaml',
  'package-lock.json',
]);

const SCANNED_EXTENSIONS = ['.ts', '.html', '.scss', '.json', '.md', '.cs'];

// Unicode Extended_Pictographic regex for matching emojis
const EMOJI_REGEX = /\p{Extended_Pictographic}/u;

let hasErrors = false;
const errors = [];

function recordError(msg) {
  hasErrors = true;
  errors.push(msg);
}

function getAllFiles(dir, results = []) {
  if (!fs.existsSync(dir)) return results;
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    if (entry.isDirectory()) {
      if (!IGNORE_DIRS.has(entry.name)) {
        getAllFiles(path.join(dir, entry.name), results);
      }
    } else if (entry.isFile()) {
      if (IGNORE_FILES.has(entry.name)) continue;
      if (SCANNED_EXTENSIONS.some((ext) => entry.name.endsWith(ext))) {
        results.push(path.join(dir, entry.name));
      }
    }
  }
  return results;
}

function scanFilesForEmoji() {
  const files = getAllFiles(repoRoot);
  for (const file of files) {
    const content = fs.readFileSync(file, 'utf8');
    const lines = content.split('\n');

    lines.forEach((line, idx) => {
      if (EMOJI_REGEX.test(line)) {
        const lineNum = idx + 1;
        const relPath = path.relative(repoRoot, file);
        recordError(`[Emoji Violation] ${relPath}:${lineNum} -> "${line.trim()}"`);
      }
    });
  }
}

console.log('--- [emoji-scanner] Running emoji prohibition check across repository ---');
scanFilesForEmoji();

if (hasErrors) {
  console.error('\n[emoji-scanner] FAILED: Found ' + errors.length + ' emoji violation(s):');
  errors.forEach((err, i) => console.error(`  ${i + 1}. ${err}`));
  console.error('\nBuild aborted due to emoji prohibition policy in CLAUDE.md.\n');
  process.exit(1);
} else {
  console.log('[emoji-scanner] PASSED: No emoji characters found across codebase and documentation.\n');
  process.exit(0);
}
