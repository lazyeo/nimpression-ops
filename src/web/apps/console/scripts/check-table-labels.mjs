#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { JSDOM } from 'jsdom';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');
const defaultAppDir = path.join(projectRoot, 'src/app');

/**
 * Exemptions list for td cells that legitimately do not have a data-label.
 * Each exemption must specify the file (relative to src/app or absolute),
 * a selector / column identifier or predicate, and a clear rationale.
 */
export const TABLE_LABEL_EXEMPTIONS = [
  {
    file: 'features/admin/audit/components/audit-diff-modal/audit-diff-modal.component.html',
    colIndex: 3, // 0-indexed column 3 is the empty arrow separator (<th class="th-arrow"></th>)
    className: 'cell-arrow',
    reason: 'Decorative directional arrow indicator between before/after diff values with no header label.',
  },
];

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
 * Check if a given TD cell is exempt from having a data-label attribute.
 */
export function isCellExempt(fileRelPath, colIndex, tdElement, exemptions = TABLE_LABEL_EXEMPTIONS) {
  const normPath = fileRelPath.replace(/\\/g, '/');
  for (const ex of exemptions) {
    if (normPath.endsWith(ex.file)) {
      if (ex.colIndex !== undefined && ex.colIndex === colIndex) return true;
      if (ex.className && tdElement.classList && tdElement.classList.contains(ex.className)) return true;
    }
  }
  return false;
}

/**
 * Scans a single HTML template content for table data-label compliance.
 * Returns { tablesCount, tdsCount, violations: Array<{ tableIndex, colIndex, line, snippet, message }> }
 */
export function scanTemplateForTableLabels(htmlContent, filePath, exemptions = TABLE_LABEL_EXEMPTIONS) {
  const violations = [];
  let tablesCount = 0;
  let tdsCount = 0;

  if (!htmlContent.includes('<table')) {
    return { tablesCount, tdsCount, violations };
  }

  const dom = new JSDOM(htmlContent, { includeNodeLocations: true });
  const doc = dom.window.document;
  const tables = doc.querySelectorAll('table');
  tablesCount = tables.length;

  tables.forEach((table, tIdx) => {
    // Get all tbody rows or direct tr children in table
    const rows = table.querySelectorAll('tbody > tr, tr:not(thead tr)');
    rows.forEach((row, rIdx) => {
      const tds = row.querySelectorAll('td');
      tds.forEach((td, colIdx) => {
        tdsCount++;
        const hasAttrDataLabel = td.hasAttribute('[attr.data-label]');
        const hasPlainDataLabel = td.hasAttribute('data-label');
        const rawAttr = td.getAttribute('[attr.data-label]');
        const rawPlain = td.getAttribute('data-label');
        const rawValue = (rawAttr !== null ? rawAttr : rawPlain !== null ? rawPlain : '').trim();
        const isActuallyEmpty = !rawValue || rawValue === "''" || rawValue === '""' || rawValue === "``";

        if (isCellExempt(filePath, colIdx, td, exemptions)) {
          return;
        }

        if (!hasAttrDataLabel && !hasPlainDataLabel) {
          const loc = dom.nodeLocation ? dom.nodeLocation(td) : null;
          const line = loc ? loc.startLine : findLineNumber(htmlContent, td.outerHTML);
          violations.push({
            tableIndex: tIdx + 1,
            rowIndex: rIdx + 1,
            colIndex: colIdx + 1,
            line,
            snippet: td.outerHTML.split('\n')[0].substring(0, 100),
            message: `Missing [attr.data-label] on <td> (Table ${tIdx + 1}, Row ${rIdx + 1}, Col ${colIdx + 1})`,
          });
        } else if (isActuallyEmpty) {
          const loc = dom.nodeLocation ? dom.nodeLocation(td) : null;
          const line = loc ? loc.startLine : findLineNumber(htmlContent, td.outerHTML);
          violations.push({
            tableIndex: tIdx + 1,
            rowIndex: rIdx + 1,
            colIndex: colIdx + 1,
            line,
            snippet: td.outerHTML.split('\n')[0].substring(0, 100),
            message: `Empty data-label binding on <td> (Table ${tIdx + 1}, Row ${rIdx + 1}, Col ${colIdx + 1})`,
          });
        }
      });
    });
  });

  return { tablesCount, tdsCount, violations };
}

function findLineNumber(content, snippet) {
  const cleanSnippet = snippet.split('>')[0];
  const idx = content.indexOf(cleanSnippet);
  if (idx === -1) return 1;
  return content.substring(0, idx).split('\n').length;
}

/**
 * Runs the complete table labels guard check.
 */
export function runTableLabelsGuard(appDir = defaultAppDir, exemptions = TABLE_LABEL_EXEMPTIONS) {
  const htmlFiles = getAllFiles(appDir, ['.html']);
  let totalTables = 0;
  let totalTds = 0;
  const allViolations = [];

  for (const file of htmlFiles) {
    const relPath = path.relative(projectRoot, file);
    const content = fs.readFileSync(file, 'utf8');
    const { tablesCount, tdsCount, violations } = scanTemplateForTableLabels(content, relPath, exemptions);
    totalTables += tablesCount;
    totalTds += tdsCount;
    if (violations.length > 0) {
      allViolations.push({ file: relPath, violations });
    }
  }

  return {
    totalTables,
    totalTds,
    allViolations,
    passed: allViolations.length === 0,
  };
}

// CLI execution
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  console.log('--- [table-labels-guard] Running mobile stacked card table label verification ---');
  const result = runTableLabelsGuard();

  console.log(`[table-labels-guard] Scanned ${result.totalTables} tables, ${result.totalTds} td cells`);

  if (!result.passed) {
    let errorCount = 0;
    for (const item of result.allViolations) {
      for (const v of item.violations) {
        errorCount++;
        console.error(`[table-labels-guard] ERROR in ${item.file}:${v.line} -> ${v.message}`);
        console.error(`    Snippet: ${v.snippet}...`);
      }
    }
    console.error(`\n[table-labels-guard] FAILED: Found ${errorCount} <td> element(s) missing [attr.data-label] binding.`);
    process.exit(1);
  }

  console.log('[table-labels-guard] PASSED: All table <td> cells have valid data-label bindings or exemptions.\n');
  process.exit(0);
}
