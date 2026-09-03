#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');
const srcDir = path.join(projectRoot, 'src');

// Unicode Extended_Pictographic regex for matching emojis
const EMOJI_REGEX = /\p{Extended_Pictographic}/u;

let hasErrors = false;
const errors = [];

function recordError(msg) {
  hasErrors = true;
  errors.push(msg);
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

function scanFilesForEmoji() {
  const files = getAllFiles(srcDir, ['.ts', '.html', '.scss', '.json']);
  for (const file of files) {
    const content = fs.readFileSync(file, 'utf8');
    const lines = content.split('\n');

    lines.forEach((line, idx) => {
      if (EMOJI_REGEX.test(line)) {
        const lineNum = idx + 1;
        const relPath = path.relative(projectRoot, file);
        recordError(`[Emoji Violation] ${relPath}:${lineNum} -> "${line.trim()}"`);
      }
    });
  }
}

console.log('--- [emoji-scanner] Running emoji prohibition check ---');
scanFilesForEmoji();

if (hasErrors) {
  console.error('\n[emoji-scanner] FAILED: Found ' + errors.length + ' emoji violation(s):');
  errors.forEach((err, i) => console.error(`  ${i + 1}. ${err}`));
  console.error('\nBuild aborted due to emoji prohibition policy in CLAUDE.md.\n');
  process.exit(1);
} else {
  console.log('[emoji-scanner] PASSED: No emoji characters found across frontend codebase.\n');
  process.exit(0);
}
