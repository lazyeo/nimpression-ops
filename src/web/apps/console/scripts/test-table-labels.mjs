#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  scanTemplateForTableLabels,
  isCellExempt,
  TABLE_LABEL_EXEMPTIONS,
  runTableLabelsGuard,
} from './check-table-labels.mjs';

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

console.log('--- [table-labels-guard-tests] Running Guard 12 automated test suite ---');

// 1. Template with completely labeled table
console.log('\n[Suite 1] Fully labeled table passes check');
const validHtml = `
<div class="table-container">
  <table class="data-table">
    <thead>
      <tr>
        <th>{{ 'TEST.COL_NAME' | i18n }}</th>
        <th>{{ 'TEST.COL_STATUS' | i18n }}</th>
      </tr>
    </thead>
    <tbody>
      @for (item of items(); track item.id) {
        <tr>
          <td [attr.data-label]="'TEST.COL_NAME' | i18n">{{ item.name }}</td>
          <td [attr.data-label]="'TEST.COL_STATUS' | i18n">{{ item.status }}</td>
        </tr>
      }
    </tbody>
  </table>
</div>
`;
const result1 = scanTemplateForTableLabels(validHtml, 'test.component.html');
assert(result1.tablesCount === 1, 'Detected 1 table');
assert(result1.tdsCount === 2, 'Detected 2 td cells');
assert(result1.violations.length === 0, 'Zero violations for compliant table');

// 2. Template with missing data-label
console.log('\n[Suite 2] Missing data-label triggers violation');
const missingHtml = `
<table class="data-table">
  <tbody>
    <tr>
      <td [attr.data-label]="'TEST.COL_NAME' | i18n">Name</td>
      <td>Missing Label Value</td>
    </tr>
  </tbody>
</table>
`;
const result2 = scanTemplateForTableLabels(missingHtml, 'test2.component.html');
assert(result2.violations.length === 1, 'Detected 1 violation for missing label');
assert(result2.violations[0].colIndex === 2, 'Violation pinpointed to column 2');

// 3. Template with empty data-label
console.log('\n[Suite 3] Empty data-label triggers violation');
const emptyLabelHtml = `
<table class="data-table">
  <tbody>
    <tr>
      <td [attr.data-label]="''">Empty</td>
    </tr>
  </tbody>
</table>
`;
const result3 = scanTemplateForTableLabels(emptyLabelHtml, 'test3.component.html');
assert(result3.violations.length === 1, 'Detected violation for empty data-label');

// 4. Exemption list handling
console.log('\n[Suite 4] Exemption list correctly skips authorized cells');
const customExemptions = [
  {
    file: 'special.component.html',
    colIndex: 1,
    className: 'cell-arrow',
    reason: 'Decorative arrow',
  },
];
const exemptHtml = `
<table class="diff-table">
  <tbody>
    <tr>
      <td [attr.data-label]="'BEFORE' | i18n">A</td>
      <td class="cell-arrow">&rarr;</td>
      <td [attr.data-label]="'AFTER' | i18n">B</td>
    </tr>
  </tbody>
</table>
`;
const result4 = scanTemplateForTableLabels(exemptHtml, 'special.component.html', customExemptions);
assert(result4.violations.length === 0, 'Exempt arrow cell produces no violation');

// 5. Template without table is safely ignored
console.log('\n[Suite 5] Non-table template returns zero violations');
const noTableHtml = `<div><p>Simple content</p></div>`;
const result5 = scanTemplateForTableLabels(noTableHtml, 'simple.component.html');
assert(result5.tablesCount === 0, '0 tables detected');
assert(result5.violations.length === 0, '0 violations');

console.log(`\n========================================`);
console.log(`Table Labels Guard Tests: ${passedTests}/${totalTests} passed, ${failedTests} failed`);
console.log(`========================================\n`);

if (failedTests > 0) {
  process.exit(1);
} else {
  process.exit(0);
}
