#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '..');
const appFeaturesDir = path.resolve(projectRoot, 'src/app/features');

let hasErrors = false;
const errors = [];
const wired = [];
const exempted = [];

function recordError(msg) {
  hasErrors = true;
  errors.push(msg);
}

// Operational page components required to have realtime invalidation wiring
const OPERATIONAL_PAGE_COMPONENTS = [
  'driver/tasks/driver-tasks.component.ts',
  'driver/shifts/driver-shifts.component.ts',
  'driver/payslips/driver-payslips.component.ts',
  'driver/profile/driver-profile.component.ts',
  'admin/dispatch/dispatch.component.ts',
  'admin/drivers/drivers.component.ts',
  'admin/vehicles/vehicles.component.ts',
  'admin/timesheets/timesheets.component.ts',
];

// Check for explicit exemption comment: // no-realtime: <reason>
const EXEMPTION_REGEX = /\/\/\s*no-realtime:\s*([^\r\n]+)/i;
const BLOCK_EXEMPTION_REGEX = /\/\*\s*no-realtime:\s*([\s\S]*?)\*\//i;

function checkRealtimeWiring() {
  for (const relCompPath of OPERATIONAL_PAGE_COMPONENTS) {
    const fullPath = path.join(appFeaturesDir, relCompPath);
    const displayPath = path.relative(projectRoot, fullPath);

    if (!fs.existsSync(fullPath)) {
      recordError(`[File Not Found] Expected operational page component not found: ${displayPath}`);
      continue;
    }

    const content = fs.readFileSync(fullPath, 'utf8');

    // 1. Check for explicit exemption
    const matchLine = EXEMPTION_REGEX.exec(content);
    const matchBlock = BLOCK_EXEMPTION_REGEX.exec(content);
    if (matchLine || matchBlock) {
      const reason = (matchLine ? matchLine[1] : matchBlock[1]).trim();
      exempted.push({ file: displayPath, reason });
      continue;
    }

    // Strip comments to inspect active code only
    const code = content.replace(/\/\*[\s\S]*?\*\/|\/\/.*/g, '');

    // 2. Check for RealtimeService invalidation subscription
    const hasRealtimeImport = code.includes('RealtimeService');
    const hasInvalidationSub =
      code.includes('invalidation$') && code.includes('.subscribe(');
    const hasDestroyCleanup =
      code.includes('takeUntilDestroyed') || code.includes('unsubscribe');

    if (hasRealtimeImport && hasInvalidationSub && hasDestroyCleanup) {
      wired.push(displayPath);
    } else {
      const missingParts = [];
      if (!hasRealtimeImport) missingParts.push('RealtimeService injection');
      if (!hasInvalidationSub) missingParts.push('invalidation$ subscription');
      if (!hasDestroyCleanup) missingParts.push('takeUntilDestroyed cleanup');

      recordError(
        `[Missing Realtime Wiring] ${displayPath} is an operational page component but is not wired to realtime.invalidation$. Missing: [${missingParts.join(', ')}]. Add subscription or explicit '// no-realtime: <reason>' exemption.`,
      );
    }
  }
}

console.log('--- [realtime-wiring-guard] Running Operational Pages Realtime Subscription Check ---');
checkRealtimeWiring();

if (wired.length > 0) {
  console.log(`\n[realtime-wiring-guard] Correctly Wired (${wired.length}):`);
  wired.forEach((f) => console.log(`  ✓ ${f}`));
}

if (exempted.length > 0) {
  console.log(`\n[realtime-wiring-guard] Explicitly Exempted (${exempted.length}):`);
  exempted.forEach((e) => console.log(`  - ${e.file} (Reason: ${e.reason})`));
}

if (hasErrors) {
  console.error(`\n[realtime-wiring-guard] FAILED: Found ${errors.length} unwired page component(s):`);
  errors.forEach((err, i) => console.error(`  ${i + 1}. ${err}`));
  console.error('\nBuild aborted due to realtime invalidation wiring violations.\n');
  process.exit(1);
} else {
  console.log(
    `\n[realtime-wiring-guard] PASSED: All ${OPERATIONAL_PAGE_COMPONENTS.length} operational page components verified for realtime SignalR invalidation wiring.\n`,
  );
  process.exit(0);
}
