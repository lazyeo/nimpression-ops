#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');
const srcDir = path.join(projectRoot, 'src');

/**
 * Exemption markers for item-by-item explicit exemption.
 * Following the exact pattern established in check-hardcoded-secrets.mjs:
 * Markers can appear on the same line or on the line immediately preceding.
 */
export const EXEMPTION_MARKERS = [
  'allow-hardcoded-color:',
  'allow-hardcoded:',
  'data-allow-hardcoded-color',
];

/**
 * Files defining the design tokens themselves are excluded from checking.
 */
export const TOKEN_DEFINITION_FILES = new Set([
  'styles/tokens.scss',
  'styles/theme.scss',
]);

const HEX_COLOR_REGEX = /#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})\b/;
const RGB_COLOR_REGEX = /\brgba?\s*\([^)]*\)/;
const HSL_COLOR_REGEX = /\bhsla?\s*\([^)]*\)/;

/**
 * Checks if a code string contains literal color specifications.
 */
export function containsColorLiteral(str) {
  return HEX_COLOR_REGEX.test(str) || RGB_COLOR_REGEX.test(str) || HSL_COLOR_REGEX.test(str);
}

/**
 * Recursively retrieves all files with specified extensions.
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
 * Checks whether a line is exempted either on the current line or by a preceding comment.
 */
export function isLineExempted(lines, idx, customMarkers = EXEMPTION_MARKERS) {
  const currentLine = lines[idx];
  if (customMarkers.some((marker) => currentLine.includes(marker))) {
    return true;
  }
  if (idx > 0 && customMarkers.some((marker) => lines[idx - 1].includes(marker))) {
    return true;
  }
  return false;
}

/**
 * Scans SCSS and HTML files for hardcoded color literals.
 */
export function scanHardcodedColors(targetSrcDir = srcDir, options = {}) {
  const ignoreDefs = options.tokenDefFiles || TOKEN_DEFINITION_FILES;
  const markers = options.exemptionMarkers || EXEMPTION_MARKERS;

  const scssFiles = getAllFiles(targetSrcDir, ['.scss']).filter((file) => {
    const rel = path.relative(targetSrcDir, file);
    return !ignoreDefs.has(rel);
  });

  const htmlFiles = getAllFiles(targetSrcDir, ['.html']);

  const violations = [];

  // 1. Scan SCSS stylesheet files
  for (const file of scssFiles) {
    const content = fs.readFileSync(file, 'utf8');
    const relPath = path.relative(projectRoot, file);
    const lines = content.split('\n');

    lines.forEach((line, idx) => {
      const trimmed = line.trim();
      if (!trimmed) return;

      if (isLineExempted(lines, idx, markers)) return;

      // Strip single-line and inline block comments
      const codeOnly = line.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/.*$/, '');
      if (containsColorLiteral(codeOnly)) {
        violations.push({
          file: relPath,
          line: idx + 1,
          type: 'SCSS hardcoded color',
          content: trimmed,
          message: `[Hardcoded Color in SCSS] ${relPath}:${idx + 1} -> "${trimmed}" (Must use var(--token) or add // allow-hardcoded-color: <reason>)`,
        });
      }
    });
  }

  // 2. Scan HTML templates for inline style color literals
  for (const file of htmlFiles) {
    const content = fs.readFileSync(file, 'utf8');
    const relPath = path.relative(projectRoot, file);
    const lines = content.split('\n');

    lines.forEach((line, idx) => {
      const trimmed = line.trim();
      if (!trimmed) return;

      if (isLineExempted(lines, idx, markers)) return;

      if (line.includes('style=') || line.includes('[style') || line.includes('<style')) {
        const codeOnly = line.replace(/<!--[\s\S]*?-->/g, '');
        if (containsColorLiteral(codeOnly)) {
          violations.push({
            file: relPath,
            line: idx + 1,
            type: 'HTML inline style hardcoded color',
            content: trimmed,
            message: `[Hardcoded Color in HTML style] ${relPath}:${idx + 1} -> "${trimmed}" (Must use CSS class with tokens or add <!-- allow-hardcoded-color: <reason> -->)`,
          });
        }
      }
    });
  }

  return violations;
}

/**
 * Runs Guard 10.
 */
export function runHardcodedColorsGuard(targetSrcDir = srcDir, options = {}) {
  console.log('--- [hardcoded-colors-guard] Running design token enforcement & color audit ---');

  const violations = scanHardcodedColors(targetSrcDir, options);

  const fileGroups = new Map();
  violations.forEach((v) => {
    if (!fileGroups.has(v.file)) fileGroups.set(v.file, []);
    fileGroups.get(v.file).push(v);
  });

  const scssFilesCount = Array.from(fileGroups.keys()).filter((f) => f.endsWith('.scss')).length;
  const htmlFilesCount = Array.from(fileGroups.keys()).filter((f) => f.endsWith('.html')).length;

  console.log(
    `[hardcoded-colors-guard] Scanned workspace: found ${violations.length} hardcoded color location(s) across ${fileGroups.size} file(s) (${scssFilesCount} scss, ${htmlFilesCount} html).`,
  );

  if (violations.length > 0) {
    console.error(`\n[hardcoded-colors-guard] FAILED: Found ${violations.length} hardcoded color violation(s):`);
    violations.forEach((v, i) => console.error(`  ${i + 1}. ${v.message}`));
    console.error(
      '\nBuild aborted: Hardcoded color values are prohibited. Use design tokens from tokens.scss / theme.scss, or explicitly register an item-by-item exemption with // allow-hardcoded-color: <reason>.\n',
    );
    return {
      success: false,
      violations,
      totalFiles: fileGroups.size,
      scssFilesCount,
      htmlFilesCount,
    };
  } else {
    console.log(
      '[hardcoded-colors-guard] PASSED: All styles and templates strictly adhere to design token variables.\n',
    );
    return {
      success: true,
      violations: [],
      totalFiles: 0,
      scssFilesCount: 0,
      htmlFilesCount: 0,
    };
  }
}

// Direct CLI execution
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const result = runHardcodedColorsGuard();
  if (!result.success) {
    process.exit(1);
  } else {
    process.exit(0);
  }
}
